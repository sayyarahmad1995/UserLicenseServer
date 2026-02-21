using Core.Entities;

namespace Core.Interfaces;

public interface ILicenseRepository : IGenericRepository<License>
{
    Task<License?> GetByIdAsync(string licenseKey, CancellationToken ct = default);
    Task<IReadOnlyList<License>> GetLicensesByUserIdAsync(int userId, CancellationToken ct = default);
}
