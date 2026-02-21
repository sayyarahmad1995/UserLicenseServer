using FluentAssertions;
using Infrastructure.Services.Security;
using Xunit;

namespace Tests.Helpers;

public class TokenHasherTests
{
    [Fact]
    public void HashToken_ShouldReturnConsistentHash()
    {
        var token = "test-token-123";

        var hash1 = TokenHasher.HashToken(token);
        var hash2 = TokenHasher.HashToken(token);

        hash1.Should().Be(hash2);
    }

    [Fact]
    public void HashToken_ShouldReturnDifferentHashForDifferentInputs()
    {
        var hash1 = TokenHasher.HashToken("token-a");
        var hash2 = TokenHasher.HashToken("token-b");

        hash1.Should().NotBe(hash2);
    }

    [Fact]
    public void HashToken_ShouldReturnBase64String()
    {
        var hash = TokenHasher.HashToken("test-token");

        // Should be valid base64
        var act = () => Convert.FromBase64String(hash);
        act.Should().NotThrow();
    }

    [Fact]
    public void HashToken_ShouldReturnSha256Length()
    {
        var hash = TokenHasher.HashToken("test-token");

        // SHA256 produces 32 bytes → 44 chars in base64 (with padding)
        Convert.FromBase64String(hash).Length.Should().Be(32);
    }

    [Fact]
    public void HashToken_ShouldNotReturnEmptyString()
    {
        var hash = TokenHasher.HashToken("any-input");

        hash.Should().NotBeNullOrEmpty();
    }

    [Fact]
    public void HashToken_WithEmptyInput_ShouldStillReturnHash()
    {
        var hash = TokenHasher.HashToken("");

        hash.Should().NotBeNullOrEmpty();
        Convert.FromBase64String(hash).Length.Should().Be(32);
    }
}
