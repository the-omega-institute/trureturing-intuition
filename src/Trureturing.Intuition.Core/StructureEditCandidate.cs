namespace Trureturing.Intuition.Core;

public static class StructureEditCandidateSchemas
{
    public const string DraftSet = "structure-edit-candidate-draft-set.v1";
    public const string CandidateSet = "structure-edit-candidate-set.v1";
    public const string Receipt = "structure-edit-candidate-set-receipt.v1";
    public const string Context = "structure-edit-candidate-context.v1";
    public const string GenerationProfile = "structure-edit-candidate-v1";
}

public static class StructureCandidateKinds
{
    public const string EvidenceAcquisition = "evidence-acquisition";
    public const string Abstraction = "abstraction";
    public const string Bridge = "bridge";
    public const string Counterexample = "counterexample";
    public const string DefinitionPackage = "definition-package";
    public const string PremiseSet = "premise-set";
    public const string Subgoal = "subgoal";
    public const string RepresentationChange = "representation-change";
    public const string OpenQuestion = "open-question";
    public const string Reroot = "reroot";
}

public sealed record StructureAffinityEvidenceRef(
    string SourceNodeId,
    string NeighborNodeId,
    int Rank);

public sealed record StructureEditCandidateDraft(
    string EditKind,
    IReadOnlyList<string> AnchorNodeIds,
    IReadOnlyList<string> AnchorClusterIds,
    IReadOnlyList<string> InterfaceEvidenceIds,
    IReadOnlyList<StructureAffinityEvidenceRef> AffinityEvidence,
    string CandidateStatement,
    string RepresentationMap,
    IReadOnlyList<string> AssumptionMap,
    IReadOnlyList<string> PreservedInvariants,
    string Falsifier,
    string VerificationRoute);

public sealed record StructureEditCandidateDraftSet(
    string Schema,
    string EpisodeRef,
    string EpisodeReceiptRef,
    string TopologyAtlasEvidenceInputReceiptRef,
    string GeneratedBy,
    string ModelSnapshot,
    IReadOnlyList<StructureEditCandidateDraft> Candidates,
    string GeneratedAt);

public sealed record StructureEditCandidateContent(
    string EditKind,
    string CandidateKind,
    string ClaimStatus,
    IReadOnlyList<string> AnchorNodeIds,
    IReadOnlyList<string> AnchorStableNodeIds,
    IReadOnlyList<string> AnchorClusterIds,
    IReadOnlyList<string> InterfaceEvidenceIds,
    IReadOnlyList<StructureAffinityEvidenceRef> AffinityEvidence,
    string CandidateStatement,
    string RepresentationMap,
    IReadOnlyList<string> AssumptionMap,
    IReadOnlyList<string> PreservedInvariants,
    string Falsifier,
    string VerificationRoute,
    string TopologyLoweringStatus);

public sealed record StructureEditCandidate(
    string CandidateId,
    StructureEditCandidateContent CandidateContent);

public sealed record StructureEditCandidateSetContent(
    string EpisodeRef,
    string EpisodeReceiptRef,
    string TopologyAtlasEvidenceInputReceiptRef,
    string TruthReleaseDigest,
    string CertifiedTopologyDigest,
    string TopologyAtlasDigest,
    string TopologyAtlasEvidenceDigest,
    string GeneratedBy,
    string ModelSnapshot,
    string GenerationProfile,
    IReadOnlyList<StructureEditCandidate> Candidates,
    string GeneratedAt);

public sealed record StructureEditCandidateSet(
    string Schema,
    string CandidateSetId,
    StructureEditCandidateSetContent CandidateSetContent);

public sealed record StructureEditCandidateSetReceipt(
    string Schema,
    string CandidateSetRef,
    string CandidateSetId,
    string EpisodeRef,
    string EpisodeReceiptRef,
    string TopologyAtlasEvidenceInputReceiptRef,
    string TruthReleaseDigest,
    string TopologyAtlasDigest,
    string TopologyAtlasEvidenceDigest,
    IReadOnlyList<string> CandidateIds,
    string GenerationProfile);

public sealed record StructureEditCandidateSetRegistration(
    string CandidateSetRef,
    string ReceiptRef,
    string CandidateSetId,
    IReadOnlyList<string> CandidateIds,
    int CandidateCount,
    string TruthReleaseDigest,
    string TopologyAtlasEvidenceDigest);

public sealed record StructureEditCandidateContextNode(
    string NodeId,
    string StableNodeId,
    string PrimaryRole,
    IReadOnlyList<string> StructuralTraits,
    IReadOnlyList<string> ClusterPath);

public sealed record StructureEditCandidateContextInterface(
    string InterfaceId,
    string SourceClusterId,
    string TargetClusterId,
    IReadOnlyList<string> SourceBoundaryNodeIds,
    IReadOnlyList<string> TargetBoundaryNodeIds,
    IReadOnlyList<string> CutBridgeEdgeIds);

public sealed record StructureEditCandidateContext(
    string Schema,
    string EpisodeRef,
    string EpisodeReceiptRef,
    string TopologyAtlasEvidenceInputReceiptRef,
    string TruthReleaseDigest,
    string TopologyAtlasDigest,
    string TopologyAtlasEvidenceDigest,
    string HumanIntent,
    string SelectionKind,
    string GestureKind,
    IReadOnlyList<string> AllowedEditKinds,
    int CandidateLimit,
    IReadOnlyList<StructureEditCandidateContextNode> AnchorNodes,
    IReadOnlyList<string> AnchorClusterIds,
    IReadOnlyList<StructureEditCandidateContextInterface> RelevantInterfaces,
    IReadOnlyList<TopologyAtlasAffinityWitnessEvidence> RelevantAffinityWitnesses);
