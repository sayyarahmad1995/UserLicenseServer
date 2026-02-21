using Api;
using Core.DTOs;
using Core.Entities;
using Core.Enums;
using Core.Interfaces;
using FluentAssertions;
using Infrastructure.Data;
using Microsoft.Extensions.DependencyInjection;
using System.Net;
using System.Text;
using System.Text.Json;
using Tests.Helpers;
using Xunit;

namespace Tests.Integration;

public class AuthControllerIntegrationTests : IntegrationTestBase
{
    protected override string DatabaseName => "eazecad_auth_test_db";

    #region Register Tests

    [Fact]
    public async Task Register_WithValidCredentials_ShouldReturnSuccess()
    {
        var registerDto = new RegisterDto
        {
            Username = "newuser",
            Email = "newuser@example.com",
            Password = "ValidPass@123"
        };

        var content = new StringContent(
            JsonSerializer.Serialize(registerDto),
            Encoding.UTF8,
            "application/json"
        );

        var response = await Client.PostAsync("/api/v1/auth/register", content);

        response.StatusCode.Should().Be(HttpStatusCode.Created);
        var responseContent = await response.Content.ReadAsStringAsync();
        responseContent.Should().Contain("Registered successfully");
    }

    [Fact]
    public async Task Register_WithDuplicateEmail_ShouldReturnBadRequest()
    {
        var registerDto1 = new RegisterDto
        {
            Username = "user1",
            Email = "duplicate@example.com",
            Password = "ValidPass@123"
        };

        var content1 = new StringContent(
            JsonSerializer.Serialize(registerDto1),
            Encoding.UTF8,
            "application/json"
        );

        await Client.PostAsync("/api/v1/auth/register", content1);

        var registerDto2 = new RegisterDto
        {
            Username = "user2",
            Email = "duplicate@example.com",
            Password = "ValidPass@123"
        };

        var content2 = new StringContent(
            JsonSerializer.Serialize(registerDto2),
            Encoding.UTF8,
            "application/json"
        );

        var response = await Client.PostAsync("/api/v1/auth/register", content2);

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        var responseContent = await response.Content.ReadAsStringAsync();
        responseContent.Should().Contain("Email already in use");
    }

    [Fact]
    public async Task Register_WithDuplicateUsername_ShouldReturnBadRequest()
    {
        var registerDto1 = new RegisterDto
        {
            Username = "duplicateuser",
            Email = "user1@example.com",
            Password = "ValidPass@123"
        };

        var content1 = new StringContent(
            JsonSerializer.Serialize(registerDto1),
            Encoding.UTF8,
            "application/json"
        );

        await Client.PostAsync("/api/v1/auth/register", content1);

        var registerDto2 = new RegisterDto
        {
            Username = "duplicateuser",
            Email = "user2@example.com",
            Password = "ValidPass@123"
        };

        var content2 = new StringContent(
            JsonSerializer.Serialize(registerDto2),
            Encoding.UTF8,
            "application/json"
        );

        var response = await Client.PostAsync("/api/v1/auth/register", content2);

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        var responseContent = await response.Content.ReadAsStringAsync();
        responseContent.Should().Contain("Username already taken");
    }

    [Fact]
    public async Task Register_WithWeakPassword_ShouldReturnBadRequest()
    {
        var registerDto = new RegisterDto
        {
            Username = "weakpassuser",
            Email = "weak@example.com",
            Password = "weak"
        };

        var content = new StringContent(
            JsonSerializer.Serialize(registerDto),
            Encoding.UTF8,
            "application/json"
        );

        var response = await Client.PostAsync("/api/v1/auth/register", content);

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        var responseContent = await response.Content.ReadAsStringAsync();
        responseContent.Should().Contain("Password");
    }

    #endregion

    #region Login Tests

    [Fact]
    public async Task Login_WithValidCredentials_ShouldReturnSuccess()
    {
        var registerDto = new RegisterDto
        {
            Username = "loginuser",
            Email = "login@example.com",
            Password = "ValidPass@123"
        };

        var registerContent = new StringContent(
            JsonSerializer.Serialize(registerDto),
            Encoding.UTF8,
            "application/json"
        );

        await Client.PostAsync("/api/v1/auth/register", registerContent);

        var loginDto = new LoginDto
        {
            Username = "loginuser",
            Password = "ValidPass@123"
        };

        var loginContent = new StringContent(
            JsonSerializer.Serialize(loginDto),
            Encoding.UTF8,
            "application/json"
        );

        var response = await Client.PostAsync("/api/v1/auth/login", loginContent);

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var responseContent = await response.Content.ReadAsStringAsync();
        responseContent.Should().Contain("Login successful");
        response.Headers.Should().ContainKey("Set-Cookie");
    }

    [Fact]
    public async Task Login_WithInvalidUsername_ShouldReturnUnauthorized()
    {
        var loginDto = new LoginDto
        {
            Username = "nonexistentuser",
            Password = "SomePassword@123"
        };

        var content = new StringContent(
            JsonSerializer.Serialize(loginDto),
            Encoding.UTF8,
            "application/json"
        );

        var response = await Client.PostAsync("/api/v1/auth/login", content);

        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
        var responseContent = await response.Content.ReadAsStringAsync();
        responseContent.Should().Contain("Invalid username or password");
    }

    [Fact]
    public async Task Login_WithInvalidPassword_ShouldReturnUnauthorized()
    {
        var registerDto = new RegisterDto
        {
            Username = "testuser",
            Email = "test@example.com",
            Password = "CorrectPass@123"
        };

        var registerContent = new StringContent(
            JsonSerializer.Serialize(registerDto),
            Encoding.UTF8,
            "application/json"
        );

        await Client.PostAsync("/api/v1/auth/register", registerContent);

        var loginDto = new LoginDto
        {
            Username = "testuser",
            Password = "WrongPass@123"
        };

        var loginContent = new StringContent(
            JsonSerializer.Serialize(loginDto),
            Encoding.UTF8,
            "application/json"
        );

        var response = await Client.PostAsync("/api/v1/auth/login", loginContent);

        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    #endregion

    #region GetCurrentUser Tests

    [Fact]
    public async Task GetCurrentUser_WithoutAuth_ShouldReturnUnauthorized()
    {
        var response = await Client.GetAsync("/api/v1/auth/me");

        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    #endregion
}
