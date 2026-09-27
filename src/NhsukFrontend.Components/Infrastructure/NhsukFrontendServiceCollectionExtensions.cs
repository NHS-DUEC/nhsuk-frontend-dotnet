using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.DependencyInjection.Extensions;

// In the dependency injection namespace, like other Add… methods, so Program.cs needs no extra using.
namespace Microsoft.Extensions.DependencyInjection;

public static class NhsukFrontendServiceCollectionExtensions
{
    /// <summary>
    /// Sets up NHS.UK frontend for the app. Serves the bundled images and manifest at <c>/assets</c>, where
    /// upstream's compiled CSS and the page template expect them, from the very start of the request pipeline,
    /// so it works wherever this line sits in <c>Program.cs</c> and whichever .NET template made the app.
    /// Safe to call more than once, and alongside <c>app.UseNhsukFrontendAssets()</c>.
    /// </summary>
    public static IServiceCollection AddNhsukFrontend(this IServiceCollection services)
    {
        services.TryAddEnumerable(ServiceDescriptor.Transient<IStartupFilter, NhsukFrontendAssetsStartupFilter>());
        return services;
    }

    private sealed class NhsukFrontendAssetsStartupFilter : IStartupFilter
    {
        public Action<IApplicationBuilder> Configure(Action<IApplicationBuilder> next) => app =>
        {
            app.UseNhsukFrontendAssets();
            next(app);
        };
    }
}
