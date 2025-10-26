using System.Linq.Expressions;

namespace Core.Spec
{
    public abstract class BaseSpecification<T> : ISpecification<T>
    {
        public Expression<Func<T, bool>>? Criteria { get; private set; }
        public List<Expression<Func<T, object>>> Includes { get; } = new();
        public Func<IQueryable<T>, IOrderedQueryable<T>>? OrderBy { get; protected set; }
        public Func<IQueryable<T>, IOrderedQueryable<T>>? OrderByDescending { get; protected set; }
        public int Take { get; private set; }
        public int Skip { get; private set; }
        public bool IsPagingEnabled { get; private set; }

        protected void AddCriteria(Expression<Func<T, bool>> criteria) => Criteria = criteria;

        protected void AddInclude(Expression<Func<T, object>> includeExpression) => Includes.Add(includeExpression);

        protected void ApplyPaging(int skip, int take)
        {
            Skip = skip;
            Take = take;
            IsPagingEnabled = true;
        }
    }
}
