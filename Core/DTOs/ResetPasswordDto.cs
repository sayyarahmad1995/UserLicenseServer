using Core.Validations;
using System.ComponentModel.DataAnnotations;

namespace Core.DTOs;

public class ResetPasswordDto
{
    [Required]
    public string Token { get; set; } = string.Empty;

    [Required]
    [StringLength(128, MinimumLength = 8)]
    [StrongPassword]
    public string NewPassword { get; set; } = string.Empty;
}
