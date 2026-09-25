using System.Net;
using System.Reflection;
using System.Text.Encodings.Web;
using System.Text.Json.Serialization;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Web;
using Microsoft.AspNetCore.Html;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc.ModelBinding;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.AspNetCore.Mvc.ViewFeatures;
using Microsoft.AspNetCore.Razor.TagHelpers;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using NhsukFrontend.Components.Forms;
using NhsukFrontend.Components.Infrastructure;

namespace NhsukFrontend.Components.TagHelpers;

/// <summary>
/// Base for the generated MVC and Razor Pages tag helpers (one per component, in <c>Generated/TagHelpers</c>).
/// Each renders its Razor component, so tag helpers and components always produce the same HTML.
/// </summary>
public abstract class NhsukComponentTagHelper<TComponent> : TagHelper where TComponent : IComponent
{
    private static readonly Dictionary<string, PropertyInfo> Parameters = typeof(TComponent).GetProperties()
        .Where(p => p.GetCustomAttribute<ParameterAttribute>() is not null)
        .ToDictionary(p => p.Name);

    [ViewContext, HtmlAttributeNotBound] public ViewContext ViewContext { get; set; } = null!;

    /// <summary>
    /// A complete options object for this component (for example <c>PanelOptions</c> for <c>nhsuk-panel</c>),
    /// for when options are built in C#. Attributes on the tag win over it.
    /// </summary>
    [HtmlAttributeName("options")] public NhsukOptions? Options { get; set; }

    /// <summary>Generated: copies the tag's attributes into component parameters.</summary>
    protected abstract void AddParameters(IDictionary<string, object?> parameters);

    /// <summary>Last chance to adjust parameters before rendering. Return false to render nothing.</summary>
    protected virtual bool BeforeRender(IDictionary<string, object?> parameters) => true;

    public override async Task ProcessAsync(TagHelperContext context, TagHelperOutput output)
    {
        var parameters = new Dictionary<string, object?>();
        if (Options is not null)
        {
            foreach (var property in Options.GetType().GetProperties())
            {
                if (property.GetCustomAttribute<JsonPropertyNameAttribute>() is null) continue;
                if (property.GetValue(Options) is { } value && Parameters.TryGetValue(property.Name, out var parameter)
                    && parameter.PropertyType.IsAssignableFrom(property.PropertyType))
                    parameters[property.Name] = value;
            }
        }
        AddParameters(parameters);

        // Tag content is the component's content, like a Nunjucks call block.
        var content = await output.GetChildContentAsync();
        if (!content.IsEmptyOrWhiteSpace && Parameters.ContainsKey("ChildContent"))
        {
            var markup = content.GetContent();
            parameters["ChildContent"] = (RenderFragment)(builder => builder.AddMarkupContent(0, markup));
        }

        // Plain HTML attributes on the tag (data-*, aria-*, required…) go to the component's main element.
        if (output.Attributes.Count > 0 && Parameters.ContainsKey("AdditionalAttributes"))
        {
            parameters["AdditionalAttributes"] = output.Attributes.ToDictionary(a => a.Name, a => AttributeValue(a));
        }

        output.TagName = null;
        output.Attributes.Clear();
        if (!BeforeRender(parameters))
        {
            output.SuppressOutput();
            return;
        }
        output.Content.SetHtmlContent(await RenderAsync(ViewContext.HttpContext, parameters));
    }

    /// <summary>Sets a parameter when given, converting plain strings to options (<c>heading="Application complete"</c>).</summary>
    protected static void Set(IDictionary<string, object?> parameters, string name, object? value)
    {
        if (value is null || !Parameters.TryGetValue(name, out var parameter)) return;
        if (value is string text && parameter.PropertyType != typeof(string))
        {
            var target = Nullable.GetUnderlyingType(parameter.PropertyType) ?? parameter.PropertyType;
            value = target.GetMethod("FromShorthand", BindingFlags.Public | BindingFlags.Static)?.Invoke(null, [text])
                ?? throw new InvalidOperationException($"{typeof(TComponent).Name}.{name} does not accept a plain string.");
        }
        parameters[name] = value;
    }

    private static object AttributeValue(TagHelperAttribute attribute)
    {
        switch (attribute.Value)
        {
            case null when attribute.ValueStyle == HtmlAttributeValueStyle.Minimized:
                return true;
            case IHtmlContent html:
                using (var writer = new StringWriter())
                {
                    html.WriteTo(writer, HtmlEncoder.Default);
                    return WebUtility.HtmlDecode(writer.ToString());
                }
            default:
                return attribute.Value?.ToString() ?? "";
        }
    }

    private const string RendererKey = "NhsukFrontend.HtmlRenderer";

    /// <summary>Renders the component, reusing one renderer per request rather than one per tag.</summary>
    internal static async Task<string> RenderAsync(HttpContext httpContext, IDictionary<string, object?> parameters)
    {
        if (httpContext.Items[RendererKey] is not HtmlRenderer renderer)
        {
            var services = httpContext.RequestServices;
            renderer = new HtmlRenderer(services, services.GetRequiredService<ILoggerFactory>());
            httpContext.Items[RendererKey] = renderer;
            httpContext.Response.RegisterForDisposeAsync(renderer);
        }
        return await renderer.Dispatcher.InvokeAsync(async () =>
            (await renderer.RenderComponentAsync<TComponent>(ParameterView.FromDictionary(parameters))).ToHtmlString());
    }
}

/// <summary>Base for tag helpers of form components, which can bind to a model property with <c>asp-for</c>.</summary>
public abstract class NhsukFieldTagHelper<TComponent> : NhsukComponentTagHelper<TComponent> where TComponent : IComponent
{
    /// <summary>The model property. Fills in the name, value, label or legend, and the first validation error.</summary>
    [HtmlAttributeName("asp-for")] public ModelExpression? For { get; set; }

    protected override bool BeforeRender(IDictionary<string, object?> parameters)
    {
        if (For is not null) parameters["Field"] = MvcFields.From(For, ViewContext);
        return true;
    }
}

/// <summary>Adds a plain-text <c>legend</c> to the components that sit in a fieldset.</summary>
internal static class Legend
{
    public static void Apply(IDictionary<string, object?> parameters, string? legend)
    {
        if (legend is not null && !parameters.ContainsKey("Fieldset")) parameters["Fieldset"] = new FieldsetOptions { Legend = legend };
    }
}

public sealed partial class NhsukRadiosTagHelper
{
    /// <summary>Plain-text legend. Defaults to the model property's display name with <c>asp-for</c>.</summary>
    [HtmlAttributeName("legend")] public string? Legend { get; set; }

    protected override bool BeforeRender(IDictionary<string, object?> parameters)
    {
        TagHelpers.Legend.Apply(parameters, Legend);
        return base.BeforeRender(parameters);
    }
}

public sealed partial class NhsukCheckboxesTagHelper
{
    /// <summary>Plain-text legend. Defaults to the model property's display name with <c>asp-for</c>.</summary>
    [HtmlAttributeName("legend")] public string? Legend { get; set; }

    protected override bool BeforeRender(IDictionary<string, object?> parameters)
    {
        TagHelpers.Legend.Apply(parameters, Legend);
        return base.BeforeRender(parameters);
    }
}

public sealed partial class NhsukDateInputTagHelper
{
    /// <summary>Plain-text legend. Defaults to the model property's display name with <c>asp-for</c>.</summary>
    [HtmlAttributeName("legend")] public string? Legend { get; set; }

    protected override bool BeforeRender(IDictionary<string, object?> parameters)
    {
        TagHelpers.Legend.Apply(parameters, Legend);
        return base.BeforeRender(parameters);
    }
}

/// <summary>
/// <c>&lt;nhsuk-error-summary /&gt;</c> with no <c>error-list</c>, <c>description</c> or content lists every model state
/// error, linking each to its field, and renders nothing when the model is valid.
/// </summary>
public sealed partial class NhsukErrorSummaryTagHelper
{
    protected override bool BeforeRender(IDictionary<string, object?> parameters)
    {
        // Only an otherwise empty summary is filled from model state; anything else renders as upstream does.
        if (parameters.ContainsKey("ErrorList") || parameters.ContainsKey("Description") || parameters.ContainsKey("ChildContent")) return true;

        var modelState = ViewContext.ViewData.ModelState;
        if (modelState.IsValid) return false;

        var items = new List<ErrorSummaryErrorListItem?>();
        // Model state is not in page order, so order by where each property is declared on the model.
        var metadata = ViewContext.ViewData.ModelMetadata;
        foreach (var (key, entry) in modelState.OrderBy(e => DeclarationOrder(metadata, e.Key), OrderComparer))
        {
            if (entry?.Errors.FirstOrDefault()?.ErrorMessage is not { Length: > 0 } message) continue;
            var isDate = modelState.ContainsKey(key + ".Day");
            items.Add(key.Length == 0
                ? new ErrorSummaryErrorListItem { Text = message }
                : new ErrorSummaryErrorListItem { Text = message, Href = "#" + NhsukField.ErrorTarget(key, isDate ? typeof(NhsukDate) : null) });
        }
        parameters["ErrorList"] = items;
        parameters.TryAdd("Heading", (ErrorSummaryHeadingOptions)"There is a problem");
        return true;
    }

    private static readonly Comparer<int[]> OrderComparer = Comparer<int[]>.Create((a, b) =>
    {
        for (var i = 0; i < Math.Min(a.Length, b.Length); i++)
            if (a[i] != b[i]) return a[i].CompareTo(b[i]);
        return a.Length.CompareTo(b.Length);
    });

    /// <summary>The position of each segment of a key like <c>Details.DateOfBirth</c> among its model's properties.</summary>
    private static int[] DeclarationOrder(ModelMetadata? metadata, string key)
    {
        var order = new List<int>();
        foreach (var segment in key.Split('.', StringSplitOptions.RemoveEmptyEntries))
        {
            var properties = metadata?.Properties.ToList() ?? [];
            var index = properties.FindIndex(p => string.Equals(p.PropertyName, segment.Split('[')[0], StringComparison.OrdinalIgnoreCase));
            order.Add(index < 0 ? int.MaxValue : index);
            metadata = index < 0 ? null : properties[index];
        }
        return [.. order];
    }
}

/// <summary>Builds an <see cref="NhsukField"/> from an MVC model expression and model state.</summary>
public static class MvcFields
{
    public static NhsukField From(ModelExpression expression, ViewContext viewContext)
    {
        var name = viewContext.ViewData.TemplateInfo.GetFullHtmlFieldName(expression.Name);
        var modelState = viewContext.ViewData.ModelState;
        var entry = modelState.TryGetValue(name, out var e) ? e : null;
        var (value, values, date) = NhsukField.Describe(expression.Model);

        // Show what the user typed, even when it could not be bound to the property's type.
        if (entry?.RawValue is string[] raw) values = raw;
        if (entry?.AttemptedValue is { } attempted) value = attempted;
        if (expression.Metadata.ModelType == typeof(NhsukDate))
        {
            date = new NhsukDate
            {
                Day = Attempted(name + ".Day") ?? date?.Day,
                Month = Attempted(name + ".Month") ?? date?.Month,
                Year = Attempted(name + ".Year") ?? date?.Year,
            };
        }

        return new NhsukField
        {
            Name = name,
            Label = expression.Metadata.DisplayName ?? expression.Metadata.PropertyName,
            Value = value,
            Values = values,
            Date = date,
            Error = entry?.Errors.FirstOrDefault()?.ErrorMessage,
        };

        string? Attempted(string key) => modelState.TryGetValue(key, out var part) ? part.AttemptedValue : null;
    }
}

