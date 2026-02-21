using System.ComponentModel.DataAnnotations;

namespace Core.DTOs;

public class UpdateRoleDto
{
    [Required]
    [RegularExpression("^(Admin|User)$", ErrorMessage = "Role must be 'Admin' or 'User'.")]
    public string Role { get; set; } = string.Empty;
}
