using System.ComponentModel.DataAnnotations;

namespace CRUDAppUsingADO.Models.Bulk;

public class RecipientCreateDto
{
    [Required, StringLength(200, MinimumLength = 1)] public string FullName { get; set; } = "";
    [EmailAddress, StringLength(320)] public string? Email { get; set; }
    [StringLength(20)] public string? Mobile { get; set; }
    [StringLength(150)] public string? GroupName { get; set; }
    [StringLength(500)] public string? Text1 { get; set; }
    [StringLength(500)] public string? Text2 { get; set; }
    [StringLength(500)] public string? Text3 { get; set; }
    [StringLength(500)] public string? Text4 { get; set; }
}

public sealed class RecipientUpdateDto : RecipientCreateDto { }

public sealed class RecipientListItemDto
{
    public long RecipientId { get; set; }
    public string FullName { get; set; } = "";
    public string? Email { get; set; }
    public string? Mobile { get; set; }
    public string? GroupName { get; set; }
    public string? Text1 { get; set; }
    public string? Text2 { get; set; }
    public string? Text3 { get; set; }
    public string? Text4 { get; set; }
    public bool IsActive { get; set; }
    public DateTime CreatedAt { get; set; }
    public bool DuplicateName { get; set; }
    public bool DuplicateEmail { get; set; }
    public bool DuplicateMobile { get; set; }
}

public sealed class RecipientPagedResult
{
    public int Page { get; set; }
    public int PageSize { get; set; }
    public int TotalRecords { get; set; }
    public int TotalPages => (int)Math.Ceiling(TotalRecords / (double)PageSize);
    public List<RecipientListItemDto> Recipients { get; set; } = [];
}

