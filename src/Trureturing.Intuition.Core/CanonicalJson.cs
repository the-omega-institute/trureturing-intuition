using System.Security.Cryptography;
using System.Text;
using System.Text.Encodings.Web;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace Trureturing.Intuition.Core;

public static class CanonicalJson
{
    public static readonly JsonSerializerOptions Options = CreateOptions();

    private static JsonSerializerOptions CreateOptions()
    {
        var options = new JsonSerializerOptions
        {
            PropertyNamingPolicy = JsonNamingPolicy.SnakeCaseLower,
            DictionaryKeyPolicy = JsonNamingPolicy.SnakeCaseLower,
            PropertyNameCaseInsensitive = false,
            WriteIndented = false,
            Encoder = JavaScriptEncoder.Default,
            UnmappedMemberHandling = JsonUnmappedMemberHandling.Disallow
        };
        options.Converters.Add(new JsonStringEnumConverter(JsonNamingPolicy.SnakeCaseLower, allowIntegerValues: false));
        return options;
    }

    public static byte[] Serialize<T>(T value)
    {
        var element = JsonSerializer.SerializeToElement(value, Options);
        using var stream = new MemoryStream();
        using (var writer = new Utf8JsonWriter(stream, new JsonWriterOptions { Indented = false }))
        {
            WriteElement(writer, element);
        }
        stream.WriteByte((byte)'\n');
        return stream.ToArray();
    }

    public static string Sha256Reference(ReadOnlySpan<byte> bytes) =>
        "sha256:" + Convert.ToHexStringLower(SHA256.HashData(bytes));

    public static T DeserializeStrict<T>(ReadOnlySpan<byte> bytes)
    {
        StrictJson.Preflight(bytes);
        return JsonSerializer.Deserialize<T>(bytes, Options)
            ?? throw new InvalidOperationException($"JSON did not deserialize to {typeof(T).Name}.");
    }

    public static T DeserializeCanonical<T>(ReadOnlySpan<byte> bytes)
    {
        var value = DeserializeStrict<T>(bytes);
        if (!bytes.SequenceEqual(Serialize(value)))
        {
            throw new InvalidOperationException("Artifact bytes are not canonical JSON.");
        }
        return value;
    }

    private static void WriteElement(Utf8JsonWriter writer, JsonElement element)
    {
        switch (element.ValueKind)
        {
            case JsonValueKind.Object:
                writer.WriteStartObject();
                foreach (var property in element.EnumerateObject().OrderBy(static item => item.Name, StringComparer.Ordinal))
                {
                    writer.WritePropertyName(property.Name);
                    WriteElement(writer, property.Value);
                }
                writer.WriteEndObject();
                break;
            case JsonValueKind.Array:
                writer.WriteStartArray();
                foreach (var item in element.EnumerateArray())
                {
                    WriteElement(writer, item);
                }
                writer.WriteEndArray();
                break;
            case JsonValueKind.String:
                writer.WriteStringValue(element.GetString());
                break;
            case JsonValueKind.Number:
                writer.WriteRawValue(element.GetRawText(), skipInputValidation: false);
                break;
            case JsonValueKind.True:
                writer.WriteBooleanValue(true);
                break;
            case JsonValueKind.False:
                writer.WriteBooleanValue(false);
                break;
            case JsonValueKind.Null:
                writer.WriteNullValue();
                break;
            default:
                throw new InvalidOperationException($"Unsupported JSON value kind {element.ValueKind}.");
        }
    }
}

public static class StrictJson
{
    public static void Preflight(ReadOnlySpan<byte> bytes)
    {
        var reader = new Utf8JsonReader(bytes, new JsonReaderOptions
        {
            AllowTrailingCommas = false,
            CommentHandling = JsonCommentHandling.Disallow
        });
        var stack = new Stack<HashSet<string>?>();
        while (reader.Read())
        {
            switch (reader.TokenType)
            {
                case JsonTokenType.StartObject:
                    stack.Push(new HashSet<string>(StringComparer.Ordinal));
                    break;
                case JsonTokenType.StartArray:
                    stack.Push(null);
                    break;
                case JsonTokenType.PropertyName:
                    if (stack.Count == 0 || stack.Peek() is not { } names)
                    {
                        throw new JsonException("Property appeared outside an object.");
                    }
                    var name = reader.GetString() ?? throw new JsonException("Null property name.");
                    if (!names.Add(name))
                    {
                        throw new JsonException($"Duplicate property '{name}'.");
                    }
                    break;
                case JsonTokenType.EndObject:
                case JsonTokenType.EndArray:
                    if (stack.Count == 0)
                    {
                        throw new JsonException("Unbalanced JSON container.");
                    }
                    stack.Pop();
                    break;
            }
        }
        if (stack.Count != 0)
        {
            throw new JsonException("Unclosed JSON container.");
        }
    }
}
