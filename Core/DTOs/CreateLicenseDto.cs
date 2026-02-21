using System.ComponentModel.DataAnnotations;

namespace Core.DTOs;

public class CreateLicenseDto
{
    [Required]
    public int UserId { get; set; }

    [Required]
    public DateTime ExpiresAt { get; set; }
}
