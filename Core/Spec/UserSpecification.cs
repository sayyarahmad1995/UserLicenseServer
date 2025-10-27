using Core.Entities;

namespace Core.Spec;

public class UserSpecification : BaseSpecification<User>
{
    public UserSpecification(UserSpecParams specParams) : base(x =>
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
        ApplyPaging(specParams.PageSize * (specParams.PageIndex - 1), specParams.PageSize);

        if (!string.IsNullOrEmpty(specParams.Sort))
        {
            switch (specParams.Sort.ToLower())
            {
                case "usernameasc":
                    AddOrderBy(u => u.Username!);
                    break;
                case "usernamedesc":
                    AddOrderByDescending(u => u.Username!);
                    break;
                case "emailasc":
                    AddOrderBy(u => u.Email!);
                    break;
                case "emaildesc":
                    AddOrderByDescending(u => u.Email!);
                    break;
                case "createdatasc":
                    AddOrderBy(u => u.CreatedAt);
                    break;
                case "createdatdesc":
                    AddOrderByDescending(u => u.CreatedAt);
                    break;
                default:
                    AddOrderBy(u => u.Username!);
                    break;
            }
        }
        else
            AddOrderBy(u => u.CreatedAt);

        if (specParams.IncludeLicenses)
            AddInclude(u => u.Licenses);
    }
}
