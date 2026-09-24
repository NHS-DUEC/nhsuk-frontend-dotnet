using Microsoft.AspNetCore.HttpOverrides;
using NhsukFrontend.Components;
using NhsukFrontend.Demo.Components;
using NhsukFrontend.Demo.Services;

var builder = WebApplication.CreateBuilder(args);

// Static server-side rendering only: no WebSockets or WebAssembly needed on Heroku.
builder.Services.AddRazorComponents();
builder.Services.AddRazorPages();
builder.Services.AddSingleton<ParityReport>();

var app = builder.Build();

// Heroku's router terminates TLS and forwards the original scheme.
var forwarded = new ForwardedHeadersOptions { ForwardedHeaders = ForwardedHeaders.XForwardedFor | ForwardedHeaders.XForwardedProto };
forwarded.KnownNetworks.Clear();
forwarded.KnownProxies.Clear();
app.UseForwardedHeaders(forwarded);

if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/error");
}

// Static rendering can't use the Router's NotFound content, so re-run the pipeline for a proper 404 page.
app.UseStatusCodePagesWithReExecute("/not-found");

app.UseNhsukFrontendAssets();
app.UseStaticFiles();
app.UseAntiforgery();

app.MapGet("/healthz", () => Results.Text("ok"));
app.MapRazorPages();
app.MapRazorComponents<App>();

// Run the parity check once at startup rather than on the first request.
_ = app.Services.GetRequiredService<ParityReport>().Results;

app.Run();
