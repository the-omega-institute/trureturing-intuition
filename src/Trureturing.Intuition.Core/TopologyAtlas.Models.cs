using System.Collections.Immutable;
using System.Numerics;

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
