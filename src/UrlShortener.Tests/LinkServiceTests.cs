using System.Security.Cryptography;
using System.Text;
using UrlShortener.Api.Data;
using UrlShortener.Api.Domain;
using UrlShortener.Api.Services;

namespace UrlShortener.Tests;

public sealed class LinkServiceTests
{
    [Fact]
    public async Task CreateAsync_RegeneratesAnAutomaticCodeAlreadyInUse()
    {
        var repository = new InMemoryLinkRepository();
        repository.Seed(new Link
        {
            Id = Guid.NewGuid(),
            Code = "USED123",
            TargetUrl = "https://example.org",
            CreatedAtUtc = DateTime.UtcNow,
            ManagementKeyHash = new string('0', 64)
        });
        var generator = new SequenceCodeGenerator("USED123", "NEW1234");
        var service = CreateService(repository, generator, new MutableTimeProvider());

        var result = await service.CreateAsync(
            new CreateLinkCommand("https://example.org", null, null),
            CancellationToken.None);

        Assert.Equal(CreateLinkStatus.Created, result.Status);
        Assert.Equal("NEW1234", result.Code);
        Assert.Equal(2, generator.Calls);
    }

    [Theory]
    [InlineData("bad0")]
    [InlineData("bad ")]
    public async Task CreateAsync_RejectsInvalidCustomCode(string code)
    {
        var service = CreateService(
            new InMemoryLinkRepository(),
            new SequenceCodeGenerator(),
            new MutableTimeProvider());

        var result = await service.CreateAsync(
            new CreateLinkCommand("https://example.org", code, null),
            CancellationToken.None);

        Assert.Equal(CreateLinkStatus.InvalidCode, result.Status);
    }

    [Fact]
    public async Task AccessAsync_RejectsExpiredLinkWithoutIncrementingCount()
    {
        var repository = new InMemoryLinkRepository();
        var clock = new MutableTimeProvider(new DateTimeOffset(2026, 10, 13, 12, 0, 0, TimeSpan.Zero));
        repository.Seed(new Link
        {
            Id = Guid.NewGuid(),
            Code = "EXPIRE1",
            TargetUrl = "https://example.org",
            CreatedAtUtc = clock.GetUtcNow().UtcDateTime.AddDays(-2),
            ExpiresAtUtc = clock.GetUtcNow().UtcDateTime,
            ManagementKeyHash = new string('0', 64)
        });
        var service = CreateService(repository, new SequenceCodeGenerator(), clock);

        var result = await service.AccessAsync("EXPIRE1", CancellationToken.None);

        Assert.True(result.IsExpired);
        Assert.Equal(0, repository.GetAccessCount("EXPIRE1"));
    }

    [Fact]
    public async Task CreateAsync_RejectsAnExpirationDateBeforeToday()
    {
        var clock = new MutableTimeProvider(new DateTimeOffset(2026, 10, 13, 12, 0, 0, TimeSpan.Zero));
        var service = CreateService(
            new InMemoryLinkRepository(),
            new SequenceCodeGenerator(),
            clock);

        var result = await service.CreateAsync(
            new CreateLinkCommand(
                "https://example.org",
                null,
                new DateTimeOffset(2026, 10, 12, 23, 59, 0, TimeSpan.Zero)),
            CancellationToken.None);

        Assert.Equal(CreateLinkStatus.ExpirationInPast, result.Status);
    }

    [Fact]
    public async Task AccessAsync_RemainsValidUntilTheEndOfTheUtcExpirationDate()
    {
        var clock = new MutableTimeProvider(new DateTimeOffset(2026, 10, 13, 12, 0, 0, TimeSpan.Zero));
        var repository = new InMemoryLinkRepository();
        var service = CreateService(repository, new SequenceCodeGenerator("DATE123"), clock);
        var createResult = await service.CreateAsync(
            new CreateLinkCommand("https://example.org", null, clock.GetUtcNow()),
            CancellationToken.None);

        Assert.Equal(CreateLinkStatus.Created, createResult.Status);
        clock.Now = new DateTimeOffset(2026, 10, 13, 23, 59, 59, TimeSpan.Zero)
            .AddTicks(TimeSpan.TicksPerSecond - 1);
        var lastValidAccess = await service.AccessAsync("DATE123", CancellationToken.None);
        Assert.False(lastValidAccess.IsExpired);

        clock.Now = new DateTimeOffset(2026, 10, 14, 0, 0, 0, TimeSpan.Zero);
        var afterExpiration = await service.AccessAsync("DATE123", CancellationToken.None);

        Assert.True(afterExpiration.IsExpired);
        Assert.Equal(1, repository.GetAccessCount("DATE123"));
    }

    [Fact]
    public async Task CreateAsync_StoresOnlyManagementKeyHash()
    {
        var repository = new InMemoryLinkRepository();
        var service = CreateService(
            repository,
            new SequenceCodeGenerator("HASH123"),
            new MutableTimeProvider());

        var result = await service.CreateAsync(
            new CreateLinkCommand("https://example.org", null, null),
            CancellationToken.None);
        var storedLink = repository.Get(result.Code!);
        var expectedHash = Convert.ToHexString(
            SHA256.HashData(Encoding.UTF8.GetBytes(result.ManagementKey!)));

        Assert.Equal(expectedHash, storedLink!.ManagementKeyHash);
        Assert.NotEqual(result.ManagementKey, storedLink.ManagementKeyHash);
    }

    private static LinkService CreateService(
        InMemoryLinkRepository repository,
        ILinkCodeGenerator generator,
        TimeProvider timeProvider) =>
        new(repository, generator, timeProvider);

    private sealed class SequenceCodeGenerator(params string[] codes) : ILinkCodeGenerator
    {
        private int _nextIndex;

        public int Calls { get; private set; }

        public string Generate()
        {
            Calls++;
            return codes[Math.Min(_nextIndex++, codes.Length - 1)];
        }
    }

    private sealed class MutableTimeProvider(DateTimeOffset? now = null) : TimeProvider
    {
        public DateTimeOffset Now { get; set; } = now ?? new DateTimeOffset(
            2026, 10, 13, 12, 0, 0, TimeSpan.Zero);

        public override DateTimeOffset GetUtcNow() => Now;
    }

    private sealed class InMemoryLinkRepository : ILinkRepository
    {
        private readonly Dictionary<string, Link> _links = [];

        public Task<bool> CodeExistsAsync(string code, CancellationToken cancellationToken) =>
            Task.FromResult(_links.ContainsKey(code));

        public Task AddAsync(Link link, CancellationToken cancellationToken)
        {
            if (_links.ContainsKey(link.Code))
            {
                throw new DuplicateLinkCodeException(link.Code, new InvalidOperationException());
            }

            _links.Add(link.Code, link);
            return Task.CompletedTask;
        }

        public Task<Link?> FindByCodeAsync(string code, CancellationToken cancellationToken) =>
            Task.FromResult(_links.GetValueOrDefault(code));

        public Task IncrementAccessCountAsync(string code, CancellationToken cancellationToken)
        {
            _links[code].AccessCount++;
            return Task.CompletedTask;
        }

        public void Seed(Link link) => _links.Add(link.Code, link);

        public long GetAccessCount(string code) => _links[code].AccessCount;

        public Link? Get(string? code) => code is null ? null : _links.GetValueOrDefault(code);
    }
}
