using Core.Entities;
using Core.Interfaces;

namespace Infrastructure.Data;

public class LicenseRepository : GenericRepository<License>, ILicenseRepository
{
    public LicenseRepository(AppDbContext context) : base(context)
    {
    }

    public async Task<License?> GetByIdAsync(string licenseKey, CancellationToken ct = default)
        => await FindByEntityAsync(l => l.LicenseKey == licenseKey, ct);

    public async Task<IReadOnlyList<License>> GetLicensesByUserIdAsync(int userId, CancellationToken ct = default)
        => await FindAllByEntityAsync(l => l.UserId == userId, ct);
}
