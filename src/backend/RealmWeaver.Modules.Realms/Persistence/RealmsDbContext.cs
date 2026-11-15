using Microsoft.EntityFrameworkCore;

using RealmWeaver.Generation.Model;

namespace RealmWeaver.Modules.Realms.Persistence;

/// <summary>
/// A generated realm. <see cref="Document"/> is the full <see cref="RealmDocument"/> as jsonb; the other columns
/// are copies of a few of its values for listing without parsing. The document never changes; only the GM's
/// <see cref="PinnedObjective"/> and the run's completion are set later.
/// </summary>
internal sealed class Realm
{
    public Guid Id { get; set; }

    /// <summary>The player who performed the ritual, or null for a GM's Quick Weave. A plain id: players live in the Inventory module.</summary>
    public Guid? PlayerId { get; set; }

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

    /// <summary>An <see cref="Contracts.AttunedNeedleDto"/> as jsonb. Null when the weaver had none, or for a GM weave.</summary>
    public string? AttunedNeedle { get; set; }

    /// <summary>
    /// A GM's chosen objective for this run, as jsonb (a <see cref="RealmObjective"/>). Stored on the realm, not the
    /// map, so it never changes the map's recipe or recorded objectives.
    /// </summary>
    public string? PinnedObjective { get; set; }

    /// <summary>When the run was completed. Null while it's in progress. Set once.</summary>
    public DateTimeOffset? CompletedAt { get; set; }

    /// <summary>The <see cref="RealmObjective"/> that completed the run, as jsonb.</summary>
    public string? CompletedObjective { get; set; }

    /// <summary>True when completing the run recorded the map into the weaver's inventory.</summary>
    public bool MapRecorded { get; set; }
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
            realm.Property(r => r.PinnedObjective).HasColumnType("jsonb");
            realm.Property(r => r.AttunedNeedle).HasColumnType("jsonb");
            realm.Property(r => r.CompletedObjective).HasColumnType("jsonb");
            realm.Property(r => r.MapName).HasMaxLength(100);
            realm.Property(r => r.NeedleName).HasMaxLength(100);
            realm.Property(r => r.ThreadName).HasMaxLength(100);
            realm.Property(r => r.CoatingName).HasMaxLength(100);
            realm.HasIndex(r => new { r.PlayerId, r.CreatedAt });
            realm.HasIndex(r => r.MapComponentId);
        });
    }
}
