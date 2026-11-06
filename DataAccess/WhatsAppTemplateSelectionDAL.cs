using CRUDAppUsingADO.Models.Bulk;
using Microsoft.Data.SqlClient;
using System.Data;

namespace CRUDAppUsingADO.DataAccess;

public sealed class WhatsAppTemplateSelectionDAL(IConfiguration config)
{
    private readonly string cs =
        config.GetConnectionString("DefaultConnection")
        ?? throw new InvalidOperationException(
            "DefaultConnection missing.");

    private const string SelectSql = @"
        SELECT
            t.TemplateId,
            t.TemplateName,
            t.TemplateType,
            t.SendType,
            t.Message,
            t.WithUserName,
            t.NoOfParameters
        FROM dbo.tblTemplates t
        INNER JOIN dbo.tblTemplateAdmin a
            ON a.TemplateAdminId = t.TemplateAdminId
        WHERE t.nBit = 1
          AND a.nBit = 1
          AND (@TemplateType IS NULL OR t.TemplateType = @TemplateType)
          AND (@SendType IS NULL OR t.SendType = @SendType)
        ORDER BY t.TemplateName;";

    public async Task<List<WhatsAppTemplateDto>> GetTemplatesAsync(
        int? templateType,
        int? sendType,
        CancellationToken ct)
    {
        var result = new List<WhatsAppTemplateDto>();

        await using var cn = new SqlConnection(cs);
        await cn.OpenAsync(ct);

        await using var cmd = new SqlCommand(SelectSql, cn);

        cmd.Parameters.Add("@TemplateType", SqlDbType.Int).Value =
            (object?)templateType ?? DBNull.Value;

        cmd.Parameters.Add("@SendType", SqlDbType.Int).Value =
            (object?)sendType ?? DBNull.Value;

        await using var reader = await cmd.ExecuteReaderAsync(ct);

        while (await reader.ReadAsync(ct))
        {
            var templateTypeValue = Convert.ToInt32(reader["TemplateType"]);
            var sendTypeValue = Convert.ToInt32(reader["SendType"]);

            result.Add(new WhatsAppTemplateDto
            {
                TemplateId = Convert.ToInt64(reader["TemplateId"]),
                TemplateName = Convert.ToString(reader["TemplateName"]) ?? "",
                TemplateType = templateTypeValue,
                TemplateTypeName = GetTemplateTypeName(templateTypeValue),
                SendType = sendTypeValue,
                SendTypeName = GetSendTypeName(sendTypeValue),
                Message = Convert.ToString(reader["Message"]) ?? "",
                WithUserName = Convert.ToBoolean(reader["WithUserName"]),
                NoOfParameters = Convert.ToInt32(reader["NoOfParameters"])
            });
        }

        return result;
    }

    public async Task<WhatsAppTemplateDto?> GetTemplateByIdAsync(
        long id,
        CancellationToken ct)
    {
        var templates = await GetTemplatesAsync(null, null, ct);

        return templates.FirstOrDefault(x => x.TemplateId == id);
    }

    private static string GetTemplateTypeName(int value)
    {
        return value switch
        {
            1 => "Utility",
            2 => "Marketing",
            _ => "Unknown"
        };
    }

    private static string GetSendTypeName(int value)
    {
        return value switch
        {
            1 => "Text",
            2 => "Text + Image",
            3 => "Text + Video",
            4 => "Text + Document",
            _ => "Unknown"
        };
    }
}