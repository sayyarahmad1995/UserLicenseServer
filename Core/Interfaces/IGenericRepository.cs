using Core.Entities;
using Core.Spec;
using System.Linq.Expressions;

namespace Core.Interfaces;

public interface IGenericRepository<T> where T : BaseEntity
{
    Task<T?> GetByIdAsync(int id, CancellationToken ct = default);
    Task<IReadOnlyList<T>> ListAllAsync(CancellationToken ct = default);
    Task<T?> GetEntityWithSpec(ISpecification<T> spec, CancellationToken ct = default);
    Task<IReadOnlyList<T>> ListAsync(ISpecification<T> spec, CancellationToken ct = default);
    Task<int> CountAsync(ISpecification<T> spec, CancellationToken ct = default);
    Task<T?> FindByEntityAsync(Expression<Func<T, bool>> expression, CancellationToken ct = default);
    Task<IReadOnlyList<T>> FindAllByEntityAsync(Expression<Func<T, bool>> expression, CancellationToken ct = default);
    void Add(T entity);
    void Update(T entity);
    void Delete(T entity);
}