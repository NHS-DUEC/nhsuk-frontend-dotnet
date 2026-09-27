using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace NhsukFrontend.Tests;

public sealed class SetupTests
{
    /// <summary>Runs the app's pipeline, wrapped by any startup filters, and returns the path the app saw.</summary>
    private static async Task<string> PathSeenByApp(IServiceProvider services, string path)
    {
        var seen = "";
        Action<IApplicationBuilder> configure = app => app.Run(context => { seen = context.Request.Path; return Task.CompletedTask; });
        foreach (var filter in services.GetServices<IStartupFilter>().Reverse()) configure = filter.Configure(configure);

        var builder = new ApplicationBuilder(services);
        configure(builder);
        await builder.Build()(new DefaultHttpContext { Request = { Path = path } });
        return seen;
    }

    [Fact]
    public async Task AddNhsukFrontend_serves_assets_at_the_path_the_css_expects()
    {
        var services = new ServiceCollection().AddNhsukFrontend().BuildServiceProvider();
        Assert.Equal("/_content/NhsukFrontend.Components/assets/images/favicon.ico", await PathSeenByApp(services, "/assets/images/favicon.ico"));
        Assert.Equal("/examples/form", await PathSeenByApp(services, "/examples/form"));
    }

    [Fact]
    public void AddNhsukFrontend_is_safe_to_call_twice()
    {
        var services = new ServiceCollection().AddNhsukFrontend().AddNhsukFrontend().BuildServiceProvider();
        Assert.Single(services.GetServices<IStartupFilter>());
    }
}
