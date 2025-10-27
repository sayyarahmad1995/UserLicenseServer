using Core.Entities;

namespace Core.Spec;

public class UserCountSpecification : BaseSpecification<User>
{
    public UserCountSpecification(UserSpecParams specParams)
        : base(x =>
            (string.IsNullOrEmpty(specParams.Username) || x.Username!.Contains(specParams.Username, StringComparison.CurrentCultureIgnoreCase)) &&
            (string.IsNullOrEmpty(specParams.Email) || x.Email!.Contains(specParams.Email, StringComparison.CurrentCultureIgnoreCase)) &&
            (string.IsNullOrEmpty(specParams.Role) || x.Role!.Equals(specParams.Role, StringComparison.CurrentCultureIgnoreCase)) &&
            (!specParams.CreatedAfter.HasValue || x.CreatedAt >= specParams.CreatedAfter.Value) &&
            (!specParams.CreatedBefore.HasValue || x.CreatedAt <= specParams.CreatedBefore.Value) &&
            (!specParams.IsVerified.HasValue || (specParams.IsVerified.Value ? x.VerifiedAt != null : x.VerifiedAt == null)) &&
            (string.IsNullOrEmpty(specParams.Search) ||
                x.Username!.Contains(specParams.Search, StringComparison.CurrentCultureIgnoreCase) ||
                x.Email!.Contains(specParams.Search, StringComparison.CurrentCultureIgnoreCase))
        )
    {
        // Notice: no paging or includes — this is only for counting total results
    }
}
