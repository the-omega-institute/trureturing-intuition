using System.Numerics;

namespace Trureturing.Intuition.Core;

public static class StructureCounterfactualSchemas
{
    public const string Publication =
        "trureturing.topology-counterfactual-publication.v1";
    public const string Valuation =
        "structure-counterfactual-valuation.v1";
    public const string Receipt =
        "structure-counterfactual-valuation-receipt.v1";
}

public sealed record TopologyCounterfactualPublicationCoordinate(
    string Schema,
    string CandidateRef,
    string CandidateId,
    string CounterfactualDigest,
    string TruthReleaseDigest,
    string TopologyAtlasDigest,
    string TopologyAtlasEvidenceDigest,
    string AlgorithmProfileDigest,
    string ProducerCommit);

public sealed record StructureCounterfactualMetrics(
    BigInteger ReachabilityGain,
    BigInteger ReachabilityLoss,
    BigInteger PathCompression,
    BigInteger ShortestPathChangeCount,
    BigInteger NewCutBridgeCount,
    BigInteger RemovedCutBridgeCount,
    BigInteger NewInterfaceCount,
    BigInteger RemovedInterfaceCount,
    BigInteger CycleWitnessCount,
    BigInteger AffectedStableNodeCount,
    BigInteger TouchedClusterCount,
    BigInteger EditOperationCount);

public sealed record StructureCounterfactualBenefitVector(
    BigInteger ReachabilityGain,
    BigInteger PathCompression,
    BigInteger RemovedCutBridges,
    BigInteger NewInterfaces,
    BigInteger ShortestPathChanges);

public sealed record StructureCounterfactualRiskVector(
    BigInteger ReachabilityLoss,
    BigInteger NewCutBridges,
    BigInteger RemovedInterfaces,
    BigInteger AffectedStableNodes,
    BigInteger TouchedClusters,
    BigInteger VerificationBurden,
    bool CycleRisk);

public sealed record StructureCounterfactualValuationContent(
    string CandidateRef,
    string CandidateId,
    string EpisodeRef,
    string EpisodeId,
    string CounterfactualRef,
    string CounterfactualDigest,
    string TruthReleaseDigest,
    string TopologyAtlasDigest,
    string TopologyAtlasEvidenceDigest,
    string AlgorithmProfileDigest,
    string ProducerCommit,
    bool Accepted,
    bool CycleRisk,
    IReadOnlyList<string> AffectedStableNodeIds,
    IReadOnlyList<string> TouchedClusterIds,
    StructureCounterfactualMetrics Metrics,
    StructureCounterfactualBenefitVector BenefitVector,
    StructureCounterfactualRiskVector RiskVector,
    string Classification,
    string UncertaintyClass,
    string Authority);

public sealed record StructureCounterfactualValuation(
    string Schema,
    string ValuationId,
    StructureCounterfactualValuationContent ValuationContent);

public sealed record StructureCounterfactualValuationReceipt(
    string Schema,
    string ValuationRef,
    string ValuationId,
    string CandidateRef,
    string CandidateId,
    string EpisodeRef,
    string CounterfactualRef,
    string CounterfactualDigest,
    string TruthReleaseDigest,
    string TopologyAtlasDigest,
    string Classification,
    string Authority);

public sealed record StructureCounterfactualValuationRegistration(
    string CounterfactualRef,
    string ValuationRef,
    string ReceiptRef,
    string ValuationId,
    string CandidateRef,
    string Classification,
    bool Accepted,
    bool CycleRisk,
    StructureCounterfactualBenefitVector BenefitVector,
    StructureCounterfactualRiskVector RiskVector);
