using System.ComponentModel.DataAnnotations;

namespace Core.DTOs;

/// <summary>
/// DTO for updating license status (admin operation).
/// </summary>
public class UpdateLicenseStatusDto
{
    /// <summary>
    /// New license status: Active, Suspended, Revoked, Expired
    /// </summary>
    [Required(ErrorMessage = "Status is required.")]
    [StringLength(20)]
    public string? Status { get; set; }

    /// <summary>
    /// Reason for status change
    /// </summary>
    [StringLength(500)]
    public string? Reason { get; set; }
}
