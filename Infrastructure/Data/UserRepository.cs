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

    public async Task<User?> GetByEmailAsync(string email)
       => await FindByEntityAsync(u => u.Email == email);

    public async Task<User?> GetByUsernameAsync(string username)
       => await FindByEntityAsync(u => u.Username == username);
}
