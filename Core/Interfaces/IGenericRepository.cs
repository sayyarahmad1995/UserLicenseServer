using Core.Entities;
using Core.Spec;
using System.Linq.Expressions;

namespace Core.Interfaces;

public interface IGenericRepository<T> where T : BaseEntity
{
    Task<T?> GetByIdAsync(int id);
    Task<IReadOnlyList<T>> ListAllAsync();
    Task<T?> GetEntityWithSpec(ISpecification<T> spec);
    Task<IReadOnlyList<T>> ListAsync(ISpecification<T> spec);
    Task<int> CountAsync(ISpecification<T> spec);
    Task<T?> FindByEntityAsync(Expression<Func<T, bool>> expression);
    Task<IReadOnlyList<T>> FindAllByEntityAsync(Expression<Func<T, bool>> expression);
    void Add(T entity);
    void Update(T entity);
    void Delete(T entity);
}