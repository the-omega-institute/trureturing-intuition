using System.Numerics;

namespace Trureturing.Intuition.Core;

public static class StructureEditSettlementSchemas
{
    public const string DeltaPublication =
        "trureturing.topology-atlas-delta-publication.v1";
    public const string Settlement = "structure-edit-settlement.v1";
    public const string Receipt = "structure-edit-settlement-receipt.v1";
}

public static class StructureEditSettlementStatuses
{
    public const string VerifiedAndRealized = "verified-and-realized";
    public const string VerifiedNotYetRealized = "verified-not-yet-realized";
    public const string Refuted = "refuted";
    public const string Unresolved = "unresolved";
    public const string InfrastructureFailure = "infrastructure-failure";
    public const string CounterfactualRejected = "counterfactual-rejected";
}

public sealed record TopologyAtlasDeltaPublicationCoordinate(
    string Schema,
    string DeltaDigest,
    string FromTruthReleaseDigest,
    string ToTruthReleaseDigest,
    string FromTopologyAtlasDigest,
    string ToTopologyAtlasDigest,
    string FromEvidenceDigest,
    string ToEvidenceDigest,
    string AlgorithmProfileDigest,
    string ProducerCommit);

public sealed record StructureEditOperationSettlement(
    int OperationOrdinal,
    string Operation,
    string StableSubjectId,
    string? StableObjectId,
    string ExpectedDeltaRelation,
    string? ObservedDeltaRelation,
    string Outcome,
    string? FromNodeId,
    string? ToNodeId,
    string? FromDependentId,
    string? ToDependentId);

public sealed record StructureEditSettlementCounts(
    BigInteger OperationCount,
    BigInteger RealizedCount,
    BigInteger NotRealizedCount,
    BigInteger AlreadyPresentCount,
    BigInteger ContradictedCount);

public sealed record StructureEditRealizedDeltaSummary(
    BigInteger NodesAdded,
    BigInteger NodesRetired,
    BigInteger EdgesAdded,
    BigInteger EdgesRemoved,
    BigInteger ClusterSplits,
    BigInteger ClusterMerges,
    BigInteger ClusterReorganizations,
    BigInteger FrontierEntered,
    BigInteger FrontierLeft);

public sealed record StructureEditSettlementContent(
    string CandidateRef,
    string CandidateId,
    string EpisodeRef,
    string EpisodeId,
    string CounterfactualValuationRef,
    string CounterfactualValuationId,
    string CounterfactualRef,
    string FormalizationResultRef,
    string FormalizationResultId,
    string FormalizationRequestRef,
    string AtlasDeltaRef,
    string AtlasDeltaDigest,
    string FromTruthReleaseDigest,
    string ToTruthReleaseDigest,
    string FromTopologyAtlasDigest,
    string ToTopologyAtlasDigest,
    string FromEvidenceDigest,
    string ToEvidenceDigest,
    string FormalizationOutcome,
    string CounterfactualClassification,
    IReadOnlyList<StructureEditOperationSettlement> OperationSettlements,
    StructureEditSettlementCounts Counts,
    StructureCounterfactualBenefitVector PredictedBenefitVector,
    StructureCounterfactualRiskVector PredictedRiskVector,
    StructureEditRealizedDeltaSummary RealizedDeltaSummary,
    string SettlementStatus,
    string CalibrationClass,
    string Authority);

public sealed record StructureEditSettlement(
    string Schema,
    string SettlementId,
    StructureEditSettlementContent SettlementContent);

public sealed record StructureEditSettlementReceipt(
    string Schema,
    string SettlementRef,
    string SettlementId,
    string CandidateRef,
    string CandidateId,
    string CounterfactualValuationRef,
    string FormalizationResultRef,
    string AtlasDeltaRef,
    string FromTruthReleaseDigest,
    string ToTruthReleaseDigest,
    string SettlementStatus,
    string CalibrationClass,
    string Authority);

public sealed record StructureEditSettlementRegistration(
    string FormalizationResultRef,
    string AtlasDeltaRef,
    string SettlementRef,
    string ReceiptRef,
    string SettlementId,
    string CandidateRef,
    string FromTruthReleaseDigest,
    string ToTruthReleaseDigest,
    string SettlementStatus,
    string CalibrationClass,
    StructureEditSettlementCounts Counts);
