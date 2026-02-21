using Core.Enums;
using Core.Helpers;
using FluentAssertions;
using Xunit;

namespace Tests.Helpers;

public class LicenseStatusHelperTests
{
    [Theory]
    [InlineData("active", LicenseStatus.Active)]
    [InlineData("expired", LicenseStatus.Expired)]
    [InlineData("revoked", LicenseStatus.Revoked)]
    public void Parse_WithValidStatus_ShouldReturnCorrectEnum(string input, LicenseStatus expected)
    {
        LicenseStatusHelper.Parse(input).Should().Be(expected);
    }

    [Theory]
    [InlineData("ACTIVE", LicenseStatus.Active)]
    [InlineData("Active", LicenseStatus.Active)]
    [InlineData("EXPIRED", LicenseStatus.Expired)]
    [InlineData("Revoked", LicenseStatus.Revoked)]
    public void Parse_ShouldBeCaseInsensitive(string input, LicenseStatus expected)
    {
        LicenseStatusHelper.Parse(input).Should().Be(expected);
    }

    [Fact]
    public void Parse_WithNull_ShouldReturnNull()
    {
        LicenseStatusHelper.Parse(null).Should().BeNull();
    }

    [Fact]
    public void Parse_WithEmpty_ShouldReturnNull()
    {
        LicenseStatusHelper.Parse("").Should().BeNull();
    }

    [Fact]
    public void Parse_WithInvalidValue_ShouldReturnNull()
    {
        LicenseStatusHelper.Parse("invalid").Should().BeNull();
    }

    [Fact]
    public void ValidStatuses_ShouldContainAllStatuses()
    {
        LicenseStatusHelper.ValidStatuses.Should().HaveCount(3);
        LicenseStatusHelper.ValidStatuses.Should().Contain("active");
        LicenseStatusHelper.ValidStatuses.Should().Contain("expired");
        LicenseStatusHelper.ValidStatuses.Should().Contain("revoked");
    }
}

public class UserStatusHelperTests
{
    [Theory]
    [InlineData("unverified", UserStatus.Unverified)]
    [InlineData("verified", UserStatus.Verified)]
    [InlineData("blocked", UserStatus.Blocked)]
    [InlineData("active", UserStatus.Active)]
    public void Parse_WithValidStatus_ShouldReturnCorrectEnum(string input, UserStatus expected)
    {
        UserStatusHelper.Parse(input).Should().Be(expected);
    }

    [Theory]
    [InlineData("UNVERIFIED", UserStatus.Unverified)]
    [InlineData("Verified", UserStatus.Verified)]
    [InlineData("BLOCKED", UserStatus.Blocked)]
    [InlineData("Active", UserStatus.Active)]
    public void Parse_ShouldBeCaseInsensitive(string input, UserStatus expected)
    {
        UserStatusHelper.Parse(input).Should().Be(expected);
    }

    [Fact]
    public void Parse_WithNull_ShouldReturnNull()
    {
        UserStatusHelper.Parse(null).Should().BeNull();
    }

    [Fact]
    public void Parse_WithEmpty_ShouldReturnNull()
    {
        UserStatusHelper.Parse("").Should().BeNull();
    }

    [Fact]
    public void Parse_WithInvalidValue_ShouldReturnNull()
    {
        UserStatusHelper.Parse("invalid").Should().BeNull();
    }

    [Fact]
    public void ValidStatuses_ShouldContainAllStatuses()
    {
        UserStatusHelper.ValidStatuses.Should().HaveCount(4);
        UserStatusHelper.ValidStatuses.Should().Contain("unverified");
        UserStatusHelper.ValidStatuses.Should().Contain("verified");
        UserStatusHelper.ValidStatuses.Should().Contain("blocked");
        UserStatusHelper.ValidStatuses.Should().Contain("active");
    }
}
