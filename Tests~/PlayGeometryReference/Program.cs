using System.Globalization;
using System.Reflection;
using System.Security.Cryptography;
using System.Text.Json;
using MajdataPlay.Scenes.Game.Parsing;

if (args.Length != 1)
    throw new ArgumentException("Supply the output TSV path.");

var assembly = Assembly.GetExecutingAssembly();
var sourceRoot = assembly.GetCustomAttributes<AssemblyMetadataAttribute>()
    .Single(attribute => attribute.Key == "PlayParsingSource").Value!;
using var hashesStream = assembly.GetManifestResourceStream("PlayGeometryReference.SourceHashes.json")!;
var hashes = JsonSerializer.Deserialize<Dictionary<string, string>>(hashesStream)!;
foreach (var (relativePath, expectedHash) in hashes)
{
    var actual = Convert.ToHexString(SHA256.HashData(File.ReadAllBytes(Path.Combine(sourceRoot, relativePath))));
    if (!actual.Equals(expectedHash, StringComparison.OrdinalIgnoreCase))
        throw new InvalidOperationException($"Play source differs from the pinned snapshot: {relativePath}");
}

var codes = new SortedSet<string>(StringComparer.Ordinal);
for (var start = 1; start <= 8; start++)
for (var end = 1; end <= 8; end++)
{
    foreach (var middle in new[] { "", "C", $"B{start}", $"A{(start + 2) % 8 + 1}",
        "P0", "Q0", "P9", "Q9", "P99", "Q99" })
        codes.Add($"{start}{middle}K{end}");
    for (var orbit = 1; orbit <= 8; orbit++)
    foreach (var direction in new[] { 'P', 'Q' })
    foreach (var route in new[] { $"{orbit}", $"{orbit}{orbit}", $"{orbit}9", $"9{orbit}", $"{orbit}{orbit % 8 + 1}" })
        codes.Add($"{start}{direction}{route}K{end}");
}
foreach (var code in new[] { "1P6K7", "1P6P9K7", "1A3P9K5", "1CP0K5", "1P0Q0K5",
    "1P09K5", "1B3K5", "1B12CK5", "1P9A3P9K5" })
    codes.Add(code);

var rows = new List<string>
{
    "# Play acd295d3f878f53ecd412d150936a465ed2e3d28: SlideDataBuilder.BuildArrowData(SlideCodeParser.Parse(code)).Length - 2",
    "# Generated from the unmodified upstream implementation; positive arrow counts only."
};
var skipped = 0;
foreach (var code in codes)
{
    try
    {
        var count = SlideDataBuilder.BuildArrowData(SlideCodeParser.Parse(code)).Length - 2;
        if (count <= 0) { skipped++; continue; }
        rows.Add(code + "\t" + count.ToString(CultureInfo.InvariantCulture));
    }
    catch (ArgumentException) { skipped++; }
}
File.WriteAllText(args[0], string.Join("\n", rows) + "\n");
Console.WriteLine($"Wrote {rows.Count - 2} reference counts; skipped {skipped} invalid or non-positive paths.");
