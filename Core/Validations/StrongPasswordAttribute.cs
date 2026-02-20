using System.ComponentModel.DataAnnotations;

namespace Core.Validations;

public class StrongPasswordAttribute : ValidationAttribute
{
    protected override ValidationResult? IsValid(object? value, ValidationContext validationContext)
    {
        var password = value as string;
        if (string.IsNullOrEmpty(password))
            return new ValidationResult("Password is required.");

        if (password.Length < 8)
            return new ValidationResult("Password must be at least 8 characters long.");

        if (!password.Any(char.IsUpper))
            return new ValidationResult("Password must contain at least one uppercase letter.");

        if (!password.Any(char.IsLower))
            return new ValidationResult("Password must contain at least one lowercase letter.");

        if (!password.Any(char.IsDigit))
            return new ValidationResult("Password must contain at least one number.");

        if (!password.Any(ch => !char.IsLetterOrDigit(ch)))
            return new ValidationResult("Password must contain at least one special character.");

        return ValidationResult.Success;
    }
}