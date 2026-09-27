using Microsoft.AspNetCore.Http;
using NhsukFrontend.Components;

// In the ASP.NET Core namespace, like other Use… methods, so Program.cs needs no extra using.
namespace Microsoft.AspNetCore.Builder;

public static class NhsukFrontendApplicationBuilderExtensions
{
    /// <summary>
    /// Serves the bundled images and manifest at <c>/assets</c>, where upstream's compiled CSS and the
    /// page template expect them. Call before <c>UseStaticFiles()</c>.
    /// </summary>
    public static IApplicationBuilder UseNhsukFrontendAssets(this IApplicationBuilder app) =>
        app.Use((context, next) =>
        {
            if (context.Request.Path.StartsWithSegments(NhsukFrontendAssets.AssetPath, out var rest))
            {
                context.Request.Path = new PathString(NhsukFrontendAssets.ContentRoot + NhsukFrontendAssets.AssetPath).Add(rest);
            }
            return next(context);
        });
}
