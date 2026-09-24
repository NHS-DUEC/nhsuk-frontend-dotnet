using System.Collections;
using System.Globalization;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace NhsukFrontend.Components.Infrastructure;

/// <summary>Marks a component as a port of an upstream Nunjucks macro.</summary>
[AttributeUsage(AttributeTargets.Class)]
public sealed class MacroComponentAttribute(string name) : Attribute
{
    /// <summary>Upstream component folder name, for example <c>error-message</c>.</summary>
    public string Name { get; } = name;
}

/// <summary>Maps a component parameter to the upstream macro option name.</summary>
[AttributeUsage(AttributeTargets.Property)]
public sealed class MacroOptionAttribute(string name) : Attribute
{
    public string Name { get; } = name;
}

/// <summary>Maps a <c>RenderFragment</c> parameter to an upstream Nunjucks <c>{% block %}</c>.</summary>
[AttributeUsage(AttributeTargets.Property)]
public sealed class MacroBlockAttribute(string name) : Attribute
{
    public string Name { get; } = name;
}

/// <summary>Base for generated options classes.</summary>
public abstract class NhsukOptions
{
    /// <summary>
    /// True when upstream passed the literal <c>true</c> instead of an object
    /// (for example <c>errorMessage: true</c> or <c>search: true</c>).
    /// </summary>
    [JsonIgnore] public bool IsTrue { get; init; }
}

/// <summary>Options that upstream also accepts as a plain string.</summary>
public interface IShorthandOptions<TSelf> where TSelf : IShorthandOptions<TSelf>
{
    static abstract TSelf FromShorthand(string value);
}

/// <summary>
/// The <c>attributes</c> macro option. Values may be strings, booleans, or an
/// <see cref="AttributeValue"/> for the <c>{ value, optional, type }</c> form.
/// </summary>
[JsonConverter(typeof(NhsukAttributesJsonConverter))]
public sealed class NhsukAttributes : Dictionary<string, object?>
{
    public NhsukAttributes() : base(StringComparer.Ordinal) { }
}

/// <summary>The long form of an attribute: <c>{ value, optional, type }</c>.</summary>
public sealed record AttributeValue(object? Value, bool Optional = false, string? Type = null);

/// <summary>
/// Ordered attribute list that follows the rules of upstream's <c>nhsukAttributes</c> macro,
/// ready for Razor's <c>@attributes</c> splatting.
/// </summary>
public sealed class Attrs : IEnumerable<KeyValuePair<string, object>>
{
    private readonly List<KeyValuePair<string, object>> _items = [];

    /// <summary>Always rendered. <c>null</c> becomes an empty value; booleans become "true"/"false".</summary>
    public Attrs Add(string name, object? value)
    {
        _items.Add(new(name, Format(value)));
        return this;
    }

    /// <summary>Skipped when null or false; rendered as a bare boolean attribute when true.</summary>
    public Attrs Optional(string name, object? value)
    {
        if (value is null || value is false) return this;
        _items.Add(new(name, value is true ? true : Format(value)));
        return this;
    }

    /// <summary>Appends the user's <c>attributes</c> option, plus any unmatched Razor attributes.</summary>
    public Attrs AddUser(NhsukAttributes? attributes, IReadOnlyDictionary<string, object>? additional = null)
    {
        if (attributes is not null)
        {
            foreach (var (name, raw) in attributes)
            {
                if (raw is AttributeValue av)
                {
                    var value = av.Type == "array" ? JsonSerializer.Serialize(av.Value) : av.Value;
                    if (av.Optional) Optional(name, value); else Add(name, value);
                }
                else Add(name, raw);
            }
        }
        if (additional is not null)
        {
            foreach (var (name, value) in additional) _items.Add(new(name, value));
        }
        return this;
    }

    public bool IsEmpty => _items.Count == 0;

    private static string Format(object? value) => value switch
    {
        null => "",
        bool b => b ? "true" : "false",
        JsonElement e when e.ValueKind == JsonValueKind.String => e.GetString() ?? "",
        JsonElement e => e.GetRawText(),
        IFormattable f => f.ToString(null, CultureInfo.InvariantCulture),
        _ => value.ToString() ?? ""
    };

    public IEnumerator<KeyValuePair<string, object>> GetEnumerator() => _items.GetEnumerator();
    IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();
}

/// <summary>Small helpers that reproduce Nunjucks truthiness and filters.</summary>
public static class Nj
{
    public static bool Truthy(string? value) => !string.IsNullOrEmpty(value);
    public static bool Truthy(bool? value) => value == true;
    public static bool Truthy(int? value) => value is not null and not 0;

    /// <summary>The <c>default(x)</c> filter: fallback only when missing.</summary>
    public static string Default(string? value, string fallback) => value ?? fallback;

    /// <summary>The <c>default(x, true)</c> filter: fallback when missing or falsy.</summary>
    public static string DefaultIfFalsy(string? value, string fallback) => string.IsNullOrEmpty(value) ? fallback : value;

    /// <summary>Upstream's <c>"x" in params.classes</c> substring check.</summary>
    public static bool Contains(string? classes, string className) => classes is not null && classes.Contains(className, StringComparison.Ordinal);

    public static string Append(string classNames, string? extra) => Truthy(extra) ? classNames + " " + extra : classNames;
}
