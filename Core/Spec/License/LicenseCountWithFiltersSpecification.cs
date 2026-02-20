using Core.Entities;

namespace Core.Spec;

public class LicenseCountWithFiltersSpecification : BaseSpecification<License>
{
    public LicenseCountWithFiltersSpecification(LicenseSpecParams specParams)
       : base(LicenseSpecificationBuilder.Build(specParams))
    {
        // Notice: no paging or includes — this is only for counting total results
    }
}
