using System.Text.Json.Serialization;

namespace NhsukFrontend.Components.Infrastructure;

/// <summary>
/// The <c>slots</c> option: named points where a component accepts extra text or HTML,
/// read by upstream's <c>nhsukSlot</c> macro.
/// </summary>
public sealed class NhsukSlots : NhsukOptions
{
    /// <summary>Content placed before the component.</summary>
    [JsonPropertyName("before")] public NhsukSlot? Before { get; set; }

    /// <summary>Content placed inside the component, before its own content.</summary>
    [JsonPropertyName("start")] public NhsukSlot? Start { get; set; }

    /// <summary>Content placed inside the component, after its own content.</summary>
    [JsonPropertyName("end")] public NhsukSlot? End { get; set; }

    /// <summary>Content placed after the component.</summary>
    [JsonPropertyName("after")] public NhsukSlot? After { get; set; }
}

/// <summary>One slot: text or HTML. Upstream also accepts a plain string, used as <c>text</c>.</summary>
public sealed class NhsukSlot : NhsukOptions, IShorthandOptions<NhsukSlot>
{
    /// <summary>Text for the slot. Ignored when <see cref="Html"/> is set.</summary>
    [JsonPropertyName("text")] public string? Text { get; set; }

    /// <summary>HTML for the slot.</summary>
    [JsonPropertyName("html")] public string? Html { get; set; }

    public static NhsukSlot FromShorthand(string value) => new() { Text = value, IsShorthand = true };
    public static implicit operator NhsukSlot(string value) => FromShorthand(value);
}
