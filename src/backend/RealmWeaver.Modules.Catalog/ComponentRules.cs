using System.Text.RegularExpressions;

using RealmWeaver.Generation.Model;
using RealmWeaver.Infrastructure.Http;
using RealmWeaver.Modules.Catalog.Contracts;
using RealmWeaver.Modules.Catalog.Persistence;

namespace RealmWeaver.Modules.Catalog;

/// <summary>Validation and mapping for component definitions.</summary>
internal static partial class ComponentRules
{
    public const int MaxNameLength = 100;
    public const int MaxDescriptionLength = 1000;
    public const int MinRank = 1;
    public const int MaxRank = 10;
    public const int MaxCategoryLength = 40;
    public const int MaxAbilities = 8;
    public const int MaxAbilityLength = 60;
    public const int MaxMobTypes = 6;
    public const int MaxGeneratorVersionLength = 20;

    [GeneratedRegex("^[a-z][a-z-]{1,29}$", RegexOptions.CultureInvariant)]
    private static partial Regex MobTypePattern();

    /// <summary>Checks everything that doesn't need the database. Map references are checked by the service.</summary>
    public static ValidationErrors Validate(ComponentInput input)
    {
        var errors = new ValidationErrors();

        errors.Require(Enum.IsDefined(input.Type), "type", "Unknown component type.");
        errors.Require(Enum.IsDefined(input.Rarity), "rarity", "Unknown rarity.");
        errors.Require(!string.IsNullOrWhiteSpace(input.Name), "name", "A name is required.");
        errors.Require((input.Name?.Trim().Length ?? 0) <= MaxNameLength, "name", $"Use at most {MaxNameLength} characters.");
        errors.Require((input.Description?.Length ?? 0) <= MaxDescriptionLength, "description", $"Use at most {MaxDescriptionLength} characters.");
        errors.Require(input.Rank is >= MinRank and <= MaxRank, "rank", $"Rank must be between {MinRank} and {MaxRank}.");

        RequireDetailsFor(errors, input.Type, ComponentType.Needle, input.Needle, "needle");
        RequireDetailsFor(errors, input.Type, ComponentType.Thread, input.Thread, "thread");
        RequireDetailsFor(errors, input.Type, ComponentType.Coating, input.Coating, "coating");
        RequireDetailsFor(errors, input.Type, ComponentType.Map, input.Map, "map");

        if (input.Type == ComponentType.Needle && input.Needle is { } needle)
        {
            errors.Require(
                needle.DifficultyTier is >= GeneratorLimits.MinDifficulty and <= GeneratorLimits.MaxDifficulty,
                "needle.difficultyTier",
                $"Difficulty tier must be between {GeneratorLimits.MinDifficulty} and {GeneratorLimits.MaxDifficulty}.");
            errors.Require(!string.IsNullOrWhiteSpace(needle.Category), "needle.category", "A category is required.");
            errors.Require((needle.Category?.Trim().Length ?? 0) <= MaxCategoryLength, "needle.category", $"Use at most {MaxCategoryLength} characters.");
            var abilities = needle.Abilities ?? [];
            errors.Require(abilities.Count <= MaxAbilities, "needle.abilities", $"Use at most {MaxAbilities} abilities.");
            errors.Require(
                abilities.All(a => !string.IsNullOrWhiteSpace(a) && a.Trim().Length <= MaxAbilityLength),
                "needle.abilities",
                $"Each ability needs 1 to {MaxAbilityLength} characters.");
        }

        if (input.Type == ComponentType.Thread && input.Thread is { } thread)
        {
            errors.Require(Enum.IsDefined(thread.Biome), "thread.biome", "Unknown biome.");
            errors.Require(Enum.IsDefined(thread.Element), "thread.element", "Unknown element.");
        }

        if (input.Type == ComponentType.Coating && input.Coating is { } coating)
        {
            var mobTypes = coating.MobTypes ?? [];
            errors.Require(mobTypes.Count is >= 1 and <= MaxMobTypes, "coating.mobTypes", $"Give 1 to {MaxMobTypes} mob types.");
            errors.Require(
                mobTypes.All(m => m is not null && MobTypePattern().IsMatch(m)),
                "coating.mobTypes",
                "Mob types are lowercase tags such as \"beast\" or \"undead\".");
            errors.Require(mobTypes.Distinct(StringComparer.Ordinal).Count() == mobTypes.Count, "coating.mobTypes", "Mob types must be distinct.");
            errors.Require(Enum.IsDefined(coating.RealmKind), "coating.realmKind", "Unknown realm kind.");
            if (coating.RealmKind == RealmKind.Field)
            {
                errors.Require(coating.FloorCount == 0, "coating.floorCount", "A field has no dungeon floors; use 0.");
            }
            else
            {
                errors.Require(
                    coating.FloorCount is >= 1 and <= GeneratorLimits.MaxFloors,
                    "coating.floorCount",
                    $"Dungeons have 1 to {GeneratorLimits.MaxFloors} floors.");
            }
        }

        if (input.Type == ComponentType.Map && input.Map is { } map)
        {
            errors.Require(map.Width is >= GeneratorLimits.MinSize and <= GeneratorLimits.MaxSize, "map.width", $"Width must be between {GeneratorLimits.MinSize} and {GeneratorLimits.MaxSize}.");
            errors.Require(map.Height is >= GeneratorLimits.MinSize and <= GeneratorLimits.MaxSize, "map.height", $"Height must be between {GeneratorLimits.MinSize} and {GeneratorLimits.MaxSize}.");
            errors.Require(
                map.EventCount is >= GeneratorLimits.MinEvents and <= GeneratorLimits.AbsoluteMaxEvents,
                "map.eventCount",
                $"Event count must be between {GeneratorLimits.MinEvents} and {GeneratorLimits.AbsoluteMaxEvents}.");
            errors.Require(
                !string.IsNullOrWhiteSpace(map.GeneratorVersion) && map.GeneratorVersion.Length <= MaxGeneratorVersionLength,
                "map.generatorVersion",
                $"A generator version of at most {MaxGeneratorVersionLength} characters is required.");
        }

        return errors;
    }

    private static void RequireDetailsFor<T>(ValidationErrors errors, ComponentType actual, ComponentType expected, T? details, string field)
        where T : class
    {
        if (actual == expected)
        {
            errors.Require(details is not null, field, $"A {expected} needs '{field}' details.");
        }
        else if (details is not null)
        {
            errors.Add(field, $"Only a {expected} has '{field}' details.");
        }
    }

    /// <summary>Copies a validated input onto an entity. The type is set on creation only.</summary>
    public static void Apply(ComponentInput input, ComponentDefinition entity)
    {
        entity.Name = input.Name.Trim();
        entity.Rarity = input.Rarity;
        entity.Rank = input.Rank;
        entity.Description = input.Description?.Trim() ?? string.Empty;
        entity.IsPlayerAssignable = input.IsPlayerAssignable;
        entity.IsConsumable = ComponentDefinition.IsConsumableType(entity.Type);

        entity.Needle = input.Needle is { } n
            ? new NeedleData { DifficultyTier = n.DifficultyTier, Category = n.Category.Trim(), Abilities = [.. (n.Abilities ?? []).Select(a => a.Trim())] }
            : null;
        entity.Thread = input.Thread is { } t ? new ThreadData { Biome = t.Biome, Element = t.Element } : null;
        entity.Coating = input.Coating is { } c
            ? new CoatingData { MobTypes = [.. c.MobTypes], RealmKind = c.RealmKind, FloorCount = c.FloorCount }
            : null;
        entity.Map = input.Map is { } m
            ? new MapData
            {
                Seed = m.Seed,
                NeedleId = m.NeedleId,
                ThreadId = m.ThreadId,
                CoatingId = m.CoatingId,
                Width = m.Width,
                Height = m.Height,
                EventCount = m.EventCount,
                GeneratorVersion = m.GeneratorVersion.Trim(),
                CreatedByPlayerId = m.CreatedByPlayerId,
            }
            : null;
    }

    public static ComponentDto ToDto(this ComponentDefinition c) => new(
        c.Id,
        c.Type,
        c.Name,
        c.Rarity,
        c.Rank,
        c.Description,
        c.IsPlayerAssignable,
        c.IsConsumable,
        c.IsArchived,
        c.CreatedAt,
        c.Needle is { } n ? new NeedleDetails(n.DifficultyTier, n.Category, n.Abilities) : null,
        c.Thread is { } t ? new ThreadDetails(t.Biome, t.Element) : null,
        c.Coating is { } co ? new CoatingDetails(co.MobTypes, co.RealmKind, co.FloorCount) : null,
        c.Map is { } m
            ? new MapDetails(m.Seed, m.NeedleId, m.ThreadId, m.CoatingId, m.Width, m.Height, m.EventCount, m.GeneratorVersion, m.CreatedByPlayerId)
            : null);
}
