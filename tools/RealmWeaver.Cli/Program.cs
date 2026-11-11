using System.Globalization;
using RealmWeaver.Generation;
using RealmWeaver.Generation.Export;
using RealmWeaver.Generation.Model;

// Small developer tool: generate a realm and write SVG, prompt, Markdown and JSON files.
//   dotnet run --project tools/RealmWeaver.Cli -- --seed 42 --biome forest --size 40x30 --events 12 --mobs beast,undead
const string Usage = """
    Usage: realmweaver [options]
      --seed <n>          Seed (default: random)
      --biome <name>      plains|forest|desert|tundra|swamp|volcanic|mountains|coast (default: plains)
      --element <name>    none|fire|water|earth|air|frost|lightning|light|shadow (default: none)
      --size <W>x<H>      Map size, 15-100 each (default: random)
      --events <n>        Event count, 5-60 capped by size (default: scaled random)
      --mobs <a,b>        Mob tags, e.g. beast,undead (default: any)
      --difficulty <n>    Needle difficulty 1-10 (default: 1)
      --danger <n>        Extra danger, e.g. mismatched map (default: 0)
      --variant <n>       Encounter variant (default: 0)
      --out <dir>         Output directory (default: samples)
    """;

var options = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
for (var i = 0; i < args.Length; i++)
{
    if (args[i] is "-h" or "--help")
    {
        Console.WriteLine(Usage);
        return 0;
    }

    if (!args[i].StartsWith("--", StringComparison.Ordinal) || i + 1 >= args.Length)
    {
        Console.Error.WriteLine($"Unexpected argument '{args[i]}'.\n{Usage}");
        return 1;
    }

    options[args[i][2..]] = args[++i];
}

string? Opt(string name) => options.GetValueOrDefault(name);
int? IntOpt(string name) => Opt(name) is { } v ? int.Parse(v, CultureInfo.InvariantCulture) : null;

int? width = null;
int? height = null;
if (Opt("size") is { } size)
{
    var parts = size.Split('x', 'X');
    width = int.Parse(parts[0], CultureInfo.InvariantCulture);
    height = int.Parse(parts.Length > 1 ? parts[1] : parts[0], CultureInfo.InvariantCulture);
}

var seed = Opt("seed") is { } s ? ulong.Parse(s, CultureInfo.InvariantCulture) : (ulong)Random.Shared.NextInt64();
var request = new GenerationRequest
{
    Seed = seed,
    Biome = Enum.Parse<Biome>(Opt("biome") ?? "plains", ignoreCase: true),
    Element = Enum.Parse<Element>(Opt("element") ?? "none", ignoreCase: true),
    Width = width,
    Height = height,
    EventCount = IntOpt("events"),
    MobTags = Opt("mobs")?.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries) ?? [],
    Difficulty = IntOpt("difficulty") ?? 1,
    DangerModifier = IntOpt("danger") ?? 0,
    EncounterVariant = IntOpt("variant") ?? 0,
};

RealmDocument realm;
try
{
    realm = RealmGenerator.Generate(request);
}
catch (RealmGenerationException ex)
{
    Console.Error.WriteLine(ex.Message);
    return 2;
}

var outDir = Opt("out") ?? "samples";
Directory.CreateDirectory(outDir);
var name = Path.Combine(outDir, $"{request.Biome.ToString().ToLowerInvariant()}-{seed}");

File.WriteAllText(name + ".svg", SvgExporter.Render(realm));
File.WriteAllText(name + ".prompt.txt", PromptExporter.Build(realm));
File.WriteAllText(name + ".md", MarkdownExporter.Build(realm));
File.WriteAllText(name + ".json", RealmJson.Serialize(realm));

var area = realm.Areas[0];
Console.WriteLine($"Seed {seed}: {area.Name} {area.Width}x{area.Height}, {area.Events.Count} events, spawn {area.Spawn}");
Console.WriteLine($"Wrote {name}.svg / .prompt.txt / .md / .json");
return 0;
