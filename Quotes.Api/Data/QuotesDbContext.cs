using Microsoft.EntityFrameworkCore;
using Quotes.Api.Models;

namespace Quotes.Api.Data;

public class QuotesDbContext : DbContext
{
    public QuotesDbContext(DbContextOptions<QuotesDbContext> options) : base(options)
    {
    }

    public DbSet<Quote> Quotes => Set<Quote>();
    public DbSet<IdempotencyRecord> IdempotencyRecords => Set<IdempotencyRecord>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<Quote>(e =>
        {
            e.HasKey(x => x.Id);
            e.Property(x => x.DocumentId).IsRequired().HasMaxLength(50);
            e.Property(x => x.Amount).HasPrecision(18, 2);
            e.Property(x => x.Currency).IsRequired().HasMaxLength(3);
            e.Property(x => x.Status).HasMaxLength(20);
            e.Property(x => x.CreatedAt).IsRequired();

            // Index for SQL task later
            e.HasIndex(x => new { x.DocumentId, x.CreatedAt });
        });

        modelBuilder.Entity<IdempotencyRecord>(e =>
        {
            e.HasKey(x => x.Id);
            e.Property(x => x.Key).IsRequired().HasMaxLength(100);
            e.Property(x => x.RequestHash).IsRequired().HasMaxLength(64);
            e.Property(x => x.ResponseBody).IsRequired();
            e.Property(x => x.CreatedAt).IsRequired();

            e.HasIndex(x => x.Key).IsUnique();
        });
    }
}