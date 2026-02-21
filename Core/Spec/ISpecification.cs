using System.Linq.Expressions;

namespace Core.Spec;

/// <summary>
/// Defines the shape of a specification for querying entities of type <typeparamref name="T"/>.
/// </summary>
public interface ISpecification<T>
{
    /// <summary>Filter predicate applied as a WHERE clause.</summary>
    Expression<Func<T, bool>>? Criteria { get; }

    /// <summary>Navigation properties to eagerly load.</summary>
    List<Expression<Func<T, object>>> Includes { get; }

    /// <summary>Ascending order expression.</summary>
    Func<IQueryable<T>, IOrderedQueryable<T>>? OrderBy { get; }

    /// <summary>Descending order expression.</summary>
    Func<IQueryable<T>, IOrderedQueryable<T>>? OrderByDescending { get; }

    /// <summary>Maximum number of rows to return (page size).</summary>
    int Take { get; }

    /// <summary>Number of rows to skip (offset).</summary>
    int Skip { get; }

    /// <summary>Whether server-side paging is enabled for this specification.</summary>
    bool IsPagingEnabled { get; }
}
