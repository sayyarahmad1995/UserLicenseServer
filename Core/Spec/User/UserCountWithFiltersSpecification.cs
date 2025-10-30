using Core.Entities;

namespace Core.Spec;

public class UserCountSpecification : BaseSpecification<User>
{
    public UserCountSpecification(UserSpecParams specParams)
        : base(UserSpecificationBuilder.Build(specParams))
    {
        // Notice: no paging or includes — this is only for counting total results
    }
}
