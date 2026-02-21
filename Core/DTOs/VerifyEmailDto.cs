using System.ComponentModel.DataAnnotations;

namespace Core.DTOs;

public class VerifyEmailDto
{
    [Required]
    public string Token { get; set; } = string.Empty;
}
