using Core.DTOs;
using Core.Entities;
using Core.Enums;
using Core.Interfaces;
using Infrastructure.Interfaces;
using Microsoft.Extensions.Logging;
using System.Security.Cryptography;

namespace Infrastructure.Services;

public class LicenseService : ILicenseService
{
    private readonly IUnitOfWork _unitOfWork;
    private readonly ILogger<LicenseService> _logger;

    public LicenseService(IUnitOfWork unitOfWork, ILogger<LicenseService> logger)
    {
        _unitOfWork = unitOfWork;
        _logger = logger;
    }

    public async Task<License> CreateLicenseAsync(CreateLicenseDto dto, CancellationToken ct = default)
    {
        var user = await _unitOfWork.UserRepository.GetByIdAsync(dto.UserId, ct)
            ?? throw new InvalidOperationException("User not found.");

        if (dto.ExpiresAt <= DateTime.UtcNow)
            throw new ArgumentException("Expiration date must be in the future.");

        var license = new License
        {
            LicenseKey = GenerateLicenseKey(),
            CreatedAt = DateTime.UtcNow,
            ExpiresAt = dto.ExpiresAt.ToUniversalTime(),
            Status = LicenseStatus.Active,
            UserId = dto.UserId
        };

        _unitOfWork.LicenseRepository.Add(license);
        await _unitOfWork.CompleteAsync(ct);

        _logger.LogInformation("License {LicenseKey} created for user {UserId}, expires {ExpiresAt}",
            license.LicenseKey, dto.UserId, license.ExpiresAt);

        return license;
    }

    public async Task RevokeLicenseAsync(int licenseId, CancellationToken ct = default)
    {
        var license = await _unitOfWork.LicenseRepository.GetByIdAsync(licenseId, ct)
            ?? throw new InvalidOperationException("License not found.");

        if (license.Status == LicenseStatus.Revoked)
            throw new InvalidOperationException("License is already revoked.");

        license.Status = LicenseStatus.Revoked;
        license.RevokedAt = DateTime.UtcNow;
        _unitOfWork.LicenseRepository.Update(license);
        await _unitOfWork.CompleteAsync(ct);

        _logger.LogInformation("License {LicenseId} (key: {LicenseKey}) revoked",
            licenseId, license.LicenseKey);
    }

    public async Task<License> RenewLicenseAsync(int licenseId, DateTime newExpiresAt, CancellationToken ct = default)
    {
        var license = await _unitOfWork.LicenseRepository.GetByIdAsync(licenseId, ct)
            ?? throw new InvalidOperationException("License not found.");

        if (newExpiresAt <= DateTime.UtcNow)
            throw new ArgumentException("New expiration date must be in the future.");

        if (license.Status == LicenseStatus.Revoked)
            throw new InvalidOperationException("Cannot renew a revoked license.");

        license.ExpiresAt = newExpiresAt.ToUniversalTime();
        license.Status = LicenseStatus.Active;
        license.RevokedAt = null;
        _unitOfWork.LicenseRepository.Update(license);
        await _unitOfWork.CompleteAsync(ct);

        _logger.LogInformation("License {LicenseId} renewed, new expiry: {ExpiresAt}",
            licenseId, newExpiresAt);

        return license;
    }

    public async Task DeleteLicenseAsync(int licenseId, CancellationToken ct = default)
    {
        var license = await _unitOfWork.LicenseRepository.GetByIdAsync(licenseId, ct)
            ?? throw new InvalidOperationException("License not found.");

        _unitOfWork.LicenseRepository.Delete(license);
        await _unitOfWork.CompleteAsync(ct);

        _logger.LogInformation("License {LicenseId} (key: {LicenseKey}) deleted permanently",
            licenseId, license.LicenseKey);
    }

    /// <summary>
    /// Generates a cryptographically random license key in the format XXXXX-XXXXX-XXXXX-XXXXX-XXXXX.
    /// </summary>
    private static string GenerateLicenseKey()
    {
        const string chars = "ABCDEFGHIJKLMNOPQRSTUVWXYZ0123456789";
        var segments = new string[5];

        for (int i = 0; i < 5; i++)
        {
            var bytes = RandomNumberGenerator.GetBytes(5);
            var segment = new char[5];
            for (int j = 0; j < 5; j++)
            {
                segment[j] = chars[bytes[j] % chars.Length];
            }
            segments[i] = new string(segment);
        }

        return string.Join("-", segments);
    }
}
