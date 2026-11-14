using Microsoft.EntityFrameworkCore;

namespace RealmWeaver.Modules.Catalog.Persistence;

internal sealed class CatalogDbContext(DbContextOptions<CatalogDbContext> options) : DbContext(options)
{
    public const string Schema = "catalog";

    public DbSet<ComponentDefinition> Components => Set<ComponentDefinition>();

    public DbSet<CatalogSettings> Settings => Set<CatalogSettings>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.HasDefaultSchema(Schema);

        modelBuilder.Entity<ComponentDefinition>(component =>
        {
            component.ToTable("component_definitions", t =>
            {
                t.HasCheckConstraint("ck_component_definitions_rank", "rank >= 1");
                t.HasCheckConstraint(
                    "ck_component_definitions_one_details",
                    "(CASE WHEN needle IS NULL THEN 0 ELSE 1 END + CASE WHEN thread IS NULL THEN 0 ELSE 1 END"
                    + " + CASE WHEN coating IS NULL THEN 0 ELSE 1 END + CASE WHEN map IS NULL THEN 0 ELSE 1 END) = 1");
            });

            component.Property(c => c.Type).HasConversion<string>().HasMaxLength(16);
            component.Property(c => c.Rarity).HasConversion<string>().HasMaxLength(16);
            component.Property(c => c.Name).HasMaxLength(ComponentRules.MaxNameLength);
            component.Property(c => c.Description).HasMaxLength(ComponentRules.MaxDescriptionLength);
            component.Property(c => c.Version).IsRowVersion();

            component.HasIndex(c => c.Type);
            component.HasIndex(c => c.Name);

            component.OwnsOne(c => c.Needle, needle => needle.ToJson());
            component.OwnsOne(c => c.Thread, thread =>
            {
                thread.ToJson();
                thread.Property(t => t.Biome).HasConversion<string>();
                thread.Property(t => t.Element).HasConversion<string>();
            });
            component.OwnsOne(c => c.Coating, coating =>
            {
                coating.ToJson();
                coating.Property(c => c.RealmKind).HasConversion<string>();
            });
            component.OwnsOne(c => c.Map, map =>
            {
                map.ToJson();
                map.OwnsOne(m => m.Recipe, recipe =>
                {
                    recipe.Property(r => r.Kind).HasConversion<string>();
                    recipe.Property(r => r.Biome).HasConversion<string>();
                    recipe.Property(r => r.Element).HasConversion<string>();
                });
            });
        });

        modelBuilder.Entity<CatalogSettings>(settings =>
        {
            settings.ToTable("settings", t => t.HasCheckConstraint("ck_settings_singleton", $"id = {CatalogSettings.SingletonId}"));
            settings.Property(s => s.Id).ValueGeneratedNever();
            settings.Property(s => s.Version).IsRowVersion();
        });
    }
}
