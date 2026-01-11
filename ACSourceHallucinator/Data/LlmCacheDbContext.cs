using ACSourceHallucinator.Data.Entities;
using Microsoft.EntityFrameworkCore;

namespace ACSourceHallucinator.Data;

public class LlmCacheDbContext : DbContext
{
    public DbSet<LlmCacheEntry> LlmCacheEntries { get; set; }

    public LlmCacheDbContext(DbContextOptions<LlmCacheDbContext> options)
        : base(options)
    {
    }

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<LlmCacheEntry>(entity =>
        {
            entity.HasKey(e => e.Id);
            entity.HasIndex(e => e.CacheKey).IsUnique();
            entity.Property(e => e.PromptText).HasColumnType("TEXT");
            entity.Property(e => e.ResponseContent).HasColumnType("TEXT");
        });
    }
}
