using UrlShortener.Api.Domain;

namespace UrlShortener.Api.Data;

public interface ILinkRepository
{
    Task<bool> CodeExistsAsync(string code, CancellationToken cancellationToken);
    Task AddAsync(Link link, CancellationToken cancellationToken);
    Task<Link?> FindByCodeAsync(string code, CancellationToken cancellationToken);
    Task IncrementAccessCountAsync(string code, CancellationToken cancellationToken);
}
