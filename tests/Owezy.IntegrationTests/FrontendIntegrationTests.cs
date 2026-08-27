using System.Net;
using Microsoft.AspNetCore.Mvc.Testing;
using Xunit;

namespace Owezy.IntegrationTests;

public class FrontendIntegrationTests : IClassFixture<WebApplicationFactory<Program>>
{
    private const string TestJwtKey = "api-test-jwt-signing-secret-key-32chars-long-12345";
    private readonly WebApplicationFactory<Program> _factory;

    public FrontendIntegrationTests(WebApplicationFactory<Program> factory)
    {
        _factory = factory.WithWebHostBuilder(builder =>
        {
            builder.UseSetting("Jwt:SigningKey", TestJwtKey);
            builder.UseSetting("Jwt:Issuer", "Owezy.Api");
            builder.UseSetting("Jwt:Audience", "Owezy.App");
            builder.UseSetting("OtpHasher:SecretKey", "test-otp-hasher-secret-key-32chars-long-12345");
        });
    }

    [Fact]
    public async Task GetRoot_Returns200OK_WithIndexHtmlContent()
    {
        var client = _factory.CreateClient();

        var response = await client.GetAsync("/");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var html = await response.Content.ReadAsStringAsync();
        Assert.Contains("Owezy — Lightweight Bill Splitting", html);
        Assert.Contains("app.js", html);
    }

    [Fact]
    public async Task GetMainCss_Returns200OK_WithCssContentType()
    {
        var client = _factory.CreateClient();

        var response = await client.GetAsync("/styles/main.css");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal("text/css", response.Content.Headers.ContentType?.MediaType);
    }

    [Fact]
    public async Task GetAppJs_Returns200OK_WithJsContentType()
    {
        var client = _factory.CreateClient();

        var response = await client.GetAsync("/js/app.js");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Contains("javascript", response.Content.Headers.ContentType?.MediaType);
    }
}
