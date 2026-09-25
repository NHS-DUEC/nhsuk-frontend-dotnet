using System.Collections;
using System.Globalization;

namespace NhsukFrontend.Components.Forms;

/// <summary>
/// Everything a form component needs to know about the model property it edits: its form name,
/// label, current value and first validation error. Built from a Blazor <c>For</c> expression or an
/// MVC <c>asp-for</c> model expression, so both stacks share the same rules.
/// </summary>
/// <remarks>
/// Components only use these values for options the caller did not set, so anything passed
/// explicitly (a custom label, a different error message) still wins.
/// </remarks>
public sealed record NhsukField
{
    /// <summary>The form field name, for example <c>Postcode</c> or <c>Model.Postcode</c>. Also used as the id.</summary>
    public required string Name { get; init; }

    /// <summary>From <c>[Display(Name = "…")]</c>, falling back to the property name.</summary>
    public string? Label { get; init; }

    /// <summary>The value to show, for single-value fields.</summary>
    public string? Value { get; init; }

    /// <summary>The selected values, for checkboxes.</summary>
    public IReadOnlyList<string>? Values { get; init; }

    /// <summary>The value of a date field.</summary>
    public NhsukDate? Date { get; init; }

    /// <summary>The first validation message for this field, if any.</summary>
    public string? Error { get; init; }

    /// <summary>The element the error summary should link to: the field, or the day input of a date.</summary>
    public static string ErrorTarget(string name, Type? propertyType) =>
        propertyType == typeof(NhsukDate) ? name + "-day" : name;

    internal static (string? Value, IReadOnlyList<string>? Values, NhsukDate? Date) Describe(object? model) => model switch
    {
        null => (null, null, null),
        string s => (s, null, null),
        NhsukDate d => (null, null, d),
        bool b => (b ? "true" : "false", null, null),
        Enum e => (e.ToString(), null, null),
        IFormattable f => (f.ToString(null, CultureInfo.InvariantCulture), null, null),
        IEnumerable items => (null, items.Cast<object?>().Select(x => Convert.ToString(x, CultureInfo.InvariantCulture) ?? "").ToList(), null),
        _ => (model.ToString(), null, null),
    };
}
