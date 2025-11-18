using Core.Entities;

namespace Core.Spec;

public class LicenseSpecification : BaseSpecification<License>
{
    public LicenseSpecification(LicenseSpecParams specParams)
       : base(LicenseSpecificationBuilder.Build(specParams))
    {
        ApplyPaging(specParams.PageSize * (specParams.PageIndex - 1), specParams.PageSize);

        if (!string.IsNullOrEmpty(specParams.Sort))
        {
            switch (specParams.Sort.ToLower())
            {
                case "createdatasc":
                    AddOrderBy(l => l.CreatedAt);
                    break;
                case "createdatdesc":
                    AddOrderByDescending(l => l.CreatedAt);
                    break;
                case "expiryasc":
                    AddOrderBy(l => l.ExpiresAt);
                    break;
                case "expirydesc":
                    AddOrderByDescending(l => l.ExpiresAt);
                    break;
                default:
                    AddOrderByDescending(l => l.CreatedAt);
                    break;
            }
        }
        else
        {
            AddOrderByDescending(l => l.CreatedAt);
        }
    }

    public LicenseSpecification(int id)
       : base(x => x.Id == id)
    {
        AddInclude(l => l.User!);
    }
}
