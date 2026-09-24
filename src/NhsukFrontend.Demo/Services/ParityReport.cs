using NhsukFrontend.Parity;

namespace NhsukFrontend.Demo.Services;

/// <summary>Renders every upstream fixture once, at startup, and keeps the results for the site.</summary>
public sealed class ParityReport
{
    private readonly Lazy<Task<IReadOnlyList<ComponentParity>>> _results = new(async () =>
    {
        await using var runner = new ParityRunner();
        return await runner.RunAllAsync();
    });

    public Task<IReadOnlyList<ComponentParity>> Results => _results.Value;

    public async Task<ComponentParity?> ForComponentAsync(string name) =>
        (await Results).FirstOrDefault(r => r.Component == name);
}
