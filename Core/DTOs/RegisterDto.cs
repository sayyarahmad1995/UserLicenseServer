using System.ComponentModel.DataAnnotations;
using Core.Validations;

namespace Core.DTOs;

public class RegisterDto
{
   [Required(ErrorMessage = "Username is required.")]
   [StringLength(50, MinimumLength = 3, ErrorMessage = "Username must be between 3 and 50 characters.")]
   [RegularExpression(@"^[a-zA-Z0-9_-]+$", ErrorMessage = "Username can only contain letters, numbers, underscores, and hyphens.")]
   public string? Username { get; set; }

   [Required(ErrorMessage = "Email is required.")]
   [EmailAddress(ErrorMessage = "Invalid email format.")]
   public string? Email { get; set; }

   [Required(ErrorMessage = "Password is required.")]
   [StringLength(128, ErrorMessage = "Password cannot exceed 128 characters.")]
   [StrongPassword]
   public string? Password { get; set; }

}
