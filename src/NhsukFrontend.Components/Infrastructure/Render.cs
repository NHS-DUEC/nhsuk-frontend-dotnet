using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Rendering;

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

    /// <summary>
    /// Port of upstream's <c>nhsukSlot</c> macro (<c>macros/slot.njk</c>): the text or HTML at one named slot.
    /// </summary>
    public static void Slot(RenderTreeBuilder builder, string name, NhsukSlots? slots)
    {
        var slot = name switch
        {
            "before" => slots?.Before,
            "start" => slots?.Start,
            "end" => slots?.End,
            "after" => slots?.After,
            _ => null,
        };
        if (slot is null || !(Nj.Truthy(slot.Text) || Nj.Truthy(slot.Html))) return;

        if (name == "after") builder.AddMarkupContent(0, "\n");
        if (Nj.Truthy(slot.Html)) builder.AddMarkupContent(1, slot.Html);
        else builder.AddContent(2, slot.Text);
        if (name == "before") builder.AddMarkupContent(3, "\n");
    }

    /// <summary>
    /// Port of upstream's <c>nhsukContent</c> macro (<c>macros/content.njk</c>): text or HTML,
    /// with optional visually hidden content and slots around it.
    /// </summary>
    public static RenderFragment Content(
        string? text = null,
        string? html = null,
        RenderFragment? caller = null,
        VisuallyHiddenOptions? visuallyHidden = null,
        string? visuallyHiddenText = null,
        NhsukSlots? slots = null) => builder =>
    {
        // Upstream ignores visually hidden text of true or false (options read from JSON arrive as these strings).
        var hidden = visuallyHidden ?? new VisuallyHiddenOptions { Text = visuallyHiddenText is "true" or "false" ? null : visuallyHiddenText };
        var hasContent = caller is not null || Nj.Truthy(html) || Nj.Truthy(text);
        var hasContentSlots = HasSlot(slots?.Start) || HasSlot(slots?.End);
        var hasHidden = Nj.Truthy(hidden.Html) || Nj.Truthy(hidden.Text);
        var placement = Nj.Truthy(hidden.Placement) ? hidden.Placement : null;

        // A space separates the hidden content from the content it accompanies, unless slots say otherwise.
        var hiddenSlots = hidden.Slots ?? new NhsukSlots
        {
            Start = hasContent && placement is "end" or null ? new NhsukSlot { Text = " " } : null,
            End = hasContent && placement == "start" ? new NhsukSlot { Text = " " } : null,
        };

        RenderFragment hiddenFragment = VisuallyHidden(new VisuallyHiddenOptions
        {
            Id = hidden.Id,
            Text = hidden.Text,
            Html = hidden.Html,
            Classes = hidden.Classes,
            Attributes = hidden.Attributes,
            Element = hidden.Element,
            Slots = hiddenSlots,
        });

        builder.OpenRegion(0);
        Slot(builder, "before", slots);
        builder.CloseRegion();

        if (hasContent || hasHidden)
        {
            if (placement == "before")
            {
                builder.AddContent(1, hiddenFragment);
                if (hasContent || hasContentSlots) builder.AddMarkupContent(2, "\n");
            }

            builder.OpenRegion(3);
            Slot(builder, "start", slots);
            builder.CloseRegion();

            if (placement == "start") builder.AddContent(4, hiddenFragment);

            if (caller is not null) builder.AddContent(5, caller);
            else if (Nj.Truthy(html)) builder.AddMarkupContent(6, html);
            else if (Nj.Truthy(text)) builder.AddContent(7, text);

            if (placement == "end" || (hasHidden && placement is null)) builder.AddContent(8, hiddenFragment);

            builder.OpenRegion(9);
            Slot(builder, "end", slots);
            builder.CloseRegion();

            if (placement == "after")
            {
                if (hasContent || hasContentSlots) builder.AddMarkupContent(10, "\n");
                builder.AddContent(11, hiddenFragment);
            }
        }

        builder.OpenRegion(12);
        Slot(builder, "after", slots);
        builder.CloseRegion();
    };

    private static bool HasSlot(NhsukSlot? slot) => slot is not null && (Nj.Truthy(slot.Text) || Nj.Truthy(slot.Html));

    /// <summary>Port of <c>components/visually-hidden/template.njk</c>.</summary>
    public static RenderFragment VisuallyHidden(
        VisuallyHiddenOptions options,
        RenderFragment? caller = null,
        IReadOnlyDictionary<string, object>? additionalAttributes = null) => builder =>
    {
        builder.OpenRegion(0);
        Slot(builder, "before", options.Slots);
        builder.CloseRegion();

        if (caller is not null || Nj.Truthy(options.Html) || Nj.Truthy(options.Text))
        {
            builder.OpenElement(1, Nj.Truthy(options.Element) ? options.Element!.ToLowerInvariant() : "span");
            builder.AddMultipleAttributes(2, new Attrs()
                .Add("class", Nj.Append("nhsuk-u-visually-hidden", options.Classes))
                .Optional("id", Nj.Truthy(options.Id) ? options.Id : null)
                .AddUser(options.Attributes, additionalAttributes));

            builder.OpenRegion(3);
            Slot(builder, "start", options.Slots);
            builder.CloseRegion();

            if (caller is not null) builder.AddContent(4, caller);
            else if (Nj.Truthy(options.Html)) builder.AddMarkupContent(5, options.Html);
            else builder.AddContent(6, options.Text);

            builder.OpenRegion(7);
            Slot(builder, "end", options.Slots);
            builder.CloseRegion();
            builder.CloseElement();
        }

        builder.OpenRegion(8);
        Slot(builder, "after", options.Slots);
        builder.CloseRegion();
    };

    /// <summary>
    /// Port of <c>components/link/template.njk</c>. Most components that render a link or a link-styled
    /// button (back link, action link, card actions, header and footer links) go through this.
    /// </summary>
    public static RenderFragment Link(
        LinkOptions options,
        RenderFragment? caller = null,
        IReadOnlyDictionary<string, object>? additionalAttributes = null) => builder =>
    {
        // className: false (or "") asks for no base class at all; a missing value uses the default.
        var noClassName = options.ClassName is "" or "false";
        var className = noClassName ? "nhsuk-link" : options.ClassName ?? "nhsuk-link";
        var classNames = noClassName ? "" : className;
        if (options.Variant == "reverse" && !Nj.Contains(options.Classes, className + "--reverse"))
            classNames = (classNames + " " + className + "--reverse").Trim();
        if (Nj.Truthy(options.Classes)) classNames = (classNames + " " + options.Classes).Trim();

#pragma warning disable CS0618 // element is deprecated upstream but still honoured
        var element = Nj.Truthy(options.Element) ? options.Element!.ToLowerInvariant() : Nj.Truthy(options.Type) ? "button" : "a";
#pragma warning restore CS0618

        var userAttributes = options.Attributes;
        var hasTarget = userAttributes?.ContainsKey("target") == true || additionalAttributes?.ContainsKey("target") == true;
        var hasRel = userAttributes?.ContainsKey("rel") == true || additionalAttributes?.ContainsKey("rel") == true;
        var openInNewTab = options.OpenInNewTab == true
            || (userAttributes is not null && userAttributes.TryGetValue("target", out var target) && target as string == "_blank")
            || (additionalAttributes is not null && additionalAttributes.TryGetValue("target", out var extraTarget) && extraTarget as string == "_blank");

        var attrs = new Attrs()
            .Optional("class", classNames.Length > 0 ? classNames : null)
            .Optional("id", options.Id)
            .Optional("name", element == "button" ? options.Name : null)
            .Optional("data-module", options.ModuleName);

        if (element == "button")
            attrs.Add("type", Nj.Default(options.Type, "submit")).Optional("value", options.Value);
        else if (element == "a")
            attrs.Add("href", Nj.Default(options.Href, "#"))
                .Optional("rel", openInNewTab && !hasRel ? "noopener noreferrer" : null)
                .Optional("target", openInNewTab && !hasTarget ? "_blank" : null);

        attrs.Optional("aria-current", options.Current is null or "false" ? null : options.Current)
            .Optional("aria-label", options.AriaLabel)
            .AddUser(userAttributes, additionalAttributes);

        builder.OpenRegion(0);
        Slot(builder, "before", options.Slots);
        builder.CloseRegion();

        if (caller is not null || Nj.Truthy(options.Html) || Nj.Truthy(options.Text))
        {
            builder.OpenElement(1, element);
            builder.AddMultipleAttributes(2, attrs);
            builder.AddContent(3, Content(
                text: options.Text,
                html: options.Html,
                caller: caller,
                visuallyHidden: Nj.Truthy(options.AriaLabel) ? null : options.VisuallyHidden,
                visuallyHiddenText: Nj.Truthy(options.AriaLabel) ? null : options.VisuallyHiddenText,
                slots: new NhsukSlots { Start = options.Slots?.Start, End = options.Slots?.End }));
            builder.CloseElement();
        }

        builder.OpenRegion(4);
        Slot(builder, "after", options.Slots);
        builder.CloseRegion();
    };

    /// <summary>
    /// Replaces a placeholder in the text and markup of child content, for templates that post-process
    /// their call block (the environment banner's <c>%{name}</c>). Components inside the child content
    /// are left as they are.
    /// </summary>
    public static RenderFragment ReplaceInMarkup(RenderFragment content, string placeholder, string replacementHtml) => builder =>
    {
#pragma warning disable BL0006 // RenderTree types: read-only inspection of our own child content
        using var probe = new RenderTreeBuilder();
        content(probe);
        var frames = probe.GetFrames();
        var simple = true;
        var html = new System.Text.StringBuilder();
        for (var i = 0; i < frames.Count && simple; i++)
        {
            var frame = frames.Array[i];
            switch (frame.FrameType)
            {
                case Microsoft.AspNetCore.Components.RenderTree.RenderTreeFrameType.Markup:
                    html.Append(frame.MarkupContent);
                    break;
                case Microsoft.AspNetCore.Components.RenderTree.RenderTreeFrameType.Text:
                    html.Append(System.Net.WebUtility.HtmlEncode(frame.TextContent));
                    break;
                default:
                    simple = false;
                    break;
            }
        }
#pragma warning restore BL0006

        if (simple) builder.AddMarkupContent(0, html.ToString().Replace(placeholder, replacementHtml));
        else builder.AddContent(1, content);
    };
}
