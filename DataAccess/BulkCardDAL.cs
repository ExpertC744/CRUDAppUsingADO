using Microsoft.Data.SqlClient;
using System.Data;
namespace CRUDAppUsingADO.DataAccess;
public sealed class BulkCardDAL(IConfiguration config)
{
 private readonly string cs=config.GetConnectionString("DefaultConnection") ?? throw new InvalidOperationException("DefaultConnection missing");
 public async Task<(int inserted,int duplicates)> ImportRecipientsAsync(Guid admin,IReadOnlyList<Models.Bulk.RecipientRow> rows,string? source,CancellationToken ct)
 { int ins=0,dup=0; await using var cn=new SqlConnection(cs); await cn.OpenAsync(ct); await using var tx=(SqlTransaction)await cn.BeginTransactionAsync(ct);
  foreach(var r in rows){ await using var cmd=new SqlCommand(@"IF EXISTS(SELECT 1 FROM dbo.CardRecipients WHERE AdminUserId=@A AND ((@E IS NOT NULL AND Email=@E) OR (@M IS NOT NULL AND Mobile=@M))) SELECT 0 ELSE BEGIN INSERT dbo.CardRecipients(AdminUserId,FullName,Email,Mobile,Text1,Text2,Text3,Text4,SourceFile) VALUES(@A,@N,@E,@M,@T1,@T2,@T3,@T4,@S); SELECT 1 END",cn,tx);
   cmd.Parameters.Add("@A",SqlDbType.UniqueIdentifier).Value=admin; cmd.Parameters.Add("@N",SqlDbType.NVarChar,200).Value=r.FullName; cmd.Parameters.Add("@E",SqlDbType.NVarChar,320).Value=(object?)r.Email??DBNull.Value; cmd.Parameters.Add("@M",SqlDbType.NVarChar,20).Value=(object?)r.Mobile??DBNull.Value;
   for(int i=1;i<=4;i++) cmd.Parameters.Add("@T"+i,SqlDbType.NVarChar,500).Value=(object?)(typeof(Models.Bulk.RecipientRow).GetProperty("Text"+i)!.GetValue(r))??DBNull.Value;
   cmd.Parameters.Add("@S",SqlDbType.NVarChar,260).Value=(object?)source??DBNull.Value; if(Convert.ToInt32(await cmd.ExecuteScalarAsync(ct))==1) ins++; else dup++;
  } await tx.CommitAsync(ct); return(ins,dup);
 }
 public async Task<DataTable> GetRecipientsAsync(Guid admin,string? search,int page,int pageSize,CancellationToken ct){var dt=new DataTable(); await using var cn=new SqlConnection(cs); await cn.OpenAsync(ct); await using var cmd=new SqlCommand(@"SELECT RecipientId,FullName,Email,Mobile,Text1,Text2,Text3,Text4,IsActive FROM dbo.CardRecipients WHERE AdminUserId=@A AND (@Q IS NULL OR FullName LIKE '%'+@Q+'%' OR Email LIKE '%'+@Q+'%' OR Mobile LIKE '%'+@Q+'%') ORDER BY RecipientId DESC OFFSET @O ROWS FETCH NEXT @P ROWS ONLY",cn); cmd.Parameters.AddWithValue("@A",admin); cmd.Parameters.AddWithValue("@Q",(object?)search??DBNull.Value); cmd.Parameters.AddWithValue("@O",(page-1)*pageSize); cmd.Parameters.AddWithValue("@P",pageSize); using var rd=await cmd.ExecuteReaderAsync(ct);dt.Load(rd);return dt;}
 public async Task<bool> SetRecipientStatusAsync(Guid admin,long id,bool active,CancellationToken ct){await using var cn=new SqlConnection(cs);await cn.OpenAsync(ct);await using var cmd=new SqlCommand("UPDATE dbo.CardRecipients SET IsActive=@B WHERE RecipientId=@I AND AdminUserId=@A",cn);cmd.Parameters.AddWithValue("@B",active);cmd.Parameters.AddWithValue("@I",id);cmd.Parameters.AddWithValue("@A",admin);return await cmd.ExecuteNonQueryAsync(ct)>0;}
    public async Task<long> SaveDesignAsync(
        Guid admin,
        string name,
        string image,
        int w,
        int h,
        string json,
        string templateType,
        string uploadFormat,
        string outputFormat,
        CancellationToken ct)
    {
        await using var cn = new SqlConnection(cs);
        await cn.OpenAsync(ct);

        const string sql = @"
        INSERT INTO dbo.CardDesigns
        (
            AdminUserId,
            DesignName,
            ImagePath,
            CanvasWidth,
            CanvasHeight,
            CanvasJson,
            TemplateType,
            UploadFormat,
            OutputFormat,
            CreatedAt
        )
        OUTPUT INSERTED.CardDesignId
        VALUES
        (
            @A, @N, @I, @W, @H, @J,
            @TT, @UF, @OF, SYSUTCDATETIME()
        );";

        await using var cmd = new SqlCommand(sql, cn);

        cmd.Parameters.Add("@A", SqlDbType.UniqueIdentifier).Value = admin;
        cmd.Parameters.Add("@N", SqlDbType.NVarChar, 200).Value = name;
        cmd.Parameters.Add("@I", SqlDbType.NVarChar, 500).Value = image;
        cmd.Parameters.Add("@W", SqlDbType.Int).Value = w;
        cmd.Parameters.Add("@H", SqlDbType.Int).Value = h;
        cmd.Parameters.Add("@J", SqlDbType.NVarChar, -1).Value = json;

        cmd.Parameters.Add("@TT", SqlDbType.NVarChar, 30).Value = templateType;
        cmd.Parameters.Add("@UF", SqlDbType.NVarChar, 10).Value = uploadFormat;
        cmd.Parameters.Add("@OF", SqlDbType.NVarChar, 10).Value = outputFormat;

        var result = await cmd.ExecuteScalarAsync(ct);

        return Convert.ToInt64(result);
    }

    public async Task<Guid> CreateJobAsync(
    Guid admin,
    Models.Bulk.BulkJobCreateDto d,
    CancellationToken ct)
    {
        if (d.RecipientIds == null || d.RecipientIds.Count == 0)
            throw new InvalidOperationException(
                "Select at least one recipient.");

        // Remove duplicate IDs before counting and inserting.
        var recipientIds = d.RecipientIds
            .Distinct()
            .ToList();

        if (recipientIds.Count > 100000)
            throw new InvalidOperationException(
                "Select between 1 and 100000 unique recipients.");

        var id = Guid.NewGuid();

        await using var cn = new SqlConnection(cs);
        await cn.OpenAsync(ct);

        await using var tx =
            (SqlTransaction)await cn.BeginTransactionAsync(ct);

        // 1. Verify that the design belongs to this admin.
        const string designSql = @"
        SELECT COUNT(*)
        FROM dbo.CardDesigns
        WHERE CardDesignId = @D
          AND AdminUserId = @A;";

        await using (var check = new SqlCommand(
            designSql, cn, tx))
        {
            check.Parameters.Add("@D", SqlDbType.BigInt).Value =
                d.CardDesignId;

            check.Parameters.Add("@A", SqlDbType.UniqueIdentifier).Value =
                admin;

            var exists = Convert.ToInt32(
                await check.ExecuteScalarAsync(ct));

            if (exists != 1)
                throw new InvalidOperationException(
                    "Card design not found for this admin.");
        }

        // 2. Create bulk job.
        // 2. Create bulk job.
        const string jobSql = @"
INSERT INTO dbo.BulkJobs
(
    JobId,
    AdminUserId,
    CardDesignId,
    TemplateId,
    OutputType,
    OutputFormat,
    Channels,
    ParameterValuesJson,
    ScheduledAtUtc,
    JobStatus,
    TotalCount
)
VALUES
(
    @J,
    @A,
    @D,
    @T,
    @O,
    @OF,
    @C,
    @P,
    @S,
    CASE
        WHEN @S IS NULL OR @S <= SYSUTCDATETIME()
        THEN 'Queued'
        ELSE 'Scheduled'
    END,
    @N
);";

        await using (var cmd = new SqlCommand(jobSql, cn, tx))
        {
            cmd.Parameters.Add("@J", SqlDbType.UniqueIdentifier).Value = id;
            cmd.Parameters.Add("@A", SqlDbType.UniqueIdentifier).Value = admin;
            cmd.Parameters.Add("@D", SqlDbType.BigInt).Value = d.CardDesignId;

            cmd.Parameters.Add("@T", SqlDbType.Int).Value =
                d.TemplateId.HasValue
                    ? checked((int)d.TemplateId.Value)
                    : DBNull.Value;

            cmd.Parameters.Add("@O", SqlDbType.NVarChar, 10).Value =
                d.OutputType;

            cmd.Parameters.Add("@OF", SqlDbType.NVarChar, 10).Value =
                d.OutputFormat;

            cmd.Parameters.Add("@C", SqlDbType.NVarChar, 20).Value =
                d.Channels;

            cmd.Parameters.Add("@P", SqlDbType.NVarChar, -1).Value =
                System.Text.Json.JsonSerializer.Serialize(
                    d.ParameterValues ?? new Dictionary<string, string>());

            cmd.Parameters.Add("@S", SqlDbType.DateTime2).Value =
                (object?)d.ScheduledAt?.UtcDateTime ?? DBNull.Value;

            cmd.Parameters.Add("@N", SqlDbType.Int).Value =
                recipientIds.Count;

            await cmd.ExecuteNonQueryAsync(ct);
        }

        // 3. Insert only active recipients belonging to this admin.
        const string recipientSql = @"
        INSERT INTO dbo.BulkJobRecipients
        (
            JobId,
            RecipientId
        )
        SELECT
            @J,
            RecipientId
        FROM dbo.CardRecipients
        WHERE RecipientId = @R
          AND AdminUserId = @A
          AND IsActive = 1;";

        foreach (var rid in recipientIds)
        {
            await using var cmd = new SqlCommand(
                recipientSql, cn, tx);

            cmd.Parameters.Add("@J", SqlDbType.UniqueIdentifier).Value = id;
            cmd.Parameters.Add("@R", SqlDbType.BigInt).Value = rid;
            cmd.Parameters.Add("@A", SqlDbType.UniqueIdentifier).Value = admin;

            var inserted = await cmd.ExecuteNonQueryAsync(ct);

            if (inserted == 0)
            {
                throw new InvalidOperationException(
                    $"Recipient {rid} is invalid, inactive or belongs to another admin.");
            }
        }

        await tx.CommitAsync(ct);

        return id;
    }
    public async Task<object?> GetJobAsync(Guid admin,Guid id,CancellationToken ct){await using var cn=new SqlConnection(cs);await cn.OpenAsync(ct);await using var cmd=new SqlCommand("SELECT JobId,JobStatus,TotalCount,ProcessedCount,SuccessCount,FailedCount,ScheduledAtUtc,CreatedAt FROM dbo.BulkJobs WHERE JobId=@J AND AdminUserId=@A",cn);cmd.Parameters.AddWithValue("@J",id);cmd.Parameters.AddWithValue("@A",admin);using var r=await cmd.ExecuteReaderAsync(ct);if(!await r.ReadAsync(ct))return null;return new{JobId=r.GetGuid(0),Status=r.GetString(1),Total=r.GetInt32(2),Processed=r.GetInt32(3),Success=r.GetInt32(4),Failed=r.GetInt32(5),ScheduledAtUtc=r.IsDBNull(6)?(DateTime?)null:r.GetDateTime(6),CreatedAt=r.GetDateTime(7)};}


    public async Task<Guid?> ClaimNextJobAsync(CancellationToken ct)
    {
        await using var cn = new SqlConnection(cs);
        await cn.OpenAsync(ct);

        const string sql = @"
;WITH NextJob AS
(
    SELECT TOP (1) *
    FROM dbo.BulkJobs WITH (UPDLOCK, READPAST, ROWLOCK)
    WHERE JobStatus = 'Queued'
      AND (ScheduledAtUtc IS NULL OR ScheduledAtUtc <= SYSUTCDATETIME())
    ORDER BY CreatedAt
)
UPDATE NextJob
SET JobStatus = 'Processing',
    StartedAt = SYSUTCDATETIME()
OUTPUT INSERTED.JobId;";

        await using var cmd = new SqlCommand(sql, cn);

        var result = await cmd.ExecuteScalarAsync(ct);

        return result == null || result == DBNull.Value
            ? null
            : (Guid)result;
    }

    public async Task<Models.Bulk.BulkJobDetails?> LoadJobAsync(
        Guid jobId,
        CancellationToken ct)
    {
        await using var cn = new SqlConnection(cs);
        await cn.OpenAsync(ct);

        const string sql = @"
SELECT
    j.JobId,
    j.AdminUserId,
    j.CardDesignId,
    j.TemplateId,
    j.OutputType,
    j.OutputFormat,
    j.Channels,
    j.ParameterValuesJson,
    d.ImagePath,
    d.CanvasWidth,
    d.CanvasHeight,
    d.CanvasJson,
    t.TemplateName,
t.Message,
ISNULL(t.NoOfParameters, 0),
ISNULL(t.TemplateType, 1) AS TemplateType,
ISNULL(t.SendType, 1) AS TemplateSendType
    ISNULL(t.SendType, 1)
FROM dbo.BulkJobs j
INNER JOIN dbo.CardDesigns d
    ON d.CardDesignId = j.CardDesignId
LEFT JOIN dbo.tblTemplates t
    ON t.TemplateId = j.TemplateId
WHERE j.JobId = @J;";

        await using var cmd = new SqlCommand(sql, cn);
        cmd.Parameters.Add("@J", SqlDbType.UniqueIdentifier).Value = jobId;

        await using var r = await cmd.ExecuteReaderAsync(ct);

        if (!await r.ReadAsync(ct))
            return null;

        return new Models.Bulk.BulkJobDetails
        {
            JobId = r.GetGuid(0),
            AdminUserId = r.GetGuid(1),
            CardDesignId = r.GetInt64(2),
            TemplateId = r.IsDBNull(3) ? null : r.GetInt32(3),
            OutputType = r.GetString(4),
            OutputFormat = r.GetString(5),
            Channels = r.GetString(6),
            ParameterValuesJson = r.IsDBNull(7) ? "{}" : r.GetString(7),
            ImagePath = r.GetString(8),
            CanvasWidth = r.GetInt32(9),
            CanvasHeight = r.GetInt32(10),
            CanvasJson = r.IsDBNull(11) ? "{}" : r.GetString(11),
            TemplateName = r.IsDBNull(12) ? null : r.GetString(12),
            TemplateMessage = r.IsDBNull(13) ? null : r.GetString(13),
            TemplateParameterCount = r.GetInt32(14),
            TemplateType = Convert.ToInt32(r.GetValue(15)),
            TemplateSendType = Convert.ToInt32(r.GetValue(16))
        };
    }

    public async Task<List<Models.Bulk.BulkRecipientDetails>>
        LoadJobRecipientsAsync(Guid jobId, CancellationToken ct)
    {
        var list = new List<Models.Bulk.BulkRecipientDetails>();

        await using var cn = new SqlConnection(cs);
        await cn.OpenAsync(ct);

        const string sql = @"
SELECT
    r.RecipientId,
    r.FullName,
    r.Email,
    r.Mobile,
    r.Text1,
    r.Text2,
    r.Text3,
    r.Text4
FROM dbo.BulkJobRecipients br
INNER JOIN dbo.CardRecipients r
    ON r.RecipientId = br.RecipientId
WHERE br.JobId = @J
  AND br.Status = 'Pending'
ORDER BY r.RecipientId;";

        await using var cmd = new SqlCommand(sql, cn);
        cmd.Parameters.Add("@J", SqlDbType.UniqueIdentifier).Value = jobId;

        await using var reader = await cmd.ExecuteReaderAsync(ct);

        while (await reader.ReadAsync(ct))
        {
            list.Add(new Models.Bulk.BulkRecipientDetails
            {
                RecipientId = reader.GetInt64(0),
                FullName = reader.IsDBNull(1) ? "" : reader.GetString(1),
                Email = reader.IsDBNull(2) ? null : reader.GetString(2),
                Mobile = reader.IsDBNull(3) ? null : reader.GetString(3),
                Text1 = reader.IsDBNull(4) ? null : reader.GetString(4),
                Text2 = reader.IsDBNull(5) ? null : reader.GetString(5),
                Text3 = reader.IsDBNull(6) ? null : reader.GetString(6),
                Text4 = reader.IsDBNull(7) ? null : reader.GetString(7)
            });
        }

        return list;
    }

    public async Task UpdateRecipientAsync(
        Guid jobId,
        long recipientId,
        string status,
        string? imagePath,
        string? error,
        CancellationToken ct)
    {
        await using var cn = new SqlConnection(cs);
        await cn.OpenAsync(ct);

        const string sql = @"
UPDATE dbo.BulkJobRecipients
SET Status = @S,
    GeneratedImagePath = @I,
    ErrorMessage = @E,
    ProcessedAtUtc =
        CASE WHEN @S IN ('Success', 'Failed')
             THEN SYSUTCDATETIME()
             ELSE NULL END
WHERE JobId = @J
  AND RecipientId = @R;";

        await using var cmd = new SqlCommand(sql, cn);

        cmd.Parameters.Add("@S", SqlDbType.NVarChar, 20).Value = status;
        cmd.Parameters.Add("@I", SqlDbType.NVarChar, 500).Value =
            (object?)imagePath ?? DBNull.Value;
        cmd.Parameters.Add("@E", SqlDbType.NVarChar, 2000).Value =
            (object?)error ?? DBNull.Value;
        cmd.Parameters.Add("@J", SqlDbType.UniqueIdentifier).Value = jobId;
        cmd.Parameters.Add("@R", SqlDbType.BigInt).Value = recipientId;

        await cmd.ExecuteNonQueryAsync(ct);
    }

    public async Task UpdateJobCountersAsync(
        Guid jobId,
        CancellationToken ct)
    {
        await using var cn = new SqlConnection(cs);
        await cn.OpenAsync(ct);

        const string sql = @"
UPDATE j
SET
    ProcessedCount =
        (SELECT COUNT(*)
         FROM dbo.BulkJobRecipients r
         WHERE r.JobId = j.JobId
           AND r.Status IN ('Success', 'Failed')),

    SuccessCount =
        (SELECT COUNT(*)
         FROM dbo.BulkJobRecipients r
         WHERE r.JobId = j.JobId
           AND r.Status = 'Success'),

    FailedCount =
        (SELECT COUNT(*)
         FROM dbo.BulkJobRecipients r
         WHERE r.JobId = j.JobId
           AND r.Status = 'Failed')
FROM dbo.BulkJobs j
WHERE j.JobId = @J;";

        await using var cmd = new SqlCommand(sql, cn);
        cmd.Parameters.Add("@J", SqlDbType.UniqueIdentifier).Value = jobId;

        await cmd.ExecuteNonQueryAsync(ct);
    }

    public async Task CompleteJobAsync(
        Guid jobId,
        CancellationToken ct)
    {
        await using var cn = new SqlConnection(cs);
        await cn.OpenAsync(ct);

        const string sql = @"
UPDATE dbo.BulkJobs
SET
    JobStatus =
        CASE
            WHEN FailedCount = TotalCount THEN 'Failed'
            ELSE 'Completed'
        END,
    CompletedAt = SYSUTCDATETIME()
WHERE JobId = @J
  AND JobStatus = 'Processing';";

        await using var cmd = new SqlCommand(sql, cn);
        cmd.Parameters.Add("@J", SqlDbType.UniqueIdentifier).Value = jobId;

        await cmd.ExecuteNonQueryAsync(ct);
    }


    public async Task FailJobAsync(
    Guid jobId,
    CancellationToken ct)
    {
        await using var cn = new SqlConnection(cs);
        await cn.OpenAsync(ct);

        const string sql = @"
UPDATE dbo.BulkJobs
SET JobStatus = 'Failed',
    CompletedAt = SYSUTCDATETIME()
WHERE JobId = @J
  AND JobStatus = 'Processing';";

        await using var cmd = new SqlCommand(sql, cn);

        cmd.Parameters.Add("@J", SqlDbType.UniqueIdentifier).Value = jobId;

        await cmd.ExecuteNonQueryAsync(ct);
    }


    public async Task<(int totalRecords, List<Models.Bulk.BulkJobListItemDto> data)>
        GetJobsAsync(
            Guid admin,
            string? status,
            int page,
            int pageSize,
            CancellationToken ct)
    {
        var list = new List<Models.Bulk.BulkJobListItemDto>();

        await using var cn = new SqlConnection(cs);
        await cn.OpenAsync(ct);

        const string countSql = @"
        SELECT COUNT(*)
        FROM dbo.BulkJobs
        WHERE AdminUserId = @A
          AND (@Status IS NULL OR JobStatus = @Status);";

        int totalRecords;

        await using (var countCmd = new SqlCommand(countSql, cn))
        {
            countCmd.Parameters.Add("@A", SqlDbType.UniqueIdentifier).Value = admin;
            countCmd.Parameters.Add("@Status", SqlDbType.NVarChar, 30).Value =
                (object?)status ?? DBNull.Value;

            totalRecords = Convert.ToInt32(
                await countCmd.ExecuteScalarAsync(ct));
        }

        const string sql = @"
        SELECT
            j.JobId,
            j.CardDesignId,
            j.JobStatus,
            j.TotalCount,

            (SELECT COUNT(*)
             FROM dbo.BulkJobRecipients r
             WHERE r.JobId = j.JobId
               AND r.Status = 'Success') AS SentCount,

            (SELECT COUNT(*)
             FROM dbo.BulkJobRecipients r
             WHERE r.JobId = j.JobId
               AND r.Status = 'Failed') AS FailedCount,

            (SELECT COUNT(*)
             FROM dbo.BulkJobRecipients r
             WHERE r.JobId = j.JobId
               AND r.Status = 'Pending') AS PendingCount,

            (SELECT COUNT(*)
             FROM dbo.BulkJobRecipients r
             WHERE r.JobId = j.JobId
               AND r.Status = 'Processing') AS ProcessingCount,

            j.ScheduledAtUtc,
            j.CreatedAt,
            j.StartedAt,
            j.CompletedAt

        FROM dbo.BulkJobs j

        WHERE j.AdminUserId = @A
          AND (@Status IS NULL OR j.JobStatus = @Status)

        ORDER BY j.CreatedAt DESC

        OFFSET @Offset ROWS
        FETCH NEXT @PageSize ROWS ONLY;";

        await using var cmd = new SqlCommand(sql, cn);

        cmd.Parameters.Add("@A", SqlDbType.UniqueIdentifier).Value = admin;
        cmd.Parameters.Add("@Status", SqlDbType.NVarChar, 30).Value =
            (object?)status ?? DBNull.Value;

        cmd.Parameters.Add("@Offset", SqlDbType.Int).Value =
            (page - 1) * pageSize;

        cmd.Parameters.Add("@PageSize", SqlDbType.Int).Value = pageSize;

        await using var reader = await cmd.ExecuteReaderAsync(ct);

        while (await reader.ReadAsync(ct))
        {
            list.Add(new Models.Bulk.BulkJobListItemDto
            {
                JobId = reader.GetGuid(0),
                CardDesignId = reader.GetInt64(1),
                JobStatus = reader.GetString(2),

                TotalCount = reader.GetInt32(3),
                SentCount = reader.GetInt32(4),
                FailedCount = reader.GetInt32(5),
                PendingCount = reader.GetInt32(6),
                ProcessingCount = reader.GetInt32(7),

                ScheduledAtUtc = reader.IsDBNull(8)
                    ? null : reader.GetDateTime(8),

                CreatedAt = reader.GetDateTime(9),

                StartedAt = reader.IsDBNull(10)
                    ? null : reader.GetDateTime(10),

                CompletedAt = reader.IsDBNull(11)
                    ? null : reader.GetDateTime(11)
            });
        }

        return (totalRecords, list);
    }



    public async Task<Models.Bulk.BulkJobRecipientListResultDto?>
        GetJobRecipientsAsync(
            Guid admin,
            Guid jobId,
            string? status,
            int page,
            int pageSize,
            CancellationToken ct)
    {
        var result = new Models.Bulk.BulkJobRecipientListResultDto
        {
            Page = page,
            PageSize = pageSize
        };

        await using var cn = new SqlConnection(cs);
        await cn.OpenAsync(ct);

        // Confirm that the job belongs to this admin.
        const string jobCheckSql = @"
        SELECT COUNT(*)
        FROM dbo.BulkJobs
        WHERE JobId = @J
          AND AdminUserId = @A;";

        await using (var checkCmd = new SqlCommand(jobCheckSql, cn))
        {
            checkCmd.Parameters.Add("@J", SqlDbType.UniqueIdentifier).Value = jobId;
            checkCmd.Parameters.Add("@A", SqlDbType.UniqueIdentifier).Value = admin;

            var exists = Convert.ToInt32(
                await checkCmd.ExecuteScalarAsync(ct));

            if (exists == 0)
                return null;
        }

        const string countSql = @"
        SELECT COUNT(*)
        FROM dbo.BulkJobRecipients br
        WHERE br.JobId = @J
          AND (@Status IS NULL OR br.Status = @Status);";

        await using (var countCmd = new SqlCommand(countSql, cn))
        {
            countCmd.Parameters.Add("@J", SqlDbType.UniqueIdentifier).Value = jobId;

            countCmd.Parameters.Add("@Status", SqlDbType.NVarChar, 20).Value =
                (object?)status ?? DBNull.Value;

            result.TotalRecords = Convert.ToInt32(
                await countCmd.ExecuteScalarAsync(ct));
        }

        result.TotalPages = (int)Math.Ceiling(
            result.TotalRecords / (double)pageSize);

        const string sql = @"
        SELECT
            r.RecipientId,
            r.FullName,
            r.Email,
            r.Mobile,
            br.Status,
            br.GeneratedImagePath,
            br.ErrorMessage,
            br.ProcessedAtUtc

        FROM dbo.BulkJobRecipients br

        INNER JOIN dbo.BulkJobs j
            ON j.JobId = br.JobId

        INNER JOIN dbo.CardRecipients r
            ON r.RecipientId = br.RecipientId

        WHERE br.JobId = @J
          AND j.AdminUserId = @A
          AND (@Status IS NULL OR br.Status = @Status)

        ORDER BY r.RecipientId

        OFFSET @Offset ROWS
        FETCH NEXT @PageSize ROWS ONLY;";

        await using var cmd = new SqlCommand(sql, cn);

        cmd.Parameters.Add("@J", SqlDbType.UniqueIdentifier).Value = jobId;
        cmd.Parameters.Add("@A", SqlDbType.UniqueIdentifier).Value = admin;

        cmd.Parameters.Add("@Status", SqlDbType.NVarChar, 20).Value =
            (object?)status ?? DBNull.Value;

        cmd.Parameters.Add("@Offset", SqlDbType.Int).Value =
            (page - 1) * pageSize;

        cmd.Parameters.Add("@PageSize", SqlDbType.Int).Value = pageSize;

        await using var reader = await cmd.ExecuteReaderAsync(ct);

        while (await reader.ReadAsync(ct))
        {
            result.Data.Add(new Models.Bulk.BulkJobRecipientStatusDto
            {
                RecipientId = reader.GetInt64(0),
                FullName = reader.IsDBNull(1) ? "" : reader.GetString(1),
                Email = reader.IsDBNull(2) ? null : reader.GetString(2),
                Mobile = reader.IsDBNull(3) ? null : reader.GetString(3),

                Status = reader.IsDBNull(4) ? "" : reader.GetString(4),

                GeneratedImagePath = reader.IsDBNull(5)
                    ? null : reader.GetString(5),

                ErrorMessage = reader.IsDBNull(6)
                    ? null : reader.GetString(6),

                ProcessedAtUtc = reader.IsDBNull(7)
                    ? null : reader.GetDateTime(7)
            });
        }

        return result;
    }

}
