using System.Numerics;
using System.Security.Cryptography;
using System.Text;

namespace Trureturing.Intuition.Core;

public static partial class TopologyAtlasEvidenceReader
{
    private static void ValidateClosure(
        TopologyAtlasEvidenceBinding binding,
        int witnessLimit,
        IReadOnlyList<TopologyAtlasNodeIdentityEvidence> identities,
        IReadOnlyList<TopologyAtlasNodeTraitsEvidence> traits,
        IReadOnlyList<TopologyAtlasClusterInterfaceEvidence> interfaces,
        IReadOnlyList<TopologyAtlasAffinityWitnessEvidence> witnesses,
        TopologyAtlasReadModel atlas)
    {
        RequireEqual(
            binding.TruthReleaseDigest,
            atlas.Binding.TruthReleaseDigest,
            "evidence and Atlas truth_release_digest");
        RequireEqual(
            binding.CertifiedTopologyDigest,
            atlas.Binding.CertifiedTopologyDigest,
            "evidence and Atlas certified_topology_digest");
        RequireEqual(
            binding.ProducerCommit,
            atlas.Binding.ProducerCommit,
            "evidence and Atlas producer_commit");

        var atlasNodes = atlas.Nodes.ToDictionary(
            node => node.NodeId,
            StringComparer.Ordinal);
        var identitiesByNode = identities.ToDictionary(
            identity => identity.NodeId,
            StringComparer.Ordinal);
        var traitsByNode = traits.ToDictionary(
            item => item.NodeId,
            StringComparer.Ordinal);
        Require(
            identitiesByNode.Count == atlasNodes.Count &&
            identitiesByNode.Keys.ToHashSet(StringComparer.Ordinal)
                .SetEquals(atlasNodes.Keys),
            "Topology Atlas evidence identities do not close over every Atlas node.");
        Require(
            traitsByNode.Count == atlasNodes.Count &&
            traitsByNode.Keys.ToHashSet(StringComparer.Ordinal)
                .SetEquals(atlasNodes.Keys),
            "Topology Atlas evidence traits do not close over every Atlas node.");

        var incoming = atlas.Nodes.ToDictionary(
            node => node.NodeId,
            _ => 0,
            StringComparer.Ordinal);
        var outgoing = atlas.Nodes.ToDictionary(
            node => node.NodeId,
            _ => 0,
            StringComparer.Ordinal);
        foreach (TopologyAtlasEdgeReadModel edge in atlas.Edges)
        {
            incoming[edge.DependentId]++;
            outgoing[edge.DependencyId]++;
        }

        foreach (TopologyAtlasNodeTraitsEvidence item in traits)
        {
            TopologyAtlasNodeIdentityEvidence identity = identitiesByNode[item.NodeId];
            TopologyAtlasNodeReadModel node = atlasNodes[item.NodeId];
            RequireEqual(
                item.StableNodeId,
                identity.StableNodeId,
                $"stable identity for {item.NodeId}");
            RequireEqual(
                item.PrimaryRole,
                node.StructuralRole,
                $"primary structural role for {item.NodeId}");
            var traitSet = item.StructuralTraits.ToHashSet(StringComparer.Ordinal);
            foreach (TopologyStructuralTraitEvidenceReadModel evidence in item.Evidence)
            {
                Require(
                    traitSet.Contains(evidence.Trait),
                    $"Evidence rule {evidence.Rule} justifies an absent trait.");
                string? unknownWitness = evidence.WitnessNodeIds.FirstOrDefault(
                    witness => !atlasNodes.ContainsKey(witness));
                Require(
                    unknownWitness is null,
                    $"Trait evidence references unknown Atlas node '{unknownWitness}'.");
            }
            if (traitSet.Contains("foundation"))
            {
                Require(
                    incoming[item.NodeId] == 0,
                    $"Foundation trait for {item.NodeId} disagrees with Atlas in-degree.");
            }
            if (traitSet.Contains("bridge"))
            {
                Require(
                    node.ArticulationStatus == "articulation-point",
                    $"Bridge trait for {item.NodeId} lacks articulation evidence.");
            }
            if (traitSet.Contains("interface"))
            {
                Require(
                    node.BoundaryScore.Numerator > 0,
                    $"Interface trait for {item.NodeId} lacks positive boundary score.");
            }
            if (traitSet.Contains("specialized-leaf"))
            {
                Require(
                    outgoing[item.NodeId] == 0,
                    $"Specialized-leaf trait for {item.NodeId} disagrees with Atlas out-degree.");
            }
        }

        ValidateInterfaces(binding.TopologyAtlasDigest, interfaces, atlas);
        ValidateAffinityWitnesses(witnessLimit, witnesses, atlas);
    }

    private static void ValidateInterfaces(
        string topologyAtlasDigest,
        IReadOnlyList<TopologyAtlasClusterInterfaceEvidence> interfaces,
        TopologyAtlasReadModel atlas)
    {
        var expectedGroups = atlas.Edges
            .Where(edge => edge.ClusterRelation == "inter-cluster")
            .GroupBy(edge => LengthKey(edge.SourceClusterId, edge.TargetClusterId))
            .ToDictionary(
                group => group.Key,
                group => group
                    .OrderBy(edge => edge.DependencyId, StringComparer.Ordinal)
                    .ThenBy(edge => edge.DependentId, StringComparer.Ordinal)
                    .ToArray(),
                StringComparer.Ordinal);
        var actualGroups = interfaces.ToDictionary(
            item => LengthKey(item.SourceClusterId, item.TargetClusterId),
            StringComparer.Ordinal);
        Require(
            actualGroups.Count == expectedGroups.Count &&
            actualGroups.Keys.ToHashSet(StringComparer.Ordinal)
                .SetEquals(expectedGroups.Keys),
            "Cluster-interface evidence does not close over every inter-cluster Atlas edge group.");

        foreach ((string key, TopologyAtlasEdgeReadModel[] expectedEdges) in expectedGroups)
        {
            TopologyAtlasClusterInterfaceEvidence actual = actualGroups[key];
            _ = atlas.GetCluster(actual.SourceClusterId);
            _ = atlas.GetCluster(actual.TargetClusterId);
            RequireEqual(
                actual.InterfaceId,
                InterfaceId(
                    topologyAtlasDigest,
                    actual.SourceClusterId,
                    actual.TargetClusterId),
                $"interface_id for {actual.SourceClusterId} -> {actual.TargetClusterId}");
            Require(
                actual.CertifiedEdges.Count == expectedEdges.Length,
                $"Interface {actual.InterfaceId} has the wrong certified-edge count.");

            for (int index = 0; index < expectedEdges.Length; index++)
            {
                TopologyAtlasEdgeReadModel expected = expectedEdges[index];
                TopologyAtlasInterfaceEdgeEvidence edge = actual.CertifiedEdges[index];
                RequireEqual(edge.DependencyId, expected.DependencyId, "interface dependency_id");
                RequireEqual(edge.DependentId, expected.DependentId, "interface dependent_id");
                RequireEqual(
                    edge.EdgeId,
                    EdgeId(edge.DependencyId, edge.DependentId),
                    "interface edge_id");
                Require(
                    edge.IsCutBridge == expected.IsCutBridge,
                    $"Interface edge {edge.EdgeId} has inconsistent cut-bridge status.");
                Require(
                    edge.EdgeBetweenness == expected.EdgeBetweenness,
                    $"Interface edge {edge.EdgeId} has inconsistent betweenness.");
                Require(
                    edge.DependencySpan == expected.DependencySpan,
                    $"Interface edge {edge.EdgeId} has inconsistent dependency span.");
                RequireEqual(
                    expected.SourceClusterId,
                    actual.SourceClusterId,
                    "interface source_cluster_id");
                RequireEqual(
                    expected.TargetClusterId,
                    actual.TargetClusterId,
                    "interface target_cluster_id");
            }

            string[] sourceBoundary = expectedEdges
                .Select(edge => edge.DependencyId)
                .Distinct(StringComparer.Ordinal)
                .Order(StringComparer.Ordinal)
                .ToArray();
            string[] targetBoundary = expectedEdges
                .Select(edge => edge.DependentId)
                .Distinct(StringComparer.Ordinal)
                .Order(StringComparer.Ordinal)
                .ToArray();
            string[] cutEdges = expectedEdges
                .Where(edge => edge.IsCutBridge)
                .Select(edge => EdgeId(edge.DependencyId, edge.DependentId))
                .Order(StringComparer.Ordinal)
                .ToArray();
            Require(
                actual.SourceBoundaryNodeIds.SequenceEqual(
                    sourceBoundary,
                    StringComparer.Ordinal),
                $"Interface {actual.InterfaceId} has inconsistent source boundary nodes.");
            Require(
                actual.TargetBoundaryNodeIds.SequenceEqual(
                    targetBoundary,
                    StringComparer.Ordinal),
                $"Interface {actual.InterfaceId} has inconsistent target boundary nodes.");
            Require(
                actual.CutBridgeEdgeIds.SequenceEqual(cutEdges, StringComparer.Ordinal),
                $"Interface {actual.InterfaceId} has inconsistent cut-bridge IDs.");

            ExactNonNegativeRational total = expectedEdges.Aggregate(
                new ExactNonNegativeRational(BigInteger.Zero, BigInteger.One),
                (sum, edge) => Add(sum, edge.EdgeBetweenness));
            Require(
                actual.TotalEdgeBetweenness == total,
                $"Interface {actual.InterfaceId} has inconsistent total betweenness.");
            Require(
                actual.DependencySpanMin == expectedEdges.Min(edge => edge.DependencySpan) &&
                actual.DependencySpanMax == expectedEdges.Max(edge => edge.DependencySpan),
                $"Interface {actual.InterfaceId} has inconsistent dependency-span bounds.");
        }
    }

    private static void ValidateAffinityWitnesses(
        int witnessLimit,
        IReadOnlyList<TopologyAtlasAffinityWitnessEvidence> witnesses,
        TopologyAtlasReadModel atlas)
    {
        var expected = atlas.StructuralAffinities.ToDictionary(
            affinity => AffinityKey(
                affinity.SourceNodeId,
                affinity.NeighborNodeId,
                affinity.Rank),
            StringComparer.Ordinal);
        var actual = witnesses.ToDictionary(
            witness => AffinityKey(
                witness.SourceNodeId,
                witness.NeighborNodeId,
                witness.Rank),
            StringComparer.Ordinal);
        Require(
            actual.Count == expected.Count &&
            actual.Keys.ToHashSet(StringComparer.Ordinal).SetEquals(expected.Keys),
            "Affinity-witness evidence does not close over every Atlas affinity.");

        IReadOnlyDictionary<string, HashSet<string>> ancestors =
            BuildTransitiveClosure(atlas, reverse: true);
        IReadOnlyDictionary<string, HashSet<string>> descendants =
            BuildTransitiveClosure(atlas, reverse: false);

        foreach ((string key, TopologyAtlasAffinityReadModel affinity) in expected)
        {
            TopologyAtlasAffinityWitnessEvidence witness = actual[key];
            HashSet<string> sharedAncestors = ancestors[affinity.SourceNodeId]
                .Intersect(ancestors[affinity.NeighborNodeId], StringComparer.Ordinal)
                .ToHashSet(StringComparer.Ordinal);
            HashSet<string> sharedDescendants = descendants[affinity.SourceNodeId]
                .Intersect(descendants[affinity.NeighborNodeId], StringComparer.Ordinal)
                .ToHashSet(StringComparer.Ordinal);

            Require(
                witness.SharedPrerequisiteWitnessIds.All(sharedAncestors.Contains),
                $"Affinity witness {key} contains a node that is not a shared prerequisite.");
            Require(
                witness.SharedDependentWitnessIds.All(sharedDescendants.Contains),
                $"Affinity witness {key} contains a node that is not a shared dependent.");
            Require(
                witness.SharedPrerequisiteWitnessIds.Count ==
                    Math.Min(witnessLimit, sharedAncestors.Count),
                $"Affinity witness {key} has an incomplete prerequisite witness budget.");
            Require(
                witness.SharedDependentWitnessIds.Count ==
                    Math.Min(witnessLimit, sharedDescendants.Count),
                $"Affinity witness {key} has an incomplete dependent witness budget.");

            if (affinity.DeepestCommonPrerequisiteDepth is null)
            {
                Require(
                    witness.DeepestCommonPrerequisiteIds.Count == 0,
                    $"Affinity witness {key} reports a deepest prerequisite without Atlas depth.");
            }
            else
            {
                string[] deepest = sharedAncestors
                    .Where(nodeId =>
                        atlas.GetNode(nodeId).Depth ==
                        affinity.DeepestCommonPrerequisiteDepth.Value)
                    .Order(StringComparer.Ordinal)
                    .Take(witnessLimit)
                    .ToArray();
                Require(
                    witness.DeepestCommonPrerequisiteIds.SequenceEqual(
                        deepest,
                        StringComparer.Ordinal),
                    $"Affinity witness {key} has inconsistent deepest common prerequisites.");
            }
        }
    }

    private static IReadOnlyDictionary<string, HashSet<string>> BuildTransitiveClosure(
        TopologyAtlasReadModel atlas,
        bool reverse)
    {
        var adjacency = atlas.Nodes.ToDictionary(
            node => node.NodeId,
            _ => new List<string>(),
            StringComparer.Ordinal);
        foreach (TopologyAtlasEdgeReadModel edge in atlas.Edges)
        {
            string source = reverse ? edge.DependentId : edge.DependencyId;
            string target = reverse ? edge.DependencyId : edge.DependentId;
            adjacency[source].Add(target);
        }

        var result = new Dictionary<string, HashSet<string>>(StringComparer.Ordinal);
        foreach (string nodeId in adjacency.Keys.Order(StringComparer.Ordinal))
        {
            var visited = new HashSet<string>(StringComparer.Ordinal);
            var pending = new Stack<string>(adjacency[nodeId]);
            while (pending.TryPop(out string? current))
            {
                if (!visited.Add(current))
                {
                    continue;
                }
                foreach (string next in adjacency[current])
                {
                    pending.Push(next);
                }
            }
            result.Add(nodeId, visited);
        }
        return result;
    }

    private static ExactNonNegativeRational Add(
        ExactNonNegativeRational left,
        ExactNonNegativeRational right)
    {
        BigInteger numerator =
            left.Numerator * right.Denominator +
            right.Numerator * left.Denominator;
        BigInteger denominator = left.Denominator * right.Denominator;
        BigInteger divisor = BigInteger.GreatestCommonDivisor(numerator, denominator);
        return new ExactNonNegativeRational(
            numerator / divisor,
            denominator / divisor);
    }

    private static string EdgeId(string dependencyId, string dependentId) =>
        "edge:sha256:" + HashText(
            "topology-certified-edge.v1\n" +
            dependencyId + "\n" +
            dependentId + "\n");

    private static string InterfaceId(
        string topologyAtlasDigest,
        string sourceClusterId,
        string targetClusterId) =>
        "interface:sha256:" + HashText(
            "topology-cluster-interface.v1\n" +
            topologyAtlasDigest + "\n" +
            sourceClusterId + "\n" +
            targetClusterId + "\n");

    private static string HashText(string value) =>
        Convert.ToHexStringLower(
            SHA256.HashData(Encoding.UTF8.GetBytes(value)));

    private static string AffinityKey(
        string sourceNodeId,
        string neighborNodeId,
        BigInteger rank) =>
        LengthKey(sourceNodeId, neighborNodeId) + ":" +
        rank.ToString(System.Globalization.CultureInfo.InvariantCulture);
}
