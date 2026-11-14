using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;

using Microsoft.EntityFrameworkCore;

using Npgsql;

using RealmWeaver.Generation;
using RealmWeaver.Generation.Model;
using RealmWeaver.Infrastructure.Persistence;
using RealmWeaver.Modules.Catalog.Contracts;
using RealmWeaver.Modules.Inventory.Contracts;
using RealmWeaver.Modules.Realms.Contracts;
using RealmWeaver.Modules.Rituals.Contracts;
using RealmWeaver.Modules.Rituals.Persistence;

namespace RealmWeaver.Modules.Rituals;

/// <summary>
/// Performs a ritual as one local ACID transaction (ADR 0005):
/// claim the idempotency key, spend the Thread and Coating, create the Map if new, grant it, and store the realm.
/// Any failure rolls all of it back.
/// </summary>
internal sealed class RitualService(
    RitualsDbContext db,
    UnitOfWork unitOfWork,
    RitualResolver resolver,
    ICatalogModule catalog,
    IInventoryModule inventory,
    IRealmsModule realms,
    TimeProvider clock)
{
    public async Task<RitualResultDto?> GetAsync(Guid ritualId, CancellationToken cancellationToken)
    {
        var ritual = await db.Rituals.AsNoTracking().FirstOrDefaultAsync(r => r.Id == ritualId, cancellationToken);
        return ritual is null ? null : ToResult(ritual);
    }

    public async Task<PerformOutcome> PerformAsync(RitualSelection selection, string idempotencyKey, CancellationToken cancellationToken)
    {
        var requestHash = Hash(selection);
        if (await ReplayAsync(idempotencyKey, requestHash, cancellationToken) is { } earlier)
        {
            return earlier;
        }

        var resolution = await resolver.ResolveAsync(selection, randomize: false, cancellationToken);
        if (resolution is null)
        {
            return new PerformOutcome.PlayerNotFound();
        }

        var preview = resolution.Preview;
        if (preview.Issues.Any(i => i.Code != RitualResolver.NotOwned))
        {
            return new PerformOutcome.Invalid(preview.Issues);
        }

        if (!preview.CanPerform)
        {
            return new PerformOutcome.NotOwned(preview.Issues);
        }

        // Everything up to here only read. Generation is pure, so it also runs before the transaction opens,
        // which keeps the row locks short.
        var needle = preview.Needle!.Component;
        var thread = preview.Thread!.Component;
        var coating = preview.Coating!.Component;
        var existingMap = preview.Map?.Component;
        var variant = RitualResolver.EncounterVariantFor(preview.IsMismatch, needle.Id, thread.Id, coating.Id);
        var request = BuildRequest(selection, resolution, existingMap?.Map, needle, thread, coating, preview.DangerModifier, variant);

        RealmDocument document;
        try
        {
            document = RealmGenerator.Generate(request);
        }
        catch (RealmGenerationException ex)
        {
            return new PerformOutcome.GenerationFailed(ex.Message);
        }

        var ritual = new Ritual
        {
            Id = Guid.CreateVersion7(),
            PlayerId = selection.PlayerId,
            IdempotencyKey = idempotencyKey,
            RequestHash = requestHash,
            NeedleId = needle.Id,
            ThreadId = thread.Id,
            CoatingId = coating.Id,
            MapId = existingMap?.Id ?? Guid.CreateVersion7(),
            MapCreated = existingMap is null,
            RealmId = Guid.CreateVersion7(),
            IsMismatch = preview.IsMismatch,
            DangerModifier = preview.DangerModifier,
            EncounterVariant = variant,
            Status = RitualStatus.Completed,
            CreatedAt = clock.GetDatabaseUtcNow(),
        };

        try
        {
            var result = await unitOfWork.ExecuteAsync(
                async ct =>
                {
                    // 1. Claim the key first. A concurrent request with the same key blocks here until we commit,
                    //    then fails on the unique index and replays our result instead of running twice.
                    db.Rituals.Add(ritual);
                    await db.SaveChangesAsync(ct);

                    // 2. Spend one Thread and one Coating, guarded by xmin.
                    await inventory.SpendOneEachAsync(selection.PlayerId, [thread.Id, coating.Id], ct);

                    // 3. Reuse the map, or record a new one in the catalog.
                    var map = existingMap ?? await catalog.CreateMapAsync(NewMapFor(ritual.MapId, document, needle, thread, coating, selection.PlayerId), ct);

                    // 4. The player keeps the map (a no-op when they already own it).
                    await inventory.GrantAsync(selection.PlayerId, map, 1, ct);

                    // 5. Store the realm snapshot.
                    await realms.SaveAsync(
                        new NewRealm(
                            ritual.RealmId,
                            selection.PlayerId,
                            map.Id,
                            ritual.DangerModifier,
                            ritual.EncounterVariant,
                            document,
                            new RealmComponentsDto(map.Name, new(needle.Id, needle.Name), new(thread.Id, thread.Name), new(coating.Id, coating.Name))),
                        ct);

                    return ToResult(ritual);
                },
                cancellationToken);

            return new PerformOutcome.Performed(result);
        }
        catch (DbUpdateException ex) when (IsIdempotencyKeyClash(ex))
        {
            return await ReplayAsync(idempotencyKey, requestHash, cancellationToken)
                ?? throw new InvalidOperationException("The idempotency key clashed but no ritual holds it.", ex);
        }
        catch (InsufficientComponentsException)
        {
            return new PerformOutcome.Conflict("Another ritual used these components first. Reload your inventory and try again.");
        }
        catch (DbUpdateConcurrencyException)
        {
            return new PerformOutcome.Conflict("Your inventory changed while the ritual ran. Reload it and try again.");
        }
    }

    private async Task<PerformOutcome?> ReplayAsync(string idempotencyKey, string requestHash, CancellationToken cancellationToken)
    {
        var earlier = await db.Rituals.AsNoTracking().FirstOrDefaultAsync(r => r.IdempotencyKey == idempotencyKey, cancellationToken);
        if (earlier is null)
        {
            return null;
        }

        return earlier.RequestHash == requestHash
            ? new PerformOutcome.Replayed(ToResult(earlier))
            : new PerformOutcome.KeyReused();
    }

    private static bool IsIdempotencyKeyClash(DbUpdateException ex) =>
        ex.InnerException is PostgresException { SqlState: PostgresErrorCodes.UniqueViolation } pg
        && pg.ConstraintName == RitualsDbContext.IdempotencyKeyIndex;

    private static GenerationRequest BuildRequest(
        RitualSelection selection,
        RitualResolution resolution,
        MapDetails? map,
        ComponentDto needle,
        ComponentDto thread,
        ComponentDto coating,
        int dangerModifier,
        int variant)
    {
        // Layout (shape, size, biome) comes from the map's recorded recipe (ADR 0006), so editing a component never
        // re-shapes an existing map. A matching ritual takes its encounter inputs from the recipe too and rebuilds the
        // realm exactly; a mismatch (a mirror) takes them from what's in the slots.
        var recipe = resolution.MapRecipe ?? (map is null ? null : LegacyRecipe(resolution));
        var useRecipeEncounters = recipe is not null && !resolution.Preview.IsMismatch;
        var kind = recipe?.Kind ?? coating.Coating!.RealmKind;

        return new GenerationRequest
        {
            Seed = map?.Seed ?? NewSeed(),
            Kind = kind,
            FloorCount = Math.Max(1, recipe?.FloorCount ?? coating.Coating!.FloorCount),
            Biome = recipe?.Biome ?? thread.Thread!.Biome,
            Element = useRecipeEncounters ? recipe!.Element : thread.Thread!.Element,
            MobTags = useRecipeEncounters ? recipe!.MobTags : coating.Coating!.MobTypes,
            Difficulty = useRecipeEncounters ? recipe!.Difficulty : needle.Needle!.DifficultyTier,
            DangerModifier = dangerModifier,
            EncounterVariant = variant,
            Width = map?.Width ?? selection.Width,
            Height = map?.Height ?? selection.Height,
            EventCount = map is null ? selection.EventCount : (kind == RealmKind.Dungeon ? null : map.EventCount),
        };
    }

    /// <summary>A map from before the recipe snapshot: fall back to its recorded components' current values.</summary>
    private static MapRecipe LegacyRecipe(RitualResolution resolution)
    {
        var thread = resolution.LayoutThread?.Thread ?? throw new InvalidOperationException("The map has no layout thread.");
        var coating = resolution.LayoutCoating?.Coating ?? throw new InvalidOperationException("The map has no layout coating.");
        var needle = resolution.Preview.Needle?.Component.Needle ?? throw new InvalidOperationException("The ritual has no needle.");
        return new MapRecipe(coating.RealmKind, coating.FloorCount, thread.Biome, thread.Element, coating.MobTypes, needle.DifficultyTier);
    }

    /// <summary>The recipe a new map records: exactly the inputs its realm was generated from.</summary>
    private static MapRecipe RecipeOf(GenerationRequest request) => new(
        request.Kind,
        request.Kind == RealmKind.Field ? 0 : request.FloorCount,
        request.Biome,
        request.Element,
        [.. request.MobTags],
        request.Difficulty);

    /// <summary>"Beginner's Reedwoven Beastward Wax Swamp Field and Dungeon Map": the components without their type word.</summary>
    internal static string MapNameFor(ComponentDto needle, ComponentDto thread, ComponentDto coating, Biome biome, RealmKind kind)
    {
        var kindWords = kind switch
        {
            RealmKind.Field => "Field",
            RealmKind.Dungeon => "Dungeon",
            _ => "Field and Dungeon",
        };

        return string.Join(
            ' ',
            new[] { Strip(needle, "Needle"), Strip(thread, "Thread"), Strip(coating, "Coating"), biome.ToString(), kindWords, "Map" }
                .Where(part => part.Length > 0));

        static string Strip(ComponentDto component, string typeWord)
        {
            var name = component.Name.Trim();
            return name.EndsWith(" " + typeWord, StringComparison.OrdinalIgnoreCase) ? name[..^(typeWord.Length + 1)].TrimEnd() : name;
        }
    }

    private static NewMap NewMapFor(Guid id, RealmDocument document, ComponentDto needle, ComponentDto thread, ComponentDto coating, Guid playerId)
    {
        var first = document.Areas[0];
        var seedTag = SeedTag(document.Seed);

        return new NewMap(
            id,
            MapNameFor(needle, thread, coating, document.Request.Biome, document.Request.Kind),
            $"Opened with {needle.Name}, {thread.Name} and {coating.Name}. Seed tag {seedTag}.",
            new MapDetails(
                document.Seed,
                needle.Id,
                thread.Id,
                coating.Id,
                first.Width,
                first.Height,
                Math.Clamp(first.Events.Count, GeneratorLimits.MinEvents, GeneratorLimits.AbsoluteMaxEvents),
                document.GeneratorVersion,
                playerId,
                RecipeOf(document.Request),
                document.Threat));
    }

    internal static string SeedTag(ulong seed) => seed.ToString("X16", CultureInfo.InvariantCulture)[..6];

    private static ulong NewSeed() => BitConverter.ToUInt64(RandomNumberGenerator.GetBytes(sizeof(ulong)));

    private static string Hash(RitualSelection selection) =>
        Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(JsonSerializer.Serialize(selection)))).ToLowerInvariant();

    private static RitualResultDto ToResult(Ritual r) => new(
        r.Id, r.PlayerId, r.RealmId, r.MapId, r.MapCreated, r.NeedleId, r.ThreadId, r.CoatingId,
        r.IsMismatch, r.DangerModifier, r.EncounterVariant, r.CreatedAt);
}

internal abstract record PerformOutcome
{
    public sealed record Performed(RitualResultDto Result) : PerformOutcome;

    public sealed record Replayed(RitualResultDto Result) : PerformOutcome;

    public sealed record PlayerNotFound : PerformOutcome;

    public sealed record Invalid(IReadOnlyList<RitualIssue> Issues) : PerformOutcome;

    public sealed record NotOwned(IReadOnlyList<RitualIssue> Issues) : PerformOutcome;

    public sealed record Conflict(string Detail) : PerformOutcome;

    public sealed record KeyReused : PerformOutcome;

    public sealed record GenerationFailed(string Detail) : PerformOutcome;
}
