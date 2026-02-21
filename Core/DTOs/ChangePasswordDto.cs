using System.ComponentModel.DataAnnotations;
using Core.Validations;

namespace Core.DTOs;

public class ChangePasswordDto
{
    [Required(ErrorMessage = "Current password is required.")]
    public required string CurrentPassword { get; set; }

    [Required(ErrorMessage = "New password is required.")]
    [StringLength(128, ErrorMessage = "Password cannot exceed 128 characters.")]
    [StrongPassword]
    public required string NewPassword { get; set; }
}
