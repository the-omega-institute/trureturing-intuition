namespace Trureturing.Intuition.Core;

public static class StructureCounterfactualSchemas
{
    public const string Patch = "structure-edit-graph-patch.v1";
    public const string PatchReceipt = "structure-edit-graph-patch-receipt.v1";
    public const string Publication = "topology-counterfactual-publication.v1";
    public const string PublicationReceipt =
        "topology-counterfactual-publication-receipt.v1";
    public const string Valuation = "structure-counterfactual-valuation.v1";
    public const string ProjectionProfile =
        "topology-counterfactual-projection-v1";
    public const string ValuationProfile =
        "structure-counterfactual-worth-vector-v1";
    public const string Authority = "advisory";
}

public static class StructureGraphPatchOperationKinds
{
    public const string AddNode = "add-node";
    public const string AddEdge = "add-edge";
    public const string RemoveEdge = "remove-edge";
}

public sealed record StructureEditGraphPatchOperation(
    string OperationId,
    string Kind,
    string? NodeId,
    string? StableNodeId,
    string? SourcePath,
    string? ModuleName,
    string? DependencyId,
    string? DependentId,
    string? StableDependencyId,
    string? StableDependentId);

public sealed record StructureEditGraphPatchContent(
    string CandidateRef,
    string CandidateId,
    string TruthReleaseDigest,
    string TopologyAtlasDigest,
    string TopologyAtlasEvidenceDigest,
    IReadOnlyList<StructureEditGraphPatchOperation> Operations,
    IReadOnlyList<string> AssumptionRefs,
    string Rationale,
    bool ExplicitlySubmittedForCounterfactual,
    string Authority);

public sealed record StructureEditGraphPatch(
    string Schema,
    string PatchId,
    StructureEditGraphPatchContent PatchContent);

public sealed record StructureEditGraphPatchReceipt(
    string Schema,
    string PatchRef,
    string PatchId,
    string CandidateRef,
    string CandidateId,
    string TruthReleaseDigest,
    string TopologyAtlasDigest,
    string TopologyAtlasEvidenceDigest,
    int OperationCount,
    string Authority);

public sealed record TopologyCounterfactualProjection(
    bool Accepted,
    bool CycleRisk,
    bool AnalysisAvailable,
    IReadOnlyList<string> AffectedStableNodeIds,
    long ReachablePairGain,
    long ReachablePairLoss,
    long ShortestPathImprovementCount,
    long TotalPathCompression,
    long NewCutBridgeCount,
    long RemovedCutBridgeCount,
    IReadOnlyList<string> TouchedClusterIds,
    long NewInterfaceHypothesisCount,
    long RemovedInterfaceHypothesisCount);

public sealed record TopologyCounterfactualPublicationContent(
    string PatchRef,
    string PatchId,
    string CandidateRef,
    string CandidateId,
    string TruthReleaseDigest,
    string TopologyAtlasDigest,
    string TopologyAtlasEvidenceDigest,
    string TopologyCounterfactualResultDigest,
    string TopologyCounterfactualProfileDigest,
    string TopologyProducerCommit,
    TopologyCounterfactualProjection Projection,
    string ProjectionProfile,
    string Authority);

public sealed record TopologyCounterfactualPublication(
    string Schema,
    string PublicationId,
    TopologyCounterfactualPublicationContent PublicationContent);

public sealed record TopologyCounterfactualPublicationReceipt(
    string Schema,
    string PublicationRef,
    string PublicationId,
    string ResultRef,
    string PatchRef,
    string PatchId,
    string CandidateRef,
    string CandidateId,
    string TruthReleaseDigest,
    string TopologyAtlasDigest,
    string TopologyAtlasEvidenceDigest,
    string TopologyCounterfactualProfileDigest,
    string TopologyProducerCommit,
    string ProjectionProfile,
    string Authority);

public sealed record StructureCounterfactualWorthVector(
    long ReachabilityGain,
    long ReachabilityLoss,
    long PathCompressionGain,
    long InterfaceHypothesisGain,
    long InterfaceHypothesisLoss,
    long CutBridgeReduction,
    long CutBridgeCreation,
    long AffectedScope,
    long PatchOperationCost,
    long CycleRiskPenalty,
    bool FormalVerificationOpen);

public sealed record StructureCounterfactualValuationContent(
    string CandidateRef,
    string CandidateId,
    string PatchRef,
    string PatchId,
    string CounterfactualPublicationRef,
    string CounterfactualPublicationId,
    string CounterfactualResultRef,
    string TruthReleaseDigest,
    string TopologyAtlasDigest,
    string TopologyAtlasEvidenceDigest,
    TopologyCounterfactualProjection Projection,
    StructureCounterfactualWorthVector WorthVector,
    bool EligibleForFormalResearch,
    string BlockingReason,
    string ValuationProfile,
    string Authority);

public sealed record StructureCounterfactualValuation(
    string Schema,
    string ValuationId,
    StructureCounterfactualValuationContent ValuationContent);

public sealed record StructureCounterfactualRegistration(
    string PatchRef,
    string PatchReceiptRef,
    string CounterfactualPublicationRef,
    string CounterfactualPublicationReceiptRef,
    string CounterfactualResultRef,
    string ValuationRef,
    string ValuationId,
    bool EligibleForFormalResearch,
    string BlockingReason);
