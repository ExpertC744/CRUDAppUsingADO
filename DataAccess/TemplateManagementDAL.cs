using CRUDAppUsingADO.Models;
using Microsoft.Data.SqlClient;
using System.Data;

namespace CRUDAppUsingADO.DataAccess
{
    public class TemplateManagementDAL
    {
        private readonly IConfiguration _configuration;

        public TemplateManagementDAL(IConfiguration configuration)
        {
            _configuration = configuration;
        }

        private SqlConnection GetConnection()
        {
            return new SqlConnection(_configuration.GetConnectionString("DefaultConnection"));
        }

        // ============================================
        // TEMPLATE ADMIN METHODS
        // ============================================

        public async Task<int> InsertTemplateAdminAsync(TemplateAdminDto dto)
        {
            using var con = GetConnection();
            using var cmd = new SqlCommand(@"
                INSERT INTO dbo.tblTemplateAdmin
                (UserId, Email, Mobile, Details)
                VALUES
                (@UserId, @Email, @Mobile, @Details);

                SELECT CAST(SCOPE_IDENTITY() AS INT);", con);

            cmd.Parameters.Add("@UserId", SqlDbType.UniqueIdentifier).Value =
                (object?)dto.UserId ?? DBNull.Value;

            cmd.Parameters.Add("@Email", SqlDbType.NVarChar, 150).Value =
                dto.Email.Trim();

            cmd.Parameters.Add("@Mobile", SqlDbType.NVarChar, 20).Value =
                dto.Mobile.Trim();

            cmd.Parameters.Add("@Details", SqlDbType.NVarChar, 500).Value =
                (object?)dto.Details ?? DBNull.Value;

            await con.OpenAsync();
            return (int)await cmd.ExecuteScalarAsync();
        }

        public async Task<bool> UpdateTemplateAdminAsync(
            int id, TemplateAdminDto dto)
        {
            using var con = GetConnection();
            using var cmd = new SqlCommand(@"
                UPDATE dbo.tblTemplateAdmin
                SET UserId = @UserId,
                    Email = @Email,
                    Mobile = @Mobile,
                    Details = @Details,
                    UpdatedAt = SYSUTCDATETIME()
                WHERE TemplateAdminId = @Id;", con);

            cmd.Parameters.Add("@Id", SqlDbType.Int).Value = id;
            cmd.Parameters.Add("@UserId", SqlDbType.UniqueIdentifier).Value =
                (object?)dto.UserId ?? DBNull.Value;
            cmd.Parameters.Add("@Email", SqlDbType.NVarChar, 150).Value =
                dto.Email.Trim();
            cmd.Parameters.Add("@Mobile", SqlDbType.NVarChar, 20).Value =
                dto.Mobile.Trim();
            cmd.Parameters.Add("@Details", SqlDbType.NVarChar, 500).Value =
                (object?)dto.Details ?? DBNull.Value;

            await con.OpenAsync();
            return await cmd.ExecuteNonQueryAsync() > 0;
        }

        public async Task<Dictionary<string, object?>?> GetTemplateAdminByIdAsync(
            int id)
        {
            using var con = GetConnection();
            using var cmd = new SqlCommand(@"
                SELECT TemplateAdminId, UserId, Email, Mobile, Details,
                       nBit, CreatedAt, UpdatedAt
                FROM dbo.tblTemplateAdmin
                WHERE TemplateAdminId = @Id;", con);

            cmd.Parameters.Add("@Id", SqlDbType.Int).Value = id;

            await con.OpenAsync();
            using var dr = await cmd.ExecuteReaderAsync();

            if (!await dr.ReadAsync())
                return null;

            return MapTemplateAdmin(dr);
        }

        public async Task<List<Dictionary<string, object?>>> GetAllTemplateAdminsAsync()
        {
            var list = new List<Dictionary<string, object?>>();

            using var con = GetConnection();
            using var cmd = new SqlCommand(@"
                SELECT TemplateAdminId, UserId, Email, Mobile, Details,
                       nBit, CreatedAt, UpdatedAt
                FROM dbo.tblTemplateAdmin
                ORDER BY TemplateAdminId DESC;", con);

            await con.OpenAsync();
            using var dr = await cmd.ExecuteReaderAsync();

            while (await dr.ReadAsync())
                list.Add(MapTemplateAdmin(dr));

            return list;
        }

        public async Task<bool> UpdateTemplateAdminStatusAsync(
            int id, bool enabled)
        {
            using var con = GetConnection();
            using var cmd = new SqlCommand(@"
                UPDATE dbo.tblTemplateAdmin
                SET nBit = @nBit,
                    UpdatedAt = SYSUTCDATETIME()
                WHERE TemplateAdminId = @Id;", con);

            cmd.Parameters.Add("@Id", SqlDbType.Int).Value = id;
            cmd.Parameters.Add("@nBit", SqlDbType.Bit).Value = enabled;

            await con.OpenAsync();
            return await cmd.ExecuteNonQueryAsync() > 0;
        }

        // ============================================
        // TEMPLATE METHODS
        // ============================================

        public async Task<int> InsertTemplateAsync(TemplateDto dto)
        {
            using var con = GetConnection();
            using var cmd = new SqlCommand(@"
                INSERT INTO dbo.tblTemplates
                (
                    TemplateType, SendType, TemplateName, Message,
                    WithUserName, NoOfParameters, TemplateAdminId
                )
                VALUES
                (
                    @TemplateType, @SendType, @TemplateName, @Message,
                    @WithUserName, @NoOfParameters, @TemplateAdminId
                );

                SELECT CAST(SCOPE_IDENTITY() AS INT);", con);

            AddTemplateParameters(cmd, dto);

            await con.OpenAsync();
            return (int)await cmd.ExecuteScalarAsync();
        }

        public async Task<bool> UpdateTemplateAsync(int id, TemplateDto dto)
        {
            using var con = GetConnection();
            using var cmd = new SqlCommand(@"
                UPDATE dbo.tblTemplates
                SET TemplateType = @TemplateType,
                    SendType = @SendType,
                    TemplateName = @TemplateName,
                    Message = @Message,
                    WithUserName = @WithUserName,
                    NoOfParameters = @NoOfParameters,
                    TemplateAdminId = @TemplateAdminId,
                    UpdatedAt = SYSUTCDATETIME()
                WHERE TemplateId = @Id;", con);

            AddTemplateParameters(cmd, dto);
            cmd.Parameters.Add("@Id", SqlDbType.Int).Value = id;

            await con.OpenAsync();
            return await cmd.ExecuteNonQueryAsync() > 0;
        }

        public async Task<Dictionary<string, object?>?> GetTemplateByIdAsync(int id)
        {
            using var con = GetConnection();
            using var cmd = new SqlCommand(@"
                SELECT
                    t.TemplateId, t.TemplateType,
                    CASE t.TemplateType
                        WHEN 1 THEN 'Utility'
                        WHEN 2 THEN 'Marketing'
                    END AS TemplateTypeName,
                    t.SendType,
                    CASE t.SendType
                        WHEN 1 THEN 'Only Text'
                        WHEN 2 THEN 'Text + Image'
                        WHEN 3 THEN 'Text + Video'
                        WHEN 4 THEN 'Text + Document'
                        WHEN 5 THEN 'Text + Button + Image'
                        WHEN 6 THEN 'Text + Button + Video'
                    END AS SendTypeName,
                    t.TemplateName, t.Message, t.WithUserName,
                    t.NoOfParameters, t.TemplateAdminId,
                    a.Email AS TemplateAdminEmail,
                    a.Mobile AS TemplateAdminMobile,
                    t.nBit, t.CreatedAt, t.UpdatedAt
                FROM dbo.tblTemplates t
                INNER JOIN dbo.tblTemplateAdmin a
                    ON a.TemplateAdminId = t.TemplateAdminId
                WHERE t.TemplateId = @Id;", con);

            cmd.Parameters.Add("@Id", SqlDbType.Int).Value = id;

            await con.OpenAsync();
            using var dr = await cmd.ExecuteReaderAsync();

            if (!await dr.ReadAsync())
                return null;

            return MapTemplate(dr);
        }

        public async Task<List<Dictionary<string, object?>>> GetAllTemplatesAsync()
        {
            var list = new List<Dictionary<string, object?>>();

            using var con = GetConnection();
            using var cmd = new SqlCommand(@"
                SELECT
                    t.TemplateId, t.TemplateType,
                    CASE t.TemplateType
                        WHEN 1 THEN 'Utility'
                        WHEN 2 THEN 'Marketing'
                    END AS TemplateTypeName,
                    t.SendType,
                    CASE t.SendType
                        WHEN 1 THEN 'Only Text'
                        WHEN 2 THEN 'Text + Image'
                        WHEN 3 THEN 'Text + Video'
                        WHEN 4 THEN 'Text + Document'
                        WHEN 5 THEN 'Text + Button + Image'
                        WHEN 6 THEN 'Text + Button + Video'
                    END AS SendTypeName,
                    t.TemplateName, t.Message, t.WithUserName,
                    t.NoOfParameters, t.TemplateAdminId,
                    a.Email AS TemplateAdminEmail,
                    a.Mobile AS TemplateAdminMobile,
                    t.nBit, t.CreatedAt, t.UpdatedAt
                FROM dbo.tblTemplates t
                INNER JOIN dbo.tblTemplateAdmin a
                    ON a.TemplateAdminId = t.TemplateAdminId
                ORDER BY t.TemplateId DESC;", con);

            await con.OpenAsync();
            using var dr = await cmd.ExecuteReaderAsync();

            while (await dr.ReadAsync())
                list.Add(MapTemplate(dr));

            return list;
        }

        public async Task<bool> UpdateTemplateStatusAsync(int id, bool enabled)
        {
            using var con = GetConnection();
            using var cmd = new SqlCommand(@"
                UPDATE dbo.tblTemplates
                SET nBit = @nBit,
                    UpdatedAt = SYSUTCDATETIME()
                WHERE TemplateId = @Id;", con);

            cmd.Parameters.Add("@Id", SqlDbType.Int).Value = id;
            cmd.Parameters.Add("@nBit", SqlDbType.Bit).Value = enabled;

            await con.OpenAsync();
            return await cmd.ExecuteNonQueryAsync() > 0;
        }

        // ============================================
        // HELPERS
        // ============================================

        private static void AddTemplateParameters(
            SqlCommand cmd, TemplateDto dto)
        {
            cmd.Parameters.Add("@TemplateType", SqlDbType.Int).Value =
                dto.TemplateType;

            cmd.Parameters.Add("@SendType", SqlDbType.Int).Value =
                dto.SendType;

            cmd.Parameters.Add("@TemplateName", SqlDbType.NVarChar, 200).Value =
                dto.TemplateName.Trim();

            cmd.Parameters.Add("@Message", SqlDbType.NVarChar, -1).Value =
                dto.Message;

            cmd.Parameters.Add("@WithUserName", SqlDbType.Bit).Value =
                dto.WithUserName;

            cmd.Parameters.Add("@NoOfParameters", SqlDbType.Int).Value =
                (object?)dto.NoOfParameters ?? DBNull.Value;

            cmd.Parameters.Add("@TemplateAdminId", SqlDbType.Int).Value =
                dto.TemplateAdminId;
        }

        private static Dictionary<string, object?> MapTemplateAdmin(
            SqlDataReader dr)
        {
            return new Dictionary<string, object?>
            {
                ["TemplateAdminId"] = dr.GetInt32(dr.GetOrdinal("TemplateAdminId")),
                ["UserId"] = dr["UserId"] == DBNull.Value ? null : dr["UserId"],
                ["Email"] = dr["Email"],
                ["Mobile"] = dr["Mobile"],
                ["Details"] = dr["Details"] == DBNull.Value ? null : dr["Details"],
                ["nBit"] = dr["nBit"],
                ["CreatedAt"] = dr["CreatedAt"],
                ["UpdatedAt"] = dr["UpdatedAt"] == DBNull.Value ? null : dr["UpdatedAt"]
            };
        }

        private static Dictionary<string, object?> MapTemplate(SqlDataReader dr)
        {
            return new Dictionary<string, object?>
            {
                ["TemplateId"] = dr["TemplateId"],
                ["TemplateType"] = dr["TemplateType"],
                ["TemplateTypeName"] = dr["TemplateTypeName"],
                ["SendType"] = dr["SendType"],
                ["SendTypeName"] = dr["SendTypeName"],
                ["TemplateName"] = dr["TemplateName"],
                ["Message"] = dr["Message"],
                ["WithUserName"] = dr["WithUserName"],
                ["NoOfParameters"] = dr["NoOfParameters"] == DBNull.Value
                    ? null : dr["NoOfParameters"],
                ["TemplateAdminId"] = dr["TemplateAdminId"],
                ["TemplateAdminEmail"] = dr["TemplateAdminEmail"],
                ["TemplateAdminMobile"] = dr["TemplateAdminMobile"],
                ["nBit"] = dr["nBit"],
                ["CreatedAt"] = dr["CreatedAt"],
                ["UpdatedAt"] = dr["UpdatedAt"] == DBNull.Value
                    ? null : dr["UpdatedAt"]
            };
        }
    }
}