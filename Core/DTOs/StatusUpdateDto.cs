using System.ComponentModel.DataAnnotations;

namespace Core.DTOs;

public class StatusUpdateDto
{
    [Required(ErrorMessage = "Status is required")]
    [StringLength(20, MinimumLength = 3, ErrorMessage = "Status must be between 3 and 20 characters")]
    [RegularExpression(@"^(verify|verified|active|block|blocked|unblock)$", 
        ErrorMessage = "Status must be one of: verify, verified, active, block, blocked, unblock")]
    public string Status { get; set; } = default!;
}
