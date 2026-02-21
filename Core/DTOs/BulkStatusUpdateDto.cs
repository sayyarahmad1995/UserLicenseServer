using System.ComponentModel.DataAnnotations;

namespace Core.DTOs;

public class BulkStatusUpdateDto
{
    [Required]
    [MinLength(1, ErrorMessage = "At least one user ID is required.")]
    public List<int> UserIds { get; set; } = new();

    [Required]
    [RegularExpression("^(verify|verified|active|block|blocked|unblock)$",
        ErrorMessage = "Status must be one of: verify, active, block, unblock")]
    public string Status { get; set; } = string.Empty;
}
