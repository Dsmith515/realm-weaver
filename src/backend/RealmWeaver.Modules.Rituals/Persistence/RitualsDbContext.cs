using Microsoft.EntityFrameworkCore;

namespace RealmWeaver.Modules.Rituals.Persistence;

/// <summary>
/// A performed ritual. Rows only exist for rituals that committed: a failed ritual rolls back its row too.
/// The unique <see cref="IdempotencyKey"/> is inserted first in the transaction, so a concurrent retry with
/// the same key waits for this one and then replays its result. Component, map, realm and player ids are
/// plain ids into other modules.
/// </summary>
internal sealed class Ritual
{
    public const int MaxIdempotencyKeyLength = 100;

    public Guid Id { get; set; }

    public Guid PlayerId { get; set; }

    public required string IdempotencyKey { get; set; }

    /// <summary>SHA-256 of the request, so a key reused for a different request is rejected rather than replayed.</summary>
    public required string RequestHash { get; set; }

    public Guid NeedleId { get; set; }

    public Guid ThreadId { get; set; }

    public Guid CoatingId { get; set; }

    public Guid MapId { get; set; }

    public bool MapCreated { get; set; }

    public Guid RealmId { get; set; }

    public bool IsMismatch { get; set; }

    public int DangerModifier { get; set; }

    public int EncounterVariant { get; set; }

    public RitualStatus Status { get; set; }

    public DateTimeOffset CreatedAt { get; set; }
}

/// <summary>Phase 1 only ever stores Completed. The Phase 2 saga adds the in-between states.</summary>
internal enum RitualStatus
{
    Completed,
}

internal sealed class RitualsDbContext(DbContextOptions<RitualsDbContext> options) : DbContext(options)
{
    public const string Schema = "rituals";
    public const string IdempotencyKeyIndex = "ux_rituals_idempotency_key";

    public DbSet<Ritual> Rituals => Set<Ritual>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.HasDefaultSchema(Schema);

        modelBuilder.Entity<Ritual>(ritual =>
        {
            ritual.ToTable("rituals");
            ritual.Property(r => r.IdempotencyKey).HasMaxLength(Ritual.MaxIdempotencyKeyLength);
            ritual.Property(r => r.RequestHash).HasMaxLength(64).IsFixedLength();
            ritual.Property(r => r.Status).HasConversion<string>().HasMaxLength(16);
            ritual.HasIndex(r => r.IdempotencyKey).IsUnique().HasDatabaseName(IdempotencyKeyIndex);
            ritual.HasIndex(r => new { r.PlayerId, r.CreatedAt });
        });
    }
}
