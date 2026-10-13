using System.Security.Cryptography;
using System.Text;
using UrlShortener.Api.Data;
using UrlShortener.Api.Domain;

namespace UrlShortener.Api.Services;

public sealed class LinkService(
    ILinkRepository repository,
    ILinkCodeGenerator codeGenerator,
    TimeProvider timeProvider) : ILinkService
{
    private const int AutomaticCodeLength = 7;
    private const int MaximumGenerationAttempts = 10;

    public async Task<CreateLinkResult> CreateAsync(
        CreateLinkCommand command,
        CancellationToken cancellationToken)
    {
        if (command.CustomCode is not null
            && !LinkCodeRules.IsValidCustomCode(command.CustomCode))
        {
            return new CreateLinkResult(CreateLinkStatus.InvalidCode);
        }

        var expirationDate = command.ExpiresAtUtc is { } requestedExpiration
            ? DateOnly.FromDateTime(requestedExpiration.UtcDateTime)
            : (DateOnly?)null;
        var today = DateOnly.FromDateTime(timeProvider.GetUtcNow().UtcDateTime);

        if (expirationDate < today)
        {
            return new CreateLinkResult(CreateLinkStatus.ExpirationInPast);
        }

        var expiresAtUtc = expirationDate?.AddDays(1)
            .ToDateTime(TimeOnly.MinValue, DateTimeKind.Utc);
        var managementKey = Convert.ToHexString(RandomNumberGenerator.GetBytes(32));
        var managementKeyHash = Convert.ToHexString(
            SHA256.HashData(Encoding.UTF8.GetBytes(managementKey)));
        var attempts = command.CustomCode is null ? MaximumGenerationAttempts : 1;

        for (var attempt = 0; attempt < attempts; attempt++)
        {
            var code = command.CustomCode ?? codeGenerator.Generate();

            if (code.Length != AutomaticCodeLength && command.CustomCode is null)
            {
                return new CreateLinkResult(CreateLinkStatus.CodeGenerationFailed);
            }

            if (await repository.CodeExistsAsync(code, cancellationToken))
            {
                if (command.CustomCode is not null)
                {
                    return new CreateLinkResult(CreateLinkStatus.CodeAlreadyExists);
                }

                continue;
            }

            var link = new Link
            {
                Id = Guid.NewGuid(),
                Code = code,
                TargetUrl = command.TargetUrl,
                CreatedAtUtc = timeProvider.GetUtcNow().UtcDateTime,
                ExpiresAtUtc = expiresAtUtc,
                ManagementKeyHash = managementKeyHash
            };

            try
            {
                await repository.AddAsync(link, cancellationToken);
                return new CreateLinkResult(
                    CreateLinkStatus.Created,
                    link.Code,
                    managementKey);
            }
            catch (DuplicateLinkCodeException)
            {
                if (command.CustomCode is not null)
                {
                    return new CreateLinkResult(CreateLinkStatus.CodeAlreadyExists);
                }
            }
        }

        return new CreateLinkResult(CreateLinkStatus.CodeGenerationFailed);
    }

    public async Task<LinkAccessResult> AccessAsync(
        string code,
        CancellationToken cancellationToken)
    {
        var link = await repository.FindByCodeAsync(code, cancellationToken);

        if (link is null)
        {
            return new LinkAccessResult(false, false, null);
        }

        if (link.ExpiresAtUtc is { } expiresAtUtc
            && timeProvider.GetUtcNow().UtcDateTime >= expiresAtUtc)
        {
            return new LinkAccessResult(true, true, null);
        }

        await repository.IncrementAccessCountAsync(code, cancellationToken);
        return new LinkAccessResult(true, false, link.TargetUrl);
    }

    public async Task<LinkStatsResult> GetStatsAsync(
        string code,
        string? managementKey,
        CancellationToken cancellationToken)
    {
        var link = await repository.FindByCodeAsync(code, cancellationToken);

        if (link is null)
        {
            return new LinkStatsResult(LinkStatsStatus.NotFound);
        }

        if (!IsManagementKeyValid(managementKey, link.ManagementKeyHash))
        {
            return new LinkStatsResult(LinkStatsStatus.Unauthorized);
        }

        return new LinkStatsResult(LinkStatsStatus.Success, link.AccessCount);
    }

    private static bool IsManagementKeyValid(string? managementKey, string storedHash)
    {
        if (string.IsNullOrEmpty(managementKey))
        {
            return false;
        }

        var suppliedHash = SHA256.HashData(Encoding.UTF8.GetBytes(managementKey));
        var expectedHash = Convert.FromHexString(storedHash);
        return CryptographicOperations.FixedTimeEquals(suppliedHash, expectedHash);
    }
}
