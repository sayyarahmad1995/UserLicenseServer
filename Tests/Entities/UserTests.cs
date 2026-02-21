using Core.Entities;
using Core.Enums;
using FluentAssertions;
using Xunit;

namespace Tests.Entities;

public class UserTests
{
    private static User CreateUser(UserStatus status = UserStatus.Unverified) => new()
    {
        Id = 1,
        Username = "testuser",
        Email = "test@example.com",
        PasswordHash = "hash",
        Role = "User",
        Status = status
    };

    #region Verify

    [Fact]
    public void Verify_FromUnverified_ShouldSetVerifiedStatus()
    {
        var user = CreateUser(UserStatus.Unverified);

        user.Verify();

        user.Status.Should().Be(UserStatus.Verified);
        user.VerifiedAt.Should().NotBeNull();
        user.UpdatedAt.Should().NotBeNull();
    }

    [Fact]
    public void Verify_WhenAlreadyVerified_ShouldNotChange()
    {
        var user = CreateUser(UserStatus.Verified);
        var originalUpdatedAt = user.UpdatedAt;

        user.Verify();

        user.Status.Should().Be(UserStatus.Verified);
        user.UpdatedAt.Should().Be(originalUpdatedAt);
    }

    [Fact]
    public void Verify_WhenActive_ShouldNotChange()
    {
        var user = CreateUser(UserStatus.Active);

        user.Verify();

        user.Status.Should().Be(UserStatus.Active);
    }

    [Fact]
    public void Verify_FromBlocked_ShouldSetVerifiedStatus()
    {
        var user = CreateUser(UserStatus.Blocked);

        user.Verify();

        user.Status.Should().Be(UserStatus.Verified);
        user.VerifiedAt.Should().NotBeNull();
    }

    #endregion

    #region Activate

    [Fact]
    public void Activate_FromVerified_ShouldSetActiveStatus()
    {
        var user = CreateUser(UserStatus.Verified);

        user.Activate();

        user.Status.Should().Be(UserStatus.Active);
        user.UpdatedAt.Should().NotBeNull();
    }

    [Fact]
    public void Activate_FromUnverified_ShouldSetActiveStatus()
    {
        var user = CreateUser(UserStatus.Unverified);

        user.Activate();

        user.Status.Should().Be(UserStatus.Active);
    }

    [Fact]
    public void Activate_FromBlocked_ShouldThrowInvalidOperationException()
    {
        var user = CreateUser(UserStatus.Blocked);

        var act = () => user.Activate();

        act.Should().Throw<InvalidOperationException>()
            .WithMessage("*blocked*");
    }

    #endregion

    #region Block

    [Fact]
    public void Block_FromActive_ShouldSetBlockedStatus()
    {
        var user = CreateUser(UserStatus.Active);

        user.Block();

        user.Status.Should().Be(UserStatus.Blocked);
        user.BlockedAt.Should().NotBeNull();
        user.UpdatedAt.Should().NotBeNull();
    }

    [Fact]
    public void Block_WhenAlreadyBlocked_ShouldNotChange()
    {
        var user = CreateUser(UserStatus.Blocked);
        user.BlockedAt = DateTime.UtcNow.AddDays(-1);
        var originalBlockedAt = user.BlockedAt;

        user.Block();

        user.Status.Should().Be(UserStatus.Blocked);
        user.BlockedAt.Should().Be(originalBlockedAt);
    }

    #endregion

    #region Unblock

    [Fact]
    public void Unblock_FromBlocked_ShouldSetActiveStatus()
    {
        var user = CreateUser(UserStatus.Blocked);
        user.BlockedAt = DateTime.UtcNow;

        user.Unblock();

        user.Status.Should().Be(UserStatus.Active);
        user.BlockedAt.Should().BeNull();
        user.UpdatedAt.Should().NotBeNull();
    }

    [Fact]
    public void Unblock_WhenNotBlocked_ShouldNotChange()
    {
        var user = CreateUser(UserStatus.Active);

        user.Unblock();

        user.Status.Should().Be(UserStatus.Active);
    }

    #endregion

    #region Default Values

    [Fact]
    public void NewUser_ShouldHaveDefaultValues()
    {
        var user = new User();

        user.Role.Should().Be("User");
        user.Status.Should().Be(UserStatus.Unverified);
        user.CreatedAt.Should().BeCloseTo(DateTime.UtcNow, TimeSpan.FromSeconds(5));
        user.Licenses.Should().NotBeNull().And.BeEmpty();
    }

    #endregion
}
