namespace UrlShortener.Api.Services;

public interface ILinkService
{
    Task<CreateLinkResult> CreateAsync(
        CreateLinkCommand command,
        CancellationToken cancellationToken);

    Task<LinkAccessResult> AccessAsync(string code, CancellationToken cancellationToken);

    Task<LinkStatsResult> GetStatsAsync(
        string code,
        string? managementKey,
        CancellationToken cancellationToken);
}

public sealed record CreateLinkCommand(
    string TargetUrl,
    string? CustomCode,
    DateTimeOffset? ExpiresAtUtc);

public enum CreateLinkStatus
{
    Created,
    InvalidCode,
    CodeAlreadyExists,
    ExpirationInPast,
    CodeGenerationFailed
}

public sealed record CreateLinkResult(
    CreateLinkStatus Status,
    string? Code = null,
    string? ManagementKey = null);

public sealed record LinkAccessResult(
    bool Exists,
    bool IsExpired,
    string? TargetUrl);

public enum LinkStatsStatus
{
    Success,
    NotFound,
    Unauthorized
}

public sealed record LinkStatsResult(LinkStatsStatus Status, long? AccessCount = null);
