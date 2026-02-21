using Core.Entities;
using Core.Interfaces;

namespace Infrastructure.Data;

public class UserRepository : GenericRepository<User>, IUserRepository
{
    private readonly AppDbContext _context;
    public UserRepository(AppDbContext context) : base(context)
    {
        _context = context;
    }

    public async Task<User?> GetByEmailAsync(string email, CancellationToken ct = default)
       => await FindByEntityAsync(u => u.Email == email, ct);

    public async Task<User?> GetByUsernameAsync(string username, CancellationToken ct = default)
       => await FindByEntityAsync(u => u.Username == username, ct);
}
