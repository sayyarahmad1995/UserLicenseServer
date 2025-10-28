using Core.Entities;

namespace Core.Spec;

public class UserCountSpecification : BaseSpecification<User>
{
    public UserCountSpecification(UserSpecParams specParams)
        : base(x =>
            (string.IsNullOrEmpty(specParams.Username) || x.Username!.ToLower().Contains(specParams.Username.ToLower())) &&
            (string.IsNullOrEmpty(specParams.Email) || x.Email!.ToLower().Contains(specParams.Email.ToLower())) &&
            (string.IsNullOrEmpty(specParams.Role) || x.Role!.ToLower().Equals(specParams.Role.ToLower())) &&
            (!specParams.CreatedAfter.HasValue || x.CreatedAt >= specParams.CreatedAfter.Value) &&
            (!specParams.CreatedBefore.HasValue || x.CreatedAt <= specParams.CreatedBefore.Value) &&
            (!specParams.IsVerified.HasValue || (specParams.IsVerified.Value ? x.VerifiedAt != null : x.VerifiedAt == null)) &&
            (string.IsNullOrEmpty(specParams.Search) ||
                x.Username!.ToLower().Contains(specParams.Search.ToLower()) ||
                x.Email!.ToLower().Contains(specParams.Search.ToLower()))
        )
    {
        // Notice: no paging or includes — this is only for counting total results
    }
}
