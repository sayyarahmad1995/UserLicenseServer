using System.Linq.Expressions;

namespace Core.Spec;

/// <summary>
/// Abstract base implementation of <see cref="ISpecification{T}"/> providing
/// criteria, includes, ordering, and paging configuration.
/// </summary>
public abstract class BaseSpecification<T> : ISpecification<T>
{
    /// <summary>Creates a specification with no initial criteria.</summary>
    protected BaseSpecification()
    {
    }

    /// <summary>Creates a specification with the given filter criteria.</summary>
    protected BaseSpecification(Expression<Func<T, bool>> criteria)
    {
        Criteria = criteria;
    }

    /// <inheritdoc />
    public Expression<Func<T, bool>>? Criteria { get; private set; }
    /// <inheritdoc />
    public List<Expression<Func<T, object>>> Includes { get; } = new();
    /// <inheritdoc />
    public Func<IQueryable<T>, IOrderedQueryable<T>>? OrderBy { get; protected set; }
    /// <inheritdoc />
    public Func<IQueryable<T>, IOrderedQueryable<T>>? OrderByDescending { get; protected set; }
    /// <inheritdoc />
    public int Take { get; private set; }
    /// <inheritdoc />
    public int Skip { get; private set; }
    /// <inheritdoc />
    public bool IsPagingEnabled { get; private set; }

    /// <summary>Set or replace the filter criteria.</summary>
    protected void AddCriteria(Expression<Func<T, bool>> criteria)
    {
        Criteria = criteria;
    }

    /// <summary>Add a navigation property to eagerly load.</summary>
    protected void AddInclude(Expression<Func<T, object>> includeExpression)
    {
        Includes.Add(includeExpression);
    }

    /// <summary>Set ascending ordering.</summary>
    protected void AddOrderBy(Expression<Func<T, object>> orderByExpression)
    {
        OrderBy = q => q.OrderBy(orderByExpression);
    }

    /// <summary>Set descending ordering.</summary>
    protected void AddOrderByDescending(Expression<Func<T, object>> orderByDescExpression)
    {
        OrderByDescending = q => q.OrderByDescending(orderByDescExpression);
    }

    /// <summary>Enable server-side paging with the given skip and take values.</summary>
    protected void ApplyPaging(int skip, int take)
    {
        Skip = skip;
        Take = take;
        IsPagingEnabled = true;
    }
}

