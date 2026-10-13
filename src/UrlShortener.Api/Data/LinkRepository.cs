using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using UrlShortener.Api.Domain;

namespace UrlShortener.Api.Data;

public sealed class LinkRepository(LinkDbContext dbContext) : ILinkRepository
{
    public Task<bool> CodeExistsAsync(
        string code,
        CancellationToken cancellationToken) =>
        dbContext.Links.AnyAsync(x => x.Code == code, cancellationToken);

    public async Task AddAsync(Link link, CancellationToken cancellationToken)
    {
        dbContext.Links.Add(link);

        try
        {
            await dbContext.SaveChangesAsync(cancellationToken);
        }
        catch (DbUpdateException exception)
            when (exception.InnerException is SqliteException
            {
                SqliteErrorCode: 19,
                SqliteExtendedErrorCode: 2067
            })
        {
            dbContext.Entry(link).State = EntityState.Detached;
            throw new DuplicateLinkCodeException(link.Code, exception);
        }
    }

    public Task<Link?> FindByCodeAsync(
        string code,
        CancellationToken cancellationToken) =>
        dbContext.Links.SingleOrDefaultAsync(x => x.Code == code, cancellationToken);

    public async Task IncrementAccessCountAsync(
        string code,
        CancellationToken cancellationToken)
    {
        await dbContext.Links
            .Where(x => x.Code == code)
            .ExecuteUpdateAsync(
                setters => setters.SetProperty(
                    link => link.AccessCount,
                    link => link.AccessCount + 1),
                cancellationToken);
    }
}

public sealed class DuplicateLinkCodeException(string code, Exception innerException)
    : Exception($"O código '{code}' já está em uso.", innerException);
