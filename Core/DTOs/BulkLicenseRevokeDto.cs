using System.ComponentModel.DataAnnotations;

namespace Core.DTOs;

public class BulkLicenseRevokeDto
{
    [Required]
    [MinLength(1, ErrorMessage = "At least one license ID is required.")]
    public List<int> LicenseIds { get; set; } = new();
}
