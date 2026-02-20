using Core.Spec;
using Microsoft.EntityFrameworkCore;

namespace Infrastructure.Data;

public class SpecificationEvaluator<T> where T : class
{
    public static IQueryable<T> GetQuery(IQueryable<T> inputQuery, ISpecification<T> specification)
    {
        var query = inputQuery;

        query = specification.Criteria != null ? query.Where(specification.Criteria) : query;

        query = specification.OrderBy != null ? specification.OrderBy(query) : query;
        query = specification.OrderByDescending != null ? specification.OrderByDescending(query) : query;

        query = specification.IsPagingEnabled ? query.Skip(specification.Skip).Take(specification.Take) : query;

        query = specification.Includes.Aggregate(query, (current, include) => current.Include(include));

        return query;
    }
}
