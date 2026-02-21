using System.ComponentModel.DataAnnotations;

namespace Core.DTOs;

public class LoginDto
{
    [Required(ErrorMessage = "Username is required.")]
    [StringLength(50, MinimumLength = 3, ErrorMessage = "Username must be between 3 and 50 characters.")]
    public required string Username { get; set; }

    [Required(ErrorMessage = "Password is required.")]
    [StringLength(128, ErrorMessage = "Password cannot exceed 128 characters.")]
    public required string Password { get; set; }
}
