using Core.Entities;

namespace Core.Interfaces;

public interface ILicenseRepository : IGenericRepository<License>
{
    Task<License?> GetByIdAsync(string licenseKey);
    Task<IReadOnlyList<License>> GetLicensesByUserIdAsync(int userId);
}
