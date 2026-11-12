using Microsoft.EntityFrameworkCore;

using RealmWeaver.Infrastructure.Persistence;

namespace RealmWeaver.Modules.Catalog.Persistence;

/// <summary>Migrates the catalog schema and adds any seed component that is missing. Admin edits are never overwritten.</summary>
internal sealed class CatalogDatabaseInitializer(CatalogDbContext db, CatalogService catalog) : IModuleDatabaseInitializer
{
    public const int RunOrder = 100;

    public int Order => RunOrder;

    public async Task InitializeAsync(CancellationToken cancellationToken)
    {
        await db.Database.MigrateAsync(cancellationToken);

        var existing = await db.Components.Select(c => c.Id).ToListAsync(cancellationToken);
        foreach (var seed in CatalogSeedData.Components.Where(s => !existing.Contains(s.Id)))
        {
            await catalog.CreateAsync(seed.Input, seed.Id, cancellationToken);
        }

        if (!await db.Settings.AnyAsync(cancellationToken))
        {
            db.Settings.Add(new CatalogSettings { PlayersCanSelfAssign = CatalogSeedData.DefaultPlayersCanSelfAssign });
            await db.SaveChangesAsync(cancellationToken);
        }
    }
}
