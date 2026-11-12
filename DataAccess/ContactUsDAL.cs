
using CRUDAppUsingADO.Models;
using Microsoft.Data.SqlClient;
using System.Data;

namespace CRUDAppUsingADO.DataAccess;

public sealed class ContactUsDAL
{
    private readonly IConfiguration _configuration;

    public ContactUsDAL(IConfiguration configuration)
    {
        _configuration = configuration;
    }

    private SqlConnection GetConnection()
    {
        return new SqlConnection(
            _configuration.GetConnectionString("DefaultConnection"));
    }

    // ==========================================
    // INSERT CONTACT MESSAGE
    // ==========================================
    public async Task<long> InsertAsync(
        ContactUsCreateDto dto,
        CancellationToken ct = default)
    {
        const string sql = @"
            INSERT INTO dbo.ContactUsMessages
            (
                FullName,
                Email,
                PhoneNumber,
                Subject,
                Message
            )
            OUTPUT INSERTED.ContactId
            VALUES
            (
                @FullName,
                @Email,
                @PhoneNumber,
                @Subject,
                @Message
            );";

        await using var con = GetConnection();
        await con.OpenAsync(ct);

        await using var cmd = new SqlCommand(sql, con);

        cmd.Parameters.Add("@FullName", SqlDbType.NVarChar, 150)
            .Value = dto.FullName.Trim();

        cmd.Parameters.Add("@Email", SqlDbType.NVarChar, 256)
            .Value = dto.Email.Trim();

        cmd.Parameters.Add("@PhoneNumber", SqlDbType.NVarChar, 25)
            .Value = dto.PhoneNumber.Trim();

        cmd.Parameters.Add("@Subject", SqlDbType.NVarChar, 100)
            .Value = string.IsNullOrWhiteSpace(dto.Subject)
                ? DBNull.Value
                : dto.Subject.Trim();

        cmd.Parameters.Add("@Message", SqlDbType.NVarChar, 200)
            .Value = dto.Message.Trim();

        var result = await cmd.ExecuteScalarAsync(ct);

        return Convert.ToInt64(result);
    }

    // ==========================================
    // GET ALL CONTACT MESSAGES
    // ==========================================
    public async Task<List<ContactUsMessageDto>> GetAllAsync(
        int page = 1,
        int pageSize = 20,
        bool? isRead = null,
        CancellationToken ct = default)
    {
        var result = new List<ContactUsMessageDto>();

        const string sql = @"
            SELECT
                ContactId,
                FullName,
                Email,
                PhoneNumber,
                Subject,
                Message,
                IsRead,
                CreatedAt
            FROM dbo.ContactUsMessages
            WHERE (@IsRead IS NULL OR IsRead = @IsRead)
            ORDER BY CreatedAt DESC, ContactId DESC
            OFFSET @Offset ROWS
            FETCH NEXT @PageSize ROWS ONLY;";

        await using var con = GetConnection();
        await con.OpenAsync(ct);

        await using var cmd = new SqlCommand(sql, con);

        cmd.Parameters.Add("@IsRead", SqlDbType.Bit).Value =
            (object?)isRead ?? DBNull.Value;

        cmd.Parameters.Add("@Offset", SqlDbType.Int).Value =
            (page - 1) * pageSize;

        cmd.Parameters.Add("@PageSize", SqlDbType.Int).Value = pageSize;

        await using var reader = await cmd.ExecuteReaderAsync(ct);

        while (await reader.ReadAsync(ct))
        {
            result.Add(MapContact(reader));
        }

        return result;
    }

    // ==========================================
    // GET CONTACT MESSAGE BY ID
    // ==========================================
    public async Task<ContactUsMessageDto?> GetByIdAsync(
        long contactId,
        CancellationToken ct = default)
    {
        const string sql = @"
            SELECT
                ContactId,
                FullName,
                Email,
                PhoneNumber,
                Subject,
                Message,
                IsRead,
                CreatedAt
            FROM dbo.ContactUsMessages
            WHERE ContactId = @ContactId;";

        await using var con = GetConnection();
        await con.OpenAsync(ct);

        await using var cmd = new SqlCommand(sql, con);

        cmd.Parameters.Add("@ContactId", SqlDbType.BigInt).Value = contactId;

        await using var reader = await cmd.ExecuteReaderAsync(ct);

        if (!await reader.ReadAsync(ct))
            return null;

        return MapContact(reader);
    }

    // ==========================================
    // MARK CONTACT MESSAGE AS READ
    // ==========================================
    public async Task<bool> MarkAsReadAsync(
        long contactId,
        CancellationToken ct = default)
    {
        const string sql = @"
            UPDATE dbo.ContactUsMessages
            SET IsRead = 1
            WHERE ContactId = @ContactId
              AND IsRead = 0;";

        await using var con = GetConnection();
        await con.OpenAsync(ct);

        await using var cmd = new SqlCommand(sql, con);

        cmd.Parameters.Add("@ContactId", SqlDbType.BigInt).Value = contactId;

        return await cmd.ExecuteNonQueryAsync(ct) > 0;
    }

    // ==========================================
    // TOTAL CONTACT COUNT
    // ==========================================
    public async Task<int> GetCountAsync(
        bool? isRead = null,
        CancellationToken ct = default)
    {
        const string sql = @"
            SELECT COUNT(*)
            FROM dbo.ContactUsMessages
            WHERE (@IsRead IS NULL OR IsRead = @IsRead);";

        await using var con = GetConnection();
        await con.OpenAsync(ct);

        await using var cmd = new SqlCommand(sql, con);

        cmd.Parameters.Add("@IsRead", SqlDbType.Bit).Value =
            (object?)isRead ?? DBNull.Value;

        return Convert.ToInt32(await cmd.ExecuteScalarAsync(ct));
    }

    private static ContactUsMessageDto MapContact(SqlDataReader reader)
    {
        return new ContactUsMessageDto
        {
            ContactId = reader.GetInt64(0),
            FullName = reader.GetString(1),
            Email = reader.GetString(2),
            PhoneNumber = reader.GetString(3),
            Subject = reader.IsDBNull(4) ? null : reader.GetString(4),
            Message = reader.GetString(5),
            IsRead = reader.GetBoolean(6),
            CreatedAt = reader.GetDateTime(7)
        };
    }
}
