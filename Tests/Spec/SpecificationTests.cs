using Core.Entities;
using Core.Enums;
using Core.Spec;
using FluentAssertions;
using Xunit;

namespace Tests.Spec;

public class SpecParamsTests
{
    #region UserSpecParams

    [Fact]
    public void UserSpecParams_Defaults_PageIndex1_PageSize10()
    {
        var p = new UserSpecParams();

        p.PageIndex.Should().Be(1);
        p.PageSize.Should().Be(10);
    }

    [Theory]
    [InlineData(0, 1)]
    [InlineData(-5, 1)]
    [InlineData(3, 3)]
    public void UserSpecParams_PageIndex_ClampsToMinimum1(int input, int expected)
    {
        var p = new UserSpecParams { PageIndex = input };
        p.PageIndex.Should().Be(expected);
    }

    [Theory]
    [InlineData(0, 1)]
    [InlineData(-1, 1)]
    [InlineData(51, 50)]
    [InlineData(100, 50)]
    [InlineData(25, 25)]
    public void UserSpecParams_PageSize_ClampsToRange1To50(int input, int expected)
    {
        var p = new UserSpecParams { PageSize = input };
        p.PageSize.Should().Be(expected);
    }

    [Fact]
    public void UserSpecParams_Search_NormalizesToLower()
    {
        var p = new UserSpecParams { Search = "TestUser" };
        p.Search.Should().Be("testuser");
    }

    [Fact]
    public void UserSpecParams_Search_NullStaysNull()
    {
        var p = new UserSpecParams { Search = null };
        p.Search.Should().BeNull();
    }

    #endregion

    #region LicenseSpecParams

    [Fact]
    public void LicenseSpecParams_Defaults_PageIndex1_PageSize10()
    {
        var p = new LicenseSpecParams();

        p.PageIndex.Should().Be(1);
        p.PageSize.Should().Be(10);
    }

    [Theory]
    [InlineData(0, 1)]
    [InlineData(-1, 1)]
    [InlineData(51, 50)]
    [InlineData(25, 25)]
    public void LicenseSpecParams_PageSize_ClampsToRange1To50(int input, int expected)
    {
        var p = new LicenseSpecParams { PageSize = input };
        p.PageSize.Should().Be(expected);
    }

    [Fact]
    public void LicenseSpecParams_Search_NormalizesToLower()
    {
        var p = new LicenseSpecParams { Search = "ABC-KEY" };
        p.Search.Should().Be("abc-key");
    }

    #endregion
}

public class UserSpecificationBuilderTests
{
    private static User CreateUser(
        string username = "alice",
        string email = "alice@example.com",
        string role = "User",
        DateTime? createdAt = null,
        UserStatus status = UserStatus.Active) => new()
    {
        Id = 1,
        Username = username,
        Email = email,
        Role = role,
        CreatedAt = createdAt ?? new DateTime(2024, 6, 15, 0, 0, 0, DateTimeKind.Utc),
        Status = status
    };

    [Fact]
    public void Build_NoFilters_MatchesAll()
    {
        var expr = UserSpecificationBuilder.Build(new UserSpecParams());
        var func = expr.Compile();

        func(CreateUser()).Should().BeTrue();
    }

    [Fact]
    public void Build_UsernameFilter_MatchesCaseInsensitive()
    {
        var expr = UserSpecificationBuilder.Build(new UserSpecParams { Username = "ALICE" });
        var func = expr.Compile();

        func(CreateUser(username: "alice")).Should().BeTrue();
        func(CreateUser(username: "bob")).Should().BeFalse();
    }

    [Fact]
    public void Build_EmailFilter_PartialMatch()
    {
        var expr = UserSpecificationBuilder.Build(new UserSpecParams { Email = "alice" });
        var func = expr.Compile();

        func(CreateUser(email: "alice@example.com")).Should().BeTrue();
        func(CreateUser(email: "bob@example.com")).Should().BeFalse();
    }

    [Fact]
    public void Build_RoleFilter_ExactCaseInsensitive()
    {
        var expr = UserSpecificationBuilder.Build(new UserSpecParams { Role = "admin" });
        var func = expr.Compile();

        func(CreateUser(role: "Admin")).Should().BeTrue();
        func(CreateUser(role: "User")).Should().BeFalse();
    }

    [Fact]
    public void Build_CreatedAfterFilter()
    {
        var cutoff = new DateTime(2024, 7, 1, 0, 0, 0, DateTimeKind.Utc);
        var expr = UserSpecificationBuilder.Build(new UserSpecParams { CreatedAfter = cutoff });
        var func = expr.Compile();

        func(CreateUser(createdAt: new DateTime(2024, 8, 1, 0, 0, 0, DateTimeKind.Utc))).Should().BeTrue();
        func(CreateUser(createdAt: new DateTime(2024, 6, 1, 0, 0, 0, DateTimeKind.Utc))).Should().BeFalse();
    }

    [Fact]
    public void Build_StatusFilter()
    {
        var expr = UserSpecificationBuilder.Build(new UserSpecParams { Status = "blocked" });
        var func = expr.Compile();

        func(CreateUser(status: UserStatus.Blocked)).Should().BeTrue();
        func(CreateUser(status: UserStatus.Active)).Should().BeFalse();
    }

    [Fact]
    public void Build_SearchFilter_MatchesUsernameOrEmail()
    {
        var expr = UserSpecificationBuilder.Build(new UserSpecParams { Search = "alice" });
        var func = expr.Compile();

        func(CreateUser(username: "alice", email: "other@x.com")).Should().BeTrue();
        func(CreateUser(username: "bob", email: "alice@x.com")).Should().BeTrue();
        func(CreateUser(username: "bob", email: "other@x.com")).Should().BeFalse();
    }

    [Fact]
    public void Build_CombinedFilters_AllMustMatch()
    {
        var expr = UserSpecificationBuilder.Build(new UserSpecParams
        {
            Username = "alice",
            Role = "Admin",
            Status = "active"
        });
        var func = expr.Compile();

        func(CreateUser(username: "alice", role: "Admin", status: UserStatus.Active)).Should().BeTrue();
        func(CreateUser(username: "alice", role: "User", status: UserStatus.Active)).Should().BeFalse();
    }
}

public class LicenseSpecificationBuilderTests
{
    private static License CreateLicense(
        int userId = 1,
        string licenseKey = "KEY-123",
        DateTime? createdAt = null,
        DateTime? expiresAt = null,
        LicenseStatus status = LicenseStatus.Active) => new()
    {
        Id = 1,
        UserId = userId,
        LicenseKey = licenseKey,
        CreatedAt = createdAt ?? new DateTime(2024, 6, 1, 0, 0, 0, DateTimeKind.Utc),
        ExpiresAt = expiresAt ?? new DateTime(2025, 6, 1, 0, 0, 0, DateTimeKind.Utc),
        Status = status
    };

    [Fact]
    public void Build_NoFilters_MatchesAll()
    {
        var expr = LicenseSpecificationBuilder.Build(new LicenseSpecParams());
        var func = expr.Compile();

        func(CreateLicense()).Should().BeTrue();
    }

    [Fact]
    public void Build_UserIdFilter()
    {
        var expr = LicenseSpecificationBuilder.Build(new LicenseSpecParams { UserId = 5 });
        var func = expr.Compile();

        func(CreateLicense(userId: 5)).Should().BeTrue();
        func(CreateLicense(userId: 3)).Should().BeFalse();
    }

    [Fact]
    public void Build_StatusFilter()
    {
        var expr = LicenseSpecificationBuilder.Build(new LicenseSpecParams { Status = "revoked" });
        var func = expr.Compile();

        func(CreateLicense(status: LicenseStatus.Revoked)).Should().BeTrue();
        func(CreateLicense(status: LicenseStatus.Active)).Should().BeFalse();
    }

    [Fact]
    public void Build_DateRangeFilters()
    {
        var after = new DateTime(2024, 5, 1, 0, 0, 0, DateTimeKind.Utc);
        var before = new DateTime(2024, 7, 1, 0, 0, 0, DateTimeKind.Utc);
        var expr = LicenseSpecificationBuilder.Build(new LicenseSpecParams
        {
            CreatedAfter = after,
            CreatedBefore = before
        });
        var func = expr.Compile();

        func(CreateLicense(createdAt: new DateTime(2024, 6, 1, 0, 0, 0, DateTimeKind.Utc))).Should().BeTrue();
        func(CreateLicense(createdAt: new DateTime(2024, 4, 1, 0, 0, 0, DateTimeKind.Utc))).Should().BeFalse();
        func(CreateLicense(createdAt: new DateTime(2024, 8, 1, 0, 0, 0, DateTimeKind.Utc))).Should().BeFalse();
    }

    [Fact]
    public void Build_SearchFilter_MatchesLicenseKey()
    {
        var expr = LicenseSpecificationBuilder.Build(new LicenseSpecParams { Search = "key-123" });
        var func = expr.Compile();

        func(CreateLicense(licenseKey: "KEY-123")).Should().BeTrue();
        func(CreateLicense(licenseKey: "OTHER-456")).Should().BeFalse();
    }

    [Fact]
    public void Build_ExpiryDateFilters()
    {
        var expAfter = new DateTime(2025, 1, 1, 0, 0, 0, DateTimeKind.Utc);
        var expBefore = new DateTime(2025, 12, 31, 0, 0, 0, DateTimeKind.Utc);
        var expr = LicenseSpecificationBuilder.Build(new LicenseSpecParams
        {
            ExpiredAfter = expAfter,
            ExpiredBefore = expBefore
        });
        var func = expr.Compile();

        func(CreateLicense(expiresAt: new DateTime(2025, 6, 1, 0, 0, 0, DateTimeKind.Utc))).Should().BeTrue();
        func(CreateLicense(expiresAt: new DateTime(2024, 6, 1, 0, 0, 0, DateTimeKind.Utc))).Should().BeFalse();
    }
}

public class UserSpecificationTests
{
    [Fact]
    public void ListConstructor_AppliesPaging()
    {
        var spec = new UserSpecification(new UserSpecParams { PageIndex = 2, PageSize = 15 });

        spec.IsPagingEnabled.Should().BeTrue();
        spec.Skip.Should().Be(15); // (2-1) * 15
        spec.Take.Should().Be(15);
    }

    [Fact]
    public void ListConstructor_DefaultSort_OrdersById()
    {
        var spec = new UserSpecification(new UserSpecParams());

        spec.OrderBy.Should().NotBeNull();
        spec.OrderByDescending.Should().BeNull();
    }

    [Theory]
    [InlineData("username_asc", true, false)]
    [InlineData("username_desc", false, true)]
    [InlineData("email_asc", true, false)]
    [InlineData("email_desc", false, true)]
    [InlineData("createdat_asc", true, false)]
    [InlineData("createdat_desc", false, true)]
    [InlineData("role_asc", true, false)]
    [InlineData("role_desc", false, true)]
    [InlineData("verifiedat_asc", true, false)]
    [InlineData("verifiedat_desc", false, true)]
    [InlineData("unknown_sort", true, false)] // defaults to OrderBy Id
    public void ListConstructor_SortOptions_SetsCorrectOrderDirection(
        string sort, bool expectOrderBy, bool expectOrderByDesc)
    {
        var spec = new UserSpecification(new UserSpecParams { Sort = sort });

        if (expectOrderBy)
            spec.OrderBy.Should().NotBeNull();
        else
            spec.OrderBy.Should().BeNull();

        if (expectOrderByDesc)
            spec.OrderByDescending.Should().NotBeNull();
        else
            spec.OrderByDescending.Should().BeNull();
    }

    [Fact]
    public void SingleIdConstructor_SetsCriteria_AndIncludesLicenses()
    {
        var spec = new UserSpecification(42);

        spec.Criteria.Should().NotBeNull();
        spec.Includes.Should().HaveCount(1);
        spec.IsPagingEnabled.Should().BeFalse();
    }

    [Fact]
    public void SingleIdConstructor_CriteriaMatchesCorrectId()
    {
        var spec = new UserSpecification(42);
        var func = spec.Criteria!.Compile();

        func(new User { Id = 42 }).Should().BeTrue();
        func(new User { Id = 99 }).Should().BeFalse();
    }
}

public class LicenseSpecificationTests
{
    [Fact]
    public void ListConstructor_AppliesPaging()
    {
        var spec = new LicenseSpecification(new LicenseSpecParams { PageIndex = 3, PageSize = 20 });

        spec.IsPagingEnabled.Should().BeTrue();
        spec.Skip.Should().Be(40); // (3-1) * 20
        spec.Take.Should().Be(20);
    }

    [Fact]
    public void ListConstructor_DefaultSort_OrdersByCreatedAtDesc()
    {
        var spec = new LicenseSpecification(new LicenseSpecParams());

        spec.OrderByDescending.Should().NotBeNull();
        spec.OrderBy.Should().BeNull();
    }

    [Theory]
    [InlineData("createdat_asc", true, false)]
    [InlineData("createdat_desc", false, true)]
    [InlineData("expiry_asc", true, false)]
    [InlineData("expiry_desc", false, true)]
    [InlineData("unknown", false, true)] // defaults to CreatedAt desc
    public void ListConstructor_SortOptions_SetsCorrectOrderDirection(
        string sort, bool expectOrderBy, bool expectOrderByDesc)
    {
        var spec = new LicenseSpecification(new LicenseSpecParams { Sort = sort });

        if (expectOrderBy)
            spec.OrderBy.Should().NotBeNull();
        else
            spec.OrderBy.Should().BeNull();

        if (expectOrderByDesc)
            spec.OrderByDescending.Should().NotBeNull();
        else
            spec.OrderByDescending.Should().BeNull();
    }

    [Fact]
    public void SingleIdConstructor_SetsCriteria_AndIncludesUser()
    {
        var spec = new LicenseSpecification(7);

        spec.Criteria.Should().NotBeNull();
        spec.Includes.Should().HaveCount(1);
        spec.IsPagingEnabled.Should().BeFalse();
    }

    [Fact]
    public void SingleIdConstructor_CriteriaMatchesCorrectId()
    {
        var spec = new LicenseSpecification(7);
        var func = spec.Criteria!.Compile();

        func(new License { Id = 7 }).Should().BeTrue();
        func(new License { Id = 99 }).Should().BeFalse();
    }
}
