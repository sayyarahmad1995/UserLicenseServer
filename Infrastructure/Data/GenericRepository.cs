using Core.Entities;
using Core.Interfaces;
using Core.Spec;
using Microsoft.EntityFrameworkCore;
using System.Linq.Expressions;

namespace Infrastructure.Data;

public class GenericRepository<T> : IGenericRepository<T> where T : BaseEntity
{
    private readonly AppDbContext _context;

    public GenericRepository(AppDbContext context)
    {
        _context = context;
    }

    public async Task<T?> GetByIdAsync(int id)
       => await _context.Set<T>().FindAsync(id);

    public async Task<IReadOnlyList<T>> ListAllAsync()
       => await _context.Set<T>().ToListAsync();

    public async Task<T?> GetEntityWithSpec(ISpecification<T> spec)
       => await ApplySpecification(spec).FirstOrDefaultAsync();

    public async Task<IReadOnlyList<T>> ListAsync(ISpecification<T> spec)
       => await ApplySpecification(spec).ToListAsync();

    public async Task<int> CountAsync(ISpecification<T> spec)
       => await ApplySpecification(spec).CountAsync();

    private IQueryable<T> ApplySpecification(ISpecification<T> spec)
       => SpecificationEvaluator<T>.GetQuery(_context.Set<T>().AsQueryable(), spec);

    public async Task<T?> FindByEntityAsync(Expression<Func<T, bool>> expression)
       => await _context.Set<T>().FirstOrDefaultAsync(expression);

    public async Task<IReadOnlyList<T>> FindAllByEntityAsync(Expression<Func<T, bool>> expression)
     => await _context.Set<T>().Where(expression).ToListAsync();

    public void Add(T entity)
       => _context.Set<T>().Add(entity);

    public void Update(T entity)
       => _context.Set<T>().Update(entity);

    public void Delete(T entity)
       => _context.Set<T>().Remove(entity);
}
