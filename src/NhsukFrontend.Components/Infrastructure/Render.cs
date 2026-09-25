using Microsoft.AspNetCore.Components;

namespace NhsukFrontend.Components.Infrastructure;

/// <summary>Render fragments shared by several component ports.</summary>
public static class Render
{
    /// <summary>Upstream's <c>html | safe if html else text</c>.</summary>
    public static RenderFragment HtmlOrText(string? html, string? text) => builder =>
    {
        if (Nj.Truthy(html)) builder.AddMarkupContent(0, html);
        else if (Nj.Truthy(text)) builder.AddContent(1, text);
    };

    /// <summary>Nothing when both are empty; used for <c>formGroup.beforeInput(s)</c> and similar.</summary>
    public static bool HasContent(string? html, string? text) => Nj.Truthy(html) || Nj.Truthy(text);
}
