using Core.Interfaces;
using FluentAssertions;
using System.Net;
using Tests.Helpers;
using Xunit;

namespace Tests.Integration;

public class UsersControllerIntegrationTests : IntegrationTestBase
{
    protected override string DatabaseName => "eazecad_users_test_db";

    [Fact]
    public async Task GetUsers_WithoutAuth_ShouldReturnUnauthorized()
    {
        var response = await Client.GetAsync("/api/v1/users");

        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task HealthCheck_ShouldReturnSuccess()
    {
        var response = await Client.GetAsync("/api/v1/health");

        response.StatusCode.Should().Be(HttpStatusCode.OK);
    }

#if DEBUG
    // These tests hit TestController endpoints which are compiled only in Debug builds.
    [Fact]
    public async Task TestEndpoint_ShouldReturnSuccess()
    {
        var response = await Client.GetAsync("/api/v1/test/ok");

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var content = await response.Content.ReadAsStringAsync();
        content.Should().Contain("Everything is working fine");
    }

    [Fact]
    public async Task TestBadRequest_ShouldReturn400()
    {
        var response = await Client.GetAsync("/api/v1/test/badrequest");

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task TestUnauthorized_ShouldReturn401()
    {
        var response = await Client.GetAsync("/api/v1/test/unauthorized");

        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task TestNotFound_ShouldReturn404()
    {
        var response = await Client.GetAsync("/api/v1/test/notfound");

        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task TestServerError_ShouldReturn500()
    {
        var response = await Client.GetAsync("/api/v1/test/servererror");

        response.StatusCode.Should().Be(HttpStatusCode.InternalServerError);
    }
#endif
}
