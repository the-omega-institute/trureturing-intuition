using System.Buffers;
using System.Text.Json;

namespace Trureturing.Intuition.Core;

internal delegate bool TryGetJsonProperty(
    string propertyName,
    out JsonElement value);

internal static class TopologyAtlasEvidenceCompatibility
{
    internal static string? FirstOrDefault(
        this IEnumerable<string> source,
        TryGetJsonProperty predicate)
    {
        foreach (string value in source)
        {
            if (predicate(value, out _))
            {
                return value;
            }
        }
        return null;
    }

    internal static byte[] ToArray(this ReadOnlySequence<byte> value)
    {
        var result = new byte[checked((int)value.Length)];
        value.CopyTo(result);
        return result;
    }
}
