using System.ComponentModel.DataAnnotations;

namespace CRUDAppUsingADO.Models.Bulk;

public sealed class RecipientBulkActionDto
{
    [Required]
    [MinLength(1)]
    public List<long> RecipientIds { get; set; } = [];
}
