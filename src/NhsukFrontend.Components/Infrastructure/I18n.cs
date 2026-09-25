namespace NhsukFrontend.Components.Infrastructure;

/// <summary>Port of upstream's <c>nhsukI18nAttributes</c> macro: translated text as <c>data-i18n.*</c> attributes for the JavaScript.</summary>
public static class I18n
{
    /// <summary>A single message: <c>data-i18n.{key}="…"</c>. Nothing when empty.</summary>
    public static void Message(Attrs attrs, string key, string? message)
    {
        if (Nj.Truthy(message)) attrs.Add("data-i18n." + key, message);
    }

    /// <summary>Messages by plural rule: <c>data-i18n.{key}.{rule}="…"</c>.</summary>
    public static void Messages(Attrs attrs, string key, NhsukAttributes? messages)
    {
        if (messages is null) return;
        foreach (var (rule, message) in messages) attrs.Add($"data-i18n.{key}.{rule}", message);
    }
}
