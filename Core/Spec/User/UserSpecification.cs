using Core.Entities;

namespace Core.Spec;

public class UserSpecification : BaseSpecification<User>
{
    public UserSpecification(UserSpecParams specParams)
       : base(UserSpecificationBuilder.Build(specParams))
    {
        ApplyPaging(specParams.PageSize * (specParams.PageIndex - 1), specParams.PageSize);

        if (!string.IsNullOrEmpty(specParams.Sort))
        {
            switch (specParams.Sort.ToLower())
            {
                case "username_asc":
                    AddOrderBy(u => u.Username);
                    break;
                case "username_desc":
                    AddOrderByDescending(u => u.Username);
                    break;
                case "email_asc":
                    AddOrderBy(u => u.Email);
                    break;
                case "email_desc":
                    AddOrderByDescending(u => u.Email);
                    break;
                case "createdat_asc":
                    AddOrderBy(u => u.CreatedAt);
                    break;
                case "createdat_desc":
                    AddOrderByDescending(u => u.CreatedAt);
                    break;
                case "verifiedat_asc":
                    AddOrderBy(u => u.VerifiedAt!);
                    break;
                case "verifiedat_desc":
                    AddOrderByDescending(u => u.VerifiedAt!);
                    break;
                case "role_asc":
                    AddOrderBy(u => u.Role);
                    break;
                case "role_desc":
                    AddOrderByDescending(u => u.Role);
                    break;
                default:
                    AddOrderBy(u => u.Id);
                    break;
            }
        }
        else
            AddOrderBy(u => u.Id);
    }

    public UserSpecification(int Id) : base(x => x.Id == Id)
    {
        AddInclude(x => x.Licenses);
    }
}
