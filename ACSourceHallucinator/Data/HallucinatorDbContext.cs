using ACSourceHallucinator.Data.Entities;
using ACSourceHallucinator.Models;
using Microsoft.EntityFrameworkCore;

namespace ACSourceHallucinator.Data;

public class HallucinatorDbContext : DbContext
{
    public DbSet<StageResult> StageResults { get; set; }
    public DbSet<LlmRequestLog> LlmRequestLogs { get; set; }
    public DbSet<LlmCacheEntry> LlmCacheEntries { get; set; }

    public HallucinatorDbContext(DbContextOptions<HallucinatorDbContext> options)
        : base(options)
    {
    }

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<StageResult>(entity =>
        {
            entity.HasKey(e => e.Id);
            entity.HasIndex(e => new { e.StageName, e.EntityType, e.FullyQualifiedName }).IsUnique();
            entity.HasIndex(e => e.FullyQualifiedName);
            entity.Property(e => e.GeneratedContent).HasColumnType("TEXT");
            entity.Property(e => e.LastFailureReason).HasColumnType("TEXT");
            entity.Property(e => e.EntityType).HasConversion<string>();
            entity.Property(e => e.Status).HasConversion<string>();
        });

        modelBuilder.Entity<LlmCacheEntry>(entity =>
        {
            entity.HasKey(e => e.Id);
            entity.HasIndex(e => e.CacheKey).IsUnique();
            entity.Property(e => e.PromptText).HasColumnType("TEXT");
            entity.Property(e => e.ResponseContent).HasColumnType("TEXT");
        });

        modelBuilder.Entity<LlmRequestLog>(entity =>
        {
            entity.HasKey(e => e.Id);
            entity.HasOne<StageResult>(e => e.StageResult)
                .WithMany(s => s.RequestLogs)
                .HasForeignKey(e => e.StageResultId)
                .OnDelete(DeleteBehavior.Cascade);
            entity.Property(e => e.Prompt).HasColumnType("TEXT");
            entity.Property(e => e.Response).HasColumnType("TEXT");
            entity.Property(e => e.FailureReason).HasColumnType("TEXT");
        });
    }
}
