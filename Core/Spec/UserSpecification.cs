using Core.Entities;

namespace Core.Spec;

public class UserSpecification : BaseSpecification<User>
{
    public UserSpecification(
        string? username = null,
        string? email = null,
        string? role = null,
        DateTime? createdAfter = null,
        DateTime? createdBefore = null,
        bool? isVerified = null,
        bool includeLicenses = false)
    {
        AddCriteria(u =>
            (string.IsNullOrEmpty(username) || u.Username!.ToLower().Contains(username.ToLower())) &&
            (string.IsNullOrEmpty(email) || u.Email!.ToLower().Contains(email.ToLower())) &&
            (string.IsNullOrEmpty(role) || u.Role!.ToLower() == role.ToLower()) &&
            (!createdAfter.HasValue || u.CreatedAt >= createdAfter.Value) &&
            (!createdBefore.HasValue || u.CreatedAt <= createdBefore.Value) &&
            (!isVerified.HasValue || (isVerified.Value ? u.VerifiedAt != null : u.VerifiedAt == null))
        );

        OrderByDescending = q => q.OrderByDescending(u => u.CreatedAt);

        if (includeLicenses)
            AddInclude(u => u.Licenses);
    }
}
