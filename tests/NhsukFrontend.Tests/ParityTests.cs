using NhsukFrontend.Parity;
using Xunit;

namespace NhsukFrontend.Tests;

/// <summary>
/// Every upstream example, rendered through the Razor components, must produce the same HTML as the
/// Nunjucks macro. One test case per fixture, so a failure names the exact example.
/// </summary>
public sealed class ParityTests
{
    public static TheoryData<string, int, string> Fixtures()
    {
        var data = new TheoryData<string, int, string>();
        foreach (var set in FixtureStore.All)
            for (var i = 0; i < set.Fixtures.Count; i++)
                data.Add(set.Component, i, set.Fixtures[i].Name);
        return data;
    }

    [Theory]
    [MemberData(nameof(Fixtures))]
    public async Task Matches_upstream(string component, int index, string name)
    {
        var fixture = FixtureStore.Get(component)!.Fixtures[index];
        Assert.True(ComponentRegistry.ByName.TryGetValue(component, out var type), $"No .NET component for '{component}'.");

        await using var runner = new ParityRunner();
        var result = await runner.CheckAsync(component, type!, fixture);

        Assert.Null(result.Error);
        Assert.True(result.Passed, $"{component}: {name} differs from upstream:\n" + string.Join('\n',
            result.Diff.Where(d => d.Kind != DiffKind.Same)
                .Select(d => (d.Kind == DiffKind.Expected ? "- upstream " : "+ dotnet   ") + d.Token)));
    }

    public static TheoryData<string, int, string> TagHelperFixtures()
    {
        var data = new TheoryData<string, int, string>();
        foreach (var set in FixtureStore.All.Where(s => s.Component != "template"))
            for (var i = 0; i < set.Fixtures.Count; i++)
                data.Add(set.Component, i, set.Fixtures[i].Name);
        return data;
    }

    /// <summary>The same examples through the generated MVC tag helpers, as a Razor Page would use them.</summary>
    [Theory]
    [MemberData(nameof(TagHelperFixtures))]
    public async Task Tag_helper_matches_upstream(string component, int index, string name)
    {
        var fixture = FixtureStore.Get(component)!.Fixtures[index];
        Assert.True(ComponentRegistry.ByName.TryGetValue(component, out var type), $"No .NET component for '{component}'.");

        await using var runner = new ParityRunner();
        var result = await runner.CheckAsync(component, type!, fixture, throughTagHelper: true);

        Assert.Null(result.Error);
        Assert.True(result.Passed, $"{component}: {name} through the tag helper differs from upstream:\n" + string.Join('\n',
            result.Diff.Where(d => d.Kind != DiffKind.Same)
                .Select(d => (d.Kind == DiffKind.Expected ? "- upstream " : "+ dotnet   ") + d.Token)));
    }

    [Fact]
    public void Every_component_has_a_tag_helper()
    {
        // The page template is a layout; in MVC that is _Layout.cshtml (see the demo's Pages/Shared/_NhsukLayout.cshtml).
        foreach (var (name, type) in ComponentRegistry.ByName.Where(c => c.Key != "template"))
            Assert.True(TagHelperParity.TagHelperFor(type) is not null, $"No tag helper for '{name}'.");
    }

    [Fact]
    public void Every_ported_component_has_fixtures_or_is_tested_through_another()
    {
        foreach (var component in FixtureStore.Manifest.Ported)
            Assert.NotNull(FixtureStore.Get(component));
    }
}
