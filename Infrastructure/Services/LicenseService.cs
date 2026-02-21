using Core.DTOs;
using Core.Entities;
using Core.Enums;
using Core.Interfaces;
using Infrastructure.Data;
using Infrastructure.Interfaces;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using System.Security.Cryptography;

namespace Infrastructure.Services;

public class LicenseService : ILicenseService
{
    private readonly IUnitOfWork _unitOfWork;
    private readonly AppDbContext _context;
    private readonly ILogger<LicenseService> _logger;

    public LicenseService(IUnitOfWork unitOfWork, AppDbContext context, ILogger<LicenseService> logger)
    {
        _unitOfWork = unitOfWork;
        _context = context;
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

    // ── Activation / usage tracking ──

    public async Task<LicenseActivation> ActivateLicenseAsync(
        string licenseKey, string fingerprint, string? hostname, string? ipAddress,
        CancellationToken ct = default)
    {
        var license = await _context.Licenses
            .Include(l => l.Activations)
            .FirstOrDefaultAsync(l => l.LicenseKey == licenseKey, ct)
            ?? throw new InvalidOperationException("License not found.");

        if (license.Status != LicenseStatus.Active)
            throw new InvalidOperationException($"License is {license.Status}.");

        if (license.ExpiresAt <= DateTime.UtcNow)
            throw new InvalidOperationException("License has expired.");

        // Check if this machine already has an active activation
        var existing = license.Activations
            .FirstOrDefault(a => a.MachineFingerprint == fingerprint && a.DeactivatedAt == null);

        if (existing != null)
        {
            // Re-activate / update heartbeat
            existing.LastSeenAt = DateTime.UtcNow;
            existing.IpAddress = ipAddress;
            existing.Hostname = hostname ?? existing.Hostname;
            await _unitOfWork.CompleteAsync(ct);

            _logger.LogInformation("License {LicenseKey} re-activated on machine {Fingerprint}",
                licenseKey, fingerprint);
            return existing;
        }

        // Check max activations
        var activeCount = license.Activations.Count(a => a.DeactivatedAt == null);
        if (license.MaxActivations > 0 && activeCount >= license.MaxActivations)
            throw new InvalidOperationException(
                $"Maximum activations ({license.MaxActivations}) reached. Deactivate another machine first.");

        var activation = new LicenseActivation
        {
            LicenseId = license.Id,
            MachineFingerprint = fingerprint,
            Hostname = hostname,
            IpAddress = ipAddress,
            ActivatedAt = DateTime.UtcNow,
            LastSeenAt = DateTime.UtcNow
        };

        _context.LicenseActivations.Add(activation);
        await _unitOfWork.CompleteAsync(ct);

        _logger.LogInformation("License {LicenseKey} activated on machine {Fingerprint} (activation #{Count})",
            licenseKey, fingerprint, activeCount + 1);

        return activation;
    }

    public async Task<LicenseValidationResultDto> ValidateLicenseAsync(
        string licenseKey, string fingerprint, CancellationToken ct = default)
    {
        var license = await _context.Licenses
            .Include(l => l.Activations)
            .FirstOrDefaultAsync(l => l.LicenseKey == licenseKey, ct);

        if (license == null)
            return new LicenseValidationResultDto { Valid = false, Reason = "License not found." };

        if (license.Status != LicenseStatus.Active)
            return new LicenseValidationResultDto
            {
                Valid = false,
                LicenseKey = licenseKey,
                Status = license.Status.ToString(),
                ExpiresAt = license.ExpiresAt,
                Reason = $"License is {license.Status}."
            };

        if (license.ExpiresAt <= DateTime.UtcNow)
            return new LicenseValidationResultDto
            {
                Valid = false,
                LicenseKey = licenseKey,
                Status = "Expired",
                ExpiresAt = license.ExpiresAt,
                Reason = "License has expired."
            };

        var activation = license.Activations
            .FirstOrDefault(a => a.MachineFingerprint == fingerprint && a.DeactivatedAt == null);

        if (activation == null)
            return new LicenseValidationResultDto
            {
                Valid = false,
                LicenseKey = licenseKey,
                Status = license.Status.ToString(),
                ExpiresAt = license.ExpiresAt,
                Reason = "License is not activated on this machine."
            };

        // Update last seen
        activation.LastSeenAt = DateTime.UtcNow;
        await _unitOfWork.CompleteAsync(ct);

        return new LicenseValidationResultDto
        {
            Valid = true,
            LicenseKey = licenseKey,
            Status = license.Status.ToString(),
            ExpiresAt = license.ExpiresAt
        };
    }

    public async Task HeartbeatAsync(string licenseKey, string fingerprint, CancellationToken ct = default)
    {
        var activation = await _context.LicenseActivations
            .Include(a => a.License)
            .FirstOrDefaultAsync(a =>
                a.License!.LicenseKey == licenseKey &&
                a.MachineFingerprint == fingerprint &&
                a.DeactivatedAt == null, ct)
            ?? throw new InvalidOperationException("No active activation found for this license and machine.");

        activation.LastSeenAt = DateTime.UtcNow;
        await _unitOfWork.CompleteAsync(ct);
    }

    public async Task DeactivateLicenseAsync(string licenseKey, string fingerprint, CancellationToken ct = default)
    {
        var activation = await _context.LicenseActivations
            .Include(a => a.License)
            .FirstOrDefaultAsync(a =>
                a.License!.LicenseKey == licenseKey &&
                a.MachineFingerprint == fingerprint &&
                a.DeactivatedAt == null, ct)
            ?? throw new InvalidOperationException("No active activation found for this license and machine.");

        activation.DeactivatedAt = DateTime.UtcNow;
        await _unitOfWork.CompleteAsync(ct);

        _logger.LogInformation("License {LicenseKey} deactivated on machine {Fingerprint}",
            licenseKey, fingerprint);
    }

    public async Task<IReadOnlyList<LicenseActivation>> GetActivationsAsync(int licenseId, CancellationToken ct = default)
    {
        return await _context.LicenseActivations
            .Where(a => a.LicenseId == licenseId)
            .OrderByDescending(a => a.ActivatedAt)
            .ToListAsync(ct);
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
