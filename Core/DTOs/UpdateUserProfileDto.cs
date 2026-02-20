using System.ComponentModel.DataAnnotations;

namespace Core.DTOs;

public class UpdateUserProfileDto
{
    [StringLength(100, MinimumLength = 3, ErrorMessage = "Username must be between 3 and 100 characters")]
    [RegularExpression(@"^[a-zA-Z0-9_-]+$", ErrorMessage = "Username can only contain letters, numbers, underscores, and hyphens")]
    public string? Username { get; set; }

    [EmailAddress(ErrorMessage = "Invalid email format")]
    [StringLength(200, ErrorMessage = "Email cannot exceed 200 characters")]
    public string? Email { get; set; }
}
