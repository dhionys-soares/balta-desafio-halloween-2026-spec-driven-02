using System.Net;
using System.Net.Http.Json;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using UrlShortener.Api.Data;
using UrlShortener.Api.Domain;

namespace UrlShortener.Tests;

public sealed class LinkEndpointsTests(ApiFactory factory) : IClassFixture<ApiFactory>
{
    [Fact]
    public async Task CreateAndRedirect_StoresTheLinkAndCountsAccess()
    {
        using var client = factory.CreateClient(new WebApplicationFactoryClientOptions
        {
            AllowAutoRedirect = false
        });
        var createResponse = await client.PostAsJsonAsync(
            "/api/links",
            new { targetUrl = "https://8.8.8.8/path" });

        Assert.Equal(HttpStatusCode.Created, createResponse.StatusCode);
        var created = await createResponse.Content.ReadFromJsonAsync<CreateLinkPayload>();
        Assert.NotNull(created);
        Assert.Matches("^[A-Z1-9]{7}$", created.Code);
        Assert.False(string.IsNullOrWhiteSpace(created.ManagementKey));

        var redirectResponse = await client.GetAsync($"/{created.Code}");

        Assert.Equal(HttpStatusCode.Found, redirectResponse.StatusCode);
        Assert.Equal("https://8.8.8.8/path", redirectResponse.Headers.Location?.ToString());

        client.DefaultRequestHeaders.Add("X-Management-Key", created.ManagementKey);
        var stats = await client.GetFromJsonAsync<LinkStatsPayload>(
            $"/api/links/{created.Code}/stats");

        Assert.Equal(1, stats?.AccessCount);
    }

    [Fact]
    public async Task CreateLink_RejectsDuplicateAndInvalidCustomCodes()
    {
        using var client = factory.CreateClient();
        var body = new { targetUrl = "https://8.8.8.8", code = "MYCODE1" };

        Assert.Equal(
            HttpStatusCode.Created,
            (await client.PostAsJsonAsync("/api/links", body)).StatusCode);
        Assert.Equal(
            HttpStatusCode.Conflict,
            (await client.PostAsJsonAsync("/api/links", body)).StatusCode);

        var invalid = await client.PostAsJsonAsync(
            "/api/links",
            new { targetUrl = "https://8.8.8.8", code = "invalid0" });
        Assert.Equal(HttpStatusCode.BadRequest, invalid.StatusCode);
        Assert.Equal(
            "application/problem+json",
            invalid.Content.Headers.ContentType?.MediaType);
    }

    [Fact]
    public async Task ExpiredLink_Returns410WithoutIncrementingCount()
    {
        const string code = "OLDLINK";
        await using (var scope = factory.Services.CreateAsyncScope())
        {
            var dbContext = scope.ServiceProvider.GetRequiredService<LinkDbContext>();
            dbContext.Links.Add(new Link
            {
                Id = Guid.NewGuid(),
                Code = code,
                TargetUrl = "https://8.8.8.8",
                CreatedAtUtc = DateTime.UtcNow.AddDays(-2),
                ExpiresAtUtc = DateTime.UtcNow.AddTicks(-1),
                ManagementKeyHash = new string('0', 64)
            });
            await dbContext.SaveChangesAsync();
        }

        using var client = factory.CreateClient(new WebApplicationFactoryClientOptions
        {
            AllowAutoRedirect = false
        });
        var response = await client.GetAsync($"/{code}");
        Assert.Equal(HttpStatusCode.Gone, response.StatusCode);

        var expiredLink = await factory.GetLinkAsync(code);
        Assert.Equal(0, expiredLink.AccessCount);
    }

    [Fact]
    public async Task Stats_RejectsIncorrectManagementKey()
    {
        using var client = factory.CreateClient();
        var created = await client.PostAsJsonAsync(
            "/api/links",
            new { targetUrl = "https://8.8.8.8", code = "KEYTEST1" });

        using var statsRequest = new HttpRequestMessage(
            HttpMethod.Get,
            "/api/links/KEYTEST1/stats");
        statsRequest.Headers.Add("X-Management-Key", "not-the-key");
        var response = await client.SendAsync(statsRequest);

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
        Assert.Equal(HttpStatusCode.Created, created.StatusCode);
    }

    [Fact]
    public async Task ConcurrentRedirects_DoNotLoseAccesses()
    {
        using var client = factory.CreateClient(new WebApplicationFactoryClientOptions
        {
            AllowAutoRedirect = false
        });
        var response = await client.PostAsJsonAsync(
            "/api/links",
            new { targetUrl = "https://8.8.8.8", code = "RACE123" });
        var created = await response.Content.ReadFromJsonAsync<CreateLinkPayload>();
        Assert.NotNull(created);

        var requests = Enumerable.Range(0, 12)
            .Select(_ => client.GetAsync($"/{created.Code}"));
        var responses = await Task.WhenAll(requests);

        Assert.All(responses, item => Assert.Equal(HttpStatusCode.Found, item.StatusCode));
        client.DefaultRequestHeaders.Add("X-Management-Key", created.ManagementKey);
        var stats = await client.GetFromJsonAsync<LinkStatsPayload>(
            $"/api/links/{created.Code}/stats");
        Assert.Equal(12, stats?.AccessCount);
    }

    public sealed record CreateLinkPayload(string ShortUrl, string Code, string ManagementKey);

    public sealed record LinkStatsPayload(string Code, long AccessCount);
}

public sealed class ApiFactory : WebApplicationFactory<Program>
{
    private readonly string _databasePath = Path.Combine(
        Path.GetTempPath(),
        $"url-shortener-{Guid.NewGuid():N}.db");

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.ConfigureAppConfiguration(configuration =>
        {
            configuration.AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["ConnectionStrings:DefaultConnection"] =
                    $"Data Source={_databasePath};Pooling=False"
            });
        });
    }

    public async Task<Link> GetLinkAsync(string code)
    {
        await using var scope = Services.CreateAsyncScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<LinkDbContext>();
        return await dbContext.Links.AsNoTracking().SingleAsync(link => link.Code == code);
    }

    protected override void Dispose(bool disposing)
    {
        base.Dispose(disposing);

        if (disposing && File.Exists(_databasePath))
        {
            File.Delete(_databasePath);
        }
    }
}
