using System.ComponentModel.DataAnnotations;
using Core.Validations;

namespace Core.DTOs;

public class RegisterDto
{
   [Required]
   [StringLength(50, MinimumLength = 3)]
   public string? Username { get; set; }

   [Required]
   [EmailAddress]
   public string? Email { get; set; }

   [Required]
   [StrongPassword]
   public string? Password { get; set; }

}
