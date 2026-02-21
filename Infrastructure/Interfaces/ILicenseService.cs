using Core.DTOs;
using Core.Entities;

namespace Infrastructure.Interfaces;

public interface ILicenseService
{
    Task<License> CreateLicenseAsync(CreateLicenseDto dto, CancellationToken ct = default);
    Task RevokeLicenseAsync(int licenseId, CancellationToken ct = default);
    Task<License> RenewLicenseAsync(int licenseId, DateTime newExpiresAt, CancellationToken ct = default);
    Task DeleteLicenseAsync(int licenseId, CancellationToken ct = default);
}
