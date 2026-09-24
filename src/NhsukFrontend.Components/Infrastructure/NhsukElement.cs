using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Rendering;

namespace NhsukFrontend.Components.Infrastructure;

/// <summary>
/// Renders an element whose tag name is only known at runtime, like upstream's
/// <c>&lt;{{ element }}&gt;</c>. Adds no markup of its own.
/// </summary>
public sealed class NhsukElement : ComponentBase
{
    [Parameter, EditorRequired] public string Tag { get; set; } = "div";
    [Parameter] public IEnumerable<KeyValuePair<string, object>>? Attributes { get; set; }
    [Parameter] public RenderFragment? ChildContent { get; set; }

    protected override void BuildRenderTree(RenderTreeBuilder builder)
    {
        builder.OpenElement(0, Tag);
        if (Attributes is not null) builder.AddMultipleAttributes(1, Attributes);
        builder.AddContent(2, ChildContent);
        builder.CloseElement();
    }
}

/// <summary>
/// The caller / html / text fallback every upstream template repeats:
/// <c>{% if caller %}…{% elif params.html %}…{% elif params.text %}…{% endif %}</c>.
/// </summary>
public sealed class NhsukContent : ComponentBase
{
    [Parameter] public RenderFragment? Caller { get; set; }
    [Parameter] public string? Html { get; set; }
    [Parameter] public string? Text { get; set; }

    protected override void BuildRenderTree(RenderTreeBuilder builder)
    {
        if (Caller is not null) builder.AddContent(0, Caller);
        else if (Nj.Truthy(Html)) builder.AddMarkupContent(1, Html);
        else if (Nj.Truthy(Text)) builder.AddContent(2, Text);
    }
}
