namespace Core.DTOs;

public class LoginResultDto
{
    public DateTime? AccessTokenExpires { get; set; }
    public string? Message { get; set; }
}
