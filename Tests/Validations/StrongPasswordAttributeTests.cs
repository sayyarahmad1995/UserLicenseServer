using Core.Validations;
using FluentAssertions;
using System.ComponentModel.DataAnnotations;
using Xunit;

namespace Tests.Validations;

public class StrongPasswordAttributeTests
{
    private readonly StrongPasswordAttribute _attribute;

    public StrongPasswordAttributeTests()
    {
        _attribute = new StrongPasswordAttribute();
    }

    private ValidationResult? Validate(string? password)
    {
        var context = new ValidationContext(new object()) { MemberName = "Password" };
        return _attribute.GetValidationResult(password, context);
    }

    [Fact]
    public void Validate_WithNull_ShouldReturnError()
    {
        var result = Validate(null);
        result.Should().NotBe(ValidationResult.Success);
        result!.ErrorMessage.Should().Contain("required");
    }

    [Fact]
    public void Validate_WithEmpty_ShouldReturnError()
    {
        var result = Validate("");
        result.Should().NotBe(ValidationResult.Success);
        result!.ErrorMessage.Should().Contain("required");
    }

    [Fact]
    public void Validate_WithTooShort_ShouldReturnError()
    {
        var result = Validate("Ab1!xyz");  // 7 chars
        result.Should().NotBe(ValidationResult.Success);
        result!.ErrorMessage.Should().Contain("8 characters");
    }

    [Fact]
    public void Validate_WithNoUppercase_ShouldReturnError()
    {
        var result = Validate("abcdefg1!");
        result.Should().NotBe(ValidationResult.Success);
        result!.ErrorMessage.Should().Contain("uppercase");
    }

    [Fact]
    public void Validate_WithNoLowercase_ShouldReturnError()
    {
        var result = Validate("ABCDEFG1!");
        result.Should().NotBe(ValidationResult.Success);
        result!.ErrorMessage.Should().Contain("lowercase");
    }

    [Fact]
    public void Validate_WithNoDigit_ShouldReturnError()
    {
        var result = Validate("Abcdefgh!");
        result.Should().NotBe(ValidationResult.Success);
        result!.ErrorMessage.Should().Contain("number");
    }

    [Fact]
    public void Validate_WithNoSpecialChar_ShouldReturnError()
    {
        var result = Validate("Abcdefg1h");
        result.Should().NotBe(ValidationResult.Success);
        result!.ErrorMessage.Should().Contain("special character");
    }

    [Fact]
    public void Validate_WithValidPassword_ShouldReturnSuccess()
    {
        var result = Validate("Abcdefg1!");
        result.Should().Be(ValidationResult.Success);
    }

    [Fact]
    public void Validate_WithExactly8Chars_ShouldReturnSuccess()
    {
        var result = Validate("Abcdef1!");  // exactly 8 chars
        result.Should().Be(ValidationResult.Success);
    }
}
