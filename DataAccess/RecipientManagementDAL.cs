using CRUDAppUsingADO.Models.Bulk;
using Microsoft.Data.SqlClient;
using System.Data;

namespace CRUDAppUsingADO.DataAccess;

public sealed class RecipientManagementDAL(IConfiguration configuration)
{
    private readonly string _cs = configuration.GetConnectionString("DefaultConnection")
        ?? throw new InvalidOperationException("ConnectionStrings:DefaultConnection is missing.");

    private static string? Clean(string? value) => string.IsNullOrWhiteSpace(value) ? null : value.Trim();
    private static string? NormalizeMobile(string? value)
    {
        if (string.IsNullOrWhiteSpace(value)) return null;
        var digits = new string(value.Where(char.IsDigit).ToArray());
        return digits.Length == 0 ? null : digits;
    }

    public async Task<(long Id, string? Conflict)> AddAsync(Guid adminId, RecipientCreateDto dto, CancellationToken ct)
    {
        await using var cn = new SqlConnection(_cs); await cn.OpenAsync(ct);
        await using var cmd = new SqlCommand(@"
IF EXISTS (SELECT 1 FROM dbo.CardRecipients WHERE AdminUserId=@A AND IsDeleted=0 AND
 ((@E IS NOT NULL AND Email=@E) OR (@M IS NOT NULL AND Mobile=@M)))
 SELECT CAST(0 AS BIGINT);
ELSE
 INSERT dbo.CardRecipients(AdminUserId,FullName,Email,Mobile,GroupName,Text1,Text2,Text3,Text4)
 OUTPUT INSERTED.RecipientId VALUES(@A,@N,@E,@M,@G,@T1,@T2,@T3,@T4);", cn);
        AddRecipientParameters(cmd, adminId, dto);
        var result = await cmd.ExecuteScalarAsync(ct);
        return result == null || result == DBNull.Value || Convert.ToInt64(result) == 0
            ? (0, "A recipient with the same email address or mobile number already exists.")
            : (Convert.ToInt64(result), null);
    }

    public async Task<(bool Updated, string? Conflict)> UpdateAsync(Guid adminId, long recipientId, RecipientUpdateDto dto, CancellationToken ct)
    {
        await using var cn = new SqlConnection(_cs); await cn.OpenAsync(ct);
        await using var cmd = new SqlCommand(@"
IF NOT EXISTS(SELECT 1 FROM dbo.CardRecipients WHERE RecipientId=@I AND AdminUserId=@A AND IsDeleted=0)
 SELECT -1;
ELSE IF EXISTS(SELECT 1 FROM dbo.CardRecipients WHERE AdminUserId=@A AND IsDeleted=0 AND RecipientId<>@I AND
 ((@E IS NOT NULL AND Email=@E) OR (@M IS NOT NULL AND Mobile=@M)))
 SELECT 0;
ELSE BEGIN
 UPDATE dbo.CardRecipients SET FullName=@N,Email=@E,Mobile=@M,GroupName=@G,Text1=@T1,Text2=@T2,Text3=@T3,Text4=@T4,UpdatedAt=SYSUTCDATETIME()
 WHERE RecipientId=@I AND AdminUserId=@A AND IsDeleted=0;
 SELECT 1;
END", cn);
        AddRecipientParameters(cmd, adminId, dto);
        cmd.Parameters.Add("@I", SqlDbType.BigInt).Value = recipientId;
        var result = Convert.ToInt32(await cmd.ExecuteScalarAsync(ct));
        return result == 1 ? (true, null) : result == 0 ? (false, "Another recipient already uses this email address or mobile number.") : (false, null);
    }

    public async Task<bool> SoftDeleteAsync(Guid adminId, long recipientId, CancellationToken ct)
    {
        await using var cn = new SqlConnection(_cs); await cn.OpenAsync(ct);
        await using var cmd = new SqlCommand(@"UPDATE dbo.CardRecipients SET IsDeleted=1,IsActive=0,UpdatedAt=SYSUTCDATETIME()
WHERE RecipientId=@I AND AdminUserId=@A AND IsDeleted=0
AND NOT EXISTS(SELECT 1 FROM dbo.BulkJobRecipients br WHERE br.RecipientId=dbo.CardRecipients.RecipientId AND br.Status IN('Pending','Processing'))", cn);
        cmd.Parameters.Add("@I", SqlDbType.BigInt).Value = recipientId;
        cmd.Parameters.Add("@A", SqlDbType.UniqueIdentifier).Value = adminId;
        return await cmd.ExecuteNonQueryAsync(ct) > 0;
    }

    public async Task<bool> SetStatusAsync(Guid adminId, long recipientId, bool active, CancellationToken ct)
    {
        await using var cn = new SqlConnection(_cs); await cn.OpenAsync(ct);
        await using var cmd = new SqlCommand("UPDATE dbo.CardRecipients SET IsActive=@B,UpdatedAt=SYSUTCDATETIME() WHERE RecipientId=@I AND AdminUserId=@A AND IsDeleted=0", cn);
        cmd.Parameters.Add("@B", SqlDbType.Bit).Value = active;
        cmd.Parameters.Add("@I", SqlDbType.BigInt).Value = recipientId;
        cmd.Parameters.Add("@A", SqlDbType.UniqueIdentifier).Value = adminId;
        return await cmd.ExecuteNonQueryAsync(ct) > 0;
    }

    public async Task<RecipientPagedResult> GetAsync(Guid adminId, string? search, string? groupName,
        string? duplicateType, bool? isActive, int page, int pageSize, CancellationToken ct)
    {
        var result = new RecipientPagedResult { Page = page, PageSize = pageSize };
        const string from = @"
FROM dbo.CardRecipients r
CROSS APPLY (SELECT COUNT(*) C FROM dbo.CardRecipients x WHERE x.AdminUserId=r.AdminUserId AND x.IsDeleted=0 AND x.FullName=r.FullName) dn
CROSS APPLY (SELECT CASE WHEN r.Email IS NULL THEN 0 ELSE (SELECT COUNT(*) FROM dbo.CardRecipients x WHERE x.AdminUserId=r.AdminUserId AND x.IsDeleted=0 AND x.Email=r.Email) END C) de
CROSS APPLY (SELECT CASE WHEN r.Mobile IS NULL THEN 0 ELSE (SELECT COUNT(*) FROM dbo.CardRecipients x WHERE x.AdminUserId=r.AdminUserId AND x.IsDeleted=0 AND x.Mobile=r.Mobile) END C) dm
WHERE r.AdminUserId=@A AND r.IsDeleted=0
AND (@Q IS NULL OR r.FullName LIKE '%'+@Q+'%' OR r.Email LIKE '%'+@Q+'%' OR r.Mobile LIKE '%'+@Q+'%' OR r.GroupName LIKE '%'+@Q+'%')
AND (@G IS NULL OR r.GroupName=@G) AND (@Active IS NULL OR r.IsActive=@Active)
AND (@Dup IS NULL OR (@Dup='Name' AND dn.C>1) OR (@Dup='Email' AND de.C>1) OR (@Dup='Mobile' AND dm.C>1))";
        await using var cn = new SqlConnection(_cs); await cn.OpenAsync(ct);
        await using (var count = new SqlCommand("SELECT COUNT_BIG(1) " + from, cn))
        {
            AddListParameters(count, adminId, search, groupName, duplicateType, isActive);
            result.TotalRecords = checked((int)Convert.ToInt64(await count.ExecuteScalarAsync(ct)));
        }
        await using (var cmd = new SqlCommand(@"SELECT r.RecipientId,r.FullName,r.Email,r.Mobile,r.GroupName,r.Text1,r.Text2,r.Text3,r.Text4,r.IsActive,r.CreatedAt,
CAST(CASE WHEN dn.C>1 THEN 1 ELSE 0 END AS bit) DuplicateName,
CAST(CASE WHEN de.C>1 THEN 1 ELSE 0 END AS bit) DuplicateEmail,
CAST(CASE WHEN dm.C>1 THEN 1 ELSE 0 END AS bit) DuplicateMobile " + from +
" ORDER BY r.RecipientId DESC OFFSET @Offset ROWS FETCH NEXT @Size ROWS ONLY", cn))
        {
            AddListParameters(cmd, adminId, search, groupName, duplicateType, isActive);
            cmd.Parameters.Add("@Offset", SqlDbType.Int).Value = (page - 1) * pageSize;
            cmd.Parameters.Add("@Size", SqlDbType.Int).Value = pageSize;
            await using var rd = await cmd.ExecuteReaderAsync(ct);
            while (await rd.ReadAsync(ct)) result.Recipients.Add(new RecipientListItemDto
            {
                RecipientId = rd.GetInt64(0), FullName = rd.GetString(1),
                Email = rd.IsDBNull(2) ? null : rd.GetString(2), Mobile = rd.IsDBNull(3) ? null : rd.GetString(3),
                GroupName = rd.IsDBNull(4) ? null : rd.GetString(4),
                Text1 = rd.IsDBNull(5) ? null : rd.GetString(5), Text2 = rd.IsDBNull(6) ? null : rd.GetString(6),
                Text3 = rd.IsDBNull(7) ? null : rd.GetString(7), Text4 = rd.IsDBNull(8) ? null : rd.GetString(8),
                IsActive = rd.GetBoolean(9), CreatedAt = rd.GetDateTime(10),
                DuplicateName = rd.GetBoolean(11), DuplicateEmail = rd.GetBoolean(12), DuplicateMobile = rd.GetBoolean(13)
            });
        }
        return result;
    }

    public async Task<(int inserted, int duplicates)> ImportAsync(Guid adminId, IReadOnlyList<RecipientRow> rows, string? source, CancellationToken ct)
    {
        var inserted = 0; var duplicates = 0;
        await using var cn = new SqlConnection(_cs); await cn.OpenAsync(ct);
        await using var tx = (SqlTransaction)await cn.BeginTransactionAsync(IsolationLevel.Serializable, ct);
        foreach (var r in rows)
        {
            await using var cmd = new SqlCommand(@"
IF EXISTS(SELECT 1 FROM dbo.CardRecipients WITH(UPDLOCK,HOLDLOCK) WHERE AdminUserId=@A AND IsDeleted=0
 AND ((@E IS NOT NULL AND Email=@E) OR (@M IS NOT NULL AND Mobile=@M))) SELECT 0;
ELSE BEGIN
 INSERT dbo.CardRecipients(AdminUserId,FullName,Email,Mobile,GroupName,Text1,Text2,Text3,Text4,SourceFile)
 VALUES(@A,@N,@E,@M,@G,@T1,@T2,@T3,@T4,@S); SELECT 1;
END", cn, tx);
            cmd.Parameters.Add("@A", SqlDbType.UniqueIdentifier).Value = adminId;
            cmd.Parameters.Add("@N", SqlDbType.NVarChar, 200).Value = r.FullName.Trim();
            cmd.Parameters.Add("@E", SqlDbType.NVarChar, 320).Value = (object?)Clean(r.Email) ?? DBNull.Value;
            cmd.Parameters.Add("@M", SqlDbType.NVarChar, 20).Value = (object?)NormalizeMobile(r.Mobile) ?? DBNull.Value;
            cmd.Parameters.Add("@G", SqlDbType.NVarChar, 150).Value = (object?)Clean(r.GroupName) ?? DBNull.Value;
            for (var i = 1; i <= 4; i++) cmd.Parameters.Add("@T" + i, SqlDbType.NVarChar, 500).Value = (object?)Clean(i switch { 1 => r.Text1, 2 => r.Text2, 3 => r.Text3, _ => r.Text4 }) ?? DBNull.Value;
            cmd.Parameters.Add("@S", SqlDbType.NVarChar, 260).Value = (object?)source ?? DBNull.Value;
            if (Convert.ToInt32(await cmd.ExecuteScalarAsync(ct)) == 1) inserted++; else duplicates++;
        }
        await tx.CommitAsync(ct);
        return (inserted, duplicates);
    }

    private static void AddRecipientParameters(SqlCommand cmd, Guid adminId, RecipientCreateDto dto)
    {
        cmd.Parameters.Add("@A", SqlDbType.UniqueIdentifier).Value = adminId;
        cmd.Parameters.Add("@N", SqlDbType.NVarChar, 200).Value = dto.FullName.Trim();
        cmd.Parameters.Add("@E", SqlDbType.NVarChar, 320).Value = (object?)Clean(dto.Email) ?? DBNull.Value;
        cmd.Parameters.Add("@M", SqlDbType.NVarChar, 20).Value = (object?)NormalizeMobile(dto.Mobile) ?? DBNull.Value;
        cmd.Parameters.Add("@G", SqlDbType.NVarChar, 150).Value = (object?)Clean(dto.GroupName) ?? DBNull.Value;
        cmd.Parameters.Add("@T1", SqlDbType.NVarChar, 500).Value = (object?)Clean(dto.Text1) ?? DBNull.Value;
        cmd.Parameters.Add("@T2", SqlDbType.NVarChar, 500).Value = (object?)Clean(dto.Text2) ?? DBNull.Value;
        cmd.Parameters.Add("@T3", SqlDbType.NVarChar, 500).Value = (object?)Clean(dto.Text3) ?? DBNull.Value;
        cmd.Parameters.Add("@T4", SqlDbType.NVarChar, 500).Value = (object?)Clean(dto.Text4) ?? DBNull.Value;
    }
    private static void AddListParameters(SqlCommand cmd, Guid adminId, string? search, string? groupName, string? duplicateType, bool? active)
    {
        cmd.Parameters.Add("@A", SqlDbType.UniqueIdentifier).Value = adminId;
        cmd.Parameters.Add("@Q", SqlDbType.NVarChar, 320).Value = (object?)Clean(search) ?? DBNull.Value;
        cmd.Parameters.Add("@G", SqlDbType.NVarChar, 150).Value = (object?)Clean(groupName) ?? DBNull.Value;
        cmd.Parameters.Add("@Dup", SqlDbType.NVarChar, 10).Value = (object?)Clean(duplicateType) ?? DBNull.Value;
        cmd.Parameters.Add("@Active", SqlDbType.Bit).Value = (object?)active ?? DBNull.Value;
    }
}
