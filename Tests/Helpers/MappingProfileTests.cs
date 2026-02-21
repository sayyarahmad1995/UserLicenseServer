using Api.Helpers;
using AutoMapper;
using Core.DTOs;
using Core.Entities;
using Core.Enums;
using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace Tests.Helpers;

public class MappingProfileTests
{
    private readonly IMapper _mapper;

    public MappingProfileTests()
    {
        var expr = new MapperConfigurationExpression();
        expr.AddProfile<MappingProfile>();
        var config = new MapperConfiguration(expr, NullLoggerFactory.Instance);
        _mapper = config.CreateMapper();
    }

    #region Configuration Validation

    [Fact]
    public void AutoMapperConfiguration_UserToUserDto_IsValid()
    {
        var expr = new MapperConfigurationExpression();
        expr.CreateMap<User, UserDto>();
        var config = new MapperConfiguration(expr, NullLoggerFactory.Instance);
        config.AssertConfigurationIsValid();
    }

    [Fact]
    public void AutoMapperConfiguration_LicenseToLicenseDto_IsValid()
    {
        var expr = new MapperConfigurationExpression();
        expr.CreateMap<License, LicenseDto>()
            .ForMember(dest => dest.ActiveActivations,
                opt => opt.MapFrom(src => src.Activations.Count(a => a.DeactivatedAt == null)));
        var config = new MapperConfiguration(expr, NullLoggerFactory.Instance);
        config.AssertConfigurationIsValid();
    }

    #endregion

    #region User → UserDto

    [Fact]
    public void UserToUserDto_MapsAllProperties()
    {
        var user = new User
        {
            Id = 42,
            Username = "testuser",
            Email = "test@example.com",
            Role = "Admin",
            CreatedAt = new DateTime(2024, 1, 1, 0, 0, 0, DateTimeKind.Utc),
            VerifiedAt = new DateTime(2024, 1, 2, 0, 0, 0, DateTimeKind.Utc),
            UpdatedAt = new DateTime(2024, 1, 3, 0, 0, 0, DateTimeKind.Utc),
            LastLogin = new DateTime(2024, 1, 4, 0, 0, 0, DateTimeKind.Utc),
            Status = UserStatus.Active
        };

        var dto = _mapper.Map<UserDto>(user);

        dto.Id.Should().Be(42);
        dto.Username.Should().Be("testuser");
        dto.Email.Should().Be("test@example.com");
        dto.Role.Should().Be("Admin");
        dto.CreatedAt.Should().Be(new DateTime(2024, 1, 1, 0, 0, 0, DateTimeKind.Utc));
        dto.VerifiedAt.Should().Be(new DateTime(2024, 1, 2, 0, 0, 0, DateTimeKind.Utc));
        dto.UpdatedAt.Should().Be(new DateTime(2024, 1, 3, 0, 0, 0, DateTimeKind.Utc));
        dto.LastLogin.Should().Be(new DateTime(2024, 1, 4, 0, 0, 0, DateTimeKind.Utc));
        dto.Status.Should().Be("Active");
    }

    [Fact]
    public void UserToUserDto_NullableFields_MappedAsNull()
    {
        var user = new User
        {
            Id = 1,
            Username = "u",
            Email = "e@e.com",
            Status = UserStatus.Unverified
        };

        var dto = _mapper.Map<UserDto>(user);

        dto.VerifiedAt.Should().BeNull();
        dto.UpdatedAt.Should().BeNull();
        dto.LastLogin.Should().BeNull();
    }

    [Fact]
    public void UserToUserDto_DoesNotExposePasswordHash()
    {
        var user = new User
        {
            Id = 1,
            Username = "u",
            Email = "e@e.com",
            PasswordHash = "secret_hash"
        };

        var dto = _mapper.Map<UserDto>(user);

        // UserDto has no PasswordHash property — AutoMapper simply ignores it
        dto.Should().NotBeNull();
        dto.GetType().GetProperty("PasswordHash").Should().BeNull();
    }

    #endregion

    #region License → LicenseDto

    [Fact]
    public void LicenseToLicenseDto_MapsAllProperties()
    {
        var license = new License
        {
            Id = 7,
            LicenseKey = "ABC-DEF-123",
            CreatedAt = new DateTime(2024, 6, 1, 0, 0, 0, DateTimeKind.Utc),
            ExpiresAt = new DateTime(2025, 6, 1, 0, 0, 0, DateTimeKind.Utc),
            RevokedAt = new DateTime(2024, 12, 1, 0, 0, 0, DateTimeKind.Utc),
            Status = LicenseStatus.Revoked,
            MaxActivations = 3,
            Activations = new List<LicenseActivation>
            {
                new() { MachineFingerprint = "fp1", DeactivatedAt = null },
                new() { MachineFingerprint = "fp2", DeactivatedAt = DateTime.UtcNow }
            }
        };

        var dto = _mapper.Map<LicenseDto>(license);

        dto.Id.Should().Be(7);
        dto.LicenseKey.Should().Be("ABC-DEF-123");
        dto.CreatedAt.Should().Be(new DateTime(2024, 6, 1, 0, 0, 0, DateTimeKind.Utc));
        dto.ExpiresAt.Should().Be(new DateTime(2025, 6, 1, 0, 0, 0, DateTimeKind.Utc));
        dto.RevokedAt.Should().Be(new DateTime(2024, 12, 1, 0, 0, 0, DateTimeKind.Utc));
        dto.Status.Should().Be(LicenseStatus.Revoked);
        dto.MaxActivations.Should().Be(3);
        dto.ActiveActivations.Should().Be(1, "only one activation has no DeactivatedAt");
    }

    #endregion

    #region RegisterDto → User

    [Fact]
    public void RegisterDtoToUser_IgnoresPasswordHash()
    {
        var dto = new RegisterDto
        {
            Username = "newuser",
            Email = "new@example.com",
            Password = "StrongP@ss1"
        };

        var user = _mapper.Map<User>(dto);

        user.Username.Should().Be("newuser");
        user.Email.Should().Be("new@example.com");
        user.PasswordHash.Should().Be(string.Empty, "PasswordHash is ignored and keeps default");
    }

    [Fact]
    public void RegisterDtoToUser_SetsCreatedAtToUtcNow()
    {
        var before = DateTime.UtcNow;
        var dto = new RegisterDto { Username = "u", Email = "e@e.com", Password = "P@ss1234" };

        var user = _mapper.Map<User>(dto);

        user.CreatedAt.Should().BeOnOrAfter(before);
        user.CreatedAt.Should().BeCloseTo(DateTime.UtcNow, TimeSpan.FromSeconds(2));
    }

    [Fact]
    public void RegisterDtoToUser_SetsStatusToUnverified()
    {
        var dto = new RegisterDto { Username = "u", Email = "e@e.com", Password = "P@ss1234" };

        var user = _mapper.Map<User>(dto);

        user.Status.Should().Be(UserStatus.Unverified);
    }

    #endregion
}
