using System.Collections.Immutable;
using System.Globalization;
using System.Numerics;
using System.Text;
using System.Text.Json;

namespace Trureturing.Intuition.Core;

public sealed record TopologyAtlasBinding(
    string TruthReleaseDigest,
    string CertifiedTopologyDigest,
    string CertifiedAlgorithmProfileDigest,
    string AtlasAlgorithmProfileDigest,
    string ProducerCommit);

public sealed record TopologyAtlasClusterReadModel(
    string ClusterId,
    string? ParentClusterId,
    int Level,
    string LevelName,
    IReadOnlyList<string> MemberNodeIds,
    IReadOnlyList<string> RepresentativeNodeIds,
    IReadOnlyList<string> BoundaryNodeIds,
    IReadOnlyList<string> RootNodeIds,
    BigInteger DepthMin,
    BigInteger DepthMax,
    BigInteger InternalEdgeCount,
    BigInteger ExternalEdgeCount);

public sealed record TopologyAtlasNodeReadModel(
    string NodeId,
    string ComponentId,
    IReadOnlyList<string> ClusterPath,
    string ArticulationStatus,
    BigInteger DominatorCoverageCount,
    ExactNonNegativeRational DominatorCoverage,
    ExactNonNegativeRational BoundaryScore,
    BigInteger KCoreLevel,
    BigInteger Depth,
    BigInteger Height,
    string StructuralRole);

public sealed record TopologyAtlasEdgeReadModel(
    string DependencyId,
    string DependentId,
    ExactNonNegativeRational EdgeBetweenness,
    bool IsCutBridge,
    string ClusterRelation,
    string SourceClusterId,
    string TargetClusterId,
    BigInteger DependencySpan);

public sealed record TopologyAtlasAffinityReadModel(
    string SourceNodeId,
    string NeighborNodeId,
    BigInteger Rank,
    bool MutualTopK,
    bool DirectDependency,
    ExactNonNegativeRational SharedAncestorJaccard,
    ExactNonNegativeRational SharedDescendantJaccard,
    BigInteger UndirectedPathDistance,
    BigInteger? DeepestCommonPrerequisiteDepth,
    ExactNonNegativeRational CombinedRank);

public sealed record TopologyAtlasHierarchyReadModel(
    int Level,
    string Name,
    IReadOnlyList<string> ClusterIds);

public sealed class TopologyAtlasReadModel
{
    private readonly IReadOnlyDictionary<string, TopologyAtlasNodeReadModel> _nodes;
    private readonly IReadOnlyDictionary<string, TopologyAtlasClusterReadModel> _clusters;

    internal TopologyAtlasReadModel(
        TopologyAtlasBinding binding,
        IReadOnlyList<TopologyAtlasClusterReadModel> clusters,
        IReadOnlyList<TopologyAtlasNodeReadModel> nodes,
        IReadOnlyList<TopologyAtlasEdgeReadModel> edges,
        IReadOnlyList<TopologyAtlasAffinityReadModel> affinities,
        IReadOnlyList<TopologyAtlasHierarchyReadModel> hierarchy)
    {
        Binding = binding;
        Clusters = clusters;
        Nodes = nodes;
        Edges = edges;
        StructuralAffinities = affinities;
        Hierarchy = hierarchy;
        _nodes = nodes.ToImmutableDictionary(node => node.NodeId, StringComparer.Ordinal);
        _clusters = clusters.ToImmutableDictionary(cluster => cluster.ClusterId, StringComparer.Ordinal);
    }

    public TopologyAtlasBinding Binding { get; }
    public IReadOnlyList<TopologyAtlasClusterReadModel> Clusters { get; }
    public IReadOnlyList<TopologyAtlasNodeReadModel> Nodes { get; }
    public IReadOnlyList<TopologyAtlasEdgeReadModel> Edges { get; }
    public IReadOnlyList<TopologyAtlasAffinityReadModel> StructuralAffinities { get; }
    public IReadOnlyList<TopologyAtlasHierarchyReadModel> Hierarchy { get; }

    public TopologyAtlasNodeReadModel GetNode(string nodeId) =>
        _nodes.TryGetValue(nodeId, out TopologyAtlasNodeReadModel? node)
            ? node
            : throw new InvalidDataException(
                $"Topology atlas does not contain node '{nodeId}'.");

    public TopologyAtlasClusterReadModel GetCluster(string clusterId) =>
        _clusters.TryGetValue(clusterId, out TopologyAtlasClusterReadModel? cluster)
            ? cluster
            : throw new InvalidDataException(
                $"Topology atlas does not contain cluster '{clusterId}'.");
}

public sealed record TopologyAtlasLoadResult(
    bool Available,
    TopologyAtlasReadModel? Atlas,
    string Status);

public static class TopologyAtlasReader
{
    private const string Schema = "topology-atlas.v1";
    private static readonly string[] HierarchyNames =
    [
        "weak-component",
        "bridge-block",
        "affinity-community"
    ];
    private static readonly HashSet<string> StructuralRoles =
        new(StringComparer.Ordinal)
        {
            "foundation",
            "hub",
            "bridge",
            "interface",
            "specialized-leaf",
            "frontier-adjacent",
            "internal"
        };

    public static TopologyAtlasLoadResult LoadFile(
        string path,
        TopologyAtlasBinding expectedBinding)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(path);
        if (!File.Exists(path))
        {
            return new TopologyAtlasLoadResult(
                false,
                null,
                "topology-atlas publication unavailable");
        }

        try
        {
            return new TopologyAtlasLoadResult(
                true,
                Read(File.ReadAllBytes(path), expectedBinding),
                "topology-atlas.v1 consumed");
        }
        catch (FileNotFoundException)
        {
            return new TopologyAtlasLoadResult(
                false,
                null,
                "topology-atlas publication unavailable");
        }
        catch (DirectoryNotFoundException)
        {
            return new TopologyAtlasLoadResult(
                false,
                null,
                "topology-atlas publication unavailable");
        }
    }

    public static TopologyAtlasReadModel Read(
        ReadOnlySpan<byte> bytes,
        TopologyAtlasBinding expectedBinding)
    {
        ArgumentNullException.ThrowIfNull(expectedBinding);
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
                "certified_algorithm_profile_digest",
                "algorithm_profile_digest",
                "producer_commit",
                "clusters",
                "node_structure",
                "edge_structure",
                "structural_affinities",
                "hierarchy");
            RequireEqual(
                ReadString(root, "schema_version", "$"),
                Schema,
                "schema_version");

            var actualBinding = new TopologyAtlasBinding(
                ReadString(root, "truth_release_digest", "$"),
                ReadString(root, "certified_topology_digest", "$"),
                ReadString(root, "certified_algorithm_profile_digest", "$"),
                ReadString(root, "algorithm_profile_digest", "$"),
                ReadString(root, "producer_commit", "$"));
            ValidateBindingShape(actualBinding, "topology atlas");
            RequireEqual(
                actualBinding.TruthReleaseDigest,
                expectedBinding.TruthReleaseDigest,
                "truth_release_digest");
            RequireEqual(
                actualBinding.CertifiedTopologyDigest,
                expectedBinding.CertifiedTopologyDigest,
                "certified_topology_digest");
            RequireEqual(
                actualBinding.CertifiedAlgorithmProfileDigest,
                expectedBinding.CertifiedAlgorithmProfileDigest,
                "certified_algorithm_profile_digest");
            RequireEqual(
                actualBinding.AtlasAlgorithmProfileDigest,
                expectedBinding.AtlasAlgorithmProfileDigest,
                "algorithm_profile_digest");
            RequireEqual(
                actualBinding.ProducerCommit,
                expectedBinding.ProducerCommit,
                "producer_commit");

            TopologyAtlasClusterReadModel[] clusters = ReadClusters(
                root.GetProperty("clusters"));
            TopologyAtlasNodeReadModel[] nodes = ReadNodes(
                root.GetProperty("node_structure"));
            TopologyAtlasEdgeReadModel[] edges = ReadEdges(
                root.GetProperty("edge_structure"));
            TopologyAtlasAffinityReadModel[] affinities = ReadAffinities(
                root.GetProperty("structural_affinities"));
            TopologyAtlasHierarchyReadModel[] hierarchy = ReadHierarchy(
                root.GetProperty("hierarchy"));
            ValidateClosure(clusters, nodes, edges, affinities, hierarchy);
            return new TopologyAtlasReadModel(
                actualBinding,
                clusters,
                nodes,
                edges,
                affinities,
                hierarchy);
        }
        catch (JsonException exception)
        {
            throw new InvalidDataException(
                "Topology atlas is malformed JSON.",
                exception);
        }
    }

    private static TopologyAtlasClusterReadModel[] ReadClusters(JsonElement value)
    {
        RequireKind(value, JsonValueKind.Array, "$.clusters");
        var result = new List<TopologyAtlasClusterReadModel>();
        var ids = new HashSet<string>(StringComparer.Ordinal);
        int index = 0;
        foreach (JsonElement item in value.EnumerateArray())
        {
            string path = $"$.clusters[{index}]";
            JsonElement cluster = RequireObject(
                item,
                path,
                "cluster_id",
                "parent_cluster_id",
                "level",
                "level_name",
                "member_node_ids",
                "representative_node_ids",
                "boundary_node_ids",
                "root_node_ids",
                "depth_min",
                "depth_max",
                "internal_edge_count",
                "external_edge_count");
            string clusterId = ReadClusterId(cluster, "cluster_id", path);
            Require(ids.Add(clusterId), $"Duplicate cluster_id '{clusterId}'.");
            int level = checked((int)ReadNonNegativeInteger(
                cluster,
                "level",
                path));
            Require(level is >= 0 and <= 2, $"{path}.level must be from 0 through 2.");
            string levelName = ReadNonEmptyString(cluster, "level_name", path);
            RequireEqual(levelName, HierarchyNames[level], $"{path}.level_name");
            string? parent = ReadNullableClusterId(
                cluster.GetProperty("parent_cluster_id"),
                $"{path}.parent_cluster_id");
            if (level == 0)
            {
                Require(parent is null, $"{path}.parent_cluster_id must be null at level 0.");
            }
            else
            {
                Require(parent is not null, $"{path}.parent_cluster_id is required below level 0.");
            }

            string[] members = ReadUniqueStringArray(
                cluster.GetProperty("member_node_ids"),
                $"{path}.member_node_ids",
                minimum: 1);
            string[] representatives = ReadUniqueStringArray(
                cluster.GetProperty("representative_node_ids"),
                $"{path}.representative_node_ids",
                minimum: 1,
                maximum: 3);
            string[] boundaries = ReadUniqueStringArray(
                cluster.GetProperty("boundary_node_ids"),
                $"{path}.boundary_node_ids");
            string[] roots = ReadUniqueStringArray(
                cluster.GetProperty("root_node_ids"),
                $"{path}.root_node_ids",
                minimum: 1);
            RequireSubset(representatives, members, $"{path}.representative_node_ids");
            RequireSubset(boundaries, members, $"{path}.boundary_node_ids");
            RequireSubset(roots, members, $"{path}.root_node_ids");
            BigInteger depthMin = ReadNonNegativeInteger(cluster, "depth_min", path);
            BigInteger depthMax = ReadNonNegativeInteger(cluster, "depth_max", path);
            Require(depthMin <= depthMax, $"{path} has an inverted depth range.");

            result.Add(new TopologyAtlasClusterReadModel(
                clusterId,
                parent,
                level,
                levelName,
                members,
                representatives,
                boundaries,
                roots,
                depthMin,
                depthMax,
                ReadNonNegativeInteger(cluster, "internal_edge_count", path),
                ReadNonNegativeInteger(cluster, "external_edge_count", path)));
            index++;
        }
        return result
            .OrderBy(cluster => cluster.Level)
            .ThenBy(cluster => cluster.ClusterId, StringComparer.Ordinal)
            .ToArray();
    }

    private static TopologyAtlasNodeReadModel[] ReadNodes(JsonElement value)
    {
        RequireKind(value, JsonValueKind.Array, "$.node_structure");
        var result = new List<TopologyAtlasNodeReadModel>();
        var ids = new HashSet<string>(StringComparer.Ordinal);
        int index = 0;
        foreach (JsonElement item in value.EnumerateArray())
        {
            string path = $"$.node_structure[{index}]";
            JsonElement node = RequireObject(
                item,
                path,
                "node_id",
                "component_id",
                "cluster_path",
                "articulation_status",
                "dominator_coverage_count",
                "dominator_coverage",
                "boundary_score",
                "k_core_level",
                "depth",
                "height",
                "structural_role");
            string nodeId = ReadNonEmptyString(node, "node_id", path);
            Require(ids.Add(nodeId), $"Duplicate node_id '{nodeId}'.");
            string articulation = ReadNonEmptyString(
                node,
                "articulation_status",
                path);
            Require(
                articulation is "ordinary" or "articulation-point",
                $"{path}.articulation_status is unsupported.");
            string role = ReadNonEmptyString(node, "structural_role", path);
            Require(StructuralRoles.Contains(role), $"{path}.structural_role is unsupported.");
            string[] clusterPath = ReadClusterIdArray(
                node.GetProperty("cluster_path"),
                $"{path}.cluster_path",
                3,
                3);
            BigInteger coverageCount = ReadNonNegativeInteger(
                node,
                "dominator_coverage_count",
                path);
            Require(coverageCount > 0, $"{path}.dominator_coverage_count must be positive.");
            result.Add(new TopologyAtlasNodeReadModel(
                nodeId,
                ReadClusterId(node, "component_id", path),
                clusterPath,
                articulation,
                coverageCount,
                ReadRational(
                    node.GetProperty("dominator_coverage"),
                    $"{path}.dominator_coverage"),
                ReadRational(
                    node.GetProperty("boundary_score"),
                    $"{path}.boundary_score"),
                ReadNonNegativeInteger(node, "k_core_level", path),
                ReadNonNegativeInteger(node, "depth", path),
                ReadNonNegativeInteger(node, "height", path),
                role));
            index++;
        }
        return result.OrderBy(node => node.NodeId, StringComparer.Ordinal).ToArray();
    }

    private static TopologyAtlasEdgeReadModel[] ReadEdges(JsonElement value)
    {
        RequireKind(value, JsonValueKind.Array, "$.edge_structure");
        var result = new List<TopologyAtlasEdgeReadModel>();
        var ids = new HashSet<string>(StringComparer.Ordinal);
        int index = 0;
        foreach (JsonElement item in value.EnumerateArray())
        {
            string path = $"$.edge_structure[{index}]";
            JsonElement edge = RequireObject(
                item,
                path,
                "dependency_id",
                "dependent_id",
                "edge_betweenness",
                "is_cut_bridge",
                "cluster_relation",
                "source_cluster_id",
                "target_cluster_id",
                "dependency_span");
            string dependency = ReadNonEmptyString(edge, "dependency_id", path);
            string dependent = ReadNonEmptyString(edge, "dependent_id", path);
            Require(
                ids.Add(dependency + "\u0000" + dependent),
                $"Duplicate edge structure '{dependency}' -> '{dependent}'.");
            string relation = ReadNonEmptyString(edge, "cluster_relation", path);
            Require(
                relation is "intra-cluster" or "inter-cluster",
                $"{path}.cluster_relation is unsupported.");
            BigInteger span = ReadNonNegativeInteger(edge, "dependency_span", path);
            Require(span > 0, $"{path}.dependency_span must be positive.");
            result.Add(new TopologyAtlasEdgeReadModel(
                dependency,
                dependent,
                ReadRational(
                    edge.GetProperty("edge_betweenness"),
                    $"{path}.edge_betweenness"),
                ReadBoolean(edge, "is_cut_bridge", path),
                relation,
                ReadClusterId(edge, "source_cluster_id", path),
                ReadClusterId(edge, "target_cluster_id", path),
                span));
            index++;
        }
        return result
            .OrderBy(edge => edge.DependencyId, StringComparer.Ordinal)
            .ThenBy(edge => edge.DependentId, StringComparer.Ordinal)
            .ToArray();
    }

    private static TopologyAtlasAffinityReadModel[] ReadAffinities(JsonElement value)
    {
        RequireKind(value, JsonValueKind.Array, "$.structural_affinities");
        var result = new List<TopologyAtlasAffinityReadModel>();
        var ids = new HashSet<string>(StringComparer.Ordinal);
        int index = 0;
        foreach (JsonElement item in value.EnumerateArray())
        {
            string path = $"$.structural_affinities[{index}]";
            JsonElement affinity = RequireObject(
                item,
                path,
                "source_node_id",
                "neighbor_node_id",
                "rank",
                "mutual_top_k",
                "direct_dependency",
                "shared_ancestor_jaccard",
                "shared_descendant_jaccard",
                "undirected_path_distance",
                "deepest_common_prerequisite_depth",
                "combined_rank");
            string source = ReadNonEmptyString(affinity, "source_node_id", path);
            string neighbor = ReadNonEmptyString(affinity, "neighbor_node_id", path);
            Require(!StringComparer.Ordinal.Equals(source, neighbor),
                $"{path} cannot be a self-affinity.");
            Require(ids.Add(source + "\u0000" + neighbor),
                $"Duplicate affinity '{source}' -> '{neighbor}'.");
            BigInteger rank = ReadNonNegativeInteger(affinity, "rank", path);
            BigInteger distance = ReadNonNegativeInteger(
                affinity,
                "undirected_path_distance",
                path);
            Require(rank > 0, $"{path}.rank must be positive.");
            Require(distance > 0, $"{path}.undirected_path_distance must be positive.");
            result.Add(new TopologyAtlasAffinityReadModel(
                source,
                neighbor,
                rank,
                ReadBoolean(affinity, "mutual_top_k", path),
                ReadBoolean(affinity, "direct_dependency", path),
                ReadRational(
                    affinity.GetProperty("shared_ancestor_jaccard"),
                    $"{path}.shared_ancestor_jaccard"),
                ReadRational(
                    affinity.GetProperty("shared_descendant_jaccard"),
                    $"{path}.shared_descendant_jaccard"),
                distance,
                ReadNullableNonNegativeInteger(
                    affinity.GetProperty("deepest_common_prerequisite_depth"),
                    $"{path}.deepest_common_prerequisite_depth"),
                ReadRational(
                    affinity.GetProperty("combined_rank"),
                    $"{path}.combined_rank")));
            index++;
        }
        return result
            .OrderBy(affinity => affinity.SourceNodeId, StringComparer.Ordinal)
            .ThenBy(affinity => affinity.Rank)
            .ThenBy(affinity => affinity.NeighborNodeId, StringComparer.Ordinal)
            .ToArray();
    }

    private static TopologyAtlasHierarchyReadModel[] ReadHierarchy(JsonElement value)
    {
        RequireKind(value, JsonValueKind.Array, "$.hierarchy");
        var result = new List<TopologyAtlasHierarchyReadModel>();
        var levels = new HashSet<int>();
        int index = 0;
        foreach (JsonElement item in value.EnumerateArray())
        {
            string path = $"$.hierarchy[{index}]";
            JsonElement levelValue = RequireObject(
                item,
                path,
                "level",
                "name",
                "cluster_ids");
            int level = checked((int)ReadNonNegativeInteger(
                levelValue,
                "level",
                path));
            Require(level is >= 0 and <= 2, $"{path}.level must be from 0 through 2.");
            Require(levels.Add(level), $"Duplicate hierarchy level {level}.");
            string name = ReadNonEmptyString(levelValue, "name", path);
            RequireEqual(name, HierarchyNames[level], $"{path}.name");
            result.Add(new TopologyAtlasHierarchyReadModel(
                level,
                name,
                ReadClusterIdArray(
                    levelValue.GetProperty("cluster_ids"),
                    $"{path}.cluster_ids")));
            index++;
        }
        Require(result.Count == 3, "$.hierarchy must contain exactly three levels.");
        return result.OrderBy(item => item.Level).ToArray();
    }

    private static void ValidateClosure(
        IReadOnlyList<TopologyAtlasClusterReadModel> clusters,
        IReadOnlyList<TopologyAtlasNodeReadModel> nodes,
        IReadOnlyList<TopologyAtlasEdgeReadModel> edges,
        IReadOnlyList<TopologyAtlasAffinityReadModel> affinities,
        IReadOnlyList<TopologyAtlasHierarchyReadModel> hierarchy)
    {
        var clusterById = clusters.ToDictionary(
            cluster => cluster.ClusterId,
            StringComparer.Ordinal);
        var nodeById = nodes.ToDictionary(node => node.NodeId, StringComparer.Ordinal);
        var nodeIds = nodeById.Keys.ToHashSet(StringComparer.Ordinal);

        for (int level = 0; level < 3; level++)
        {
            string[] expectedClusters = clusters
                .Where(cluster => cluster.Level == level)
                .Select(cluster => cluster.ClusterId)
                .Order(StringComparer.Ordinal)
                .ToArray();
            string[] publishedClusters = hierarchy[level].ClusterIds
                .Order(StringComparer.Ordinal)
                .ToArray();
            Require(expectedClusters.SequenceEqual(
                    publishedClusters,
                    StringComparer.Ordinal),
                $"Hierarchy level {level} does not close over its clusters.");

            var membership = new Dictionary<string, string>(StringComparer.Ordinal);
            foreach (TopologyAtlasClusterReadModel cluster in clusters.Where(
                cluster => cluster.Level == level))
            {
                foreach (string member in cluster.MemberNodeIds)
                {
                    Require(nodeIds.Contains(member),
                        $"Cluster {cluster.ClusterId} contains unknown node '{member}'.");
                    Require(membership.TryAdd(member, cluster.ClusterId),
                        $"Node '{member}' appears in more than one level-{level} cluster.");
                }
            }
            Require(membership.Count == nodeIds.Count,
                $"Level {level} clusters do not cover every atlas node.");
        }

        foreach (TopologyAtlasClusterReadModel cluster in clusters)
        {
            if (cluster.Level == 0)
            {
                continue;
            }
            Require(cluster.ParentClusterId is not null &&
                clusterById.TryGetValue(
                    cluster.ParentClusterId,
                    out TopologyAtlasClusterReadModel? parent),
                $"Cluster {cluster.ClusterId} has an unknown parent.");
            Require(parent!.Level == cluster.Level - 1,
                $"Cluster {cluster.ClusterId} parent is at the wrong hierarchy level.");
            Require(cluster.MemberNodeIds.All(parent.MemberNodeIds.Contains),
                $"Cluster {cluster.ClusterId} is not a subset of its parent.");
        }

        foreach (TopologyAtlasNodeReadModel node in nodes)
        {
            Require(node.ClusterPath.Count == 3,
                $"Node {node.NodeId} must have exactly three cluster path entries.");
            RequireEqual(node.ComponentId, node.ClusterPath[0],
                $"Node {node.NodeId} component_id");
            for (int level = 0; level < 3; level++)
            {
                string clusterId = node.ClusterPath[level];
                Require(clusterById.TryGetValue(
                        clusterId,
                        out TopologyAtlasClusterReadModel? cluster),
                    $"Node {node.NodeId} references unknown cluster {clusterId}.");
                Require(cluster!.Level == level &&
                    cluster.MemberNodeIds.Contains(node.NodeId, StringComparer.Ordinal),
                    $"Node {node.NodeId} cluster path does not match cluster membership.");
            }
        }

        foreach (TopologyAtlasEdgeReadModel edge in edges)
        {
            Require(nodeById.ContainsKey(edge.DependencyId),
                $"Edge references unknown dependency '{edge.DependencyId}'.");
            Require(nodeById.ContainsKey(edge.DependentId),
                $"Edge references unknown dependent '{edge.DependentId}'.");
            string expectedSource = nodeById[edge.DependencyId].ClusterPath[2];
            string expectedTarget = nodeById[edge.DependentId].ClusterPath[2];
            RequireEqual(edge.SourceClusterId, expectedSource,
                $"Edge {edge.DependencyId}->{edge.DependentId} source_cluster_id");
            RequireEqual(edge.TargetClusterId, expectedTarget,
                $"Edge {edge.DependencyId}->{edge.DependentId} target_cluster_id");
            string expectedRelation = StringComparer.Ordinal.Equals(
                expectedSource,
                expectedTarget)
                ? "intra-cluster"
                : "inter-cluster";
            RequireEqual(edge.ClusterRelation, expectedRelation,
                $"Edge {edge.DependencyId}->{edge.DependentId} cluster_relation");
        }

        foreach (TopologyAtlasAffinityReadModel affinity in affinities)
        {
            Require(nodeById.ContainsKey(affinity.SourceNodeId),
                $"Affinity references unknown source '{affinity.SourceNodeId}'.");
            Require(nodeById.ContainsKey(affinity.NeighborNodeId),
                $"Affinity references unknown neighbor '{affinity.NeighborNodeId}'.");
        }
        foreach (IGrouping<string, TopologyAtlasAffinityReadModel> source in affinities
            .GroupBy(affinity => affinity.SourceNodeId, StringComparer.Ordinal))
        {
            BigInteger expectedRank = BigInteger.One;
            foreach (TopologyAtlasAffinityReadModel affinity in source.OrderBy(
                affinity => affinity.Rank))
            {
                Require(affinity.Rank == expectedRank,
                    $"Affinity ranks for {source.Key} must be contiguous from 1.");
                expectedRank++;
            }
        }
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
        Require(missing is null, $"{path} is missing required property '{missing}'.");
        string? unknown = actual.Except(expected, StringComparer.Ordinal).FirstOrDefault();
        Require(unknown is null, $"{path} contains unknown property '{unknown}'.");
        return value;
    }

    private static string[] ReadUniqueStringArray(
        JsonElement value,
        string path,
        int minimum = 0,
        int? maximum = null)
    {
        RequireKind(value, JsonValueKind.Array, path);
        var result = new List<string>();
        var seen = new HashSet<string>(StringComparer.Ordinal);
        int index = 0;
        foreach (JsonElement item in value.EnumerateArray())
        {
            string text = ReadNonEmptyString(item, $"{path}[{index}]");
            Require(seen.Add(text), $"{path} contains duplicate '{text}'.");
            result.Add(text);
            index++;
        }
        Require(result.Count >= minimum,
            $"{path} must contain at least {minimum} item(s).");
        if (maximum is not null)
        {
            Require(result.Count <= maximum.Value,
                $"{path} must contain at most {maximum.Value} item(s).");
        }
        return result.ToArray();
    }

    private static string[] ReadClusterIdArray(
        JsonElement value,
        string path,
        int minimum = 0,
        int? maximum = null)
    {
        string[] result = ReadUniqueStringArray(value, path, minimum, maximum);
        foreach (string clusterId in result)
        {
            RequireClusterId(clusterId, path);
        }
        return result;
    }

    private static void RequireSubset(
        IReadOnlyList<string> values,
        IReadOnlyList<string> members,
        string path)
    {
        var memberSet = members.ToHashSet(StringComparer.Ordinal);
        Require(values.All(memberSet.Contains), $"{path} must be a member subset.");
    }

    private static string ReadClusterId(
        JsonElement parent,
        string name,
        string path)
    {
        string result = ReadNonEmptyString(parent, name, path);
        RequireClusterId(result, $"{path}.{name}");
        return result;
    }

    private static string? ReadNullableClusterId(JsonElement value, string path)
    {
        if (value.ValueKind == JsonValueKind.Null)
        {
            return null;
        }
        string result = ReadNonEmptyString(value, path);
        RequireClusterId(result, path);
        return result;
    }

    private static void RequireClusterId(string value, string path) =>
        Require(
            value.Length == "cluster:sha256:".Length + 64 &&
            value.StartsWith("cluster:sha256:", StringComparison.Ordinal) &&
            IsLowerHex(value["cluster:sha256:".Length..]),
            $"{path} must use cluster:sha256:<64hex>.");

    private static ExactNonNegativeRational ReadRational(
        JsonElement value,
        string path)
    {
        JsonElement rational = RequireObject(value, path, "numerator", "denominator");
        BigInteger numerator = ReadNonNegativeInteger(rational, "numerator", path);
        BigInteger denominator = ReadInteger(rational, "denominator", path);
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
        BigInteger result = ReadInteger(value, path);
        Require(result >= 0, $"{path} must be non-negative.");
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
            _ => throw new InvalidDataException($"{path}.{name} must be a boolean.")
        };
    }

    private static string ReadString(JsonElement parent, string name, string path) =>
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
        BigInteger result = ReadInteger(parent, name, path);
        Require(result >= 0, $"{path}.{name} must be non-negative.");
        return result;
    }

    private static BigInteger ReadInteger(
        JsonElement parent,
        string name,
        string path) =>
        ReadInteger(parent.GetProperty(name), $"{path}.{name}");

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
        TopologyAtlasBinding binding,
        string source)
    {
        RequireSha256(binding.TruthReleaseDigest, $"{source} truth_release_digest");
        RequireSha256(binding.CertifiedTopologyDigest,
            $"{source} certified_topology_digest");
        RequireSha256(binding.CertifiedAlgorithmProfileDigest,
            $"{source} certified_algorithm_profile_digest");
        RequireSha256(binding.AtlasAlgorithmProfileDigest,
            $"{source} atlas_algorithm_profile_digest");
        Require(binding.ProducerCommit.Length == 40 && IsLowerHex(binding.ProducerCommit),
            $"{source} producer_commit must be 40 lowercase hexadecimal characters.");
    }

    private static void RequireSha256(string value, string field) =>
        Require(value.Length == 71 &&
            value.StartsWith("sha256:", StringComparison.Ordinal) &&
            IsLowerHex(value["sha256:".Length..]),
            $"{field} must use sha256:<64hex>.");

    private static bool IsLowerHex(string value) => value.All(character =>
        character is >= '0' and <= '9' or >= 'a' and <= 'f');

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
            if (!reader.Read())
            {
                throw new InvalidDataException("Topology atlas is empty.");
            }
            ReadUniqueValue(ref reader);
            if (reader.Read())
            {
                throw new InvalidDataException("Topology atlas contains trailing content.");
            }
        }
        catch (JsonException exception)
        {
            throw new InvalidDataException(
                "Topology atlas is malformed JSON.",
                exception);
        }
    }

    private static void ReadUniqueValue(ref Utf8JsonReader reader)
    {
        if (reader.TokenType == JsonTokenType.StartObject)
        {
            var names = new HashSet<string>(StringComparer.Ordinal);
            while (reader.Read() && reader.TokenType != JsonTokenType.EndObject)
            {
                Require(reader.TokenType == JsonTokenType.PropertyName,
                    "Expected an object property name.");
                string name = reader.GetString()
                    ?? throw new InvalidDataException("Object property name is null.");
                Require(names.Add(name), $"Duplicate object member '{name}'.");
                Require(reader.Read(), $"Property '{name}' has no value.");
                ReadUniqueValue(ref reader);
            }
            Require(reader.TokenType == JsonTokenType.EndObject,
                "Unterminated object.");
            return;
        }

        if (reader.TokenType == JsonTokenType.StartArray)
        {
            while (reader.Read() && reader.TokenType != JsonTokenType.EndArray)
            {
                ReadUniqueValue(ref reader);
            }
            Require(reader.TokenType == JsonTokenType.EndArray,
                "Unterminated array.");
        }
    }

    private static void RequireKind(
        JsonElement value,
        JsonValueKind kind,
        string path) =>
        Require(value.ValueKind == kind, $"{path} must be {kind}.");

    private static void RequireEqual(string actual, string expected, string field) =>
        Require(StringComparer.Ordinal.Equals(actual, expected),
            $"{field} does not match the bound topology atlas coordinate.");

    private static void Require(bool condition, string message)
    {
        if (!condition)
        {
            throw new InvalidDataException(message);
        }
    }
}
