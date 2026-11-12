
namespace CRUDAppUsingADO.Models.Bulk;

public sealed class BulkJobListItemDto
{
    public Guid JobId { get; set; }
    public long CardDesignId { get; set; }

    public string JobStatus { get; set; } = "";

    public int TotalCount { get; set; }
    public int SentCount { get; set; }
    public int FailedCount { get; set; }
    public int PendingCount { get; set; }
    public int ProcessingCount { get; set; }

    public DateTime? ScheduledAtUtc { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime? StartedAt { get; set; }
    public DateTime? CompletedAt { get; set; }
}

public sealed class BulkJobRecipientStatusDto
{
    public long RecipientId { get; set; }
    public string FullName { get; set; } = "";

    public string? Email { get; set; }
    public string? Mobile { get; set; }

    public string Status { get; set; } = "";

    public string? GeneratedImagePath { get; set; }
    public string? ErrorMessage { get; set; }

    public DateTime? ProcessedAtUtc { get; set; }
}

public sealed class BulkJobRecipientListResultDto
{
    public int TotalRecords { get; set; }
    public int Page { get; set; }
    public int PageSize { get; set; }
    public int TotalPages { get; set; }

    public List<BulkJobRecipientStatusDto> Data { get; set; } = [];
}
