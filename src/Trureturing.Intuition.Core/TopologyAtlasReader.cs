using System.Globalization;
using System.Numerics;
using System.Text;
using System.Text.Json;

namespace Trureturing.Intuition.Core;

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
            JsonElement root = ExactObject(
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
            Equal(Text(root, "schema_version", "$"), Schema, "schema_version");

            var actualBinding = new TopologyAtlasBinding(
                Text(root, "truth_release_digest", "$"),
                Text(root, "certified_topology_digest", "$"),
                Text(root, "certified_algorithm_profile_digest", "$"),
                Text(root, "algorithm_profile_digest", "$"),
                Text(root, "producer_commit", "$"));
            ValidateBindingShape(actualBinding, "topology atlas");
            Equal(
                actualBinding.TruthReleaseDigest,
                expectedBinding.TruthReleaseDigest,
                "truth_release_digest");
            Equal(
                actualBinding.CertifiedTopologyDigest,
                expectedBinding.CertifiedTopologyDigest,
                "certified_topology_digest");
            Equal(
                actualBinding.CertifiedAlgorithmProfileDigest,
                expectedBinding.CertifiedAlgorithmProfileDigest,
                "certified_algorithm_profile_digest");
            Equal(
                actualBinding.AtlasAlgorithmProfileDigest,
                expectedBinding.AtlasAlgorithmProfileDigest,
                "algorithm_profile_digest");
            Equal(
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
        Kind(value, JsonValueKind.Array, "$.clusters");
        var result = new List<TopologyAtlasClusterReadModel>();
        var ids = new HashSet<string>(StringComparer.Ordinal);
        int index = 0;
        foreach (JsonElement item in value.EnumerateArray())
        {
            string path = $"$.clusters[{index}]";
            JsonElement cluster = ExactObject(
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
            string clusterId = ClusterId(cluster, "cluster_id", path);
            Require(ids.Add(clusterId), $"Duplicate cluster_id '{clusterId}'.");
            int level = SmallInt(cluster, "level", path, 0, 2);
            string levelName = NonEmptyText(cluster, "level_name", path);
            Equal(levelName, HierarchyNames[level], $"{path}.level_name");
            string? parent = NullableClusterId(
                cluster.GetProperty("parent_cluster_id"),
                $"{path}.parent_cluster_id");
            Require(
                level == 0 ? parent is null : parent is not null,
                level == 0
                    ? $"{path}.parent_cluster_id must be null at level 0."
                    : $"{path}.parent_cluster_id is required below level 0.");

            string[] members = UniqueTexts(
                cluster.GetProperty("member_node_ids"),
                $"{path}.member_node_ids",
                minimum: 1);
            string[] representatives = UniqueTexts(
                cluster.GetProperty("representative_node_ids"),
                $"{path}.representative_node_ids",
                minimum: 1,
                maximum: 3);
            string[] boundaries = UniqueTexts(
                cluster.GetProperty("boundary_node_ids"),
                $"{path}.boundary_node_ids");
            string[] roots = UniqueTexts(
                cluster.GetProperty("root_node_ids"),
                $"{path}.root_node_ids",
                minimum: 1);
            Subset(representatives, members, $"{path}.representative_node_ids");
            Subset(boundaries, members, $"{path}.boundary_node_ids");
            Subset(roots, members, $"{path}.root_node_ids");
            BigInteger depthMin = NonNegativeInteger(cluster, "depth_min", path);
            BigInteger depthMax = NonNegativeInteger(cluster, "depth_max", path);
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
                NonNegativeInteger(cluster, "internal_edge_count", path),
                NonNegativeInteger(cluster, "external_edge_count", path)));
            index++;
        }

        return result
            .OrderBy(cluster => cluster.Level)
            .ThenBy(cluster => cluster.ClusterId, StringComparer.Ordinal)
            .ToArray();
    }

    private static TopologyAtlasNodeReadModel[] ReadNodes(JsonElement value)
    {
        Kind(value, JsonValueKind.Array, "$.node_structure");
        var result = new List<TopologyAtlasNodeReadModel>();
        var ids = new HashSet<string>(StringComparer.Ordinal);
        int index = 0;
        foreach (JsonElement item in value.EnumerateArray())
        {
            string path = $"$.node_structure[{index}]";
            JsonElement node = ExactObject(
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
            string nodeId = NonEmptyText(node, "node_id", path);
            Require(ids.Add(nodeId), $"Duplicate node_id '{nodeId}'.");
            string articulation = NonEmptyText(
                node,
                "articulation_status",
                path);
            Require(
                articulation is "ordinary" or "articulation-point",
                $"{path}.articulation_status is unsupported.");
            string role = NonEmptyText(node, "structural_role", path);
            Require(StructuralRoles.Contains(role),
                $"{path}.structural_role is unsupported.");
            string[] clusterPath = ClusterIds(
                node.GetProperty("cluster_path"),
                $"{path}.cluster_path",
                minimum: 3,
                maximum: 3);
            BigInteger coverageCount = NonNegativeInteger(
                node,
                "dominator_coverage_count",
                path);
            Require(coverageCount > 0,
                $"{path}.dominator_coverage_count must be positive.");

            result.Add(new TopologyAtlasNodeReadModel(
                nodeId,
                ClusterId(node, "component_id", path),
                clusterPath,
                articulation,
                coverageCount,
                Rational(
                    node.GetProperty("dominator_coverage"),
                    $"{path}.dominator_coverage"),
                Rational(
                    node.GetProperty("boundary_score"),
                    $"{path}.boundary_score"),
                NonNegativeInteger(node, "k_core_level", path),
                NonNegativeInteger(node, "depth", path),
                NonNegativeInteger(node, "height", path),
                role));
            index++;
        }

        return result.OrderBy(node => node.NodeId, StringComparer.Ordinal).ToArray();
    }

    private static TopologyAtlasEdgeReadModel[] ReadEdges(JsonElement value)
    {
        Kind(value, JsonValueKind.Array, "$.edge_structure");
        var result = new List<TopologyAtlasEdgeReadModel>();
        var ids = new HashSet<string>(StringComparer.Ordinal);
        int index = 0;
        foreach (JsonElement item in value.EnumerateArray())
        {
            string path = $"$.edge_structure[{index}]";
            JsonElement edge = ExactObject(
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
            string dependency = NonEmptyText(edge, "dependency_id", path);
            string dependent = NonEmptyText(edge, "dependent_id", path);
            Require(ids.Add(dependency + "\u0000" + dependent),
                $"Duplicate edge structure '{dependency}' -> '{dependent}'.");
            string relation = NonEmptyText(edge, "cluster_relation", path);
            Require(relation is "intra-cluster" or "inter-cluster",
                $"{path}.cluster_relation is unsupported.");
            BigInteger span = NonNegativeInteger(edge, "dependency_span", path);
            Require(span > 0, $"{path}.dependency_span must be positive.");

            result.Add(new TopologyAtlasEdgeReadModel(
                dependency,
                dependent,
                Rational(
                    edge.GetProperty("edge_betweenness"),
                    $"{path}.edge_betweenness"),
                Boolean(edge, "is_cut_bridge", path),
                relation,
                ClusterId(edge, "source_cluster_id", path),
                ClusterId(edge, "target_cluster_id", path),
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
        Kind(value, JsonValueKind.Array, "$.structural_affinities");
        var result = new List<TopologyAtlasAffinityReadModel>();
        var ids = new HashSet<string>(StringComparer.Ordinal);
        int index = 0;
        foreach (JsonElement item in value.EnumerateArray())
        {
            string path = $"$.structural_affinities[{index}]";
            JsonElement affinity = ExactObject(
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
            string source = NonEmptyText(affinity, "source_node_id", path);
            string neighbor = NonEmptyText(affinity, "neighbor_node_id", path);
            Require(!StringComparer.Ordinal.Equals(source, neighbor),
                $"{path} cannot be a self-affinity.");
            Require(ids.Add(source + "\u0000" + neighbor),
                $"Duplicate affinity '{source}' -> '{neighbor}'.");
            BigInteger rank = NonNegativeInteger(affinity, "rank", path);
            BigInteger distance = NonNegativeInteger(
                affinity,
                "undirected_path_distance",
                path);
            Require(rank > 0, $"{path}.rank must be positive.");
            Require(distance > 0,
                $"{path}.undirected_path_distance must be positive.");

            result.Add(new TopologyAtlasAffinityReadModel(
                source,
                neighbor,
                rank,
                Boolean(affinity, "mutual_top_k", path),
                Boolean(affinity, "direct_dependency", path),
                Rational(
                    affinity.GetProperty("shared_ancestor_jaccard"),
                    $"{path}.shared_ancestor_jaccard"),
                Rational(
                    affinity.GetProperty("shared_descendant_jaccard"),
                    $"{path}.shared_descendant_jaccard"),
                distance,
                NullableNonNegativeInteger(
                    affinity.GetProperty("deepest_common_prerequisite_depth"),
                    $"{path}.deepest_common_prerequisite_depth"),
                Rational(
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
        Kind(value, JsonValueKind.Array, "$.hierarchy");
        var result = new List<TopologyAtlasHierarchyReadModel>();
        var levels = new HashSet<int>();
        int index = 0;
        foreach (JsonElement item in value.EnumerateArray())
        {
            string path = $"$.hierarchy[{index}]";
            JsonElement levelValue = ExactObject(
                item,
                path,
                "level",
                "name",
                "cluster_ids");
            int level = SmallInt(levelValue, "level", path, 0, 2);
            Require(levels.Add(level), $"Duplicate hierarchy level {level}.");
            string name = NonEmptyText(levelValue, "name", path);
            Equal(name, HierarchyNames[level], $"{path}.name");
            result.Add(new TopologyAtlasHierarchyReadModel(
                level,
                name,
                ClusterIds(
                    levelValue.GetProperty("cluster_ids"),
                    $"{path}.cluster_ids")));
            index++;
        }
        Require(result.Count == 3,
            "$.hierarchy must contain exactly three levels.");
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
            string[] expected = clusters
                .Where(cluster => cluster.Level == level)
                .Select(cluster => cluster.ClusterId)
                .Order(StringComparer.Ordinal)
                .ToArray();
            string[] published = hierarchy[level].ClusterIds
                .Order(StringComparer.Ordinal)
                .ToArray();
            Require(expected.SequenceEqual(published, StringComparer.Ordinal),
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
            if (cluster.ParentClusterId is null ||
                !clusterById.TryGetValue(
                    cluster.ParentClusterId,
                    out TopologyAtlasClusterReadModel? parent))
            {
                throw new InvalidDataException(
                    $"Cluster {cluster.ClusterId} has an unknown parent.");
            }
            Require(parent.Level == cluster.Level - 1,
                $"Cluster {cluster.ClusterId} parent is at the wrong hierarchy level.");
            Require(cluster.MemberNodeIds.All(member =>
                    parent.MemberNodeIds.Contains(member, StringComparer.Ordinal)),
                $"Cluster {cluster.ClusterId} is not a subset of its parent.");
        }

        foreach (TopologyAtlasNodeReadModel node in nodes)
        {
            Require(node.ClusterPath.Count == 3,
                $"Node {node.NodeId} must have exactly three cluster path entries.");
            Equal(node.ComponentId, node.ClusterPath[0],
                $"Node {node.NodeId} component_id");
            for (int level = 0; level < 3; level++)
            {
                string clusterId = node.ClusterPath[level];
                if (!clusterById.TryGetValue(
                    clusterId,
                    out TopologyAtlasClusterReadModel? cluster))
                {
                    throw new InvalidDataException(
                        $"Node {node.NodeId} references unknown cluster {clusterId}.");
                }
                Require(cluster.Level == level &&
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
            Equal(edge.SourceClusterId, expectedSource,
                $"Edge {edge.DependencyId}->{edge.DependentId} source_cluster_id");
            Equal(edge.TargetClusterId, expectedTarget,
                $"Edge {edge.DependencyId}->{edge.DependentId} target_cluster_id");
            string expectedRelation = StringComparer.Ordinal.Equals(
                expectedSource,
                expectedTarget)
                ? "intra-cluster"
                : "inter-cluster";
            Equal(edge.ClusterRelation, expectedRelation,
                $"Edge {edge.DependencyId}->{edge.DependentId} cluster_relation");
        }

        foreach (TopologyAtlasAffinityReadModel affinity in affinities)
        {
            Require(nodeById.ContainsKey(affinity.SourceNodeId),
                $"Affinity references unknown source '{affinity.SourceNodeId}'.");
            Require(nodeById.ContainsKey(affinity.NeighborNodeId),
                $"Affinity references unknown neighbor '{affinity.NeighborNodeId}'.");
        }
        foreach (IGrouping<string, TopologyAtlasAffinityReadModel> group in affinities
            .GroupBy(affinity => affinity.SourceNodeId, StringComparer.Ordinal))
        {
            BigInteger expectedRank = BigInteger.One;
            foreach (TopologyAtlasAffinityReadModel affinity in group.OrderBy(
                affinity => affinity.Rank))
            {
                Require(affinity.Rank == expectedRank,
                    $"Affinity ranks for {group.Key} must be contiguous from 1.");
                expectedRank++;
            }
        }
    }

    private static JsonElement ExactObject(
        JsonElement value,
        string path,
        params string[] properties)
    {
        Kind(value, JsonValueKind.Object, path);
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

    private static string[] UniqueTexts(
        JsonElement value,
        string path,
        int minimum = 0,
        int? maximum = null)
    {
        Kind(value, JsonValueKind.Array, path);
        var result = new List<string>();
        var seen = new HashSet<string>(StringComparer.Ordinal);
        int index = 0;
        foreach (JsonElement item in value.EnumerateArray())
        {
            string text = NonEmptyText(item, $"{path}[{index}]");
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

    private static string[] ClusterIds(
        JsonElement value,
        string path,
        int minimum = 0,
        int? maximum = null)
    {
        string[] result = UniqueTexts(value, path, minimum, maximum);
        foreach (string clusterId in result)
        {
            RequireClusterId(clusterId, path);
        }
        return result;
    }

    private static void Subset(
        IReadOnlyList<string> values,
        IReadOnlyList<string> members,
        string path)
    {
        var memberSet = members.ToHashSet(StringComparer.Ordinal);
        Require(values.All(memberSet.Contains),
            $"{path} must be a member subset.");
    }

    private static string ClusterId(
        JsonElement parent,
        string name,
        string path)
    {
        string result = NonEmptyText(parent, name, path);
        RequireClusterId(result, $"{path}.{name}");
        return result;
    }

    private static string? NullableClusterId(JsonElement value, string path)
    {
        if (value.ValueKind == JsonValueKind.Null)
        {
            return null;
        }
        string result = NonEmptyText(value, path);
        RequireClusterId(result, path);
        return result;
    }

    private static void RequireClusterId(string value, string path) =>
        Require(
            value.Length == "cluster:sha256:".Length + 64 &&
            value.StartsWith("cluster:sha256:", StringComparison.Ordinal) &&
            LowerHex(value["cluster:sha256:".Length..]),
            $"{path} must use cluster:sha256:<64hex>.");

    private static ExactNonNegativeRational Rational(
        JsonElement value,
        string path)
    {
        JsonElement rational = ExactObject(value, path, "numerator", "denominator");
        BigInteger numerator = NonNegativeInteger(rational, "numerator", path);
        BigInteger denominator = Integer(rational, "denominator", path);
        Require(denominator > 0, $"{path}.denominator must be positive.");
        Require(BigInteger.GreatestCommonDivisor(numerator, denominator) == BigInteger.One,
            $"{path} must be reduced.");
        return new ExactNonNegativeRational(numerator, denominator);
    }

    private static BigInteger? NullableNonNegativeInteger(
        JsonElement value,
        string path)
    {
        if (value.ValueKind == JsonValueKind.Null)
        {
            return null;
        }
        BigInteger result = Integer(value, path);
        Require(result >= 0, $"{path} must be non-negative.");
        return result;
    }

    private static bool Boolean(
        JsonElement parent,
        string name,
        string path)
    {
        JsonElement value = parent.GetProperty(name);
        return value.ValueKind switch
        {
            JsonValueKind.True => true,
            JsonValueKind.False => false,
            _ => throw new InvalidDataException(
                $"{path}.{name} must be a boolean.")
        };
    }

    private static string Text(JsonElement parent, string name, string path) =>
        Text(parent.GetProperty(name), $"{path}.{name}");

    private static string Text(JsonElement value, string path)
    {
        Kind(value, JsonValueKind.String, path);
        return value.GetString()!;
    }

    private static string NonEmptyText(
        JsonElement parent,
        string name,
        string path) =>
        NonEmptyText(parent.GetProperty(name), $"{path}.{name}");

    private static string NonEmptyText(JsonElement value, string path)
    {
        string result = Text(value, path);
        Require(result.Length > 0, $"{path} must not be empty.");
        return result;
    }

    private static int SmallInt(
        JsonElement parent,
        string name,
        string path,
        int minimum,
        int maximum)
    {
        BigInteger value = NonNegativeInteger(parent, name, path);
        Require(value >= minimum && value <= maximum,
            $"{path}.{name} must be from {minimum} through {maximum}.");
        return (int)value;
    }

    private static BigInteger NonNegativeInteger(
        JsonElement parent,
        string name,
        string path)
    {
        BigInteger result = Integer(parent, name, path);
        Require(result >= 0, $"{path}.{name} must be non-negative.");
        return result;
    }

    private static BigInteger Integer(
        JsonElement parent,
        string name,
        string path) =>
        Integer(parent.GetProperty(name), $"{path}.{name}");

    private static BigInteger Integer(JsonElement value, string path)
    {
        Kind(value, JsonValueKind.Number, path);
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
        Sha256(binding.TruthReleaseDigest, $"{source} truth_release_digest");
        Sha256(binding.CertifiedTopologyDigest,
            $"{source} certified_topology_digest");
        Sha256(binding.CertifiedAlgorithmProfileDigest,
            $"{source} certified_algorithm_profile_digest");
        Sha256(binding.AtlasAlgorithmProfileDigest,
            $"{source} atlas_algorithm_profile_digest");
        Require(binding.ProducerCommit.Length == 40 && LowerHex(binding.ProducerCommit),
            $"{source} producer_commit must be 40 lowercase hexadecimal characters.");
    }

    private static void Sha256(string value, string field) =>
        Require(value.Length == 71 &&
            value.StartsWith("sha256:", StringComparison.Ordinal) &&
            LowerHex(value["sha256:".Length..]),
            $"{field} must use sha256:<64hex>.");

    private static bool LowerHex(string value) => value.All(character =>
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
                throw new InvalidDataException(
                    "Topology atlas contains trailing content.");
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
                    ?? throw new InvalidDataException(
                        "Object property name is null.");
                Require(names.Add(name),
                    $"Duplicate object member '{name}'.");
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

    private static void Kind(
        JsonElement value,
        JsonValueKind expected,
        string path) =>
        Require(value.ValueKind == expected,
            $"{path} must be {expected}.");

    private static void Equal(string actual, string expected, string field) =>
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
