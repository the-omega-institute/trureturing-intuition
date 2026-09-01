using System.Collections.Immutable;
using System.Globalization;
using System.Numerics;
using System.Text;
using System.Text.Json;

namespace Trureturing.Intuition.Core;

public sealed record TopologyAtlasEvidenceBinding(
    string TruthReleaseDigest,
    string CertifiedTopologyDigest,
    string TopologyAtlasDigest,
    string AlgorithmProfileDigest,
    string ProducerCommit);

public sealed record TopologyAtlasStableIdentityReadModel(
    string NodeId,
    string StableNodeId,
    string IdentityBasis,
    string? Gid,
    string SourcePath,
    string? ModuleName);

public sealed record TopologyAtlasTraitEvidenceReadModel(
    string Trait,
    string Rule,
    BigInteger? IntegerValue,
    ExactNonNegativeRational? RationalValue,
    IReadOnlyList<string> WitnessNodeIds);

public sealed record TopologyAtlasNodeTraitsReadModel(
    string NodeId,
    string StableNodeId,
    string PrimaryRole,
    IReadOnlyList<string> StructuralTraits,
    IReadOnlyList<TopologyAtlasTraitEvidenceReadModel> Evidence);

public sealed record TopologyAtlasClusterInterfaceReadModel(
    string SourceClusterId,
    string TargetClusterId,
    IReadOnlyList<string> SourceBoundaryNodeIds,
    IReadOnlyList<string> TargetBoundaryNodeIds,
    IReadOnlyList<(string DependencyId, string DependentId)> CertifiedEdgeWitnesses,
    JsonElement ExactRecord);

public sealed record TopologyAtlasAffinityWitnessReadModel(
    string SourceNodeId,
    string NeighborNodeId,
    BigInteger? Rank,
    IReadOnlyList<string> SharedPrerequisiteNodeIds,
    IReadOnlyList<string> SharedDependentNodeIds,
    IReadOnlyList<string> DeepestCommonPrerequisiteNodeIds,
    JsonElement ExactRecord);

public sealed class TopologyAtlasEvidenceReadModel
{
    private readonly IReadOnlyDictionary<string, TopologyAtlasStableIdentityReadModel>
        _identityByNodeId;
    private readonly IReadOnlyDictionary<string, TopologyAtlasStableIdentityReadModel>
        _identityByStableId;
    private readonly IReadOnlyDictionary<string, TopologyAtlasNodeTraitsReadModel>
        _traitsByNodeId;

    internal TopologyAtlasEvidenceReadModel(
        TopologyAtlasEvidenceBinding binding,
        int maximumWitnessesPerRelation,
        IReadOnlyList<TopologyAtlasStableIdentityReadModel> nodeIdentities,
        IReadOnlyList<TopologyAtlasNodeTraitsReadModel> nodeTraits,
        IReadOnlyList<TopologyAtlasClusterInterfaceReadModel> clusterInterfaces,
        IReadOnlyList<TopologyAtlasAffinityWitnessReadModel> affinityWitnesses)
    {
        Binding = binding;
        MaximumWitnessesPerRelation = maximumWitnessesPerRelation;
        NodeIdentities = nodeIdentities;
        NodeTraits = nodeTraits;
        ClusterInterfaces = clusterInterfaces;
        AffinityWitnesses = affinityWitnesses;
        _identityByNodeId = nodeIdentities.ToImmutableDictionary(
            identity => identity.NodeId,
            StringComparer.Ordinal);
        _identityByStableId = nodeIdentities.ToImmutableDictionary(
            identity => identity.StableNodeId,
            StringComparer.Ordinal);
        _traitsByNodeId = nodeTraits.ToImmutableDictionary(
            traits => traits.NodeId,
            StringComparer.Ordinal);
    }

    public TopologyAtlasEvidenceBinding Binding { get; }
    public int MaximumWitnessesPerRelation { get; }
    public IReadOnlyList<TopologyAtlasStableIdentityReadModel> NodeIdentities { get; }
    public IReadOnlyList<TopologyAtlasNodeTraitsReadModel> NodeTraits { get; }
    public IReadOnlyList<TopologyAtlasClusterInterfaceReadModel> ClusterInterfaces { get; }
    public IReadOnlyList<TopologyAtlasAffinityWitnessReadModel> AffinityWitnesses { get; }

    public TopologyAtlasStableIdentityReadModel GetIdentityByNodeId(string nodeId) =>
        _identityByNodeId.TryGetValue(nodeId, out TopologyAtlasStableIdentityReadModel? value)
            ? value
            : throw new InvalidDataException(
                $"Topology Atlas evidence does not contain node '{nodeId}'.");

    public TopologyAtlasStableIdentityReadModel GetIdentityByStableId(
        string stableNodeId) =>
        _identityByStableId.TryGetValue(
            stableNodeId,
            out TopologyAtlasStableIdentityReadModel? value)
            ? value
            : throw new InvalidDataException(
                $"Topology Atlas evidence does not contain stable node '{stableNodeId}'.");

    public TopologyAtlasNodeTraitsReadModel GetTraits(string nodeId) =>
        _traitsByNodeId.TryGetValue(nodeId, out TopologyAtlasNodeTraitsReadModel? value)
            ? value
            : throw new InvalidDataException(
                $"Topology Atlas evidence does not contain traits for '{nodeId}'.");
}

public sealed record TopologyAtlasEvidenceLoadResult(
    bool Available,
    TopologyAtlasEvidenceReadModel? Evidence,
    string Status);

public static class TopologyAtlasEvidenceReader
{
    private const string Schema = "topology-atlas-evidence.v1";
    private static readonly IReadOnlySet<string> IdentityBases =
        new HashSet<string>(StringComparer.Ordinal)
        {
            "truth-gid",
            "node-id-fallback"
        };
    private static readonly IReadOnlySet<string> StructuralRoles =
        new HashSet<string>(StringComparer.Ordinal)
        {
            "foundation",
            "hub",
            "bridge",
            "interface",
            "specialized-leaf",
            "frontier-adjacent",
            "internal"
        };

    public static TopologyAtlasEvidenceLoadResult LoadFile(
        string path,
        TopologyAtlasEvidenceBinding expectedBinding)
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
                Read(File.ReadAllBytes(path), expectedBinding),
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
        TopologyAtlasEvidenceBinding expectedBinding)
    {
        ArgumentNullException.ThrowIfNull(expectedBinding);
        ValidateBindingShape(expectedBinding, "expected binding");
        StrictJson.Preflight(bytes);
        RejectFloatingPointLexemes(bytes);

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
                "maximum_witnesses_per_relation",
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
                actualBinding.AlgorithmProfileDigest,
                expectedBinding.AlgorithmProfileDigest,
                "algorithm_profile_digest");
            RequireEqual(
                actualBinding.ProducerCommit,
                expectedBinding.ProducerCommit,
                "producer_commit");

            BigInteger maximum = ReadNonNegativeInteger(
                root,
                "maximum_witnesses_per_relation",
                "$");
            Require(maximum is >= 1 and <= 32,
                "$.maximum_witnesses_per_relation must be from 1 through 32.");

            TopologyAtlasStableIdentityReadModel[] identities = ReadIdentities(
                root.GetProperty("node_identities"));
            TopologyAtlasNodeTraitsReadModel[] traits = ReadTraits(
                root.GetProperty("node_traits"));
            TopologyAtlasClusterInterfaceReadModel[] interfaces = ReadInterfaces(
                root.GetProperty("cluster_interfaces"));
            TopologyAtlasAffinityWitnessReadModel[] witnesses = ReadAffinityWitnesses(
                root.GetProperty("affinity_witnesses"));
            ValidateClosure(identities, traits, interfaces, witnesses, checked((int)maximum));

            return new TopologyAtlasEvidenceReadModel(
                actualBinding,
                checked((int)maximum),
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

    private static TopologyAtlasStableIdentityReadModel[] ReadIdentities(
        JsonElement value)
    {
        RequireKind(value, JsonValueKind.Array, "$.node_identities");
        var result = new List<TopologyAtlasStableIdentityReadModel>();
        var nodeIds = new HashSet<string>(StringComparer.Ordinal);
        var stableIds = new HashSet<string>(StringComparer.Ordinal);
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
            string stableId = ReadNonEmptyString(
                identity,
                "stable_node_id",
                path);
            Require(nodeIds.Add(nodeId), $"Duplicate node identity '{nodeId}'.");
            Require(stableIds.Add(stableId),
                $"Duplicate stable node identity '{stableId}'.");
            string basis = ReadNonEmptyString(identity, "identity_basis", path);
            Require(IdentityBases.Contains(basis),
                $"{path}.identity_basis is unsupported.");
            string? gid = ReadNullableString(identity.GetProperty("gid"), $"{path}.gid");
            Require((basis == "truth-gid") == (gid is not null),
                $"{path}.gid disagrees with identity_basis.");
            result.Add(new TopologyAtlasStableIdentityReadModel(
                nodeId,
                stableId,
                basis,
                gid,
                ReadNonEmptyString(identity, "source_path", path),
                ReadNullableString(
                    identity.GetProperty("module_name"),
                    $"{path}.module_name")));
            index++;
        }
        return result.OrderBy(item => item.NodeId, StringComparer.Ordinal).ToArray();
    }

    private static TopologyAtlasNodeTraitsReadModel[] ReadTraits(JsonElement value)
    {
        RequireKind(value, JsonValueKind.Array, "$.node_traits");
        var result = new List<TopologyAtlasNodeTraitsReadModel>();
        var nodeIds = new HashSet<string>(StringComparer.Ordinal);
        int index = 0;
        foreach (JsonElement item in value.EnumerateArray())
        {
            string path = $"$.node_traits[{index}]";
            JsonElement traits = RequireObject(
                item,
                path,
                "node_id",
                "stable_node_id",
                "primary_role",
                "structural_traits",
                "evidence");
            string nodeId = ReadNonEmptyString(traits, "node_id", path);
            Require(nodeIds.Add(nodeId), $"Duplicate node traits '{nodeId}'.");
            string role = ReadNonEmptyString(traits, "primary_role", path);
            Require(StructuralRoles.Contains(role),
                $"{path}.primary_role is unsupported.");
            string[] structuralTraits = ReadUniqueStringArray(
                traits.GetProperty("structural_traits"),
                $"{path}.structural_traits",
                minimum: 1);
            Require(structuralTraits.All(StructuralRoles.Contains),
                $"{path}.structural_traits contains an unsupported trait.");
            Require(structuralTraits.Contains(role, StringComparer.Ordinal),
                $"{path}.structural_traits must contain primary_role.");
            result.Add(new TopologyAtlasNodeTraitsReadModel(
                nodeId,
                ReadNonEmptyString(traits, "stable_node_id", path),
                role,
                structuralTraits,
                ReadTraitEvidence(traits.GetProperty("evidence"), $"{path}.evidence")));
            index++;
        }
        return result.OrderBy(item => item.NodeId, StringComparer.Ordinal).ToArray();
    }

    private static TopologyAtlasTraitEvidenceReadModel[] ReadTraitEvidence(
        JsonElement value,
        string path)
    {
        RequireKind(value, JsonValueKind.Array, path);
        var result = new List<TopologyAtlasTraitEvidenceReadModel>();
        var traits = new HashSet<string>(StringComparer.Ordinal);
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
            Require(traits.Add(trait), $"{path} repeats trait '{trait}'.");
            BigInteger? integerValue = ReadNullableNonNegativeInteger(
                evidence.GetProperty("integer_value"),
                $"{itemPath}.integer_value");
            ExactNonNegativeRational? rationalValue = ReadNullableRational(
                evidence.GetProperty("rational_value"),
                $"{itemPath}.rational_value");
            Require(integerValue is null || rationalValue is null,
                $"{itemPath} cannot carry integer and rational values together.");
            result.Add(new TopologyAtlasTraitEvidenceReadModel(
                trait,
                ReadNonEmptyString(evidence, "rule", itemPath),
                integerValue,
                rationalValue,
                ReadUniqueStringArray(
                    evidence.GetProperty("witness_node_ids"),
                    $"{itemPath}.witness_node_ids")));
            index++;
        }
        return result.OrderBy(item => item.Trait, StringComparer.Ordinal).ToArray();
    }

    private static TopologyAtlasClusterInterfaceReadModel[] ReadInterfaces(
        JsonElement value)
    {
        RequireKind(value, JsonValueKind.Array, "$.cluster_interfaces");
        var result = new List<TopologyAtlasClusterInterfaceReadModel>();
        var keys = new HashSet<string>(StringComparer.Ordinal);
        int index = 0;
        foreach (JsonElement item in value.EnumerateArray())
        {
            string path = $"$.cluster_interfaces[{index}]";
            RequireKind(item, JsonValueKind.Object, path);
            string source = ReadRequiredClusterId(item, "source_cluster_id", path);
            string target = ReadRequiredClusterId(item, "target_cluster_id", path);
            Require(keys.Add(source + "\u0000" + target),
                $"Duplicate cluster interface '{source}' -> '{target}'.");
            result.Add(new TopologyAtlasClusterInterfaceReadModel(
                source,
                target,
                ReadOptionalStringArray(item, "source_boundary_node_ids", path),
                ReadOptionalStringArray(item, "target_boundary_node_ids", path),
                ReadOptionalEdgeWitnesses(item, path),
                item.Clone()));
            index++;
        }
        return result
            .OrderBy(item => item.SourceClusterId, StringComparer.Ordinal)
            .ThenBy(item => item.TargetClusterId, StringComparer.Ordinal)
            .ToArray();
    }

    private static TopologyAtlasAffinityWitnessReadModel[] ReadAffinityWitnesses(
        JsonElement value)
    {
        RequireKind(value, JsonValueKind.Array, "$.affinity_witnesses");
        var result = new List<TopologyAtlasAffinityWitnessReadModel>();
        var keys = new HashSet<string>(StringComparer.Ordinal);
        int index = 0;
        foreach (JsonElement item in value.EnumerateArray())
        {
            string path = $"$.affinity_witnesses[{index}]";
            RequireKind(item, JsonValueKind.Object, path);
            string source = ReadNonEmptyString(item, "source_node_id", path);
            string neighbor = ReadNonEmptyString(item, "neighbor_node_id", path);
            Require(keys.Add(source + "\u0000" + neighbor),
                $"Duplicate affinity witness '{source}' -> '{neighbor}'.");
            result.Add(new TopologyAtlasAffinityWitnessReadModel(
                source,
                neighbor,
                ReadOptionalPositiveInteger(item, "rank", path),
                ReadOptionalStringArray(item, "shared_prerequisite_node_ids", path),
                ReadOptionalStringArray(item, "shared_dependent_node_ids", path),
                ReadOptionalStringArray(
                    item,
                    "deepest_common_prerequisite_node_ids",
                    path),
                item.Clone()));
            index++;
        }
        return result
            .OrderBy(item => item.SourceNodeId, StringComparer.Ordinal)
            .ThenBy(item => item.NeighborNodeId, StringComparer.Ordinal)
            .ToArray();
    }

    private static void ValidateClosure(
        IReadOnlyList<TopologyAtlasStableIdentityReadModel> identities,
        IReadOnlyList<TopologyAtlasNodeTraitsReadModel> traits,
        IReadOnlyList<TopologyAtlasClusterInterfaceReadModel> interfaces,
        IReadOnlyList<TopologyAtlasAffinityWitnessReadModel> witnesses,
        int maximumWitnesses)
    {
        var identityByNode = identities.ToDictionary(
            item => item.NodeId,
            StringComparer.Ordinal);
        Require(identities.Count > 0,
            "Topology Atlas evidence must contain at least one node identity.");
        Require(traits.Count == identities.Count,
            "Topology Atlas evidence must publish one trait record per identity.");
        foreach (TopologyAtlasNodeTraitsReadModel item in traits)
        {
            Require(identityByNode.TryGetValue(
                    item.NodeId,
                    out TopologyAtlasStableIdentityReadModel? identity),
                $"Node traits reference unknown node '{item.NodeId}'.");
            RequireEqual(
                item.StableNodeId,
                identity!.StableNodeId,
                $"stable_node_id for {item.NodeId}");
            foreach (TopologyAtlasTraitEvidenceReadModel evidence in item.Evidence)
            {
                Require(item.StructuralTraits.Contains(
                        evidence.Trait,
                        StringComparer.Ordinal),
                    $"Trait evidence for {item.NodeId} describes an unpublished trait.");
                Require(evidence.WitnessNodeIds.Count <= maximumWitnesses,
                    $"Trait evidence for {item.NodeId} exceeds maximum witnesses.");
                Require(evidence.WitnessNodeIds.All(identityByNode.ContainsKey),
                    $"Trait evidence for {item.NodeId} references an unknown node.");
            }
        }
        foreach (TopologyAtlasClusterInterfaceReadModel item in interfaces)
        {
            Require(item.SourceBoundaryNodeIds.Count <= maximumWitnesses,
                "Cluster interface exceeds source witness bound.");
            Require(item.TargetBoundaryNodeIds.Count <= maximumWitnesses,
                "Cluster interface exceeds target witness bound.");
            Require(item.CertifiedEdgeWitnesses.Count <= maximumWitnesses,
                "Cluster interface exceeds certified edge witness bound.");
            Require(item.SourceBoundaryNodeIds.All(identityByNode.ContainsKey),
                "Cluster interface references an unknown source boundary node.");
            Require(item.TargetBoundaryNodeIds.All(identityByNode.ContainsKey),
                "Cluster interface references an unknown target boundary node.");
            Require(item.CertifiedEdgeWitnesses.All(edge =>
                    identityByNode.ContainsKey(edge.DependencyId) &&
                    identityByNode.ContainsKey(edge.DependentId)),
                "Cluster interface references an unknown certified edge endpoint.");
        }
        foreach (TopologyAtlasAffinityWitnessReadModel item in witnesses)
        {
            Require(identityByNode.ContainsKey(item.SourceNodeId),
                $"Affinity witness references unknown source '{item.SourceNodeId}'.");
            Require(identityByNode.ContainsKey(item.NeighborNodeId),
                $"Affinity witness references unknown neighbor '{item.NeighborNodeId}'.");
            foreach (IReadOnlyList<string> values in new[]
            {
                item.SharedPrerequisiteNodeIds,
                item.SharedDependentNodeIds,
                item.DeepestCommonPrerequisiteNodeIds
            })
            {
                Require(values.Count <= maximumWitnesses,
                    "Affinity witness exceeds the configured witness bound.");
                Require(values.All(identityByNode.ContainsKey),
                    "Affinity witness references an unknown witness node.");
            }
        }
    }

    private static IReadOnlyList<(string DependencyId, string DependentId)>
        ReadOptionalEdgeWitnesses(JsonElement parent, string path)
    {
        string[] candidates =
        [
            "certified_edge_witnesses",
            "crossing_edge_witnesses",
            "edge_witnesses"
        ];
        string? name = candidates.FirstOrDefault(parent.TryGetProperty);
        if (name is null)
        {
            return [];
        }
        JsonElement value = parent.GetProperty(name);
        RequireKind(value, JsonValueKind.Array, $"{path}.{name}");
        var result = new List<(string DependencyId, string DependentId)>();
        var keys = new HashSet<string>(StringComparer.Ordinal);
        int index = 0;
        foreach (JsonElement item in value.EnumerateArray())
        {
            string itemPath = $"{path}.{name}[{index}]";
            RequireKind(item, JsonValueKind.Object, itemPath);
            string dependency = ReadNonEmptyString(
                item,
                "dependency_id",
                itemPath);
            string dependent = ReadNonEmptyString(
                item,
                "dependent_id",
                itemPath);
            Require(keys.Add(dependency + "\u0000" + dependent),
                $"{path}.{name} contains a duplicate edge.");
            result.Add((dependency, dependent));
            index++;
        }
        return result
            .OrderBy(item => item.DependencyId, StringComparer.Ordinal)
            .ThenBy(item => item.DependentId, StringComparer.Ordinal)
            .ToArray();
    }

    private static string[] ReadOptionalStringArray(
        JsonElement parent,
        string name,
        string path) =>
        parent.TryGetProperty(name, out JsonElement value)
            ? ReadUniqueStringArray(value, $"{path}.{name}")
            : [];

    private static BigInteger? ReadOptionalPositiveInteger(
        JsonElement parent,
        string name,
        string path)
    {
        if (!parent.TryGetProperty(name, out JsonElement value))
        {
            return null;
        }
        BigInteger parsed = ReadInteger(value, $"{path}.{name}");
        Require(parsed > 0, $"{path}.{name} must be positive.");
        return parsed;
    }

    private static string ReadRequiredClusterId(
        JsonElement parent,
        string name,
        string path)
    {
        string value = ReadNonEmptyString(parent, name, path);
        Require(value.Length == "cluster:sha256:".Length + 64 &&
            value.StartsWith("cluster:sha256:", StringComparison.Ordinal) &&
            IsLowerHex(value.AsSpan("cluster:sha256:".Length)),
            $"{path}.{name} must be cluster:sha256:<64 lowercase hex>.");
        return value;
    }

    private static ExactNonNegativeRational? ReadNullableRational(
        JsonElement value,
        string path)
    {
        if (value.ValueKind == JsonValueKind.Null)
        {
            return null;
        }
        JsonElement rational = RequireObject(
            value,
            path,
            "numerator",
            "denominator");
        BigInteger numerator = ReadNonNegativeInteger(
            rational,
            "numerator",
            path);
        BigInteger denominator = ReadInteger(
            rational.GetProperty("denominator"),
            $"{path}.denominator");
        Require(denominator > 0, $"{path}.denominator must be positive.");
        Require(BigInteger.GreatestCommonDivisor(numerator, denominator) == BigInteger.One,
            $"{path} must be reduced.");
        return new ExactNonNegativeRational(numerator, denominator);
    }

    private static BigInteger? ReadNullableNonNegativeInteger(
        JsonElement value,
        string path)
    {
        if (value.ValueKind == JsonValueKind.Null)
        {
            return null;
        }
        BigInteger parsed = ReadInteger(value, path);
        Require(parsed >= 0, $"{path} must be non-negative.");
        return parsed;
    }

    private static string? ReadNullableString(JsonElement value, string path)
    {
        if (value.ValueKind == JsonValueKind.Null)
        {
            return null;
        }
        RequireKind(value, JsonValueKind.String, path);
        string result = value.GetString()!;
        Require(result.Length > 0, $"{path} must not be empty when present.");
        return result;
    }

    private static string[] ReadUniqueStringArray(
        JsonElement value,
        string path,
        int minimum = 0)
    {
        RequireKind(value, JsonValueKind.Array, path);
        var result = new List<string>();
        var seen = new HashSet<string>(StringComparer.Ordinal);
        int index = 0;
        foreach (JsonElement item in value.EnumerateArray())
        {
            string text = ReadNonEmptyString(item, $"{path}[{index}]");
            Require(seen.Add(text), $"{path} contains duplicate value '{text}'.");
            result.Add(text);
            index++;
        }
        Require(result.Count >= minimum,
            $"{path} must contain at least {minimum} value(s).");
        string[] sorted = result.Order(StringComparer.Ordinal).ToArray();
        Require(result.SequenceEqual(sorted, StringComparer.Ordinal),
            $"{path} must be ordinal-sorted.");
        return result.ToArray();
    }

    private static JsonElement RequireObject(
        JsonElement value,
        string path,
        params string[] properties)
    {
        RequireKind(value, JsonValueKind.Object, path);
        var expected = properties.ToHashSet(StringComparer.Ordinal);
        var actual = value.EnumerateObject()
            .Select(property => property.Name)
            .ToHashSet(StringComparer.Ordinal);
        string? missing = expected.Except(actual, StringComparer.Ordinal).FirstOrDefault();
        Require(missing is null,
            $"{path} is missing required property '{missing}'.");
        string? unknown = actual.Except(expected, StringComparer.Ordinal).FirstOrDefault();
        Require(unknown is null,
            $"{path} contains unknown property '{unknown}'.");
        return value;
    }

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
        string path) =>
        ReadNonEmptyString(parent.GetProperty(name), $"{path}.{name}");

    private static string ReadNonEmptyString(JsonElement value, string path)
    {
        string result = ReadString(value, path);
        Require(result.Length > 0, $"{path} must not be empty.");
        return result;
    }

    private static BigInteger ReadNonNegativeInteger(
        JsonElement parent,
        string name,
        string path)
    {
        BigInteger result = ReadInteger(parent.GetProperty(name), $"{path}.{name}");
        Require(result >= 0, $"{path}.{name} must be non-negative.");
        return result;
    }

    private static BigInteger ReadInteger(JsonElement value, string path)
    {
        RequireKind(value, JsonValueKind.Number, path);
        string raw = value.GetRawText();
        Require(BigInteger.TryParse(
                raw,
                NumberStyles.AllowLeadingSign,
                CultureInfo.InvariantCulture,
                out BigInteger result),
            $"{path} must be an integer.");
        return result;
    }

    private static void ValidateBindingShape(
        TopologyAtlasEvidenceBinding binding,
        string source)
    {
        RequireSha256(binding.TruthReleaseDigest,
            $"{source} truth_release_digest");
        RequireSha256(binding.CertifiedTopologyDigest,
            $"{source} certified_topology_digest");
        RequireSha256(binding.TopologyAtlasDigest,
            $"{source} topology_atlas_digest");
        RequireSha256(binding.AlgorithmProfileDigest,
            $"{source} algorithm_profile_digest");
        Require(binding.ProducerCommit.Length == 40 &&
            IsLowerHex(binding.ProducerCommit.AsSpan()),
            $"{source} producer_commit must be 40 lowercase hexadecimal characters.");
    }

    private static void RejectFloatingPointLexemes(ReadOnlySpan<byte> bytes)
    {
        var reader = new Utf8JsonReader(bytes);
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
            Require(!raw.Contains('.', StringComparison.Ordinal) &&
                !raw.Contains('e', StringComparison.OrdinalIgnoreCase),
                "Topology Atlas evidence forbids floating-point numeric lexemes.");
        }
    }

    private static void RequireSha256(string value, string field) =>
        Require(value.Length == 71 &&
            value.StartsWith("sha256:", StringComparison.Ordinal) &&
            IsLowerHex(value.AsSpan(7)),
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

    private static void RequireKind(
        JsonElement value,
        JsonValueKind expected,
        string path) =>
        Require(value.ValueKind == expected,
            $"{path} must be {expected}.");

    private static void RequireEqual(
        string actual,
        string expected,
        string field) =>
        Require(StringComparer.Ordinal.Equals(actual, expected),
            $"{field} does not match the expected binding.");

    private static void Require(bool condition, string message)
    {
        if (!condition)
        {
            throw new InvalidDataException(message);
        }
    }
}
