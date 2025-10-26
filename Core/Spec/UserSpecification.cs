using Core.Entities;
using Core.Spec;
using Infrastructure.Data.Helpers;
using System.Linq.Expressions;

namespace Core.Specifications
{
    public class UserSpecification : BaseSpecification<User>
    {
        public UserSpecification(string? username, string? email = null, string? role = null, bool? onlyVerified = null)
        {
            Expression<Func<User, bool>> criteria = u => true;

            if (!string.IsNullOrEmpty(username))
                criteria = criteria.AndAlso(u => u.Username!.ToLower().Contains(username.ToLower()));

            if (!string.IsNullOrEmpty(email))
                criteria = criteria.AndAlso(u => u.Email!.ToLower().Contains(email.ToLower()));

            if (!string.IsNullOrEmpty(role))
                criteria = criteria.AndAlso(u => u.Role == role);

            if (onlyVerified.HasValue && onlyVerified.Value)
                criteria = criteria.AndAlso(u => u.VerifiedAt != null);

            AddCriteria(criteria);

            AddInclude(u => u.Licenses);
        }
    }
}
