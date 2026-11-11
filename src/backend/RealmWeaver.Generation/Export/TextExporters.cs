using System.Globalization;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using RealmWeaver.Generation.Grid;
using RealmWeaver.Generation.Model;

namespace RealmWeaver.Generation.Export;

/// <summary>Builds a text prompt for an image model to paint a top-down battle map of one area.</summary>
public static class PromptExporter
{
    public static string Build(RealmDocument realm, string? areaId = null)
    {
        ArgumentNullException.ThrowIfNull(realm);
        var area = areaId is null ? realm.Areas[0] : realm.Areas.Single(a => a.Id == areaId);
        var request = realm.Request;

        var sb = new StringBuilder();
        Line(sb, $"Top-down orthographic battle map for a grid-based tactics RPG.");
        Line(sb, $"Square grid of {area.Width} columns by {area.Height} rows, one square per tile, viewed straight down with no perspective.");
        var element = request.Element == Element.None ? "" : $" with a strong {request.Element.ToString().ToLowerInvariant()} elemental influence";
        if (area.Kind == AreaKind.Field)
        {
            Line(sb, $"Setting: {request.Biome.ToString().ToLowerInvariant()} wilderness{element}.");
        }
        else
        {
            var host = realm.Theme?.HostName.ToLowerInvariant() ?? "ruin";
            Line(sb, $"Setting: dungeon floor {area.FloorNumber} beneath a {host} in a {request.Biome.ToString().ToLowerInvariant()} region{element}. Solid rock fills everything outside the rooms and corridors.");
            Line(sb, $"Construction:");
            foreach (var group in area.Rooms.GroupBy(r => r.Material).OrderByDescending(g => g.Count()))
            {
                var info = DungeonMaterials.Of(group.Key);
                var share = group.Count() * 100.0 / area.Rooms.Count;
                Line(sb, $"- {info.Name.ToLowerInvariant()} ({info.Style.ToString().ToLowerInvariant()}): about {Math.Round(share):0}% of rooms, e.g. {string.Join(", ", group.Take(3).Select(r => r.Location))}");
            }

            foreach (var t in area.Rooms.Where(r => r.IsTransition))
            {
                Line(sb, $"- at {t.Location} the {DungeonMaterials.Of(t.TransitionFrom!.Value).Name.ToLowerInvariant()} gives way to {DungeonMaterials.Of(t.Material).Name.ToLowerInvariant()}");
            }
        }

        sb.AppendLine();

        Line(sb, $"Terrain mix:");
        var total = area.Width * area.Height;
        var mix = area.TerrainRows.SelectMany(r => r)
            .GroupBy(c => c)
            .Select(g => (Info: TerrainInfo.FromCode(g.Key), Share: g.Count() * 100.0 / total))
            .Where(x => x.Share >= 2)
            .OrderByDescending(x => x.Share);
        foreach (var (info, share) in mix)
        {
            Line(sb, $"- {info.Name.ToLowerInvariant()}: about {Math.Round(share):0}%");
        }

        sb.AppendLine();
        Line(sb, $"Key locations (columns A-{TileLabels.Column(area.Width - 1)} left to right, rows 1-{area.Height} top to bottom):");
        if (area.Spawn is { } spawn)
        {
            Line(sb, $"- Party starting point at {spawn}");
        }

        foreach (var e in area.Events)
        {
            Line(sb, $"- {e.Name.ToLowerInvariant()} covering {e.Location}");
        }

        // Traps and secret doors are for the game master only, so they are left out of the art prompt.
        foreach (var f in area.Features.Where(f => f.Kind is not (FeatureKind.Trap or FeatureKind.SecretDoor)))
        {
            Line(sb, $"- {f.Description.ToLowerInvariant()} at {f.Location}");
        }

        sb.AppendLine();
        Line(sb, $"Style: hand-painted fantasy cartography, crisp readable tiles, even lighting, muted natural colors, faint grid lines.");
        Line(sb, $"Do not draw text, letters, numbers, creatures or a UI. Keep every location at the listed grid squares.");
        return sb.ToString();
    }

    private static void Line(StringBuilder sb, FormattableString text) => sb.AppendLine(text.ToString(CultureInfo.InvariantCulture));
}

/// <summary>Markdown placement sheet: where every event, mob and feature goes.</summary>
public static class MarkdownExporter
{
    public static string Build(RealmDocument realm)
    {
        ArgumentNullException.ThrowIfNull(realm);
        var sb = new StringBuilder();
        Line(sb, $"# Realm {realm.Seed}");
        Line(sb, $"");
        Line(sb, $"- Kind: {realm.Request.Kind}; biome: {realm.Request.Biome}; element: {realm.Request.Element}");
        Line(sb, $"- Threat: {realm.Threat} (difficulty {realm.Request.Difficulty} + danger {realm.Request.DangerModifier})");
        Line(sb, $"- Generator version: {realm.GeneratorVersion}");
        if (realm.Theme is { } theme)
        {
            var branches = theme.BranchMaterials.Count == 0 ? "none" : string.Join(", ", theme.BranchMaterials.Select(m => DungeonMaterials.Of(m).Name));
            Line(sb, $"- Dungeon host: {theme.HostName}; primary material: {DungeonMaterials.Of(theme.PrimaryMaterial).Name}; possible branches: {branches}");
        }

        foreach (var area in realm.Areas)
        {
            Line(sb, $"");
            Line(sb, $"## {area.Name} ({area.Width}x{area.Height})");
            if (area.Spawn is { } spawn)
            {
                Line(sb, $"");
                Line(sb, $"Spawn point: **{spawn}**");
            }

            Line(sb, $"");
            Line(sb, $"| Id | Event | Category | Location | Rooms | Mobs | Templates |");
            Line(sb, $"|---|---|---|---|---|---|---|");
            foreach (var e in area.Events)
            {
                var mobs = e.Mobs.Count == 0 ? "-" : string.Join(", ", e.Mobs.Select(m => $"{m.Count}x {m.Name} ({m.Role})"));
                var templates = e.DifficultyTemplateIds.Count == 0 ? "-" : string.Join(", ", e.DifficultyTemplateIds);
                var rooms = e.RoomIds.Count == 0 ? "-" : string.Join(", ", e.RoomIds);
                Line(sb, $"| {e.Id} | {e.Name} | {e.Category} | {e.Location} | {rooms} | {mobs} | {templates} |");
            }

            if (area.Rooms.Count > 0)
            {
                Line(sb, $"");
                Line(sb, $"| Room | Location | Material | Connects to | Notes |");
                Line(sb, $"|---|---|---|---|---|");
                foreach (var r in area.Rooms)
                {
                    var notes = new List<string>();
                    if (r.IsEntrance) notes.Add("entrance");
                    if (r.HasStairsDown) notes.Add("stairs down");
                    if (r.IsBossRoom) notes.Add("boss room");
                    if (r.IsSecret) notes.Add("secret room");
                    if (r.IsTransition) notes.Add($"transition from {DungeonMaterials.Of(r.TransitionFrom!.Value).Name}");
                    Line(sb, $"| {r.Id} | {r.Location} | {DungeonMaterials.Of(r.Material).Name} | {string.Join(", ", r.Connections)} | {(notes.Count == 0 ? "-" : string.Join("; ", notes))} |");
                }
            }

            if (area.Features.Count > 0)
            {
                Line(sb, $"");
                Line(sb, $"| Id | Feature | Location | Notes |");
                Line(sb, $"|---|---|---|---|");
                foreach (var f in area.Features)
                {
                    Line(sb, $"| {f.Id} | {f.Kind} | {f.Location} | {f.Description} |");
                }
            }
        }

        return sb.ToString();
    }

    private static void Line(StringBuilder sb, FormattableString text) => sb.AppendLine(text.ToString(CultureInfo.InvariantCulture));
}

/// <summary>JSON settings shared by the API and exports: camelCase, enums as strings.</summary>
public static class RealmJson
{
    public static JsonSerializerOptions Options { get; } = new(JsonSerializerDefaults.Web)
    {
        WriteIndented = true,
        Converters = { new JsonStringEnumConverter(JsonNamingPolicy.CamelCase) },
    };

    public static string Serialize(RealmDocument realm) => JsonSerializer.Serialize(realm, Options);

    public static RealmDocument Deserialize(string json) =>
        JsonSerializer.Deserialize<RealmDocument>(json, Options) ?? throw new JsonException("Empty realm document.");
}
