using System.Linq.Expressions;
using Core.Entities;
using Core.Spec;

namespace Core.Interfaces;

public interface IGenericRepository<T> where T : BaseEntity
{
	Task<T?> GetByIdAsync(int id);
	Task<IReadOnlyList<T>> ListAllAsync();
	Task<T?> GetEntityWithSpec(ISpecification<T> spec);
	Task<IReadOnlyList<T>> ListAsync(ISpecification<T> spec);
	Task<int> CountAsync(ISpecification<T> spec);
	Task<T?> FindByEntityAsync(Expression<Func<T, bool>> expression);
	void Add(T entity);
	void Update(T entity);
	void Delete(T entity);
}