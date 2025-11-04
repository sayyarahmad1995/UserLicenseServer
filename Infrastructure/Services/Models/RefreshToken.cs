namespace Infrastructure.Services.Models;

public class RefreshToken
{
	public string TokenId { get; set; } = null!;
	public string UserId { get; set; } = null!;
	public string TokenHash { get; set; } = null!;
	public DateTime Expires { get; set; }
	public bool Revoked { get; set; } = false;
	public DateTime Created { get; set; }
	public DateTime? RevokedAt { get; set; }
	public string? ReplacedByTokenId { get; set; }
}
