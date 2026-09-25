using System.Reflection;
using System.Text.Json.Serialization;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Web;
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
/// Base for the MVC and Razor Pages tag helpers. Each renders one Razor component. Options can be given
/// as attributes for the common cases, or as a full options object with <c>options="..."</c>.
/// </summary>
public abstract class NhsukComponentTagHelper<TComponent> : TagHelper where TComponent : IComponent
{
    private static readonly Dictionary<string, PropertyInfo> Parameters = typeof(TComponent).GetProperties()
        .Where(p => p.GetCustomAttribute<ParameterAttribute>() is not null)
        .ToDictionary(p => p.Name);

    [ViewContext, HtmlAttributeNotBound] public ViewContext ViewContext { get; set; } = null!;

    /// <summary>
    /// Any generated options object for this component (for example <c>InputOptions</c> for <c>nhsuk-input</c>).
    /// Every option set on it is passed through; attributes on the tag win over it.
    /// </summary>
    [HtmlAttributeName("options")] public NhsukOptions? Options { get; set; }

    [HtmlAttributeName("id")] public string? Id { get; set; }
    [HtmlAttributeName("classes")] public string? Classes { get; set; }

    /// <summary>Adds the tag helper's own options to the component parameters.</summary>
    protected virtual void AddParameters(IDictionary<string, object?> parameters) { }

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
        Set(parameters, "Id", Id);
        Set(parameters, "Classes", Classes);
        AddParameters(parameters);

        output.TagName = null;
        output.Content.SetHtmlContent(await RenderAsync(ViewContext.HttpContext.RequestServices, parameters));
    }

    /// <summary>Sets a parameter when given, converting plain strings to options (<c>label="Postcode"</c>).</summary>
    protected static void Set(IDictionary<string, object?> parameters, string name, object? value)
    {
        if (value is null || !Parameters.TryGetValue(name, out var parameter)) return;
        if (value is string text && parameter.PropertyType != typeof(string))
        {
            var shorthand = Nullable.GetUnderlyingType(parameter.PropertyType) ?? parameter.PropertyType;
            value = shorthand.GetMethod("FromShorthand", BindingFlags.Public | BindingFlags.Static)?.Invoke(null, [text])
                ?? throw new InvalidOperationException($"{typeof(TComponent).Name}.{name} does not accept a plain string.");
        }
        parameters[name] = value;
    }

    internal static async Task<string> RenderAsync(IServiceProvider services, IDictionary<string, object?> parameters)
    {
        await using var renderer = new HtmlRenderer(services, services.GetRequiredService<ILoggerFactory>());
        return await renderer.Dispatcher.InvokeAsync(async () =>
            (await renderer.RenderComponentAsync<TComponent>(ParameterView.FromDictionary(parameters))).ToHtmlString());
    }
}

/// <summary>Base for tag helpers bound to a model property with <c>asp-for</c>.</summary>
public abstract class NhsukFieldTagHelper<TComponent> : NhsukComponentTagHelper<TComponent> where TComponent : IComponent
{
    /// <summary>The model property. Fills in the name, value, label or legend, and the first validation error.</summary>
    [HtmlAttributeName("asp-for")] public ModelExpression? For { get; set; }

    [HtmlAttributeName("hint")] public string? Hint { get; set; }

    protected override void AddParameters(IDictionary<string, object?> parameters)
    {
        Set(parameters, "Hint", Hint);
        if (For is not null) parameters["Field"] = MvcFields.From(For, ViewContext);
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

/// <summary><c>&lt;nhsuk-input asp-for="Postcode" hint="For example, LS1 4AP" width="10" /&gt;</c></summary>
[HtmlTargetElement("nhsuk-input", TagStructure = TagStructure.WithoutEndTag)]
public sealed class InputTagHelper : NhsukFieldTagHelper<NhsukInput>
{
    [HtmlAttributeName("label")] public string? Label { get; set; }
    [HtmlAttributeName("type")] public string? Type { get; set; }
    [HtmlAttributeName("width")] public int? Width { get; set; }
    [HtmlAttributeName("autocomplete")] public string? Autocomplete { get; set; }
    [HtmlAttributeName("inputmode")] public string? Inputmode { get; set; }
    [HtmlAttributeName("spellcheck")] public bool? Spellcheck { get; set; }

    protected override void AddParameters(IDictionary<string, object?> parameters)
    {
        base.AddParameters(parameters);
        Set(parameters, "Label", Label);
        Set(parameters, "Type", Type);
        Set(parameters, "Width", Width);
        Set(parameters, "Autocomplete", Autocomplete);
        Set(parameters, "Inputmode", Inputmode);
        Set(parameters, "Spellcheck", Spellcheck);
    }
}

/// <summary><c>&lt;nhsuk-textarea asp-for="Details" rows="5" /&gt;</c></summary>
[HtmlTargetElement("nhsuk-textarea", TagStructure = TagStructure.WithoutEndTag)]
public sealed class TextareaTagHelper : NhsukFieldTagHelper<NhsukTextarea>
{
    [HtmlAttributeName("label")] public string? Label { get; set; }
    [HtmlAttributeName("rows")] public int? Rows { get; set; }

    protected override void AddParameters(IDictionary<string, object?> parameters)
    {
        base.AddParameters(parameters);
        Set(parameters, "Label", Label);
        Set(parameters, "Rows", Rows?.ToString());
    }
}

/// <summary><c>&lt;nhsuk-character-count asp-for="Details" maxlength="200" /&gt;</c></summary>
[HtmlTargetElement("nhsuk-character-count", TagStructure = TagStructure.WithoutEndTag)]
public sealed class CharacterCountTagHelper : NhsukFieldTagHelper<NhsukCharacterCount>
{
    [HtmlAttributeName("label")] public string? Label { get; set; }
    [HtmlAttributeName("maxlength")] public int? Maxlength { get; set; }
    [HtmlAttributeName("maxwords")] public int? Maxwords { get; set; }
    [HtmlAttributeName("rows")] public int? Rows { get; set; }

    protected override void AddParameters(IDictionary<string, object?> parameters)
    {
        base.AddParameters(parameters);
        Set(parameters, "Label", Label);
        Set(parameters, "Maxlength", Maxlength?.ToString());
        Set(parameters, "Maxwords", Maxwords?.ToString());
        Set(parameters, "Rows", Rows?.ToString());
    }
}

/// <summary><c>&lt;nhsuk-select asp-for="Region" items="Model.Regions" /&gt;</c></summary>
[HtmlTargetElement("nhsuk-select", TagStructure = TagStructure.WithoutEndTag)]
public sealed class SelectTagHelper : NhsukFieldTagHelper<NhsukSelect>
{
    [HtmlAttributeName("label")] public string? Label { get; set; }
    [HtmlAttributeName("items")] public IEnumerable<SelectItemsItem>? Items { get; set; }

    protected override void AddParameters(IDictionary<string, object?> parameters)
    {
        base.AddParameters(parameters);
        Set(parameters, "Label", Label);
        Set(parameters, "Items", Items?.Cast<SelectItemsItem?>().ToList());
    }
}

/// <summary><c>&lt;nhsuk-radios asp-for="Contact" items="…" /&gt;</c>. The legend defaults to the display name.</summary>
[HtmlTargetElement("nhsuk-radios", TagStructure = TagStructure.WithoutEndTag)]
public sealed class RadiosTagHelper : NhsukFieldTagHelper<NhsukRadios>
{
    [HtmlAttributeName("legend")] public string? Legend { get; set; }
    [HtmlAttributeName("items")] public IEnumerable<RadiosItemsItem>? Items { get; set; }
    [HtmlAttributeName("inline")] public bool? Inline { get; set; }

    protected override void AddParameters(IDictionary<string, object?> parameters)
    {
        base.AddParameters(parameters);
        if (Legend is not null) parameters["Fieldset"] = new FieldsetOptions { Legend = Legend };
        Set(parameters, "Items", Items?.Cast<RadiosItemsItem?>().ToList());
        Set(parameters, "Inline", Inline);
    }
}

/// <summary><c>&lt;nhsuk-checkboxes asp-for="Symptoms" items="…" /&gt;</c>. Binds to a list of strings.</summary>
[HtmlTargetElement("nhsuk-checkboxes", TagStructure = TagStructure.WithoutEndTag)]
public sealed class CheckboxesTagHelper : NhsukFieldTagHelper<NhsukCheckboxes>
{
    [HtmlAttributeName("legend")] public string? Legend { get; set; }
    [HtmlAttributeName("items")] public IEnumerable<CheckboxesItemsItem>? Items { get; set; }

    protected override void AddParameters(IDictionary<string, object?> parameters)
    {
        base.AddParameters(parameters);
        if (Legend is not null) parameters["Fieldset"] = new FieldsetOptions { Legend = Legend };
        Set(parameters, "Items", Items?.Cast<CheckboxesItemsItem?>().ToList());
    }
}

/// <summary><c>&lt;nhsuk-date-input asp-for="DateOfBirth" hint="For example, 15 3 1984" /&gt;</c>. Binds to <see cref="NhsukDate"/>.</summary>
[HtmlTargetElement("nhsuk-date-input", TagStructure = TagStructure.WithoutEndTag)]
public sealed class DateInputTagHelper : NhsukFieldTagHelper<NhsukDateInput>
{
    [HtmlAttributeName("legend")] public string? Legend { get; set; }

    protected override void AddParameters(IDictionary<string, object?> parameters)
    {
        base.AddParameters(parameters);
        if (Legend is not null) parameters["Fieldset"] = new FieldsetOptions { Legend = Legend };
    }
}

/// <summary>
/// <c>&lt;nhsuk-error-summary /&gt;</c>: lists every model state error, linking to its field.
/// Renders nothing when the model is valid.
/// </summary>
[HtmlTargetElement("nhsuk-error-summary", TagStructure = TagStructure.WithoutEndTag)]
public sealed class ErrorSummaryTagHelper : NhsukComponentTagHelper<NhsukErrorSummary>
{
    [HtmlAttributeName("heading")] public string Heading { get; set; } = "There is a problem";

    public override Task ProcessAsync(TagHelperContext context, TagHelperOutput output)
    {
        if (ViewContext.ViewData.ModelState.IsValid)
        {
            output.SuppressOutput();
            return Task.CompletedTask;
        }
        return base.ProcessAsync(context, output);
    }

    protected override void AddParameters(IDictionary<string, object?> parameters)
    {
        var modelState = ViewContext.ViewData.ModelState;
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
        Set(parameters, "Heading", Heading);
        parameters["ErrorList"] = items;
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
