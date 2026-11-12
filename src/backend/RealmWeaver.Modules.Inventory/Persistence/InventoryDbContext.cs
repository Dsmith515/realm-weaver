using Microsoft.EntityFrameworkCore;

namespace RealmWeaver.Modules.Inventory.Persistence;

internal sealed class Player
{
    public const int MaxNameLength = 60;

    public Guid Id { get; set; }

    public required string Name { get; set; }

    public DateTimeOffset CreatedAt { get; set; }

    public List<InventoryItem> Items { get; set; } = [];
}

/// <summary>
/// A stack of one component owned by one player. <see cref="ComponentDefinitionId"/> is a plain id with no
/// foreign key: the component lives in the Catalog module's schema, which this module never touches.
/// </summary>
internal sealed class InventoryItem
{
    public Guid PlayerId { get; set; }

    public Guid ComponentDefinitionId { get; set; }

    public int Quantity { get; set; }

    public DateTimeOffset AcquiredAt { get; set; }

    /// <summary>Postgres <c>xmin</c>. Two rituals spending the last thread can't both succeed.</summary>
    public uint Version { get; set; }
}

internal sealed class InventoryDbContext(DbContextOptions<InventoryDbContext> options) : DbContext(options)
{
    public const string Schema = "inventory";

    public DbSet<Player> Players => Set<Player>();

    public DbSet<InventoryItem> Items => Set<InventoryItem>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.HasDefaultSchema(Schema);

        modelBuilder.Entity<Player>(player =>
        {
            player.ToTable("players");
            player.Property(p => p.Name).HasMaxLength(Player.MaxNameLength);
            player.HasIndex(p => p.Name);
            player.HasMany(p => p.Items).WithOne().HasForeignKey(i => i.PlayerId).OnDelete(DeleteBehavior.Cascade);
        });

        modelBuilder.Entity<InventoryItem>(item =>
        {
            item.ToTable("inventory_items", t => t.HasCheckConstraint("ck_inventory_items_quantity", "quantity >= 0"));
            item.HasKey(i => new { i.PlayerId, i.ComponentDefinitionId });
            item.HasIndex(i => i.ComponentDefinitionId);
            item.Property(i => i.Version).IsRowVersion();
        });
    }
}
