// Renders every upstream fixture through the .NET components and compares the HTML.
//
//   dotnet run --project tools/NhsukFrontend.ParityCheck                 summary, exits 1 on any failure
//   dotnet run --project tools/NhsukFrontend.ParityCheck -- button -v    one component, with diffs

using NhsukFrontend.Parity;

var verbose = args.Contains("-v") || args.Contains("--verbose");

// --emit-snippets <file>: write every example's Razor snippet into one .razor file, so CI can
// compile it and prove the documented usage is valid.
var emitIndex = Array.IndexOf(args, "--emit-snippets");
if (emitIndex >= 0)
{
    var file = args[emitIndex + 1];
    var sb = new System.Text.StringBuilder("@using NhsukFrontend.Components\n@using NhsukFrontend.Components.Infrastructure\n\n");
    foreach (var set in FixtureStore.All)
    {
        if (!ComponentRegistry.ByName.TryGetValue(set.Component, out var type)) continue;
        foreach (var fixture in set.Fixtures)
        {
            sb.Append($"@* {set.Component}: {fixture.Name.Replace("*", "")} *@\n");
            sb.Append(Snippets.Razor(type, fixture)).Append("\n\n");
        }
    }
    File.WriteAllText(file, sb.ToString());
    Console.WriteLine($"Wrote {file}");
    return 0;
}
var only = args.Where(a => !a.StartsWith('-')).ToHashSet();

await using var runner = new ParityRunner();
var sets = FixtureStore.All.Where(s => only.Count == 0 || only.Contains(s.Component));

Console.WriteLine($"Parity against nhsuk-frontend {FixtureStore.Manifest.UpstreamVersion}\n");

int passed = 0, total = 0;
foreach (var set in sets)
{
    var result = await runner.RunAsync(set);
    passed += result.PassedCount;
    total += result.Results.Count;
    var mark = result.AllPassed ? "PASS" : "FAIL";
    Console.WriteLine($"{mark}  {set.Component,-16} {result.PassedCount,3}/{result.Results.Count}");

    if (result.ComponentType is null)
    {
        Console.WriteLine("        - no .NET component written yet");
        continue;
    }

    foreach (var failure in result.Results.Where(r => !r.Passed))
    {
        Console.WriteLine($"        - {failure.Fixture.Name}{(failure.Error is null ? "" : $": {failure.Error}")}");
        if (!verbose || failure.Error is not null) continue;
        foreach (var line in failure.Diff.Where(d => d.Kind != DiffKind.Same))
        {
            Console.WriteLine($"            {(line.Kind == DiffKind.Expected ? "- upstream" : "+ dotnet  ")} {line.Token}");
        }
    }
}

Console.WriteLine($"\n{passed}/{total} fixtures match upstream.");
Console.WriteLine($"Not yet ported: {string.Join(", ", FixtureStore.Manifest.NotYetPorted)}");
return passed == total ? 0 : 1;
