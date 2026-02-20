using Core.Entities;
using Core.Helpers;
using System.Linq.Expressions;

namespace Core.Spec;

public static class UserSpecificationBuilder
{
    public static Expression<Func<User, bool>> Build(UserSpecParams specParams)
    {
        var statusEnum = UserStatusHelper.Parse(specParams.Status);

        return x =>
        (string.IsNullOrEmpty(specParams.Username) || x.Username!.ToLower().Contains(specParams.Username.ToLower())) &&
        (string.IsNullOrEmpty(specParams.Email) || x.Email!.ToLower().Contains(specParams.Email.ToLower())) &&
        (string.IsNullOrEmpty(specParams.Role) || x.Role!.ToLower().Equals(specParams.Role.ToLower())) &&
        (!specParams.CreatedAfter.HasValue || x.CreatedAt >= specParams.CreatedAfter.Value) &&
        (!specParams.CreatedBefore.HasValue || x.CreatedAt <= specParams.CreatedBefore.Value) &&
        (!statusEnum.HasValue || x.Status == statusEnum.Value) &&
        (string.IsNullOrEmpty(specParams.Search) ||
           x.Username!.ToLower().Contains(specParams.Search.ToLower()) ||
           x.Email!.ToLower().Contains(specParams.Search.ToLower()));
    }
}
