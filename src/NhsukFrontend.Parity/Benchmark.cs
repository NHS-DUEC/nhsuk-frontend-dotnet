using System.Diagnostics;
using System.Text.Json;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Web;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using NhsukFrontend.Components;

namespace NhsukFrontend.Parity;

/// <summary>
/// Rough rendering cost of the tag helpers compared with rendering the Razor components directly.
/// Not a rigorous benchmark: it runs in-process, single-threaded, and excludes the rest of the MVC pipeline.
/// </summary>
public static class Benchmark
{
    public static async Task RunAsync(TextWriter output, int requests = 200)
    {
        var input = new Fixture
        {
            Name = "benchmark input",
            Context = JsonSerializer.SerializeToElement(new
            {
                label = new { text = "Full name" }, hint = new { text = "As it appears on your passport" }, name = "fullName", value = "Ada",
            }),
        };
        var table = new Fixture
        {
            Name = "benchmark table",
            Context = JsonSerializer.SerializeToElement(new
            {
                caption = "Appointments",
                head = new[] { new { text = "Date" }, new { text = "Clinic" }, new { text = "Clinician" }, new { text = "Status" } },
                rows = Enumerable.Range(1, 200).Select(i => new[]
                {
                    new { text = $"{i % 28 + 1} March 2026" }, new { text = "Outpatients" }, new { text = $"Dr Example {i}" }, new { text = "Booked" },
                }),
            }),
        };

        await using var services = new ServiceCollection().AddLogging().BuildServiceProvider();
        var loggers = services.GetRequiredService<ILoggerFactory>();

        output.WriteLine($"Mean time per simulated request, over {requests} requests after warm-up (lower is better).");
        output.WriteLine("Each request gets its own renderer, as in ASP.NET Core; options are prepared up front, as compiled Razor does.");
        output.WriteLine();
        foreach (var (label, type, fixture, tagsPerRequest) in new[]
        {
            ("Form page: 20 inputs with labels and hints", typeof(NhsukInput), input, 20),
            ("Table: 200 rows x 4 columns", typeof(NhsukTables), table, 1),
        })
        {
            var parameters = ParityRunner.BuildParameters(type, fixture.Context, fixture.CallBlock);
            var prepared = TagHelperParity.Prepare(type, fixture);

            Func<Task> direct = async () =>
            {
                await using var renderer = new HtmlRenderer(services, loggers);
                await renderer.Dispatcher.InvokeAsync(async () =>
                {
                    for (var i = 0; i < tagsPerRequest; i++)
                        (await renderer.RenderComponentAsync(type, ParameterView.FromDictionary(parameters))).ToHtmlString();
                });
            };
            Func<Task> shared = async () =>
            {
                var http = new DefaultHttpContext { RequestServices = services };
                for (var i = 0; i < tagsPerRequest; i++) await TagHelperParity.ProcessAsync(prepared, http);
                await DisposeRequest(http);
            };
            Func<Task> perTag = async () =>
            {
                for (var i = 0; i < tagsPerRequest; i++)
                {
                    var http = new DefaultHttpContext { RequestServices = services };
                    await TagHelperParity.ProcessAsync(prepared, http);
                    await DisposeRequest(http);
                }
            };

            // Run each twice and keep the faster, to reduce warm-up and ordering effects.
            var d = Math.Min(await Time(requests, direct), await Time(requests, direct));
            var s = Math.Min(await Time(requests, shared), await Time(requests, shared));
            var p = Math.Min(await Time(requests, perTag), await Time(requests, perTag));
            output.WriteLine(label);
            output.WriteLine($"  Razor components rendered directly (as in Blazor)   {d,8:0.000} ms");
            output.WriteLine($"  Tag helpers, one renderer per request (this port)  {s,8:0.000} ms  ({s / d:0.00}x)");
            output.WriteLine($"  Tag helpers, new renderer for every tag             {p,8:0.000} ms  ({p / d:0.00}x)");
            output.WriteLine();
        }
    }

    private static async Task<double> Time(int requests, Func<Task> request)
    {
        for (var i = 0; i < Math.Max(20, requests / 10); i++) await request();
        var watch = Stopwatch.StartNew();
        for (var i = 0; i < requests; i++) await request();
        return watch.Elapsed.TotalMilliseconds / requests;
    }

    private static async Task DisposeRequest(HttpContext http)
    {
        if (http.Items["NhsukFrontend.HtmlRenderer"] is IAsyncDisposable renderer) await renderer.DisposeAsync();
    }
}
