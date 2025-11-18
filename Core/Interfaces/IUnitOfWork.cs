using Core.Entities;

namespace Core.Interfaces;

public interface IUnitOfWork : IDisposable
{
    IUserRepository UserRepository { get; }
    IGenericRepository<TEntity> Repository<TEntity>() where TEntity : BaseEntity;
    Task<int> CompleteAsync();
}
