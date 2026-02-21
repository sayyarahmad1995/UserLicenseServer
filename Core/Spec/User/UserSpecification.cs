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
                case "usernameasc":
                    AddOrderBy(u => u.Username);
                    break;
                case "username_desc":
                    AddOrderByDescending(u => u.Username);
                    break;
                case "email":
                    AddOrderBy(u => u.Email);
                    break;
                case "email_desc":
                    AddOrderByDescending(u => u.Email);
                    break;
                case "createdatasc":
                    AddOrderBy(u => u.CreatedAt);
                    break;
                case "createdatdesc":
                    AddOrderByDescending(u => u.CreatedAt);
                    break;
                case "verifiedatasc":
                    AddOrderBy(u => u.VerifiedAt!);
                    break;
                case "verifiedatdesc":
                    AddOrderByDescending(u => u.VerifiedAt!);
                    break;
                case "roleasc":
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
