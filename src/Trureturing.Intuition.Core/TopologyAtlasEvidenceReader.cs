using System.Buffers;
using System.Globalization;
using System.Numerics;
using System.Text;
using System.Text.Json;

namespace Trureturing.Intuition.Core;

public static partial class TopologyAtlasEvidenceReader
{
    private const string Schema = "topology-atlas-evidence.v1";
    private static readonly string[] TraitOrder =
    [
        "foundation",
        "hub",
        "bridge",
        "interface",
        "frontier-adjacent",
        "specialized-leaf",
        "internal"
    ];
    private static readonly IReadOnlyDictionary<string, int> TraitRank =
        TraitOrder.Select((value, index) => (value, index))
            .ToDictionary(item => item.value, item => item.index, StringComparer.Ordinal);

    public static TopologyAtlasEvidenceLoadResult LoadFile(
        string path,
        TopologyAtlasEvidenceBinding expectedBinding,
        TopologyAtlasReadModel atlas)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(path);
        if (!File.Exists(path))
        {
            return new TopologyAtlasEvidenceLoadResult(
                false,
                null,
                "topology-atlas-evidence publication unavailable");
        }

        try
        {
            return new TopologyAtlasEvidenceLoadResult(
                true,
                Read(File.ReadAllBytes(path), expectedBinding, atlas),
                "topology-atlas-evidence.v1 consumed");
        }
        catch (FileNotFoundException)
        {
            return new TopologyAtlasEvidenceLoadResult(
                false,
                null,
                "topology-atlas-evidence publication unavailable");
        }
        catch (DirectoryNotFoundException)
        {
            return new TopologyAtlasEvidenceLoadResult(
                false,
                null,
                "topology-atlas-evidence publication unavailable");
        }
    }

    public static TopologyAtlasEvidenceReadModel Read(
        ReadOnlySpan<byte> bytes,
        TopologyAtlasEvidenceBinding expectedBinding,
        TopologyAtlasReadModel atlas)
    {
        ArgumentNullException.ThrowIfNull(expectedBinding);
        ArgumentNullException.ThrowIfNull(atlas);
        ValidateBindingShape(expectedBinding, "expected binding");
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
                "truth_release_digest",
                "certified_topology_digest",
                "topology_atlas_digest",
                "algorithm_profile_digest",
                "producer_commit",
                "witness_limit",
                "node_identities",
                "node_traits",
                "cluster_interfaces",
                "affinity_witnesses");
            RequireEqual(
                ReadString(root, "schema_version", "$"),
                Schema,
                "schema_version");

            var actualBinding = new TopologyAtlasEvidenceBinding(
                ReadString(root, "truth_release_digest", "$"),
                ReadString(root, "certified_topology_digest", "$"),
                ReadString(root, "topology_atlas_digest", "$"),
                ReadString(root, "algorithm_profile_digest", "$"),
                ReadString(root, "producer_commit", "$"));
            ValidateBindingShape(actualBinding, "topology atlas evidence");
            RequireEqual(
                actualBinding.TruthReleaseDigest,
                expectedBinding.TruthReleaseDigest,
                "truth_release_digest");
            RequireEqual(
                actualBinding.CertifiedTopologyDigest,
                expectedBinding.CertifiedTopologyDigest,
                "certified_topology_digest");
            RequireEqual(
                actualBinding.TopologyAtlasDigest,
                expectedBinding.TopologyAtlasDigest,
                "topology_atlas_digest");
            RequireEqual(
                actualBinding.EvidenceAlgorithmProfileDigest,
                expectedBinding.EvidenceAlgorithmProfileDigest,
                "algorithm_profile_digest");
            RequireEqual(
                actualBinding.ProducerCommit,
                expectedBinding.ProducerCommit,
                "producer_commit");

            int witnessLimit = checked((int)ReadNonNegativeInteger(
                root,
                "witness_limit",
                "$"));
            Require(
                witnessLimit is >= 1 and <= 16,
                "$.witness_limit must be from 1 through 16.");

            TopologyAtlasNodeIdentityEvidence[] identities = ReadIdentities(
                root.GetProperty("node_identities"));
            TopologyAtlasNodeTraitsEvidence[] traits = ReadNodeTraits(
                root.GetProperty("node_traits"));
            TopologyAtlasClusterInterfaceEvidence[] interfaces = ReadInterfaces(
                root.GetProperty("cluster_interfaces"));
            TopologyAtlasAffinityWitnessEvidence[] witnesses = ReadAffinityWitnesses(
                root.GetProperty("affinity_witnesses"),
                witnessLimit);

            ValidateClosure(
                actualBinding,
                witnessLimit,
                identities,
                traits,
                interfaces,
                witnesses,
                atlas);
            return new TopologyAtlasEvidenceReadModel(
                actualBinding,
                witnessLimit,
                identities,
                traits,
                interfaces,
                witnesses);
        }
        catch (JsonException exception)
        {
            throw new InvalidDataException(
                "Topology Atlas evidence is malformed JSON.",
                exception);
        }
        catch (OverflowException exception)
        {
            throw new InvalidDataException(
                "Topology Atlas evidence contains an out-of-range integer.",
                exception);
        }
    }

    private static TopologyAtlasNodeIdentityEvidence[] ReadIdentities(
        JsonElement value)
    {
        RequireKind(value, JsonValueKind.Array, "$.node_identities");
        var result = new List<TopologyAtlasNodeIdentityEvidence>();
        var stableIds = new HashSet<string>(StringComparer.Ordinal);
        string? previousNodeId = null;
        int index = 0;
        foreach (JsonElement item in value.EnumerateArray())
        {
            string path = $"$.node_identities[{index}]";
            JsonElement identity = RequireObject(
                item,
                path,
                "node_id",
                "stable_node_id",
                "identity_basis",
                "gid",
                "source_path",
                "module_name");
            string nodeId = ReadNonEmptyString(identity, "node_id", path);
            RequireStrictlyAfter(previousNodeId, nodeId, $"{path}.node_id");
            previousNodeId = nodeId;
            string stableNodeId = ReadNonEmptyString(
                identity,
                "stable_node_id",
                path);
            Require(
                stableIds.Add(stableNodeId),
                $"Duplicate stable_node_id '{stableNodeId}'.");
            string basis = ReadNonEmptyString(
                identity,
                "identity_basis",
                path);
            string? gid = ReadNullableString(
                identity.GetProperty("gid"),
                $"{path}.gid");
            string? moduleName = ReadNullableString(
                identity.GetProperty("module_name"),
                $"{path}.module_name");
            if (basis == "truth-gid")
            {
                Require(gid is not null, $"{path}.gid is required for truth-gid.");
                RequireEqual(stableNodeId, gid!, $"{path}.stable_node_id");
            }
            else if (basis == "node-id-fallback")
            {
                Require(gid is null, $"{path}.gid must be null for node-id-fallback.");
                RequireEqual(stableNodeId, nodeId, $"{path}.stable_node_id");
            }
            else
            {
                throw new InvalidDataException(
                    $"{path}.identity_basis is unsupported.");
            }
            result.Add(new TopologyAtlasNodeIdentityEvidence(
                nodeId,
                stableNodeId,
                basis,
                gid,
                ReadNonEmptyString(identity, "source_path", path),
                moduleName));
            index++;
        }
        return result.ToArray();
    }

    private static TopologyAtlasNodeTraitsEvidence[] ReadNodeTraits(
        JsonElement value)
    {
        RequireKind(value, JsonValueKind.Array, "$.node_traits");
        var result = new List<TopologyAtlasNodeTraitsEvidence>();
        string? previousNodeId = null;
        int index = 0;
        foreach (JsonElement item in value.EnumerateArray())
        {
            string path = $"$.node_traits[{index}]";
            JsonElement node = RequireObject(
                item,
                path,
                "node_id",
                "stable_node_id",
                "primary_role",
                "structural_traits",
                "evidence");
            string nodeId = ReadNonEmptyString(node, "node_id", path);
            RequireStrictlyAfter(previousNodeId, nodeId, $"{path}.node_id");
            previousNodeId = nodeId;
            string primaryRole = ReadNonEmptyString(node, "primary_role", path);
            Require(
                TraitRank.ContainsKey(primaryRole),
                $"{path}.primary_role is unsupported.");
            string[] structuralTraits = ReadUniqueStringArray(
                node.GetProperty("structural_traits"),
                $"{path}.structural_traits",
                minimum: 1,
                requireOrdinalOrder: false);
            int previousRank = -1;
            foreach (string trait in structuralTraits)
            {
                Require(
                    TraitRank.TryGetValue(trait, out int rank),
                    $"{path}.structural_traits contains unsupported trait '{trait}'.");
                Require(
                    rank > previousRank,
                    $"{path}.structural_traits must use the canonical trait order.");
                previousRank = rank;
            }
            TopologyStructuralTraitEvidenceReadModel[] evidence =
                ReadTraitEvidence(
                    node.GetProperty("evidence"),
                    path,
                    structuralTraits);
            result.Add(new TopologyAtlasNodeTraitsEvidence(
                nodeId,
                ReadNonEmptyString(node, "stable_node_id", path),
                primaryRole,
                structuralTraits,
                evidence));
            index++;
        }
        return result.ToArray();
    }

    private static TopologyStructuralTraitEvidenceReadModel[] ReadTraitEvidence(
        JsonElement value,
        string parentPath,
        IReadOnlyList<string> structuralTraits)
    {
        string path = parentPath + ".evidence";
        RequireKind(value, JsonValueKind.Array, path);
        var result = new List<TopologyStructuralTraitEvidenceReadModel>();
        var coveredTraits = new HashSet<string>(StringComparer.Ordinal);
        int previousTraitRank = -1;
        string? previousRule = null;
        int index = 0;
        foreach (JsonElement item in value.EnumerateArray())
        {
            string itemPath = $"{path}[{index}]";
            JsonElement evidence = RequireObject(
                item,
                itemPath,
                "trait",
                "rule",
                "integer_value",
                "rational_value",
                "witness_node_ids");
            string trait = ReadNonEmptyString(evidence, "trait", itemPath);
            Require(
                structuralTraits.Contains(trait, StringComparer.Ordinal),
                $"{itemPath}.trait is absent from structural_traits.");
            int traitRank = TraitRank[trait];
            string rule = ReadNonEmptyString(evidence, "rule", itemPath);
            Require(
                traitRank > previousTraitRank ||
                traitRank == previousTraitRank &&
                    previousRule is not null &&
                    StringComparer.Ordinal.Compare(previousRule, rule) < 0,
                $"{path} must be ordered by trait and rule.");
            previousTraitRank = traitRank;
            previousRule = rule;
            coveredTraits.Add(trait);
            result.Add(new TopologyStructuralTraitEvidenceReadModel(
                trait,
                rule,
                ReadNullableNonNegativeInteger(
                    evidence.GetProperty("integer_value"),
                    $"{itemPath}.integer_value"),
                ReadNullableRational(
                    evidence.GetProperty("rational_value"),
                    $"{itemPath}.rational_value"),
                ReadUniqueStringArray(
                    evidence.GetProperty("witness_node_ids"),
                    $"{itemPath}.witness_node_ids",
                    requireOrdinalOrder: true)));
            index++;
        }
        Require(result.Count > 0, $"{path} must not be empty.");
        Require(
            structuralTraits.All(coveredTraits.Contains),
            $"{path} must justify every structural trait.");
        return result.ToArray();
    }

    private static TopologyAtlasClusterInterfaceEvidence[] ReadInterfaces(
        JsonElement value)
    {
        RequireKind(value, JsonValueKind.Array, "$.cluster_interfaces");
        var result = new List<TopologyAtlasClusterInterfaceEvidence>();
        string? previousKey = null;
        int index = 0;
        foreach (JsonElement item in value.EnumerateArray())
        {
            string path = $"$.cluster_interfaces[{index}]";
            JsonElement clusterInterface = RequireObject(
                item,
                path,
                "interface_id",
                "source_cluster_id",
                "target_cluster_id",
                "certified_edges",
                "source_boundary_node_ids",
                "target_boundary_node_ids",
                "cut_bridge_edge_ids",
                "total_edge_betweenness",
                "dependency_span_min",
                "dependency_span_max");
            string sourceClusterId = ReadPrefixedDigestId(
                clusterInterface,
                "source_cluster_id",
                path,
                "cluster:sha256:");
            string targetClusterId = ReadPrefixedDigestId(
                clusterInterface,
                "target_cluster_id",
                path,
                "cluster:sha256:");
            Require(
                !StringComparer.Ordinal.Equals(sourceClusterId, targetClusterId),
                $"{path} must cross two different clusters.");
            string key = LengthKey(sourceClusterId, targetClusterId);
            RequireStrictlyAfter(previousKey, key, path);
            previousKey = key;
            BigInteger spanMin = ReadNonNegativeInteger(
                clusterInterface,
                "dependency_span_min",
                path);
            BigInteger spanMax = ReadNonNegativeInteger(
                clusterInterface,
                "dependency_span_max",
                path);
            Require(spanMin > 0, $"{path}.dependency_span_min must be positive.");
            Require(spanMax >= spanMin, $"{path} has an inverted dependency span.");
            result.Add(new TopologyAtlasClusterInterfaceEvidence(
                ReadPrefixedDigestId(
                    clusterInterface,
                    "interface_id",
                    path,
                    "interface:sha256:"),
                sourceClusterId,
                targetClusterId,
                ReadInterfaceEdges(
                    clusterInterface.GetProperty("certified_edges"),
                    path),
                ReadUniqueStringArray(
                    clusterInterface.GetProperty("source_boundary_node_ids"),
                    $"{path}.source_boundary_node_ids",
                    requireOrdinalOrder: true),
                ReadUniqueStringArray(
                    clusterInterface.GetProperty("target_boundary_node_ids"),
                    $"{path}.target_boundary_node_ids",
                    requireOrdinalOrder: true),
                ReadUniquePrefixedIdArray(
                    clusterInterface.GetProperty("cut_bridge_edge_ids"),
                    $"{path}.cut_bridge_edge_ids",
                    "edge:sha256:"),
                ReadRational(
                    clusterInterface.GetProperty("total_edge_betweenness"),
                    $"{path}.total_edge_betweenness"),
                spanMin,
                spanMax));
            index++;
        }
        return result.ToArray();
    }

    private static TopologyAtlasInterfaceEdgeEvidence[] ReadInterfaceEdges(
        JsonElement value,
        string parentPath)
    {
        string path = parentPath + ".certified_edges";
        RequireKind(value, JsonValueKind.Array, path);
        var result = new List<TopologyAtlasInterfaceEdgeEvidence>();
        string? previousKey = null;
        int index = 0;
        foreach (JsonElement item in value.EnumerateArray())
        {
            string itemPath = $"{path}[{index}]";
            JsonElement edge = RequireObject(
                item,
                itemPath,
                "edge_id",
                "dependency_id",
                "dependent_id",
                "is_cut_bridge",
                "edge_betweenness",
                "dependency_span");
            string dependencyId = ReadNonEmptyString(edge, "dependency_id", itemPath);
            string dependentId = ReadNonEmptyString(edge, "dependent_id", itemPath);
            string key = LengthKey(dependencyId, dependentId);
            RequireStrictlyAfter(previousKey, key, itemPath);
            previousKey = key;
            BigInteger dependencySpan = ReadNonNegativeInteger(
                edge,
                "dependency_span",
                itemPath);
            Require(dependencySpan > 0, $"{itemPath}.dependency_span must be positive.");
            result.Add(new TopologyAtlasInterfaceEdgeEvidence(
                ReadPrefixedDigestId(edge, "edge_id", itemPath, "edge:sha256:"),
                dependencyId,
                dependentId,
                ReadBoolean(edge, "is_cut_bridge", itemPath),
                ReadRational(
                    edge.GetProperty("edge_betweenness"),
                    $"{itemPath}.edge_betweenness"),
                dependencySpan));
            index++;
        }
        Require(result.Count > 0, $"{path} must not be empty.");
        return result.ToArray();
    }

    private static TopologyAtlasAffinityWitnessEvidence[] ReadAffinityWitnesses(
        JsonElement value,
        int witnessLimit)
    {
        RequireKind(value, JsonValueKind.Array, "$.affinity_witnesses");
        var result = new List<TopologyAtlasAffinityWitnessEvidence>();
        (string Source, BigInteger Rank, string Neighbor)? previous = null;
        int index = 0;
        foreach (JsonElement item in value.EnumerateArray())
        {
            string path = $"$.affinity_witnesses[{index}]";
            JsonElement witness = RequireObject(
                item,
                path,
                "source_node_id",
                "neighbor_node_id",
                "rank",
                "shared_prerequisite_witness_ids",
                "shared_dependent_witness_ids",
                "deepest_common_prerequisite_ids");
            string sourceNodeId = ReadNonEmptyString(
                witness,
                "source_node_id",
                path);
            string neighborNodeId = ReadNonEmptyString(
                witness,
                "neighbor_node_id",
                path);
            Require(
                !StringComparer.Ordinal.Equals(sourceNodeId, neighborNodeId),
                $"{path} cannot witness self-affinity.");
            BigInteger rank = ReadNonNegativeInteger(witness, "rank", path);
            Require(rank > 0, $"{path}.rank must be positive.");
            var current = (sourceNodeId, rank, neighborNodeId);
            if (previous is not null)
            {
                int comparison = StringComparer.Ordinal.Compare(
                    previous.Value.Source,
                    current.sourceNodeId);
                if (comparison == 0)
                {
                    comparison = previous.Value.Rank.CompareTo(current.rank);
                }
                if (comparison == 0)
                {
                    comparison = StringComparer.Ordinal.Compare(
                        previous.Value.Neighbor,
                        current.neighborNodeId);
                }
                Require(comparison < 0, "$.affinity_witnesses must use producer order.");
            }
            previous = current;
            string[] prerequisites = ReadUniqueStringArray(
                witness.GetProperty("shared_prerequisite_witness_ids"),
                $"{path}.shared_prerequisite_witness_ids");
            string[] dependents = ReadUniqueStringArray(
                witness.GetProperty("shared_dependent_witness_ids"),
                $"{path}.shared_dependent_witness_ids");
            string[] deepest = ReadUniqueStringArray(
                witness.GetProperty("deepest_common_prerequisite_ids"),
                $"{path}.deepest_common_prerequisite_ids",
                requireOrdinalOrder: true);
            Require(
                prerequisites.Length <= witnessLimit &&
                dependents.Length <= witnessLimit &&
                deepest.Length <= witnessLimit,
                $"{path} exceeds witness_limit.");
            result.Add(new TopologyAtlasAffinityWitnessEvidence(
                sourceNodeId,
                neighborNodeId,
                rank,
                prerequisites,
                dependents,
                deepest));
            index++;
        }
        return result.ToArray();
    }

    private static void Preflight(ReadOnlySpan<byte> bytes)
    {
        StrictJson.Preflight(bytes);
        try
        {
            var reader = new Utf8JsonReader(
                bytes,
                new JsonReaderOptions
                {
                    AllowTrailingCommas = false,
                    CommentHandling = JsonCommentHandling.Disallow
                });
            while (reader.Read())
            {
                if (reader.TokenType != JsonTokenType.Number)
                {
                    continue;
                }
                string raw = Encoding.UTF8.GetString(
                    reader.HasValueSequence
                        ? reader.ValueSequence.ToArray()
                        : reader.ValueSpan);
                if (raw.Contains('.', StringComparison.Ordinal) ||
                    raw.Contains('e', StringComparison.OrdinalIgnoreCase))
                {
                    throw new InvalidDataException(
                        "Topology Atlas evidence forbids floating-point numeric lexemes.");
                }
            }
        }
        catch (JsonException exception)
        {
            throw new InvalidDataException(
                "Topology Atlas evidence is malformed JSON.",
                exception);
        }
    }

    private static JsonElement RequireObject(
        JsonElement value,
        string path,
        params string[] requiredProperties)
    {
        RequireKind(value, JsonValueKind.Object, path);
        var expected = requiredProperties.ToHashSet(StringComparer.Ordinal);
        var actual = value.EnumerateObject()
            .Select(property => property.Name)
            .ToHashSet(StringComparer.Ordinal);
        string? missing = expected.Except(actual, StringComparer.Ordinal).FirstOrDefault();
        Require(missing is null, $"{path} is missing required property '{missing}'.");
        string? unknown = actual.Except(expected, StringComparer.Ordinal).FirstOrDefault();
        Require(unknown is null, $"{path} contains unknown property '{unknown}'.");
        return value;
    }

    private static void RequireKind(
        JsonElement value,
        JsonValueKind kind,
        string path) =>
        Require(value.ValueKind == kind, $"{path} must be {kind}.");

    private static string ReadString(
        JsonElement parent,
        string name,
        string path) =>
        ReadString(parent.GetProperty(name), $"{path}.{name}");

    private static string ReadString(JsonElement value, string path)
    {
        RequireKind(value, JsonValueKind.String, path);
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

    private static string? ReadNullableString(JsonElement value, string path)
    {
        if (value.ValueKind == JsonValueKind.Null)
        {
            return null;
        }
        string result = ReadString(value, path);
        Require(result.Length > 0, $"{path} must not be empty.");
        return result;
    }

    private static bool ReadBoolean(
        JsonElement parent,
        string name,
        string path)
    {
        JsonElement value = parent.GetProperty(name);
        Require(
            value.ValueKind is JsonValueKind.True or JsonValueKind.False,
            $"{path}.{name} must be boolean.");
        return value.GetBoolean();
    }

    private static BigInteger ReadNonNegativeInteger(
        JsonElement parent,
        string name,
        string path)
    {
        BigInteger value = ReadInteger(parent.GetProperty(name), $"{path}.{name}");
        Require(value >= 0, $"{path}.{name} must be non-negative.");
        return value;
    }

    private static BigInteger ReadInteger(JsonElement value, string path)
    {
        RequireKind(value, JsonValueKind.Number, path);
        string raw = value.GetRawText();
        Require(
            BigInteger.TryParse(
                raw,
                NumberStyles.AllowLeadingSign,
                CultureInfo.InvariantCulture,
                out BigInteger result),
            $"{path} must be an integer.");
        return result;
    }

    private static BigInteger? ReadNullableNonNegativeInteger(
        JsonElement value,
        string path)
    {
        if (value.ValueKind == JsonValueKind.Null)
        {
            return null;
        }
        BigInteger result = ReadInteger(value, path);
        Require(result >= 0, $"{path} must be non-negative.");
        return result;
    }

    private static ExactNonNegativeRational ReadRational(
        JsonElement value,
        string path)
    {
        JsonElement rational = RequireObject(
            value,
            path,
            "numerator",
            "denominator");
        BigInteger numerator = ReadInteger(
            rational.GetProperty("numerator"),
            $"{path}.numerator");
        BigInteger denominator = ReadInteger(
            rational.GetProperty("denominator"),
            $"{path}.denominator");
        Require(numerator >= 0, $"{path}.numerator must be non-negative.");
        Require(denominator > 0, $"{path}.denominator must be positive.");
        Require(
            BigInteger.GreatestCommonDivisor(numerator, denominator) == BigInteger.One,
            $"{path} must be reduced.");
        return new ExactNonNegativeRational(numerator, denominator);
    }

    private static ExactNonNegativeRational? ReadNullableRational(
        JsonElement value,
        string path) =>
        value.ValueKind == JsonValueKind.Null
            ? null
            : ReadRational(value, path);

    private static string[] ReadUniqueStringArray(
        JsonElement value,
        string path,
        int minimum = 0,
        int? maximum = null,
        bool requireOrdinalOrder = false)
    {
        RequireKind(value, JsonValueKind.Array, path);
        var result = new List<string>();
        var unique = new HashSet<string>(StringComparer.Ordinal);
        string? previous = null;
        int index = 0;
        foreach (JsonElement item in value.EnumerateArray())
        {
            string itemPath = $"{path}[{index}]";
            string text = ReadString(item, itemPath);
            Require(text.Length > 0, $"{itemPath} must not be empty.");
            Require(unique.Add(text), $"{path} contains duplicate value '{text}'.");
            if (requireOrdinalOrder)
            {
                RequireStrictlyAfter(previous, text, itemPath);
                previous = text;
            }
            result.Add(text);
            index++;
        }
        Require(result.Count >= minimum, $"{path} has too few values.");
        if (maximum is not null)
        {
            Require(result.Count <= maximum.Value, $"{path} has too many values.");
        }
        return result.ToArray();
    }

    private static string[] ReadUniquePrefixedIdArray(
        JsonElement value,
        string path,
        string prefix)
    {
        string[] result = ReadUniqueStringArray(
            value,
            path,
            requireOrdinalOrder: true);
        foreach (string item in result)
        {
            RequirePrefixedDigestId(item, path, prefix);
        }
        return result;
    }

    private static string ReadPrefixedDigestId(
        JsonElement parent,
        string name,
        string path,
        string prefix)
    {
        string result = ReadNonEmptyString(parent, name, path);
        RequirePrefixedDigestId(result, $"{path}.{name}", prefix);
        return result;
    }

    private static void RequirePrefixedDigestId(
        string value,
        string path,
        string prefix) =>
        Require(
            value.Length == prefix.Length + 64 &&
            value.StartsWith(prefix, StringComparison.Ordinal) &&
            IsLowerHex(value.AsSpan(prefix.Length)),
            $"{path} must use {prefix}<64 lowercase hex>.");

    private static void RequireStrictlyAfter(
        string? previous,
        string current,
        string path)
    {
        if (previous is not null)
        {
            Require(
                StringComparer.Ordinal.Compare(previous, current) < 0,
                $"{path} is not strictly ordered.");
        }
    }

    private static string LengthKey(string left, string right) =>
        left.Length.ToString(CultureInfo.InvariantCulture) + ":" + left + right;

    private static void ValidateBindingShape(
        TopologyAtlasEvidenceBinding binding,
        string source)
    {
        RequireSha256(binding.TruthReleaseDigest, $"{source} truth_release_digest");
        RequireSha256(
            binding.CertifiedTopologyDigest,
            $"{source} certified_topology_digest");
        RequireSha256(
            binding.TopologyAtlasDigest,
            $"{source} topology_atlas_digest");
        RequireSha256(
            binding.EvidenceAlgorithmProfileDigest,
            $"{source} algorithm_profile_digest");
        Require(
            binding.ProducerCommit.Length == 40 &&
            IsLowerHex(binding.ProducerCommit.AsSpan()),
            $"{source} producer_commit must be 40 lowercase hexadecimal characters.");
    }

    private static void RequireSha256(string value, string field) =>
        Require(
            value.Length == 71 &&
            value.StartsWith("sha256:", StringComparison.Ordinal) &&
            IsLowerHex(value.AsSpan("sha256:".Length)),
            $"{field} must use sha256:<64 lowercase hex>.");

    private static bool IsLowerHex(ReadOnlySpan<char> value)
    {
        foreach (char character in value)
        {
            if (character is not (>= '0' and <= '9') and
                not (>= 'a' and <= 'f'))
            {
                return false;
            }
        }
        return true;
    }

    private static void RequireEqual(
        string actual,
        string expected,
        string field) =>
        Require(
            StringComparer.Ordinal.Equals(actual, expected),
            $"{field} does not match the exact bound coordinate.");

    private static void Require(bool condition, string message)
    {
        if (!condition)
        {
            throw new InvalidDataException(message);
        }
    }
}
