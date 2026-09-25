using System.Reflection;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using NhsukFrontend.Components.Infrastructure;

namespace NhsukFrontend.Parity;

/// <summary>Shows how an upstream example is written in Razor (and in the original Nunjucks).</summary>
public static class Snippets
{
    public static string Razor(Type componentType, Fixture fixture)
    {
        var properties = componentType.GetProperties()
            .Select(p => (p, attr: p.GetCustomAttribute<MacroOptionAttribute>()))
            .Where(x => x.attr is not null)
            .ToDictionary(x => x.attr!.Name, x => x.p);

        var attributes = new List<string>();
        if (fixture.Context.ValueKind == JsonValueKind.Object)
        {
            foreach (var option in fixture.Context.EnumerateObject())
            {
                if (!properties.TryGetValue(option.Name, out var property)) continue;
                attributes.Add($"{property.Name}={Attribute(property.PropertyType, option.Value, property.GetCustomAttribute<KeepFalseAttribute>() is not null)}");
            }
        }

        var name = componentType.Name;
        var content = fixture.CallBlock ?? (fixture.Blocks?.TryGetValue("content", out var c) == true ? c : null);
        var blocks = fixture.Blocks?.Where(b => b.Key != "content").ToList() ?? [];

        var sb = new StringBuilder("<").Append(name);
        var multiline = attributes.Sum(a => a.Length) > 70 || attributes.Count > 3;
        foreach (var attribute in attributes)
        {
            sb.Append(multiline ? "\n    " : " ").Append(attribute);
        }

        if (content is null && blocks.Count == 0) return sb.Append(multiline ? "\n/>" : " />").ToString();

        sb.Append(multiline ? "\n>" : ">");
        foreach (var (block, html) in blocks)
        {
            var tag = componentType.GetProperties().First(p => p.GetCustomAttribute<MacroBlockAttribute>()?.Name == block).Name;
            sb.Append($"\n    <{tag}>{html}</{tag}>");
        }
        if (content is not null)
        {
            sb.Append(blocks.Count > 0 ? $"\n    <ChildContent>{content.Trim()}</ChildContent>\n" : "\n    " + content.Trim().Replace("\n", "\n    ") + "\n");
        }
        else sb.Append('\n');
        return sb.Append("</").Append(name).Append('>').ToString();
    }

    /// <summary>
    /// The same example as an MVC / Razor Pages tag helper, or null for the page template (a layout in MVC).
    /// Plain text uses the short attribute (<c>heading="…"</c>); anything richer uses <c>heading-options</c>.
    /// Deprecated options have no attribute, so they go in the <c>options</c> object.
    /// </summary>
    public static string? TagHelper(Type componentType, Fixture fixture)
    {
        var helperType = TagHelperParity.TagHelperFor(componentType);
        if (helperType is null) return null;
        var tag = helperType.GetCustomAttributes<Microsoft.AspNetCore.Razor.TagHelpers.HtmlTargetElementAttribute>().First().Tag;
        var names = helperType.GetProperties()
            .Select(p => (p, attr: p.GetCustomAttribute<Microsoft.AspNetCore.Razor.TagHelpers.HtmlAttributeNameAttribute>()?.Name))
            .Where(x => x.attr is not null)
            .ToDictionary(x => x.p.Name, x => x.attr!);
        var parameters = componentType.GetProperties()
            .Select(p => (p, attr: p.GetCustomAttribute<MacroOptionAttribute>()))
            .Where(x => x.attr is not null)
            .ToDictionary(x => x.attr!.Name, x => x.p);

        var attributes = new List<string>();
        var leftovers = new Dictionary<string, JsonElement>();
        if (fixture.Context.ValueKind == JsonValueKind.Object)
        {
            foreach (var option in fixture.Context.EnumerateObject())
            {
                if (!parameters.TryGetValue(option.Name, out var parameter)) continue;
                var keepFalse = parameter.GetCustomAttribute<KeepFalseAttribute>() is not null;
                var hasPair = names.ContainsKey(parameter.Name + "Options");
                if (hasPair && option.Value.ValueKind == JsonValueKind.String && names.TryGetValue(parameter.Name, out var text))
                    attributes.Add($"{text}={Attribute(typeof(string), option.Value)}");
                else if (hasPair && IsTextOnly(option.Value) && names.TryGetValue(parameter.Name, out var shortName))
                    attributes.Add($"{shortName}={Attribute(typeof(string), option.Value.GetProperty("text"))}"); // { text: "…" }
                else if (hasPair)
                    attributes.Add($"{names[parameter.Name + "Options"]}={Attribute(parameter.PropertyType, option.Value, keepFalse)}");
                else if (names.TryGetValue(parameter.Name, out var name))
                    attributes.Add($"{name}={Attribute(parameter.PropertyType, option.Value, keepFalse)}");
                else
                    leftovers[option.Name] = option.Value;
            }
        }
        if (leftovers.Count > 0)
        {
            var optionsType = componentType.Assembly.GetType($"NhsukFrontend.Components.{componentType.Name["Nhsuk".Length..]}Options")!;
            attributes.Add($"options={Attribute(optionsType, JsonSerializer.SerializeToElement(leftovers))}");
        }

        var sb = new StringBuilder("<").Append(tag);
        var multiline = attributes.Sum(a => a.Length) > 70 || attributes.Count > 3;
        foreach (var attribute in attributes) sb.Append(multiline ? "\n    " : " ").Append(attribute);
        if (fixture.CallBlock is null) return sb.Append(multiline ? "\n/>" : " />").ToString();
        sb.Append(multiline ? "\n>" : ">");
        sb.Append("\n    ").Append(fixture.CallBlock.Trim().Replace("\n", "\n    ")).Append('\n');
        return sb.Append("</").Append(tag).Append('>').ToString();
    }

    private static bool IsTextOnly(JsonElement value) =>
        value.ValueKind == JsonValueKind.Object && value.EnumerateObject().Count() == 1
        && value.TryGetProperty("text", out var text) && text.ValueKind == JsonValueKind.String;

    public static string Nunjucks(string component, Fixture fixture)
    {
        var json = JsonSerializer.Serialize(fixture.Context, new JsonSerializerOptions
        {
            WriteIndented = true,
            Encoder = System.Text.Encodings.Web.JavaScriptEncoder.UnsafeRelaxedJsonEscaping,
        });
        if (component == "template") return "{% extends \"nhsuk/template.njk\" %}";

        var macro = string.Concat(component.Split('-').Select((part, i) => i == 0 ? part : char.ToUpperInvariant(part[0]) + part[1..]));
        var import = $"{{% from \"nhsuk/components/{component}/macro.njk\" import {macro} %}}\n\n";
        return fixture.CallBlock is null
            ? $"{import}{{{{ {macro}({json}) }}}}"
            : $"{import}{{% call {macro}({json}) %}}\n  {fixture.CallBlock.Trim()}\n{{% endcall %}}";
    }

    private static string Attribute(Type type, JsonElement value, bool keepFalse = false)
    {
        var target = Nullable.GetUnderlyingType(type) ?? type;
        if (target == typeof(string) && value.ValueKind == JsonValueKind.String)
        {
            var s = value.GetString()!;
            return s.Contains('"') || s.Contains('@') || s.Contains('\n') ? $"@({CSharp(type, value)})" : $"\"{s}\"";
        }
        if (target == typeof(bool) || target == typeof(int)) return $"\"{value.GetRawText().Trim('"').ToLowerInvariant()}\"";
        return $"\"@({CSharp(type, value, 1, keepFalse)})\"";
    }

    /// <summary>Renders a JSON value as a C# expression of the given parameter type.</summary>
    private static string CSharp(Type type, JsonElement value, int depth = 0, bool keepFalse = false)
    {
        var target = Nullable.GetUnderlyingType(type) ?? type;
        var indent = new string(' ', 4 * (depth + 1));
        var closing = new string(' ', 4 * depth);

        if (value.ValueKind is JsonValueKind.Null or JsonValueKind.Undefined) return "null";
        if (target == typeof(string)) return Literal(value.ValueKind == JsonValueKind.String ? value.GetString()! : value.GetRawText());
        if (target == typeof(bool)) return value.ValueKind == JsonValueKind.True ? "true" : "false";
        if (target == typeof(int)) return value.GetRawText().Trim('"');

        if (target == typeof(NhsukAttributes))
        {
            var entries = value.EnumerateObject().Select(p => $"{indent}[{Literal(p.Name)}] = {AttributeValue(p.Value)},");
            return "new NhsukAttributes\n" + closing + "{\n" + string.Join("\n", entries) + "\n" + closing + "}";
        }

        if (typeof(NhsukOptions).IsAssignableFrom(target))
        {
            if (value.ValueKind == JsonValueKind.String) return Literal(value.GetString()!); // implicit string conversion
            // `false` usually means "absent", but for [KeepFalse] options it means "leave this out".
            if (value.ValueKind == JsonValueKind.False && keepFalse) return $"new {target.Name} {{ IsFalse = true }}";
            if (value.ValueKind is JsonValueKind.True or JsonValueKind.False) return value.ValueKind == JsonValueKind.True ? "true" : "null";
            var props = target.GetProperties()
                .Select(p => (p, name: p.GetCustomAttribute<JsonPropertyNameAttribute>()?.Name))
                .Where(x => x.name is not null)
                .ToDictionary(x => x.name!, x => x.p);
            var entries = value.EnumerateObject()
                .Where(p => props.ContainsKey(p.Name))
                .Select(p => $"{indent}{props[p.Name].Name} = {CSharp(props[p.Name].PropertyType, p.Value, depth + 1, props[p.Name].GetCustomAttribute<KeepFalseAttribute>() is not null)},");
            return $"new {target.Name}\n{closing}{{\n{string.Join("\n", entries)}\n{closing}}}";
        }

        if (target.IsGenericType && target.GetGenericTypeDefinition() == typeof(List<>))
        {
            var itemType = target.GetGenericArguments()[0];
            var items = value.ValueKind == JsonValueKind.Array ? value.EnumerateArray().ToList() : [value];
            var entries = items.Select(i => $"{indent}{CSharp(itemType, i, depth + 1)},");
            return "[\n" + string.Join("\n", entries) + "\n" + closing + "]";
        }

        return value.GetRawText();
    }

    private static string AttributeValue(JsonElement value) => value.ValueKind switch
    {
        JsonValueKind.String => Literal(value.GetString()!),
        JsonValueKind.True => "true",
        JsonValueKind.False => "false",
        JsonValueKind.Object => $"new AttributeValue({(value.TryGetProperty("value", out var v) ? AttributeValue(v) : "null")}, Optional: {(value.TryGetProperty("optional", out var o) && o.ValueKind == JsonValueKind.True ? "true" : "false")})",
        _ => Literal(value.GetRawText()),
    };

    // Verbatim strings for multi-line HTML: Razor's parser accepts them inside attributes, raw literals it does not.
    private static string Literal(string s) =>
        s.Contains('\n') ? "@\"" + s.Replace("\"", "\"\"") + "\"" : "\"" + s.Replace("\\", "\\\\").Replace("\"", "\\\"") + "\"";
}
