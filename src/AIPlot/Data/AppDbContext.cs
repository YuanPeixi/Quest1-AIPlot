using AIPlot.Models;
using Microsoft.EntityFrameworkCore;

namespace AIPlot.Data;

/// <summary>应用数据库上下文（SQLite）</summary>
public class AppDbContext : DbContext
{
    public AppDbContext(DbContextOptions<AppDbContext> options) : base(options) { }

    public DbSet<Story> Stories => Set<Story>();
    public DbSet<Character> Characters => Set<Character>();
    public DbSet<StoryMessage> Messages => Set<StoryMessage>();
    public DbSet<Checkpoint> Checkpoints => Set<Checkpoint>();
    public DbSet<WorldBook> WorldBooks => Set<WorldBook>();
    public DbSet<WorldEntry> WorldEntries => Set<WorldEntry>();

    protected override void OnModelCreating(ModelBuilder mb)
    {
        mb.Entity<Story>(e =>
        {
            e.HasKey(s => s.Id);
            e.Property(s => s.Title).IsRequired().HasMaxLength(200);
            e.HasMany(s => s.Characters).WithOne(c => c.Story)
                .HasForeignKey(c => c.StoryId).OnDelete(DeleteBehavior.Cascade);
            e.HasMany(s => s.Messages).WithOne(m => m.Story)
                .HasForeignKey(m => m.StoryId).OnDelete(DeleteBehavior.Cascade);
            e.HasMany(s => s.Checkpoints).WithOne(cp => cp.Story)
                .HasForeignKey(cp => cp.StoryId).OnDelete(DeleteBehavior.Cascade);
            e.HasOne(s => s.WorldBook).WithOne(wb => wb.Story)
                .HasForeignKey<WorldBook>(wb => wb.StoryId).OnDelete(DeleteBehavior.Cascade);
        });

        mb.Entity<Character>(e =>
        {
            e.HasKey(c => c.Id);
            e.Property(c => c.Name).IsRequired().HasMaxLength(100);
        });

        mb.Entity<StoryMessage>(e =>
        {
            e.HasKey(m => m.Id);
            e.HasIndex(m => new { m.StoryId, m.Sequence });
        });

        mb.Entity<WorldBook>(e =>
        {
            e.HasKey(wb => wb.Id);
            e.HasMany(wb => wb.Entries).WithOne(we => we.WorldBook)
                .HasForeignKey(we => we.WorldBookId).OnDelete(DeleteBehavior.Cascade);
        });
    }
}
