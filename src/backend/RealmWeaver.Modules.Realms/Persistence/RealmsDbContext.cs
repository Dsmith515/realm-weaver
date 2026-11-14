using Microsoft.EntityFrameworkCore;

using RealmWeaver.Generation.Model;

namespace RealmWeaver.Modules.Realms.Persistence;

/// <summary>
/// A generated realm. <see cref="Document"/> is the full <see cref="RealmDocument"/> as jsonb; the other columns
/// are copies of a few of its values for listing without parsing. Realms are never updated.
/// </summary>
internal sealed class Realm
{
    public Guid Id { get; set; }

    /// <summary>The player who performed the ritual. A plain id: players live in the Inventory module.</summary>
    public Guid PlayerId { get; set; }

    /// <summary>The Map component this realm was opened from. A plain id: maps live in the Catalog module.</summary>
    public Guid MapComponentId { get; set; }

    public RealmKind Kind { get; set; }

    public Biome Biome { get; set; }

    public int Width { get; set; }

    public int Height { get; set; }

    public int FloorCount { get; set; }

    public int Threat { get; set; }

    public int DangerModifier { get; set; }

    public int EncounterVariant { get; set; }

    public ulong Seed { get; set; }

    public required string GeneratorVersion { get; set; }

    public required string Document { get; set; }

    /// <summary>The areas' ids, kinds and names, so listing realms doesn't parse whole documents.</summary>
    public required string Areas { get; set; }

    public DateTimeOffset CreatedAt { get; set; }

    // Copies of names at weaving time (ADR 0006). Null for realms stored before they were recorded.
    public string? MapName { get; set; }

    public Guid? NeedleId { get; set; }

    public string? NeedleName { get; set; }

    public Guid? ThreadId { get; set; }

    public string? ThreadName { get; set; }

    public Guid? CoatingId { get; set; }

    public string? CoatingName { get; set; }
}

internal sealed class RealmsDbContext(DbContextOptions<RealmsDbContext> options) : DbContext(options)
{
    public const string Schema = "realms";

    public DbSet<Realm> Realms => Set<Realm>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.HasDefaultSchema(Schema);

        modelBuilder.Entity<Realm>(realm =>
        {
            realm.ToTable("realms");
            realm.Property(r => r.Kind).HasConversion<string>().HasMaxLength(24);
            realm.Property(r => r.Biome).HasConversion<string>().HasMaxLength(24);
            realm.Property(r => r.GeneratorVersion).HasMaxLength(20);
            realm.Property(r => r.Document).HasColumnType("jsonb");
            realm.Property(r => r.Areas).HasColumnType("jsonb");
            realm.Property(r => r.MapName).HasMaxLength(100);
            realm.Property(r => r.NeedleName).HasMaxLength(100);
            realm.Property(r => r.ThreadName).HasMaxLength(100);
            realm.Property(r => r.CoatingName).HasMaxLength(100);
            realm.HasIndex(r => new { r.PlayerId, r.CreatedAt });
            realm.HasIndex(r => r.MapComponentId);
        });
    }
}
