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

    public async Task<T?> GetByIdAsync(int id, CancellationToken ct = default)
       => await _context.Set<T>().FindAsync(new object[] { id }, ct);

    public async Task<IReadOnlyList<T>> ListAllAsync(CancellationToken ct = default)
       => await _context.Set<T>().AsNoTracking().ToListAsync(ct);

    public async Task<T?> GetEntityWithSpec(ISpecification<T> spec, CancellationToken ct = default)
       => await ApplySpecification(spec).AsNoTracking().FirstOrDefaultAsync(ct);

    public async Task<IReadOnlyList<T>> ListAsync(ISpecification<T> spec, CancellationToken ct = default)
       => await ApplySpecification(spec).AsNoTracking().ToListAsync(ct);

    public async Task<int> CountAsync(ISpecification<T> spec, CancellationToken ct = default)
       => await ApplySpecification(spec).CountAsync(ct);

    private IQueryable<T> ApplySpecification(ISpecification<T> spec)
       => SpecificationEvaluator<T>.GetQuery(_context.Set<T>().AsQueryable(), spec);

    public async Task<T?> FindByEntityAsync(Expression<Func<T, bool>> expression, CancellationToken ct = default)
       => await _context.Set<T>().AsNoTracking().FirstOrDefaultAsync(expression, ct);

    public async Task<IReadOnlyList<T>> FindAllByEntityAsync(Expression<Func<T, bool>> expression, CancellationToken ct = default)
     => await _context.Set<T>().AsNoTracking().Where(expression).ToListAsync(ct);

    public void Add(T entity)
       => _context.Set<T>().Add(entity);

    public void Update(T entity)
       => _context.Set<T>().Update(entity);

    public void Delete(T entity)
       => _context.Set<T>().Remove(entity);
}
