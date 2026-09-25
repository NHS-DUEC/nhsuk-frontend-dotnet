using System.Reflection;
using System.Text.Json;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Web;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using NhsukFrontend.Components;
using NhsukFrontend.Components.Infrastructure;

namespace NhsukFrontend.Parity;

public sealed record ParityResult(
    string Component,
    Fixture Fixture,
    bool Passed,
    string? ActualHtml,
    IReadOnlyList<DiffLine> Diff,
    string? Error)
{
    public static ParityResult Failed(string component, Fixture fixture, string error) =>
        new(component, fixture, false, null, [], error);
}

public sealed record ComponentParity(string Component, string DisplayName, Type? ComponentType, IReadOnlyList<ParityResult> Results)
{
    public int PassedCount => Results.Count(r => r.Passed);
    public bool AllPassed => ComponentType is not null && Results.All(r => r.Passed);
}

public static class ComponentRegistry
{
    /// <summary>Every ported component, keyed by upstream name (for example <c>error-message</c>).</summary>
    public static readonly IReadOnlyDictionary<string, Type> ByName = typeof(NhsukButton).Assembly
        .GetTypes()
        .Select(t => (t, attr: t.GetCustomAttribute<MacroComponentAttribute>()))
        .Where(x => x.attr is not null && typeof(IComponent).IsAssignableFrom(x.t))
        .ToDictionary(x => x.attr!.Name, x => x.t);
}

public sealed class ParityRunner : IAsyncDisposable
{
    private readonly ServiceProvider _services = new ServiceCollection().AddLogging().BuildServiceProvider();
    private readonly HtmlRenderer _renderer;

    public ParityRunner()
    {
        _renderer = new HtmlRenderer(_services, _services.GetRequiredService<ILoggerFactory>() ?? NullLoggerFactory.Instance);
    }

    public async Task<IReadOnlyList<ComponentParity>> RunAllAsync()
    {
        var all = new List<ComponentParity>();
        foreach (var set in FixtureStore.All) all.Add(await RunAsync(set));
        return all;
    }

    public async Task<ComponentParity> RunAsync(FixtureSet set)
    {
        ComponentRegistry.ByName.TryGetValue(set.Component, out var type);
        var results = new List<ParityResult>();
        foreach (var fixture in set.Fixtures)
        {
            results.Add(type is null
                ? ParityResult.Failed(set.Component, fixture, "No .NET component has been written for this upstream component yet.")
                : await CheckAsync(set.Component, type, fixture));
        }
        return new ComponentParity(set.Component, set.Name, type, results);
    }

    public async Task<ParityResult> CheckAsync(string component, Type type, Fixture fixture)
    {
        string actual;
        try
        {
            actual = await RenderAsync(type, fixture);
        }
        catch (Exception ex)
        {
            return ParityResult.Failed(component, fixture, ex.GetBaseException().Message);
        }

        var expectedTokens = HtmlNormalizer.Tokens(fixture.Html);
        var actualTokens = HtmlNormalizer.Tokens(actual);
        var passed = expectedTokens.SequenceEqual(actualTokens);
        var diff = passed ? [] : TokenDiff.Compute(expectedTokens, actualTokens);
        return new ParityResult(component, fixture, passed, actual, diff, null);
    }

    /// <summary>Renders a fixture's Nunjucks context through the matching Razor component.</summary>
    public Task<string> RenderAsync(Type type, Fixture fixture) =>
        RenderAsync(type, BuildParameters(type, fixture.Context, fixture.CallBlock, fixture.Blocks));

    public Task<string> RenderAsync(Type type, IDictionary<string, object?> parameters) =>
        _renderer.Dispatcher.InvokeAsync(async () =>
        {
            var output = await _renderer.RenderComponentAsync(type, ParameterView.FromDictionary(parameters));
            return output.ToHtmlString();
        });

    public static Dictionary<string, object?> BuildParameters(
        Type type, JsonElement context, string? callBlock, IReadOnlyDictionary<string, string>? blocks = null)
    {
        var byOption = type.GetProperties()
            .Select(p => (p, attr: p.GetCustomAttribute<MacroOptionAttribute>()))
            .Where(x => x.attr is not null)
            .ToDictionary(x => x.attr!.Name, x => x.p);

        var parameters = new Dictionary<string, object?>();
        if (context.ValueKind == JsonValueKind.Object)
        {
            foreach (var option in context.EnumerateObject())
            {
                if (!byOption.TryGetValue(option.Name, out var property))
                    throw new InvalidOperationException($"Upstream option '{option.Name}' has no matching parameter.");
                parameters[property.Name] = NhsukJson.ReadOption(option.Value, property);
            }
        }
        if (callBlock is not null)
        {
            parameters["ChildContent"] = (RenderFragment)(b => b.AddMarkupContent(0, callBlock));
        }
        if (blocks is not null)
        {
            var byBlock = type.GetProperties()
                .Select(p => (p, attr: p.GetCustomAttribute<MacroBlockAttribute>()))
                .Where(x => x.attr is not null)
                .ToDictionary(x => x.attr!.Name, x => x.p);
            foreach (var (block, html) in blocks)
            {
                if (!byBlock.TryGetValue(block, out var property))
                    throw new InvalidOperationException($"Upstream block '{block}' has no matching parameter.");
                parameters[property.Name] = (RenderFragment)(b => b.AddMarkupContent(0, html));
            }
        }
        return parameters;
    }

    public async ValueTask DisposeAsync()
    {
        await _renderer.DisposeAsync();
        await _services.DisposeAsync();
    }
}
