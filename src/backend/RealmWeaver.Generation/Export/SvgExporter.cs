using System.Globalization;
using System.Text;
using RealmWeaver.Generation.Content;
using RealmWeaver.Generation.Grid;
using RealmWeaver.Generation.Model;

namespace RealmWeaver.Generation.Export;

/// <summary>Renders one area of a realm as a bird's-eye SVG with grid labels, events and a legend.</summary>
public static class SvgExporter
{
    private const int Tile = 20;
    private const int LeftGutter = 32;
    private const int TopGutter = 22;
    private const int LegendLine = 16;

    public static string Render(RealmDocument realm, string? areaId = null)
    {
        ArgumentNullException.ThrowIfNull(realm);
        var area = areaId is null ? realm.Areas[0] : realm.Areas.Single(a => a.Id == areaId);

        var mapWidth = area.Width * Tile;
        var mapHeight = area.Height * Tile;
        var legendTop = TopGutter + mapHeight + 24;
        var terrains = area.TerrainRows.SelectMany(r => r).Distinct().Select(TerrainInfo.FromCode).OrderBy(t => t.Terrain).ToList();
        var materials = area.Rooms.Select(r => r.Material).Distinct().Order().Select(DungeonMaterials.Of).ToList();
        var legendLines = 2 + terrains.Count + materials.Count + area.Events.Count + area.Features.Count + 4;
        var totalWidth = LeftGutter + mapWidth + 16;
        var totalHeight = legendTop + (legendLines * LegendLine) + 16;

        var svg = new StringBuilder();
        Append(svg, $"<svg xmlns=\"http://www.w3.org/2000/svg\" width=\"{totalWidth}\" height=\"{totalHeight}\" viewBox=\"0 0 {totalWidth} {totalHeight}\" font-family=\"Segoe UI, Roboto, sans-serif\">");
        Append(svg, $"<title>{Escape(area.Name)} - seed {realm.Seed}</title>");
        Append(svg, $"<rect width=\"{totalWidth}\" height=\"{totalHeight}\" fill=\"#fbf8f1\"/>");

        // Terrain
        Append(svg, $"<g id=\"terrain\" transform=\"translate({LeftGutter},{TopGutter})\" shape-rendering=\"crispEdges\">");
        for (var y = 0; y < area.Height; y++)
        {
            var row = area.TerrainRows[y];
            var x = 0;
            while (x < area.Width)
            {
                // Merge runs of the same terrain into one rect to keep the file small.
                var start = x;
                while (x < area.Width && row[x] == row[start])
                {
                    x++;
                }

                var color = TerrainInfo.FromCode(row[start]).Color;
                Append(svg, $"<rect x=\"{start * Tile}\" y=\"{y * Tile}\" width=\"{(x - start) * Tile}\" height=\"{Tile}\" fill=\"{color}\"/>");
            }
        }

        // Grid lines
        AppendRaw(svg, "<g stroke=\"#000\" stroke-opacity=\"0.15\" stroke-width=\"1\">");
        for (var x = 0; x <= area.Width; x++)
        {
            Append(svg, $"<line x1=\"{x * Tile}\" y1=\"0\" x2=\"{x * Tile}\" y2=\"{mapHeight}\"/>");
        }

        for (var y = 0; y <= area.Height; y++)
        {
            Append(svg, $"<line x1=\"0\" y1=\"{y * Tile}\" x2=\"{mapWidth}\" y2=\"{y * Tile}\"/>");
        }

        AppendRaw(svg, "</g></g>");

        // Coordinate labels
        AppendRaw(svg, "<g id=\"labels\" font-size=\"9\" fill=\"#444\" text-anchor=\"middle\">");
        for (var x = 0; x < area.Width; x++)
        {
            Append(svg, $"<text x=\"{LeftGutter + (x * Tile) + (Tile / 2)}\" y=\"{TopGutter - 7}\">{TileLabels.Column(x)}</text>");
        }

        for (var y = 0; y < area.Height; y++)
        {
            Append(svg, $"<text x=\"{LeftGutter / 2}\" y=\"{TopGutter + (y * Tile) + 13}\">{TileLabels.Row(y)}</text>");
        }

        AppendRaw(svg, "</g>");

        // Dungeon rooms: outlined in their material's color; transition rooms are dashed.
        if (area.Rooms.Count > 0)
        {
            Append(svg, $"<g id=\"rooms\" transform=\"translate({LeftGutter},{TopGutter})\" fill=\"none\">");
            foreach (var room in area.Rooms)
            {
                var b = room.Bounds;
                var color = DungeonMaterials.Of(room.Material).Color;
                var dash = room.IsTransition ? " stroke-dasharray=\"5 3\"" : "";
                var kind = room.IsTransition ? $" (transition from {DungeonMaterials.Of(room.TransitionFrom!.Value).Name})" : "";
                Append(svg, $"<g data-room-id=\"{room.Id}\" data-material=\"{room.Material}\"><title>{room.Id} {Escape(DungeonMaterials.Of(room.Material).Name)}{Escape(kind)}</title>");
                Append(svg, $"<rect x=\"{b.X * Tile}\" y=\"{b.Y * Tile}\" width=\"{b.Width * Tile}\" height=\"{b.Height * Tile}\" stroke=\"{color}\" stroke-width=\"2\"{dash}/>");
                Append(svg, $"<text x=\"{(b.X * Tile) + 2}\" y=\"{(b.Y * Tile) + 9}\" font-size=\"8\" fill=\"#fff\" stroke=\"#000\" stroke-width=\"0.3\">{room.Id}</text></g>");
            }

            AppendRaw(svg, "</g>");
        }

        // Events
        Append(svg, $"<g id=\"events\" transform=\"translate({LeftGutter},{TopGutter})\">");
        foreach (var e in area.Events)
        {
            var r = e.Footprint;
            var color = CategoryColor(e.Category);
            Append(svg, $"<g data-event-id=\"{e.Id}\" data-template=\"{Escape(e.TemplateId)}\">");
            Append(svg, $"<title>{e.Id} {Escape(e.Name)} ({e.Location})</title>");
            Append(svg, $"<rect x=\"{(r.X * Tile) + 1}\" y=\"{(r.Y * Tile) + 1}\" width=\"{(r.Width * Tile) - 2}\" height=\"{(r.Height * Tile) - 2}\" rx=\"3\" fill=\"{color}\" fill-opacity=\"0.35\" stroke=\"{color}\" stroke-width=\"2\"/>");
            Append(svg, $"<text x=\"{(r.X * Tile) + (r.Width * Tile / 2)}\" y=\"{(r.Y * Tile) + (r.Height * Tile / 2) + 4}\" font-size=\"10\" font-weight=\"700\" fill=\"#111\" text-anchor=\"middle\">{e.Id}</text>");
            AppendRaw(svg, "</g>");
        }

        if (area.Spawn is { } spawn)
        {
            var cx = (spawn.X * Tile) + (Tile / 2);
            var cy = (spawn.Y * Tile) + (Tile / 2);
            Append(svg, $"<g id=\"spawn\"><title>Spawn ({spawn})</title><circle cx=\"{cx}\" cy=\"{cy}\" r=\"8\" fill=\"#ffffff\" stroke=\"#111\" stroke-width=\"2\"/><text x=\"{cx}\" y=\"{cy + 4}\" font-size=\"10\" font-weight=\"700\" text-anchor=\"middle\">S</text></g>");
        }

        foreach (var f in area.Features)
        {
            var (symbol, color) = FeatureMarker(f.Kind);
            var fx = (f.Position.X * Tile) + (Tile / 2);
            var fy = (f.Position.Y * Tile) + (Tile / 2);
            Append(svg, $"<g data-feature-id=\"{f.Id}\" data-kind=\"{f.Kind}\"><title>{f.Id} {Escape(f.Description)} ({f.Location})</title><rect x=\"{fx - 7}\" y=\"{fy - 7}\" width=\"14\" height=\"14\" rx=\"2\" fill=\"{color}\" stroke=\"#111\"/><text x=\"{fx}\" y=\"{fy + 4}\" font-size=\"10\" font-weight=\"700\" fill=\"#fff\" text-anchor=\"middle\">{Escape(symbol)}</text></g>");
        }

        AppendRaw(svg, "</g>");

        // Legend
        var line = legendTop;
        AppendRaw(svg, "<g id=\"legend\" font-size=\"12\" fill=\"#222\">");
        Append(svg, $"<text x=\"{LeftGutter}\" y=\"{line}\" font-weight=\"700\">{Escape(area.Name)} - {area.Width}x{area.Height} - seed {realm.Seed} - threat {realm.Threat}</text>");
        line += LegendLine * 2;

        foreach (var t in terrains)
        {
            Append(svg, $"<rect x=\"{LeftGutter}\" y=\"{line - 10}\" width=\"12\" height=\"12\" fill=\"{t.Color}\" stroke=\"#0003\"/><text x=\"{LeftGutter + 18}\" y=\"{line}\">{Escape(t.Name)}{(t.Passable ? "" : " (impassable)")}</text>");
            line += LegendLine;
        }

        foreach (var m in materials)
        {
            Append(svg, $"<rect x=\"{LeftGutter}\" y=\"{line - 10}\" width=\"12\" height=\"12\" fill=\"none\" stroke=\"{m.Color}\" stroke-width=\"2\"/><text x=\"{LeftGutter + 18}\" y=\"{line}\">{Escape(m.Name)} rooms ({m.Style}){(materials.Count > 1 ? "; dashed outline = transition" : "")}</text>");
            line += LegendLine;
        }

        if (area.Spawn is { } s)
        {
            Append(svg, $"<text x=\"{LeftGutter}\" y=\"{line}\">S  Spawn point: {s}</text>");
            line += LegendLine;
        }

        foreach (var e in area.Events)
        {
            var mobs = e.Mobs.Count == 0 ? "" : " - " + string.Join(", ", e.Mobs.Select(m => $"{m.Count}x {m.Name}"));
            Append(svg, $"<text x=\"{LeftGutter}\" y=\"{line}\"><tspan font-weight=\"700\" fill=\"{CategoryColor(e.Category)}\">{e.Id}</tspan> {Escape(e.Name)} ({e.Location}){Escape(mobs)}</text>");
            line += LegendLine;
        }

        foreach (var f in area.Features)
        {
            Append(svg, $"<text x=\"{LeftGutter}\" y=\"{line}\">{f.Id} {f.Kind} ({f.Location}) {Escape(f.Description)}</text>");
            line += LegendLine;
        }

        AppendRaw(svg, "</g></svg>");
        return svg.ToString();
    }

    public static string CategoryColor(EventCategory category) => category switch
    {
        EventCategory.Encounter => "#c0392b",
        EventCategory.Settlement => "#8e44ad",
        EventCategory.Social => "#2471a3",
        EventCategory.Landmark => "#b7950b",
        EventCategory.Hazard => "#d35400",
        EventCategory.Structure => "#34495e",
        _ => "#333333",
    };

    public static (string Symbol, string Color) FeatureMarker(FeatureKind kind) => kind switch
    {
        FeatureKind.Trap => ("!", "#c0392b"),
        FeatureKind.Chest => ("$", "#b7950b"),
        FeatureKind.SecretDoor => ("?", "#6c3483"),
        FeatureKind.StairsDown => (">", "#1f618d"),
        FeatureKind.StairsUp => ("<", "#1f618d"),
        FeatureKind.DungeonEntrance => ("@", "#117a65"),
        _ => ("*", "#555555"),
    };

    private static void Append(StringBuilder sb, FormattableString text) => AppendRaw(sb, text.ToString(CultureInfo.InvariantCulture));

    private static void AppendRaw(StringBuilder sb, string text) => sb.Append(text).Append('\n');

    private static string Escape(string text) => text
        .Replace("&", "&amp;", StringComparison.Ordinal)
        .Replace("<", "&lt;", StringComparison.Ordinal)
        .Replace(">", "&gt;", StringComparison.Ordinal)
        .Replace("\"", "&quot;", StringComparison.Ordinal);
}
