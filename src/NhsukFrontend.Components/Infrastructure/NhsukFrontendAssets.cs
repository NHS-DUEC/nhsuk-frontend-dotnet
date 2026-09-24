using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;

namespace NhsukFrontend.Components;

/// <summary>Paths to the compiled NHS.UK frontend assets bundled in this library.</summary>
public static class NhsukFrontendAssets
{
    /// <summary>Where ASP.NET Core serves this library's wwwroot (static web assets).</summary>
    public const string ContentRoot = "/_content/NhsukFrontend.Components";

    public const string Stylesheet = ContentRoot + "/nhsuk-frontend.min.css";
    public const string Script = ContentRoot + "/nhsuk-frontend.min.js";

    /// <summary>The upstream default <c>assetPath</c>; the compiled CSS references images here.</summary>
    public const string AssetPath = "/assets";

    /// <summary>
    /// Stylesheet link plus the ES module that initialises every component, for the page template's
    /// <c>Head</c> and <c>BodyEnd</c> blocks.
    /// </summary>
    public static string HeadHtml => $"<link rel=\"stylesheet\" href=\"{Stylesheet}\">";

    public static string BodyEndHtml =>
        $"<script type=\"module\">import {{ initAll }} from '{Script}'; initAll()</script>";
}

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
