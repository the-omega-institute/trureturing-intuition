using System.Globalization;
using System.Numerics;

namespace Trureturing.Intuition.Core;

public sealed record TopologyAtlasEvidenceBinding(
    string TruthReleaseDigest,
    string CertifiedTopologyDigest,
    string TopologyAtlasDigest,
    string EvidenceAlgorithmProfileDigest,
    string ProducerCommit);

public sealed record TopologyAtlasNodeIdentityEvidence(
    string NodeId,
    string StableNodeId,
    string IdentityBasis,
    string? Gid,
    string SourcePath,
    string? ModuleName);

public sealed record TopologyStructuralTraitEvidenceReadModel(
    string Trait,
    string Rule,
    BigInteger? IntegerValue,
    ExactNonNegativeRational? RationalValue,
    IReadOnlyList<string> WitnessNodeIds);

public sealed record TopologyAtlasNodeTraitsEvidence(
    string NodeId,
    string StableNodeId,
    string PrimaryRole,
    IReadOnlyList<string> StructuralTraits,
    IReadOnlyList<TopologyStructuralTraitEvidenceReadModel> Evidence);

public sealed record TopologyAtlasInterfaceEdgeEvidence(
    string EdgeId,
    string DependencyId,
    string DependentId,
    bool IsCutBridge,
    ExactNonNegativeRational EdgeBetweenness,
    BigInteger DependencySpan);

public sealed record TopologyAtlasClusterInterfaceEvidence(
    string InterfaceId,
    string SourceClusterId,
    string TargetClusterId,
    IReadOnlyList<TopologyAtlasInterfaceEdgeEvidence> CertifiedEdges,
    IReadOnlyList<string> SourceBoundaryNodeIds,
    IReadOnlyList<string> TargetBoundaryNodeIds,
    IReadOnlyList<string> CutBridgeEdgeIds,
    ExactNonNegativeRational TotalEdgeBetweenness,
    BigInteger DependencySpanMin,
    BigInteger DependencySpanMax);

public sealed record TopologyAtlasAffinityWitnessEvidence(
    string SourceNodeId,
    string NeighborNodeId,
    BigInteger Rank,
    IReadOnlyList<string> SharedPrerequisiteWitnessIds,
    IReadOnlyList<string> SharedDependentWitnessIds,
    IReadOnlyList<string> DeepestCommonPrerequisiteIds);

public sealed class TopologyAtlasEvidenceReadModel
{
    private readonly IReadOnlyDictionary<string, TopologyAtlasNodeIdentityEvidence>
        _identityByNode;
    private readonly IReadOnlyDictionary<string, TopologyAtlasNodeIdentityEvidence>
        _identityByStableNode;
    private readonly IReadOnlyDictionary<string, TopologyAtlasNodeTraitsEvidence>
        _traitsByNode;
    private readonly IReadOnlyDictionary<string, TopologyAtlasClusterInterfaceEvidence>
        _interfaceByClusters;
    private readonly IReadOnlyDictionary<string, TopologyAtlasAffinityWitnessEvidence>
        _affinityByEndpoints;

    internal TopologyAtlasEvidenceReadModel(
        TopologyAtlasEvidenceBinding binding,
        int witnessLimit,
        IReadOnlyList<TopologyAtlasNodeIdentityEvidence> nodeIdentities,
        IReadOnlyList<TopologyAtlasNodeTraitsEvidence> nodeTraits,
        IReadOnlyList<TopologyAtlasClusterInterfaceEvidence> clusterInterfaces,
        IReadOnlyList<TopologyAtlasAffinityWitnessEvidence> affinityWitnesses)
    {
        Binding = binding;
        WitnessLimit = witnessLimit;
        NodeIdentities = nodeIdentities;
        NodeTraits = nodeTraits;
        ClusterInterfaces = clusterInterfaces;
        AffinityWitnesses = affinityWitnesses;
        _identityByNode = nodeIdentities.ToDictionary(
            item => item.NodeId,
            StringComparer.Ordinal);
        _identityByStableNode = nodeIdentities.ToDictionary(
            item => item.StableNodeId,
            StringComparer.Ordinal);
        _traitsByNode = nodeTraits.ToDictionary(
            item => item.NodeId,
            StringComparer.Ordinal);
        _interfaceByClusters = clusterInterfaces.ToDictionary(
            item => PairKey(item.SourceClusterId, item.TargetClusterId),
            StringComparer.Ordinal);
        _affinityByEndpoints = affinityWitnesses.ToDictionary(
            item => AffinityKey(item.SourceNodeId, item.NeighborNodeId, item.Rank),
            StringComparer.Ordinal);
    }

    public TopologyAtlasEvidenceBinding Binding { get; }
    public int WitnessLimit { get; }
    public IReadOnlyList<TopologyAtlasNodeIdentityEvidence> NodeIdentities { get; }
    public IReadOnlyList<TopologyAtlasNodeTraitsEvidence> NodeTraits { get; }
    public IReadOnlyList<TopologyAtlasClusterInterfaceEvidence> ClusterInterfaces { get; }
    public IReadOnlyList<TopologyAtlasAffinityWitnessEvidence> AffinityWitnesses { get; }

    public TopologyAtlasNodeIdentityEvidence GetIdentity(string nodeId) =>
        _identityByNode.TryGetValue(nodeId, out TopologyAtlasNodeIdentityEvidence? value)
            ? value
            : throw new InvalidDataException(
                $"Topology Atlas evidence does not contain node '{nodeId}'.");

    public TopologyAtlasNodeIdentityEvidence GetStableIdentity(string stableNodeId) =>
        _identityByStableNode.TryGetValue(
            stableNodeId,
            out TopologyAtlasNodeIdentityEvidence? value)
            ? value
            : throw new InvalidDataException(
                $"Topology Atlas evidence does not contain stable node '{stableNodeId}'.");

    public string StableNodeId(string nodeId) => GetIdentity(nodeId).StableNodeId;

    public TopologyAtlasNodeTraitsEvidence GetTraits(string nodeId) =>
        _traitsByNode.TryGetValue(nodeId, out TopologyAtlasNodeTraitsEvidence? value)
            ? value
            : throw new InvalidDataException(
                $"Topology Atlas evidence does not contain traits for '{nodeId}'.");

    public TopologyAtlasClusterInterfaceEvidence? FindInterface(
        string sourceClusterId,
        string targetClusterId) =>
        _interfaceByClusters.TryGetValue(
            PairKey(sourceClusterId, targetClusterId),
            out TopologyAtlasClusterInterfaceEvidence? value)
            ? value
            : null;

    public TopologyAtlasAffinityWitnessEvidence? FindAffinityWitness(
        string sourceNodeId,
        string neighborNodeId,
        BigInteger rank) =>
        _affinityByEndpoints.TryGetValue(
            AffinityKey(sourceNodeId, neighborNodeId, rank),
            out TopologyAtlasAffinityWitnessEvidence? value)
            ? value
            : null;

    private static string PairKey(string left, string right) =>
        left.Length.ToString(CultureInfo.InvariantCulture) + ":" + left + right;

    private static string AffinityKey(
        string sourceNodeId,
        string neighborNodeId,
        BigInteger rank) =>
        PairKey(sourceNodeId, neighborNodeId) + ":" +
        rank.ToString(CultureInfo.InvariantCulture);
}

public sealed record TopologyAtlasEvidenceLoadResult(
    bool Available,
    TopologyAtlasEvidenceReadModel? Evidence,
    string Status);
