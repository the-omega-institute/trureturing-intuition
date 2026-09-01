using System.Buffers;
using System.Text.Json;

namespace Trureturing.Intuition.Core;

internal static class TopologyAtlasEvidenceJsonExtensions
{
    internal static byte[] CopyValueBytes(this ref Utf8JsonReader reader) =>
        reader.HasValueSequence
            ? reader.ValueSequence.ToArray()
            : reader.ValueSpan.ToArray();

    internal static string? FirstPropertyName(
        this JsonElement value,
        IReadOnlyList<string> candidates)
    {
        foreach (string candidate in candidates)
        {
            if (value.TryGetProperty(candidate, out _))
            {
                return candidate;
            }
        }
        return null;
    }
}
