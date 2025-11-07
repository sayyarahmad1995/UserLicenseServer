using Core.Entities;
using Core.Interfaces;

namespace Infrastructure.Data;

public sealed class UnitOfWork : IUnitOfWork
{
   private readonly AppDbContext _context;
   private readonly Dictionary<Type, object> _repositories = new();
   private UserRepository? _userRepository;

   public UnitOfWork(AppDbContext context)
   {
      _context = context;
   }

   public IUserRepository UserRepository => _userRepository ??= new UserRepository(_context);

   public IGenericRepository<TEntity> Repository<TEntity>() where TEntity : BaseEntity
   {
      var type = typeof(TEntity);

      if (!_repositories.ContainsKey(type))
      {
         var repoType = typeof(GenericRepository<>).MakeGenericType(type);
         var repoInstance = Activator.CreateInstance(repoType, _context)!;
         _repositories[type] = repoInstance;
      }

      return (IGenericRepository<TEntity>)_repositories[type];
   }

   public async Task<int> CompleteAsync() => await _context.SaveChangesAsync();

   public void Dispose()
   {
      _context.Dispose();
      GC.SuppressFinalize(this);
   }
}
