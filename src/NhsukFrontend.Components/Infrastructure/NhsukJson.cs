using System.Collections.Concurrent;
using System.Reflection;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace NhsukFrontend.Components.Infrastructure;

/// <summary>
/// JSON settings for reading upstream-style option objects (fixtures, config files).
/// Upstream templates accept looser shapes than macro-options.json declares; these converters
/// accept the same shapes: strings for objects, <c>true</c> for "defaults", <c>false</c>/<c>null</c>
/// for "absent", and a single object where a list is expected.
/// </summary>
public static class NhsukJson
{
    public static readonly JsonSerializerOptions Options = Create();

    private static JsonSerializerOptions Create()
    {
        var options = new JsonSerializerOptions
        {
            NumberHandling = JsonNumberHandling.AllowReadingFromString,
            PropertyNameCaseInsensitive = false,
        };
        options.Converters.Add(new OptionsConverterFactory());
        options.Converters.Add(new OptionsListConverterFactory());
        options.MakeReadOnly(populateMissingResolver: true);
        return options;
    }

    private sealed class OptionsConverterFactory : JsonConverterFactory
    {
        public override bool CanConvert(Type t) => typeof(NhsukOptions).IsAssignableFrom(t) && !t.IsAbstract;

        public override JsonConverter CreateConverter(Type t, JsonSerializerOptions o) =>
            (JsonConverter)Activator.CreateInstance(typeof(OptionsConverter<>).MakeGenericType(t))!;
    }

    private sealed class OptionsConverter<T> : JsonConverter<T> where T : NhsukOptions, new()
    {
        private static readonly ConcurrentDictionary<string, PropertyInfo> Properties = new(
            typeof(T).GetProperties()
                .Select(p => (p, name: p.GetCustomAttribute<JsonPropertyNameAttribute>()?.Name))
                .Where(x => x.name is not null)
                .ToDictionary(x => x.name!, x => x.p));

        private static readonly MethodInfo? Shorthand = typeof(T).GetMethod("FromShorthand", BindingFlags.Public | BindingFlags.Static);

        public override bool HandleNull => true;

        public override T? Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
        {
            switch (reader.TokenType)
            {
                case JsonTokenType.Null:
                case JsonTokenType.False:
                    return null;
                case JsonTokenType.True:
                    return new T { IsTrue = true };
                case JsonTokenType.String when Shorthand is not null:
                    return (T)Shorthand.Invoke(null, [reader.GetString()!])!;
                case JsonTokenType.StartObject:
                    var result = new T();
                    using (var doc = JsonDocument.ParseValue(ref reader))
                    {
                        foreach (var prop in doc.RootElement.EnumerateObject())
                        {
                            if (!Properties.TryGetValue(prop.Name, out var info)) continue; // unknown options are ignored, as in Nunjucks
                            info.SetValue(result, prop.Value.Deserialize(info.PropertyType, options));
                        }
                    }
                    return result;
                default:
                    throw new JsonException($"Cannot read {typeof(T).Name} from {reader.TokenType}.");
            }
        }

        public override void Write(Utf8JsonWriter writer, T value, JsonSerializerOptions options) =>
            throw new NotSupportedException();
    }

    private sealed class OptionsListConverterFactory : JsonConverterFactory
    {
        public override bool CanConvert(Type t) =>
            t.IsGenericType && t.GetGenericTypeDefinition() == typeof(List<>) && typeof(NhsukOptions).IsAssignableFrom(t.GetGenericArguments()[0]);

        public override JsonConverter CreateConverter(Type t, JsonSerializerOptions o) =>
            (JsonConverter)Activator.CreateInstance(typeof(OptionsListConverter<>).MakeGenericType(t.GetGenericArguments()[0]))!;
    }

    private sealed class OptionsListConverter<T> : JsonConverter<List<T?>> where T : NhsukOptions
    {
        public override List<T?>? Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
        {
            if (reader.TokenType == JsonTokenType.Null) return null;
            if (reader.TokenType == JsonTokenType.StartObject) return [JsonSerializer.Deserialize<T>(ref reader, options)];

            var list = new List<T?>();
            if (reader.TokenType != JsonTokenType.StartArray) throw new JsonException();
            while (reader.Read() && reader.TokenType != JsonTokenType.EndArray)
            {
                list.Add(JsonSerializer.Deserialize<T>(ref reader, options));
            }
            return list;
        }

        public override void Write(Utf8JsonWriter writer, List<T?> value, JsonSerializerOptions options) =>
            throw new NotSupportedException();
    }
}

/// <summary>Reads the <c>attributes</c> option in all the forms upstream supports.</summary>
public sealed class NhsukAttributesJsonConverter : JsonConverter<NhsukAttributes>
{
    public override NhsukAttributes? Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
    {
        if (reader.TokenType == JsonTokenType.Null) return null;
        using var doc = JsonDocument.ParseValue(ref reader);
        if (doc.RootElement.ValueKind != JsonValueKind.Object) return null;

        var result = new NhsukAttributes();
        foreach (var prop in doc.RootElement.EnumerateObject())
        {
            result[prop.Name] = prop.Value.ValueKind == JsonValueKind.Object
                ? new AttributeValue(
                    Scalar(prop.Value.TryGetProperty("value", out var v) ? v : default),
                    prop.Value.TryGetProperty("optional", out var opt) && opt.ValueKind == JsonValueKind.True,
                    prop.Value.TryGetProperty("type", out var type) ? type.GetString() : null)
                : Scalar(prop.Value);
        }
        return result;
    }

    private static object? Scalar(JsonElement e) => e.ValueKind switch
    {
        JsonValueKind.Undefined or JsonValueKind.Null => null,
        JsonValueKind.String => e.GetString(),
        JsonValueKind.True => true,
        JsonValueKind.False => false,
        JsonValueKind.Number => e.GetRawText(),
        _ => e.Clone(),
    };

    public override void Write(Utf8JsonWriter writer, NhsukAttributes value, JsonSerializerOptions options) =>
        throw new NotSupportedException();
}
