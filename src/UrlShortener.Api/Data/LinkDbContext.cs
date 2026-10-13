using Microsoft.EntityFrameworkCore;
using UrlShortener.Api.Domain;

namespace UrlShortener.Api.Data;

public sealed class LinkDbContext(DbContextOptions<LinkDbContext> options)
    : DbContext(options)
{
    public DbSet<Link> Links => Set<Link>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        var link = modelBuilder.Entity<Link>();

        link.HasKey(x => x.Id);
        link.HasIndex(x => x.Code).IsUnique();
        link.Property(x => x.Code).HasMaxLength(20).IsRequired();
        link.Property(x => x.TargetUrl).HasMaxLength(2048).IsRequired();
        link.Property(x => x.ManagementKeyHash).HasMaxLength(64).IsRequired();
        link.Property(x => x.CreatedAtUtc).IsRequired();
        link.Property(x => x.AccessCount).HasDefaultValue(0L);
    }
}
