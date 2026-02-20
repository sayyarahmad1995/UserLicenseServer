using Core.Entities;
using Core.Interfaces;

namespace Infrastructure.Data;

public class LicenseRepository : GenericRepository<License>, ILicenseRepository
{
    public LicenseRepository(AppDbContext context) : base(context)
    {
    }

    public async Task<License?> GetByIdAsync(string licenseKey)
        => await FindByEntityAsync(l => l.LicenseKey == licenseKey);

    public async Task<IReadOnlyList<License>> GetLicensesByUserIdAsync(int userId)
        => await FindAllByEntityAsync(l => l.UserId == userId);
}
