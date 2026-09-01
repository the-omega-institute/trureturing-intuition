using System.Globalization;
using System.Numerics;
using System.Text;
using System.Text.Json;

namespace Trureturing.Intuition.Core;

public sealed record TopologyCounterfactualBinding(
    string TruthReleaseDigest,
    string TopologyAtlasDigest,
    string? TopologyAtlasEvidenceDigest,
    string AlgorithmProfileDigest,
    string ProducerCommit);

public sealed record TopologyCounterfactualProjection(
    TopologyCounterfactualBinding Binding,
    bool Accepted,
    bool CycleRisk,
    IReadOnlyList<string> AffectedStableNodeIds,
    IReadOnlyList<string> TouchedClusterIds,
    BigInteger ReachabilityGain,
    BigInteger ReachabilityLoss,
    BigInteger PathCompression,
    BigInteger ShortestPathChangeCount,
    BigInteger NewCutBridgeCount,
    BigInteger RemovedCutBridgeCount,
    BigInteger NewInterfaceCount,
    BigInteger RemovedInterfaceCount,
    BigInteger CycleWitnessCount,
    BigInteger EditOperationCount);

public static class TopologyCounterfactualReader
{
    private const string Schema = "topology-counterfactual.v1";

    public static TopologyCounterfactualProjection Read(
        ReadOnlySpan<byte> bytes,
        TopologyCounterfactualBinding expected)
    {
        ArgumentNullException.ThrowIfNull(expected);
        ValidateBinding(expected, "expected counterfactual binding");
        Preflight(bytes);
        try
        {
            using JsonDocument document = JsonDocument.Parse(
                bytes.ToArray(),
                new JsonDocumentOptions
                {
                    AllowTrailingCommas = false,
                    CommentHandling = JsonCommentHandling.Disallow
                });
            JsonElement root = document.RootElement;
            Require(root.ValueKind == JsonValueKind.Object,
                "Topology counterfactual must be an object.");
            string schema = ReadRequiredStringAny(
                root,
                "$",
                "schema_version",
                "schema");
            RequireEqual(schema, Schema, "schema_version");

            var actual = new TopologyCounterfactualBinding(
                ReadRequiredStringAny(
                    root,
                    "$",
                    "truth_release_digest"),
                ReadRequiredStringAny(
                    root,
                    "$",
                    "topology_atlas_digest"),
                ReadOptionalStringAny(
                    root,
                    "topology_atlas_evidence_digest",
                    "evidence_digest"),
                ReadRequiredStringAny(
                    root,
                    "$",
                    "algorithm_profile_digest",
                    "counterfactual_profile_digest"),
                ReadRequiredStringAny(
                    root,
                    "$",
                    "producer_commit"));
            ValidateBinding(actual, "topology counterfactual");
            RequireEqual(
                actual.TruthReleaseDigest,
                expected.TruthReleaseDigest,
                "truth_release_digest");
            RequireEqual(
                actual.TopologyAtlasDigest,
                expected.TopologyAtlasDigest,
                "topology_atlas_digest");
            if (expected.TopologyAtlasEvidenceDigest is not null)
            {
                RequireEqual(
                    actual.TopologyAtlasEvidenceDigest,
                    expected.TopologyAtlasEvidenceDigest,
                    "topology_atlas_evidence_digest");
            }
            RequireEqual(
                actual.AlgorithmProfileDigest,
                expected.AlgorithmProfileDigest,
                "algorithm_profile_digest");
            RequireEqual(
                actual.ProducerCommit,
                expected.ProducerCommit,
                "producer_commit");

            bool accepted = ReadRequiredBooleanAny(root, "$", "accepted");
            bool cycleRisk = ReadRequiredBooleanAny(
                root,
                "$",
                "cycle_risk",
                "would_create_cycle");
            JsonElement analysis = root;
            if (root.TryGetProperty("analysis", out JsonElement candidateAnalysis))
            {
                if (candidateAnalysis.ValueKind == JsonValueKind.Null)
                {
                    Require(!accepted,
                        "Accepted topology counterfactual requires analysis.");
                }
                else
                {
                    Require(candidateAnalysis.ValueKind == JsonValueKind.Object,
                        "analysis must be an object or null.");
                    analysis = candidateAnalysis;
                }
            }
            Require(!(accepted && cycleRisk),
                "A cycle-risk counterfactual cannot be accepted.");

            string[] affected = ReadStringArrayMetric(
                analysis,
                "affected_stable_node_ids",
                "affected_node_ids");
            string[] touched = ReadStringArrayMetric(
                analysis,
                "touched_cluster_ids",
                "affected_cluster_ids");
            return new TopologyCounterfactualProjection(
                actual,
                accepted,
                cycleRisk,
                affected,
                touched,
                ReadCountMetric(
                    analysis,
                    "reachable_pair_gain",
                    "reachable_pairs_added",
                    "reachable_pairs_gained"),
                ReadCountMetric(
                    analysis,
                    "reachable_pair_loss",
                    "reachable_pairs_removed",
                    "reachable_pairs_lost"),
                ReadPathCompression(analysis),
                ReadCountMetric(
                    analysis,
                    "shortest_path_change_count",
                    "shortest_path_changes"),
                ReadCountMetric(
                    analysis,
                    "new_cut_bridge_count",
                    "new_cut_bridges"),
                ReadCountMetric(
                    analysis,
                    "removed_cut_bridge_count",
                    "removed_cut_bridges"),
                ReadCountMetric(
                    analysis,
                    "new_interface_count",
                    "new_interface_hypotheses",
                    "new_cross_cluster_interfaces"),
                ReadCountMetric(
                    analysis,
                    "removed_interface_count",
                    "removed_interface_hypotheses",
                    "removed_cross_cluster_interfaces"),
                ReadCountMetric(
                    root,
                    "cycle_witness_count",
                    "cycle_witnesses",
                    "cycle_path"),
                ReadCountMetric(
                    root,
                    "edit_operation_count",
                    "operations",
                    "graph_patch"));
        }
        catch (JsonException exception)
        {
            throw new InvalidDataException(
                "Topology counterfactual is malformed JSON.",
                exception);
        }
    }

    private static BigInteger ReadPathCompression(JsonElement root)
    {
        if (TryFindProperty(
                root,
                [
                    "path_compression",
                    "path_compression_total",
                    "total_saved_hops",
                    "total_distance_reduction"
                ],
                out JsonElement value))
        {
            BigInteger direct = CountValue(value, "path_compression");
            if (direct > 0 || value.ValueKind == JsonValueKind.Number)
            {
                return direct;
            }
            if (value.ValueKind == JsonValueKind.Object)
            {
                foreach (string name in new[]
                {
                    "total_saved_hops",
                    "total_reduction",
                    "saved_hops",
                    "distance_reduction"
                })
                {
                    if (value.TryGetProperty(name, out JsonElement child))
                    {
                        return CountValue(child, $"path_compression.{name}");
                    }
                }
            }
        }
        if (TryFindProperty(
                root,
                ["shortest_path_changes"],
                out JsonElement changes) &&
            changes.ValueKind == JsonValueKind.Array)
        {
            BigInteger total = BigInteger.Zero;
            foreach (JsonElement change in changes.EnumerateArray())
            {
                if (change.ValueKind != JsonValueKind.Object) continue;
                BigInteger? before = ReadOptionalIntegerAny(
                    change,
                    "before_distance",
                    "old_distance",
                    "from_distance",
                    "previous_length");
                BigInteger? after = ReadOptionalIntegerAny(
                    change,
                    "after_distance",
                    "new_distance",
                    "to_distance",
                    "current_length");
                if (before is not null && after is not null && before > after)
                {
                    total += before.Value - after.Value;
                }
            }
            return total;
        }
        return BigInteger.Zero;
    }

    private static BigInteger ReadCountMetric(
        JsonElement root,
        params string[] names)
    {
        return TryFindProperty(root, names, out JsonElement value)
            ? CountValue(value, names[0])
            : BigInteger.Zero;
    }

    private static BigInteger CountValue(JsonElement value, string path)
    {
        if (value.ValueKind == JsonValueKind.Number)
        {
            string raw = value.GetRawText();
            Require(
                BigInteger.TryParse(
                    raw,
                    NumberStyles.AllowLeadingSign,
                    CultureInfo.InvariantCulture,
                    out BigInteger result) && result >= 0,
                $"{path} must be a non-negative integer.");
            return result;
        }
        if (value.ValueKind == JsonValueKind.Array)
        {
            return new BigInteger(value.GetArrayLength());
        }
        if (value.ValueKind == JsonValueKind.Object)
        {
            foreach (string name in new[] { "count", "total", "value" })
            {
                if (value.TryGetProperty(name, out JsonElement child))
                {
                    return CountValue(child, $"{path}.{name}");
                }
            }
        }
        if (value.ValueKind == JsonValueKind.Null) return BigInteger.Zero;
        throw new InvalidDataException(
            $"{path} cannot be projected as a non-negative count.");
    }

    private static string[] ReadStringArrayMetric(
        JsonElement root,
        params string[] names)
    {
        if (!TryFindProperty(root, names, out JsonElement value)) return [];
        Require(value.ValueKind == JsonValueKind.Array,
            $"{names[0]} must be an array.");
        var result = new List<string>();
        var seen = new HashSet<string>(StringComparer.Ordinal);
        foreach (JsonElement item in value.EnumerateArray())
        {
            Require(item.ValueKind == JsonValueKind.String,
                $"{names[0]} must contain strings.");
            string text = item.GetString()!;
            Require(text.Length > 0, $"{names[0]} contains an empty identity.");
            Require(seen.Add(text), $"{names[0]} contains duplicate '{text}'.");
            result.Add(text);
        }
        string[] sorted = result.Order(StringComparer.Ordinal).ToArray();
        Require(result.SequenceEqual(sorted, StringComparer.Ordinal),
            $"{names[0]} must be strictly ordinal-sorted.");
        return result.ToArray();
    }

    private static BigInteger? ReadOptionalIntegerAny(
        JsonElement root,
        params string[] names)
    {
        foreach (string name in names)
        {
            if (!root.TryGetProperty(name, out JsonElement value)) continue;
            if (value.ValueKind == JsonValueKind.Null) return null;
            return CountValue(value, name);
        }
        return null;
    }

    private static bool TryFindProperty(
        JsonElement value,
        IReadOnlyCollection<string> names,
        out JsonElement result)
    {
        if (value.ValueKind == JsonValueKind.Object)
        {
            foreach (JsonProperty property in value.EnumerateObject())
            {
                if (names.Contains(property.Name, StringComparer.Ordinal))
                {
                    result = property.Value;
                    return true;
                }
            }
            foreach (JsonProperty property in value.EnumerateObject())
            {
                if (TryFindProperty(property.Value, names, out result)) return true;
            }
        }
        else if (value.ValueKind == JsonValueKind.Array)
        {
            foreach (JsonElement item in value.EnumerateArray())
            {
                if (TryFindProperty(item, names, out result)) return true;
            }
        }
        result = default;
        return false;
    }

    private static string ReadRequiredStringAny(
        JsonElement root,
        string path,
        params string[] names)
    {
        string? value = ReadOptionalStringAny(root, names);
        return !string.IsNullOrEmpty(value)
            ? value
            : throw new InvalidDataException(
                $"{path} is missing required string {string.Join(" or ", names)}.");
    }

    private static string? ReadOptionalStringAny(
        JsonElement root,
        params string[] names)
    {
        foreach (string name in names)
        {
            if (!root.TryGetProperty(name, out JsonElement value)) continue;
            if (value.ValueKind == JsonValueKind.Null) return null;
            Require(value.ValueKind == JsonValueKind.String,
                $"{name} must be a string or null.");
            string? result = value.GetString();
            Require(!string.IsNullOrEmpty(result), $"{name} must not be empty.");
            return result;
        }
        return null;
    }

    private static bool ReadRequiredBooleanAny(
        JsonElement root,
        string path,
        params string[] names)
    {
        foreach (string name in names)
        {
            if (!root.TryGetProperty(name, out JsonElement value)) continue;
            return value.ValueKind switch
            {
                JsonValueKind.True => true,
                JsonValueKind.False => false,
                _ => throw new InvalidDataException($"{path}.{name} must be boolean.")
            };
        }
        throw new InvalidDataException(
            $"{path} is missing boolean {string.Join(" or ", names)}.");
    }

    private static void ValidateBinding(
        TopologyCounterfactualBinding value,
        string source)
    {
        RequireDigest(value.TruthReleaseDigest, $"{source} truth release");
        RequireDigest(value.TopologyAtlasDigest, $"{source} Topology Atlas");
        if (value.TopologyAtlasEvidenceDigest is not null)
        {
            RequireDigest(
                value.TopologyAtlasEvidenceDigest,
                $"{source} Topology Atlas evidence");
        }
        RequireDigest(value.AlgorithmProfileDigest, $"{source} algorithm profile");
        Require(
            value.ProducerCommit.Length == 40 && IsLowerHex(value.ProducerCommit),
            $"{source} producer_commit must be 40 lowercase hexadecimal characters.");
    }

    private static void Preflight(ReadOnlySpan<byte> bytes)
    {
        try
        {
            var reader = new Utf8JsonReader(
                bytes,
                new JsonReaderOptions
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
                        Require(stack.Count > 0 && stack.Peek() is not null,
                            "Topology counterfactual contains a property outside an object.");
                        string name = reader.GetString()
                            ?? throw new InvalidDataException(
                                "Topology counterfactual contains a null property name.");
                        Require(stack.Peek()!.Add(name),
                            $"Topology counterfactual repeats property '{name}'.");
                        break;
                    case JsonTokenType.EndObject:
                    case JsonTokenType.EndArray:
                        Require(stack.Count > 0,
                            "Topology counterfactual contains an unbalanced container.");
                        stack.Pop();
                        break;
                    case JsonTokenType.Number:
                        string raw = Encoding.UTF8.GetString(
                            reader.HasValueSequence
                                ? reader.ValueSequence.ToArray()
                                : reader.ValueSpan);
                        Require(
                            !raw.Contains('.', StringComparison.Ordinal) &&
                            !raw.Contains('e', StringComparison.OrdinalIgnoreCase),
                            "Topology counterfactual forbids floating numeric lexemes.");
                        break;
                }
            }
            Require(stack.Count == 0,
                "Topology counterfactual contains an unclosed container.");
        }
        catch (JsonException exception)
        {
            throw new InvalidDataException(
                "Topology counterfactual is malformed JSON.",
                exception);
        }
    }

    private static void RequireDigest(string value, string name)
    {
        Require(
            value.Length == 71 &&
            value.StartsWith("sha256:", StringComparison.Ordinal) &&
            IsLowerHex(value[7..]),
            $"{name} must use sha256:<64 lowercase hex>.");
    }

    private static bool IsLowerHex(string value) => value.All(character =>
        character is >= '0' and <= '9' or >= 'a' and <= 'f');

    private static void RequireEqual(
        string? actual,
        string? expected,
        string name)
    {
        Require(StringComparer.Ordinal.Equals(actual, expected),
            $"Topology counterfactual {name} does not match expected coordinates.");
    }

    private static void Require(bool condition, string message)
    {
        if (!condition) throw new InvalidDataException(message);
    }
}
