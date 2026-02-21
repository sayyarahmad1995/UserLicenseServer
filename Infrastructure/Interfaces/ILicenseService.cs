using Core.DTOs;
using Core.Entities;

namespace Infrastructure.Interfaces;

public interface ILicenseService
{
    Task<License> CreateLicenseAsync(CreateLicenseDto dto, CancellationToken ct = default);
    Task RevokeLicenseAsync(int licenseId, CancellationToken ct = default);
    Task<License> RenewLicenseAsync(int licenseId, DateTime newExpiresAt, CancellationToken ct = default);
    Task DeleteLicenseAsync(int licenseId, CancellationToken ct = default);

    // ── Activation / usage tracking ──
    Task<LicenseActivation> ActivateLicenseAsync(string licenseKey, string fingerprint, string? hostname, string? ipAddress, CancellationToken ct = default);
    Task<LicenseValidationResultDto> ValidateLicenseAsync(string licenseKey, string fingerprint, CancellationToken ct = default);
    Task HeartbeatAsync(string licenseKey, string fingerprint, CancellationToken ct = default);
    Task DeactivateLicenseAsync(string licenseKey, string fingerprint, CancellationToken ct = default);
    Task<IReadOnlyList<LicenseActivation>> GetActivationsAsync(int licenseId, CancellationToken ct = default);
}
