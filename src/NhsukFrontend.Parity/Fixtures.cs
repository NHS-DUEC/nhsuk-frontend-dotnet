using System.Reflection;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace NhsukFrontend.Parity;

/// <summary>One upstream example: the options passed to the Nunjucks macro and the HTML it rendered.</summary>
public sealed class Fixture
{
    [JsonPropertyName("name")] public string Name { get; init; } = "";
    [JsonPropertyName("description")] public string? Description { get; init; }
    [JsonPropertyName("context")] public JsonElement Context { get; init; }
    [JsonPropertyName("callBlock")] public string? CallBlock { get; init; }

    /// <summary>Page template only: HTML for each overridden <c>{% block %}</c>.</summary>
    [JsonPropertyName("blocks")] public Dictionary<string, string>? Blocks { get; init; }
    [JsonPropertyName("html")] public string Html { get; init; } = "";
    [JsonPropertyName("options")] public FixtureOptions? Options { get; init; }

    /// <summary>Upstream marks some fixtures as test-only (not shown in its review app).</summary>
    public bool Hidden => Options?.Hidden == true;
}

public sealed class FixtureOptions
{
    [JsonPropertyName("hidden")] public bool Hidden { get; init; }
}

public sealed class FixtureSet
{
    [JsonPropertyName("name")] public string Name { get; init; } = "";
    [JsonPropertyName("component")] public string Component { get; init; } = "";
    [JsonPropertyName("fixtures")] public List<Fixture> Fixtures { get; init; } = [];
}

public sealed class FixtureManifest
{
    [JsonPropertyName("upstreamVersion")] public string UpstreamVersion { get; init; } = "";
    [JsonPropertyName("ported")] public List<string> Ported { get; init; } = [];
    [JsonPropertyName("notYetPorted")] public List<string> NotYetPorted { get; init; } = [];
}

public static class FixtureStore
{
    private static readonly Assembly Assembly = typeof(FixtureStore).Assembly;
    private static readonly Lazy<FixtureManifest> LazyManifest = new(() => Read<FixtureManifest>("_manifest"));
    private static readonly Lazy<IReadOnlyList<FixtureSet>> LazySets = new(() =>
        LazyManifest.Value.Ported.Select(Read<FixtureSet>).ToList());

    public static FixtureManifest Manifest => LazyManifest.Value;
    public static IReadOnlyList<FixtureSet> All => LazySets.Value;
    public static FixtureSet? Get(string component) => All.FirstOrDefault(s => s.Component == component);

    private static T Read<T>(string name)
    {
        using var stream = Assembly.GetManifestResourceStream($"Fixtures/{name}.json")
            ?? throw new InvalidOperationException($"Missing embedded fixture file {name}.json. Run `npm run sync` in /upstream.");
        return JsonSerializer.Deserialize<T>(stream)!;
    }
}
