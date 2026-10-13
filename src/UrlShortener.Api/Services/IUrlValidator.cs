namespace UrlShortener.Api.Services;

public interface IUrlValidator
{
    Task<string?> ValidateAsync(string? value, CancellationToken cancellationToken);
}
