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
                case "createdat_asc":
                    AddOrderBy(l => l.CreatedAt);
                    break;
                case "createdat_desc":
                    AddOrderByDescending(l => l.CreatedAt);
                    break;
                case "expiry_asc":
                    AddOrderBy(l => l.ExpiresAt);
                    break;
                case "expiry_desc":
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
