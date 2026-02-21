using Core.DTOs;
using Microsoft.AspNetCore.Http;

namespace Infrastructure.Interfaces;

public interface IAuthService
{
    Task<LoginResultDto> LoginAsync(LoginDto dto, HttpResponse response, CancellationToken ct = default);
    Task ChangePasswordAsync(int userId, string currentPassword, string newPassword, CancellationToken ct = default);
    Task<string> GenerateVerificationTokenAsync(int userId, CancellationToken ct = default);
    Task VerifyEmailAsync(string token, CancellationToken ct = default);
    Task ResendVerificationAsync(string email, CancellationToken ct = default);
    Task<string> GeneratePasswordResetTokenAsync(string email, CancellationToken ct = default);
    Task ResetPasswordAsync(string token, string newPassword, CancellationToken ct = default);
}
