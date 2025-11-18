using System.Linq.Expressions;

namespace Core.Spec;

public interface ISpecification<T>
{
    Expression<Func<T, bool>>? Criteria { get; }
    List<Expression<Func<T, object>>> Includes { get; }
    Func<IQueryable<T>, IOrderedQueryable<T>>? OrderBy { get; }
    Func<IQueryable<T>, IOrderedQueryable<T>>? OrderByDescending { get; }

    int Take { get; }
    int Skip { get; }
    bool IsPagingEnabled { get; }
}
