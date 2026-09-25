using System.Reflection;
using System.Text.Encodings.Web;
using System.Text.Json;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc.ModelBinding;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.AspNetCore.Mvc.ViewFeatures;
using Microsoft.AspNetCore.Razor.TagHelpers;
using NhsukFrontend.Components.Infrastructure;

namespace NhsukFrontend.Parity;

/// <summary>
/// Renders an upstream fixture through a component's generated MVC tag helper, the way a Razor Page would:
/// each option becomes a tag attribute and a call block becomes the tag's content.
/// </summary>
public static class TagHelperParity
{
    /// <summary>The generated tag helper for a component, if there is one (the page template has none).</summary>
    public static Type? TagHelperFor(Type componentType) =>
        componentType.Assembly.GetType($"NhsukFrontend.Components.TagHelpers.{componentType.Name}TagHelper");

    /// <summary>A tag helper type and the attribute values a compiled Razor page would set on it.</summary>
    public sealed record Prepared(Type HelperType, IReadOnlyList<(PropertyInfo Property, object? Value)> Assignments, string? Content);

    public static Task<string> RenderAsync(Type componentType, Fixture fixture, IServiceProvider services, HttpContext? httpContext = null) =>
        ProcessAsync(Prepare(componentType, fixture), httpContext ?? new DefaultHttpContext { RequestServices = services });

    /// <summary>Works out the tag's attributes from a fixture: each option becomes an attribute.</summary>
    public static Prepared Prepare(Type componentType, Fixture fixture)
    {
        var helperType = TagHelperFor(componentType) ?? throw new InvalidOperationException($"No tag helper for {componentType.Name}.");
        var attributes = helperType.GetProperties()
            .Where(p => p.GetCustomAttribute<HtmlAttributeNameAttribute>() is not null)
            .ToDictionary(p => p.Name);
        var parameters = componentType.GetProperties()
            .Select(p => (p, option: p.GetCustomAttribute<Components.Infrastructure.MacroOptionAttribute>()))
            .Where(x => x.option is not null)
            .ToDictionary(x => x.option!.Name, x => x.p);

        var assignments = new List<(PropertyInfo, object?)>();
        // Deprecated options have no tag attribute; they go in through the `options` object, as a developer would.
        var leftovers = new Dictionary<string, JsonElement>();
        if (fixture.Context.ValueKind == JsonValueKind.Object)
        {
            foreach (var option in fixture.Context.EnumerateObject())
            {
                if (!parameters.TryGetValue(option.Name, out var parameter))
                    throw new InvalidOperationException($"Upstream option '{option.Name}' has no matching parameter.");

                if (option.Value.ValueKind == JsonValueKind.String && attributes.ContainsKey(parameter.Name + "Options")
                    && attributes.TryGetValue(parameter.Name, out var textAttribute))
                    assignments.Add((textAttribute, option.Value.GetString()));                         // heading="Text"
                else if (attributes.TryGetValue(parameter.Name + "Options", out var objectAttribute))
                    assignments.Add((objectAttribute, NhsukJson.ReadOption(option.Value, parameter)));  // heading-options="@(…)"
                else if (attributes.TryGetValue(parameter.Name, out var attribute) && attribute.PropertyType.IsAssignableFrom(parameter.PropertyType))
                    assignments.Add((attribute, NhsukJson.ReadOption(option.Value, parameter)));
                else
                    leftovers[option.Name] = option.Value;
            }
        }
        if (leftovers.Count > 0)
        {
            var optionsType = componentType.Assembly.GetType($"NhsukFrontend.Components.{componentType.Name["Nhsuk".Length..]}Options")!;
            assignments.Add((attributes["Options"], JsonSerializer.SerializeToElement(leftovers).Deserialize(optionsType, NhsukJson.Options)));
        }
        return new Prepared(helperType, assignments, fixture.CallBlock);
    }

    /// <summary>Runs a prepared tag helper as Razor would, within one request.</summary>
    public static async Task<string> ProcessAsync(Prepared prepared, HttpContext httpContext, TagHelperAttributeList? htmlAttributes = null)
    {
        var helper = (TagHelper)Activator.CreateInstance(prepared.HelperType)!;
        foreach (var (property, value) in prepared.Assignments) property.SetValue(helper, value);
        prepared.HelperType.GetProperty("ViewContext")!.SetValue(helper, new ViewContext
        {
            HttpContext = httpContext,
            ViewData = new ViewDataDictionary(new EmptyModelMetadataProvider(), new ModelStateDictionary()),
        });

        var tagName = prepared.HelperType.GetCustomAttributes<HtmlTargetElementAttribute>().First().Tag;
        var context = new TagHelperContext(tagName, new TagHelperAttributeList(), new Dictionary<object, object>(), "parity");
        var output = new TagHelperOutput(tagName, htmlAttributes ?? new TagHelperAttributeList(), (_, _) =>
        {
            var content = new DefaultTagHelperContent();
            if (prepared.Content is not null) content.SetHtmlContent(prepared.Content);
            return Task.FromResult<TagHelperContent>(content);
        });

        await helper.ProcessAsync(context, output);
        using var writer = new StringWriter();
        output.WriteTo(writer, HtmlEncoder.Default);
        return writer.ToString();
    }
}
