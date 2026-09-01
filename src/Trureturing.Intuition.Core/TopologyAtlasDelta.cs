using System.Globalization;
using System.Numerics;
using System.Text;
using System.Text.Json;

namespace Trureturing.Intuition.Core;

public sealed record TopologyAtlasDeltaBinding(
    string FromTruthReleaseDigest,
    string ToTruthReleaseDigest,
    string FromTopologyAtlasDigest,
    string ToTopologyAtlasDigest,
    string FromEvidenceDigest,
    string ToEvidenceDigest,
    string AlgorithmProfileDigest,
    string ProducerCommit);

public sealed record TopologyAtlasDeltaNodeTransitionReadModel(
    string StableNodeId,
    string Relation,
    string? FromNodeId,
    string? ToNodeId,
    bool SourcePathChanged,
    string? FromPrimaryRole,
    string? ToPrimaryRole,
    IReadOnlyList<string> AddedTraits,
    IReadOnlyList<string> RemovedTraits);

public sealed record TopologyAtlasDeltaEdgeTransitionReadModel(
    string StableDependencyId,
    string StableDependentId,
    string Relation,
    string? FromDependencyId,
    string? FromDependentId,
    string? ToDependencyId,
    string? ToDependentId);

public sealed record TopologyAtlasDeltaFrontierReadModel(
    IReadOnlyList<string> EnteredFrontier,
    IReadOnlyList<string> LeftFrontier);

public sealed record TopologyAtlasDeltaSummaryReadModel(
    BigInteger NodesAdded,
    BigInteger NodesRetired,
    BigInteger NodesRetained,
    BigInteger EdgesAdded,
    BigInteger EdgesRemoved,
    BigInteger EdgesRetained,
    BigInteger ClusterContinuations,
    BigInteger ClusterSplits,
    BigInteger ClusterMerges,
    BigInteger ClusterReorganizations,
    BigInteger ClustersNew,
    BigInteger ClustersRetired);

public sealed record TopologyAtlasDeltaReadModel(
    TopologyAtlasDeltaBinding Binding,
    IReadOnlyList<TopologyAtlasDeltaNodeTransitionReadModel> NodeTransitions,
    IReadOnlyList<TopologyAtlasDeltaEdgeTransitionReadModel> EdgeTransitions,
    IReadOnlyDictionary<string, BigInteger> ClusterLineageRelationCounts,
    TopologyAtlasDeltaFrontierReadModel FrontierDelta,
    TopologyAtlasDeltaSummaryReadModel Summary);

public static class TopologyAtlasDeltaReader
{
    private const string Schema = "topology-atlas-delta.v1";
    private static readonly IReadOnlySet<string> NodeRelations =
        new HashSet<string>(StringComparer.Ordinal)
        {
            "added",
            "retained",
            "retired"
        };
    private static readonly IReadOnlySet<string> EdgeRelations =
        new HashSet<string>(StringComparer.Ordinal)
        {
            "added",
            "retained",
            "removed"
        };
    private static readonly IReadOnlySet<string> ClusterRelations =
        new HashSet<string>(StringComparer.Ordinal)
        {
            "continuation",
            "split",
            "merge",
            "reorganization",
            "new",
            "retired"
        };

    public static TopologyAtlasDeltaReadModel Read(
        ReadOnlySpan<byte> bytes,
        TopologyAtlasDeltaBinding expected)
    {
        ArgumentNullException.ThrowIfNull(expected);
        ValidateBinding(expected, "expected Atlas delta binding");
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
            JsonElement root = RequireObject(
                document.RootElement,
                "$",
                "schema_version",
                "from_truth_release_digest",
                "to_truth_release_digest",
                "from_topology_atlas_digest",
                "to_topology_atlas_digest",
                "from_evidence_digest",
                "to_evidence_digest",
                "algorithm_profile_digest",
                "producer_commit",
                "node_transitions",
                "edge_transitions",
                "cluster_lineage",
                "frontier_delta",
                "summary");
            RequireEqual(
                ReadString(root, "schema_version", "$"),
                Schema,
                "schema_version");
            var actual = new TopologyAtlasDeltaBinding(
                ReadString(root, "from_truth_release_digest", "$"),
                ReadString(root, "to_truth_release_digest", "$"),
                ReadString(root, "from_topology_atlas_digest", "$"),
                ReadString(root, "to_topology_atlas_digest", "$"),
                ReadString(root, "from_evidence_digest", "$"),
                ReadString(root, "to_evidence_digest", "$"),
                ReadString(root, "algorithm_profile_digest", "$"),
                ReadString(root, "producer_commit", "$"));
            ValidateBinding(actual, "Topology Atlas delta");
            RequireEqual(
                actual.FromTruthReleaseDigest,
                expected.FromTruthReleaseDigest,
                "from_truth_release_digest");
            RequireEqual(
                actual.ToTruthReleaseDigest,
                expected.ToTruthReleaseDigest,
                "to_truth_release_digest");
            RequireEqual(
                actual.FromTopologyAtlasDigest,
                expected.FromTopologyAtlasDigest,
                "from_topology_atlas_digest");
            RequireEqual(
                actual.ToTopologyAtlasDigest,
                expected.ToTopologyAtlasDigest,
                "to_topology_atlas_digest");
            RequireEqual(
                actual.FromEvidenceDigest,
                expected.FromEvidenceDigest,
                "from_evidence_digest");
            RequireEqual(
                actual.ToEvidenceDigest,
                expected.ToEvidenceDigest,
                "to_evidence_digest");
            RequireEqual(
                actual.AlgorithmProfileDigest,
                expected.AlgorithmProfileDigest,
                "algorithm_profile_digest");
            RequireEqual(
                actual.ProducerCommit,
                expected.ProducerCommit,
                "producer_commit");

            TopologyAtlasDeltaNodeTransitionReadModel[] nodes = ReadNodes(
                root.GetProperty("node_transitions"));
            TopologyAtlasDeltaEdgeTransitionReadModel[] edges = ReadEdges(
                root.GetProperty("edge_transitions"));
            IReadOnlyDictionary<string, BigInteger> lineage = ReadLineage(
                root.GetProperty("cluster_lineage"));
            TopologyAtlasDeltaFrontierReadModel frontier = ReadFrontier(
                root.GetProperty("frontier_delta"));
            TopologyAtlasDeltaSummaryReadModel summary = ReadSummary(
                root.GetProperty("summary"));
            ValidateSummary(nodes, edges, lineage, summary);
            return new TopologyAtlasDeltaReadModel(
                actual,
                nodes,
                edges,
                lineage,
                frontier,
                summary);
        }
        catch (JsonException exception)
        {
            throw new InvalidDataException(
                "Topology Atlas delta is malformed JSON.",
                exception);
        }
    }

    private static TopologyAtlasDeltaNodeTransitionReadModel[] ReadNodes(
        JsonElement value)
    {
        RequireKind(value, JsonValueKind.Array, "$.node_transitions");
        var result = new List<TopologyAtlasDeltaNodeTransitionReadModel>();
        string? previous = null;
        int index = 0;
        foreach (JsonElement item in value.EnumerateArray())
        {
            string path = $"$.node_transitions[{index}]";
            JsonElement node = RequireObject(
                item,
                path,
                "stable_node_id",
                "relation",
                "from_node_id",
                "to_node_id",
                "source_path_changed",
                "from_primary_role",
                "to_primary_role",
                "added_traits",
                "removed_traits");
            string stable = ReadNonEmptyString(node, "stable_node_id", path);
            Require(previous is null ||
                StringComparer.Ordinal.Compare(previous, stable) < 0,
                "node_transitions must be strictly ordinal-sorted.");
            previous = stable;
            string relation = ReadNonEmptyString(node, "relation", path);
            Require(NodeRelations.Contains(relation),
                $"{path}.relation is unsupported.");
            string? from = ReadNullableString(
                node.GetProperty("from_node_id"),
                $"{path}.from_node_id");
            string? to = ReadNullableString(
                node.GetProperty("to_node_id"),
                $"{path}.to_node_id");
            if (relation == "added") Require(from is null && to is not null,
                $"{path} added relation has invalid endpoints.");
            if (relation == "retired") Require(from is not null && to is null,
                $"{path} retired relation has invalid endpoints.");
            if (relation == "retained") Require(from is not null && to is not null,
                $"{path} retained relation requires both endpoints.");
            result.Add(new TopologyAtlasDeltaNodeTransitionReadModel(
                stable,
                relation,
                from,
                to,
                ReadBoolean(node, "source_path_changed", path),
                ReadNullableString(
                    node.GetProperty("from_primary_role"),
                    $"{path}.from_primary_role"),
                ReadNullableString(
                    node.GetProperty("to_primary_role"),
                    $"{path}.to_primary_role"),
                ReadSortedStrings(node.GetProperty("added_traits"),
                    $"{path}.added_traits"),
                ReadSortedStrings(node.GetProperty("removed_traits"),
                    $"{path}.removed_traits")));
            index++;
        }
        return result.ToArray();
    }

    private static TopologyAtlasDeltaEdgeTransitionReadModel[] ReadEdges(
        JsonElement value)
    {
        RequireKind(value, JsonValueKind.Array, "$.edge_transitions");
        var result = new List<TopologyAtlasDeltaEdgeTransitionReadModel>();
        string? previous = null;
        int index = 0;
        foreach (JsonElement item in value.EnumerateArray())
        {
            string path = $"$.edge_transitions[{index}]";
            JsonElement edge = RequireObject(
                item,
                path,
                "stable_dependency_id",
                "stable_dependent_id",
                "relation",
                "from_dependency_id",
                "from_dependent_id",
                "to_dependency_id",
                "to_dependent_id");
            string dependency = ReadNonEmptyString(
                edge,
                "stable_dependency_id",
                path);
            string dependent = ReadNonEmptyString(
                edge,
                "stable_dependent_id",
                path);
            string key = dependency + "\u0000" + dependent;
            Require(previous is null ||
                StringComparer.Ordinal.Compare(previous, key) < 0,
                "edge_transitions must be strictly ordinal-sorted.");
            previous = key;
            string relation = ReadNonEmptyString(edge, "relation", path);
            Require(EdgeRelations.Contains(relation),
                $"{path}.relation is unsupported.");
            result.Add(new TopologyAtlasDeltaEdgeTransitionReadModel(
                dependency,
                dependent,
                relation,
                ReadNullableString(edge.GetProperty("from_dependency_id"),
                    $"{path}.from_dependency_id"),
                ReadNullableString(edge.GetProperty("from_dependent_id"),
                    $"{path}.from_dependent_id"),
                ReadNullableString(edge.GetProperty("to_dependency_id"),
                    $"{path}.to_dependency_id"),
                ReadNullableString(edge.GetProperty("to_dependent_id"),
                    $"{path}.to_dependent_id")));
            index++;
        }
        return result.ToArray();
    }

    private static IReadOnlyDictionary<string, BigInteger> ReadLineage(
        JsonElement value)
    {
        RequireKind(value, JsonValueKind.Array, "$.cluster_lineage");
        var counts = ClusterRelations.ToDictionary(
            relation => relation,
            _ => BigInteger.Zero,
            StringComparer.Ordinal);
        int index = 0;
        foreach (JsonElement item in value.EnumerateArray())
        {
            string path = $"$.cluster_lineage[{index}]";
            RequireKind(item, JsonValueKind.Object, path);
            if (!item.TryGetProperty("relation", out JsonElement relationValue) ||
                relationValue.ValueKind != JsonValueKind.String)
            {
                throw new InvalidDataException(
                    $"{path}.relation is missing or invalid.");
            }
            string relation = relationValue.GetString()!;
            Require(ClusterRelations.Contains(relation),
                $"{path}.relation is unsupported.");
            counts[relation] += BigInteger.One;
            index++;
        }
        return counts;
    }

    private static TopologyAtlasDeltaFrontierReadModel ReadFrontier(
        JsonElement value)
    {
        JsonElement frontier = RequireObject(
            value,
            "$.frontier_delta",
            "entered_frontier",
            "left_frontier");
        return new TopologyAtlasDeltaFrontierReadModel(
            ReadSortedStrings(
                frontier.GetProperty("entered_frontier"),
                "$.frontier_delta.entered_frontier"),
            ReadSortedStrings(
                frontier.GetProperty("left_frontier"),
                "$.frontier_delta.left_frontier"));
    }

    private static TopologyAtlasDeltaSummaryReadModel ReadSummary(
        JsonElement value)
    {
        JsonElement summary = RequireObject(
            value,
            "$.summary",
            "nodes_added",
            "nodes_retired",
            "nodes_retained",
            "edges_added",
            "edges_removed",
            "edges_retained",
            "cluster_continuations",
            "cluster_splits",
            "cluster_merges",
            "cluster_reorganizations",
            "clusters_new",
            "clusters_retired");
        return new TopologyAtlasDeltaSummaryReadModel(
            ReadNonNegative(summary, "nodes_added", "$.summary"),
            ReadNonNegative(summary, "nodes_retired", "$.summary"),
            ReadNonNegative(summary, "nodes_retained", "$.summary"),
            ReadNonNegative(summary, "edges_added", "$.summary"),
            ReadNonNegative(summary, "edges_removed", "$.summary"),
            ReadNonNegative(summary, "edges_retained", "$.summary"),
            ReadNonNegative(summary, "cluster_continuations", "$.summary"),
            ReadNonNegative(summary, "cluster_splits", "$.summary"),
            ReadNonNegative(summary, "cluster_merges", "$.summary"),
            ReadNonNegative(summary, "cluster_reorganizations", "$.summary"),
            ReadNonNegative(summary, "clusters_new", "$.summary"),
            ReadNonNegative(summary, "clusters_retired", "$.summary"));
    }

    private static void ValidateSummary(
        IReadOnlyList<TopologyAtlasDeltaNodeTransitionReadModel> nodes,
        IReadOnlyList<TopologyAtlasDeltaEdgeTransitionReadModel> edges,
        IReadOnlyDictionary<string, BigInteger> lineage,
        TopologyAtlasDeltaSummaryReadModel summary)
    {
        Require(summary.NodesAdded == nodes.Count(value => value.Relation == "added"),
            "summary.nodes_added disagrees with node_transitions.");
        Require(summary.NodesRetired == nodes.Count(value => value.Relation == "retired"),
            "summary.nodes_retired disagrees with node_transitions.");
        Require(summary.NodesRetained == nodes.Count(value => value.Relation == "retained"),
            "summary.nodes_retained disagrees with node_transitions.");
        Require(summary.EdgesAdded == edges.Count(value => value.Relation == "added"),
            "summary.edges_added disagrees with edge_transitions.");
        Require(summary.EdgesRemoved == edges.Count(value => value.Relation == "removed"),
            "summary.edges_removed disagrees with edge_transitions.");
        Require(summary.EdgesRetained == edges.Count(value => value.Relation == "retained"),
            "summary.edges_retained disagrees with edge_transitions.");
        Require(summary.ClusterContinuations == lineage["continuation"],
            "summary.cluster_continuations disagrees with cluster_lineage.");
        Require(summary.ClusterSplits == lineage["split"],
            "summary.cluster_splits disagrees with cluster_lineage.");
        Require(summary.ClusterMerges == lineage["merge"],
            "summary.cluster_merges disagrees with cluster_lineage.");
        Require(summary.ClusterReorganizations == lineage["reorganization"],
            "summary.cluster_reorganizations disagrees with cluster_lineage.");
        Require(summary.ClustersNew == lineage["new"],
            "summary.clusters_new disagrees with cluster_lineage.");
        Require(summary.ClustersRetired == lineage["retired"],
            "summary.clusters_retired disagrees with cluster_lineage.");
    }

    private static JsonElement RequireObject(
        JsonElement value,
        string path,
        params string[] properties)
    {
        RequireKind(value, JsonValueKind.Object, path);
        var expected = properties.ToHashSet(StringComparer.Ordinal);
        var actual = value.EnumerateObject()
            .Select(item => item.Name)
            .ToHashSet(StringComparer.Ordinal);
        string? missing = expected.Except(actual, StringComparer.Ordinal).FirstOrDefault();
        Require(missing is null, $"{path} is missing property '{missing}'.");
        string? unknown = actual.Except(expected, StringComparer.Ordinal).FirstOrDefault();
        Require(unknown is null, $"{path} contains unknown property '{unknown}'.");
        return value;
    }

    private static string ReadString(
        JsonElement parent,
        string name,
        string path)
    {
        JsonElement value = parent.GetProperty(name);
        RequireKind(value, JsonValueKind.String, $"{path}.{name}");
        return value.GetString()!;
    }

    private static string ReadNonEmptyString(
        JsonElement parent,
        string name,
        string path)
    {
        string value = ReadString(parent, name, path);
        Require(value.Length > 0, $"{path}.{name} must not be empty.");
        return value;
    }

    private static string? ReadNullableString(
        JsonElement value,
        string path)
    {
        if (value.ValueKind == JsonValueKind.Null) return null;
        RequireKind(value, JsonValueKind.String, path);
        string result = value.GetString()!;
        Require(result.Length > 0, $"{path} must not be empty.");
        return result;
    }

    private static bool ReadBoolean(
        JsonElement parent,
        string name,
        string path)
    {
        JsonElement value = parent.GetProperty(name);
        return value.ValueKind switch
        {
            JsonValueKind.True => true,
            JsonValueKind.False => false,
            _ => throw new InvalidDataException($"{path}.{name} must be boolean.")
        };
    }

    private static BigInteger ReadNonNegative(
        JsonElement parent,
        string name,
        string path)
    {
        JsonElement value = parent.GetProperty(name);
        RequireKind(value, JsonValueKind.Number, $"{path}.{name}");
        string raw = value.GetRawText();
        Require(BigInteger.TryParse(
                raw,
                NumberStyles.AllowLeadingSign,
                CultureInfo.InvariantCulture,
                out BigInteger result) && result >= 0,
            $"{path}.{name} must be a non-negative integer.");
        return result;
    }

    private static string[] ReadSortedStrings(
        JsonElement value,
        string path)
    {
        RequireKind(value, JsonValueKind.Array, path);
        var result = new List<string>();
        string? previous = null;
        foreach (JsonElement item in value.EnumerateArray())
        {
            RequireKind(item, JsonValueKind.String, path);
            string text = item.GetString()!;
            Require(text.Length > 0, $"{path} contains an empty string.");
            Require(previous is null ||
                StringComparer.Ordinal.Compare(previous, text) < 0,
                $"{path} must be strictly ordinal-sorted and unique.");
            previous = text;
            result.Add(text);
        }
        return result.ToArray();
    }

    private static void ValidateBinding(
        TopologyAtlasDeltaBinding value,
        string source)
    {
        foreach ((string name, string digest) in new[]
        {
            ("from_truth_release_digest", value.FromTruthReleaseDigest),
            ("to_truth_release_digest", value.ToTruthReleaseDigest),
            ("from_topology_atlas_digest", value.FromTopologyAtlasDigest),
            ("to_topology_atlas_digest", value.ToTopologyAtlasDigest),
            ("from_evidence_digest", value.FromEvidenceDigest),
            ("to_evidence_digest", value.ToEvidenceDigest),
            ("algorithm_profile_digest", value.AlgorithmProfileDigest)
        })
        {
            Require(digest.Length == 71 &&
                digest.StartsWith("sha256:", StringComparison.Ordinal) &&
                digest[7..].All(character =>
                    character is >= '0' and <= '9' or >= 'a' and <= 'f'),
                $"{source} {name} must use sha256:<64 lowercase hex>.");
        }
        Require(value.ProducerCommit.Length == 40 &&
            value.ProducerCommit.All(character =>
                character is >= '0' and <= '9' or >= 'a' and <= 'f'),
            $"{source} producer_commit must be 40 lowercase hexadecimal characters.");
    }

    private static void Preflight(ReadOnlySpan<byte> bytes)
    {
        try
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
                        Require(stack.Count > 0 && stack.Peek() is not null,
                            "Topology Atlas delta contains a property outside an object.");
                        string name = reader.GetString()
                            ?? throw new InvalidDataException(
                                "Topology Atlas delta contains a null property name.");
                        Require(stack.Peek()!.Add(name),
                            $"Topology Atlas delta repeats property '{name}'.");
                        break;
                    case JsonTokenType.EndObject:
                    case JsonTokenType.EndArray:
                        Require(stack.Count > 0,
                            "Topology Atlas delta contains an unbalanced container.");
                        stack.Pop();
                        break;
                    case JsonTokenType.Number:
                        string raw = Encoding.UTF8.GetString(
                            reader.HasValueSequence
                                ? reader.ValueSequence.ToArray()
                                : reader.ValueSpan);
                        Require(!raw.Contains('.', StringComparison.Ordinal) &&
                            !raw.Contains('e', StringComparison.OrdinalIgnoreCase),
                            "Topology Atlas delta forbids floating numeric lexemes.");
                        break;
                }
            }
            Require(stack.Count == 0,
                "Topology Atlas delta contains an unclosed container.");
        }
        catch (JsonException exception)
        {
            throw new InvalidDataException(
                "Topology Atlas delta is malformed JSON.",
                exception);
        }
    }

    private static void RequireKind(
        JsonElement value,
        JsonValueKind expected,
        string path)
    {
        Require(value.ValueKind == expected, $"{path} must be {expected}.");
    }

    private static void RequireEqual(
        string actual,
        string expected,
        string name)
    {
        Require(StringComparer.Ordinal.Equals(actual, expected),
            $"Topology Atlas delta {name} does not match expected coordinates.");
    }

    private static void Require(bool condition, string message)
    {
        if (!condition) throw new InvalidDataException(message);
    }
}
