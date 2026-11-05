using ClosedXML.Excel;
using CRUDAppUsingADO.DataAccess;
using CRUDAppUsingADO.Models.Bulk;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using System.ComponentModel.DataAnnotations;
using System.Security.Claims;

namespace CRUDAppUsingADO.Controllers;

[ApiController]
[Route("api/admin/recipients")]
[Authorize(Roles = "Admin")]
public sealed class RecipientManagementController(
    RecipientManagementDAL dal,
    AdminProfileDAL adminProfileDAL) : ControllerBase
{
    private bool TryGetAdminId(out Guid id)
    {
        var claim = User.FindFirst(ClaimTypes.NameIdentifier) ?? User.FindFirst("sub");
        return Guid.TryParse(claim?.Value, out id);
    }

    private async Task<bool> IsEnabledAdminAsync(Guid id, CancellationToken ct)
        => await adminProfileDAL.IsAdminEnabledAsync(id);

    [HttpPost]
    [ProducesResponseType(typeof(object), StatusCodes.Status201Created)]
    public async Task<IActionResult> Create([FromBody] RecipientCreateDto dto, CancellationToken ct)
    {
        if (!TryGetAdminId(out var adminId)) return Unauthorized();
        if (!await IsEnabledAdminAsync(adminId, ct)) return Forbid();
        if (!ModelState.IsValid) return ValidationProblem(ModelState);
        if (string.IsNullOrWhiteSpace(dto.Email) && string.IsNullOrWhiteSpace(dto.Mobile))
            return BadRequest("At least one contact field (Email or Mobile) is required.");
        if (!ValidMobile(dto.Mobile)) return BadRequest("Mobile must contain 10 to 15 digits.");
        var result = await dal.AddAsync(adminId, dto, ct);
        if (result.Id == 0) return Conflict(new { message = result.Conflict });
        return CreatedAtAction(nameof(GetAll), new { recipientId = result.Id }, new { recipientId = result.Id, message = "Recipient added successfully." });
    }

    [HttpPut("{id:long}")]
    public async Task<IActionResult> Update(long id, [FromBody] RecipientUpdateDto dto, CancellationToken ct)
    {
        if (!TryGetAdminId(out var adminId)) return Unauthorized();
        if (!await IsEnabledAdminAsync(adminId, ct)) return Forbid();
        if (!ModelState.IsValid) return ValidationProblem(ModelState);
        if (string.IsNullOrWhiteSpace(dto.Email) && string.IsNullOrWhiteSpace(dto.Mobile))
            return BadRequest("At least one contact field (Email or Mobile) is required.");
        if (!ValidMobile(dto.Mobile)) return BadRequest("Mobile must contain 10 to 15 digits.");
        var result = await dal.UpdateAsync(adminId, id, dto, ct);
        if (result.Conflict != null) return Conflict(new { message = result.Conflict });
        return result.Updated ? Ok(new { message = "Recipient updated successfully." }) : NotFound();
    }

    [HttpDelete("{id:long}")]
    public async Task<IActionResult> Delete(long id, CancellationToken ct)
    {
        if (!TryGetAdminId(out var adminId)) return Unauthorized();
        if (!await IsEnabledAdminAsync(adminId, ct)) return Forbid();
        var deleted = await dal.SoftDeleteAsync(adminId, id, ct);
        return deleted ? Ok(new { message = "Recipient deleted successfully." }) : Conflict(new { message = "Recipient was not found or is currently used by a pending/processing job." });
    }

    [HttpGet]
    public async Task<IActionResult> GetAll(
        [FromQuery] string? search,
        [FromQuery] string? groupName,
        [FromQuery] string? duplicateType,
        [FromQuery] bool? isActive,
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 20,
        CancellationToken ct = default)
    {
        if (!TryGetAdminId(out var adminId)) return Unauthorized();
        if (!await IsEnabledAdminAsync(adminId, ct)) return Forbid();
        if (duplicateType != null && !new[] { "Name", "Email", "Mobile" }.Contains(duplicateType, StringComparer.OrdinalIgnoreCase))
            return BadRequest("duplicateType must be Name, Email or Mobile.");
        page = Math.Max(1, page);
        pageSize = Math.Clamp(pageSize, 1, 200);
        var result = await dal.GetAsync(adminId, search, groupName,
            duplicateType == null ? null : char.ToUpperInvariant(duplicateType[0]) + duplicateType[1..].ToLowerInvariant(),
            isActive, page, pageSize, ct);
        return Ok(result);
    }

    [HttpPatch("{id:long}/status")]
    public async Task<IActionResult> SetStatus(long id, [FromBody] RecipientStatusDto dto, CancellationToken ct)
    {
        if (!TryGetAdminId(out var adminId)) return Unauthorized();
        if (!await IsEnabledAdminAsync(adminId, ct)) return Forbid();
        var updated = await dal.SetStatusAsync(adminId, id, dto.IsActive, ct);
        return updated ? Ok(new { message = dto.IsActive ? "Recipient activated." : "Recipient deactivated." }) : NotFound();
    }

    [HttpPost("import")]
    [Consumes("multipart/form-data")]
    [RequestSizeLimit(20_000_000)]
    public async Task<IActionResult> Import([FromForm] IFormFile? file, CancellationToken ct)
    {
        if (!TryGetAdminId(out var adminId)) return Unauthorized();
        if (!await IsEnabledAdminAsync(adminId, ct)) return Forbid();
        if (file == null || file.Length == 0) return BadRequest("Please select an Excel file.");
        if (file.Length > 20_000_000) return BadRequest("Maximum Excel file size is 20 MB.");
        if (!string.Equals(Path.GetExtension(file.FileName), ".xlsx", StringComparison.OrdinalIgnoreCase))
            return BadRequest("Only .xlsx files are supported.");

        var rows = new List<RecipientRow>();
        var errors = new List<string>();
        try
        {
            await using var stream = file.OpenReadStream();
            using var workbook = new XLWorkbook(stream);
            var sheet = workbook.Worksheets.FirstOrDefault();
            if (sheet == null) return BadRequest("Excel worksheet not found.");
            var headerRow = sheet.Row(1);
            var headers = headerRow.CellsUsed().ToDictionary(c => c.GetString().Trim(), c => c.Address.ColumnNumber, StringComparer.OrdinalIgnoreCase);
            if (!headers.ContainsKey("Name")) return BadRequest("Required Excel column 'Name' is missing.");
            string? Value(IXLRow row, string key) => headers.TryGetValue(key, out var col) ? row.Cell(col).GetString().Trim() : null;
            var lastRow = sheet.LastRowUsed()?.RowNumber() ?? 1;
            for (var rowNo = 2; rowNo <= lastRow; rowNo++)
            {
                var row = sheet.Row(rowNo);
                if (!row.CellsUsed().Any()) continue;
                var name = Value(row, "Name") ?? "";
                var email = Value(row, "Email");
                var mobile = Value(row, "Mobile");
                if (string.IsNullOrWhiteSpace(name) || (string.IsNullOrWhiteSpace(email) && string.IsNullOrWhiteSpace(mobile)))
                { errors.Add($"Row {rowNo}: Name and at least one of Email or Mobile are required."); continue; }
                if (!string.IsNullOrWhiteSpace(email) && !new EmailAddressAttribute().IsValid(email))
                { errors.Add($"Row {rowNo}: Invalid email address."); continue; }
                if (!ValidMobile(mobile))
                { errors.Add($"Row {rowNo}: Mobile must contain 10 to 15 digits when supplied."); continue; }
                rows.Add(new RecipientRow
                {
                    FullName = name.Trim(), Email = email, Mobile = mobile,
                    GroupName = Value(row, "GroupName"), Text1 = Value(row, "Text1"), Text2 = Value(row, "Text2"),
                    Text3 = Value(row, "Text3"), Text4 = Value(row, "Text4")
                });
            }
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            return BadRequest(new { message = "The uploaded file is not a valid Excel workbook.", detail = ex.Message });
        }

        var imported = await dal.ImportAsync(adminId, rows, Path.GetFileName(file.FileName), ct);
        return Ok(new RecipientImportResult
        {
            Inserted = imported.inserted,
            Duplicates = imported.duplicates,
            Invalid = errors.Count,
            Errors = errors
        });
    }

    [HttpGet("sample-excel")]
    public IActionResult DownloadSampleExcel()
    {
        using var workbook = new XLWorkbook();
        var ws = workbook.Worksheets.Add("Recipients");
        var headers = new[] { "Name", "Email", "Mobile", "GroupName", "Text1", "Text2", "Text3", "Text4" };
        for (var i = 0; i < headers.Length; i++) ws.Cell(1, i + 1).Value = headers[i];
        ws.Range(1, 1, 1, headers.Length).Style.Font.Bold = true;
        ws.Range(1, 1, 1, headers.Length).Style.Fill.BackgroundColor = XLColor.LightBlue;
        ws.Cell(2, 1).Value = "Rahul Sharma";
        ws.Cell(2, 2).Value = "rahul@example.com";
        ws.Cell(2, 3).Value = "9876543210";
        ws.Cell(2, 4).Value = "Diwali 2026";
        ws.Cell(2, 5).Value = "Dear";
        ws.Columns().AdjustToContents();
        using var ms = new MemoryStream();
        workbook.SaveAs(ms);
        return File(ms.ToArray(), "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet", "CustomPlus_Recipients_Sample.xlsx");
    }

    private static bool ValidMobile(string? mobile)
    {
        if (string.IsNullOrWhiteSpace(mobile)) return true;
        if (mobile.Any(c => !char.IsDigit(c) && !"+() .-".Contains(c))) return false;
        var digits = new string(mobile.Where(char.IsDigit).ToArray());
        return digits.Length is >= 10 and <= 15;
    }
}
