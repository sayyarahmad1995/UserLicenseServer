using System.ComponentModel.DataAnnotations;

namespace Core.DTOs;

public class RenewLicenseDto
{
    /// <summary>
    /// New expiration date. Must be in the future.
    /// </summary>
    [Required]
    public DateTime NewExpiresAt { get; set; }
}
