using System.Collections.Immutable;
using System.Security.Cryptography;
using System.Text;
using System.Text.Encodings.Web;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace Trureturing.Intuition.Core;

public static class Schemas
{
    public const string TruthVerifierIdentity = "Trureturing.Truth";
    public const string TruthReceipt = "truth-release-verification-receipt.v1";
    public const string IntakeEnvelope = "intuition-intake-envelope.v1";
    public const string RunRequest = "intuition-run-request.v1";
    public const string TargetInterface = "target-interface.v1";
    public const string ResidualWitness = "residual-witness.v1";
    public const string ResidualUniverse = "residual-universe.v1";
    public const string CandidateEdit = "candidate-edit.v1";
    public const string State = "intuition-state.v1";
    public const string Proposal = "intuition-proposal.v1";
    public const string ProposalSet = "intuition-proposal-set.v1";
    public const string Critique = "intuition-critique.v1";
    public const string CritiqueSet = "intuition-critique-set.v1";
    public const string Valuation = "intuition-valuation.v1";
    public const string ValuationSet = "intuition-valuation-set.v1";
    public const string Allocation = "intuition-allocation.v1";
    public const string Authorization = "owner-authorization.v1";
    public const string Attempt = "research-attempt.v1";
    public const string Settlement = "intuition-settlement.v1";
    public const string IndependentSettlement = "independent-settlement.v1";
    public const string FormalizationRequest = "formalization-request.v1";
    public const string Ledger = "intuition-ledger.v1";
    public const string LocalDevFrozenNode = "local-dev-frozen-truth-node.v1";
    public const string LocalDevTruthSubset = "local-dev-mock-truth-subset.v1";
    public const string LocalDevTruthRelease = "local-dev-mock-truth-release.v1";
    public const string LocalDevSettlementEvidence = "local-dev-mock-settlement-evidence.v1";
    public const string Release = "intuition-release.v1";
    public const string ReplayCase = "temporal-replay-case.v1";
    public const string ReplayScore = "replay-score.v1";
    public const string Calibration = "calibration-report.v1";
}

public static class SettlementAuthorities
{
    public const string IndependentVerifier = "Trureturing.IndependentVerifier";
    public const string LocalDevMockIndependentVerifier = "local-dev-independent-settlement-fixture";
}

public enum AdequacyMode { ExactFormal, FiniteEnumerated, FiniteWitnessed, Statistical }
public enum ResidualUniverseKind { FiniteObserved, FormalComplete }
public enum CandidateKind { PremiseSet, Bridge, Subgoal, Abstraction, Reroot, Counterexample, DefinitionPackage, EvidenceAcquisition }
public enum CatalogStatus { Unsearched, Duplicate, Escaped }
public enum SemanticStatus { Unknown, ResidualWitnessed, FiniteObservedCover, FormallyRefining }
public enum CertificationStatus { Unattempted, Proved, Refuted, Wall, Duplicate, Trivial, Open, InfrastructureFailure }
public enum MetricStatus { Open, Measured }
public enum ResearchOutcome { Proved, Refuted, Wall, Duplicate, Trivial, Open, InfrastructureFailure }
public enum CoverageLevel { None, WitnessCut, FiniteObservedCover, FormalCover }
public enum LocalDevTruthSubsetKind { Graph, Export }
public enum ConceptRelation { DirectPrerequisite, DirectDependent, SiblingLemma }

public sealed record VerificationBudget(
    long VerifierCalls,
    long ExpandedStates,
    long GeneratedLemmas,
    long Tokens,
    double GpuSeconds,
    double WallSeconds,
    double HumanReviewMinutes);

public sealed record TruthReleaseVerificationReceipt(
    string Schema,
    string ReleaseDigest,
    string SourceCommit,
    string SourceTree,
    string TruthGraphRef,
    string TruthExportRef,
    string VerifiedBy,
    long VerifiedAtUnix);

public sealed record IntakeEnvelope(
    string Schema,
    string RunId,
    string TruthReleaseReceiptPath,
    string TargetInterfacePath,
    string ResidualUniversePath,
    IReadOnlyList<string> CandidateEditPaths,
    string HistoryCutoff,
    VerificationBudget Budget,
    string VerificationProtocol,
    string ModelSnapshot,
    string AgentMode,
    ConceptNeighborhood Neighborhood);

public sealed record IntuitionRunRequest(
    string Schema,
    string RunId,
    string TruthReleaseReceiptRef,
    string TargetInterfaceRef,
    string ResidualUniverseRef,
    IReadOnlyList<string> CandidateUniverse,
    string HistoryCutoff,
    VerificationBudget Budget,
    string VerificationProtocol,
    string ModelSnapshot,
    string SelectionMode,
    ConceptNeighborhood Neighborhood);

public sealed record ConceptNeighborhoodMember(
    string CandidateId,
    string RelatedNodeId,
    string RelatedNodeRef,
    ConceptRelation Relation);

public sealed record ConceptNeighborhood(
    string NeighborhoodId,
    string TargetNodeId,
    string TargetNodeRef,
    string ModuleId,
    string DomainId,
    int CandidateLimit,
    IReadOnlyList<ConceptNeighborhoodMember> Members);

public sealed record TargetInterface(
    string Schema,
    string TargetId,
    string CurrentReadoutRef,
    string TargetReadoutRef,
    string WorldRef,
    AdequacyMode AdequacyMode,
    string? FormalAdequacyReceiptRef);

public sealed record ResidualWitness(
    string Schema,
    string WitnessId,
    string TargetInterfaceRef,
    string StateXRef,
    string StateYRef,
    string CurrentEqualReceiptRef,
    string TargetDistinctReceiptRef,
    string EvidenceStatus);

public sealed record ResidualUniverse(
    string Schema,
    string UniverseId,
    string TargetInterfaceRef,
    ResidualUniverseKind Kind,
    IReadOnlyList<string> WitnessRefs,
    string? FormalCompletenessReceiptRef);

public sealed record CandidateEdit(
    string Schema,
    string CandidateId,
    CandidateKind CandidateKind,
    IReadOnlyList<string> Inputs,
    IReadOnlyList<string> Outputs,
    string RepresentationMap,
    IReadOnlyList<string> AssumptionMap,
    IReadOnlyList<string> PreservedInvariants,
    IReadOnlyList<string> ClaimedResidualCuts,
    string Falsifier,
    string VerificationRoute);

public sealed record IntuitionState(
    string Schema,
    string RunId,
    string TruthReleaseReceiptRef,
    string ReleaseDigest,
    string SourceCommit,
    string SourceTree,
    string TruthGraphRef,
    string TruthExportRef,
    string TargetInterfaceRef,
    string ResidualUniverseRef,
    IReadOnlyList<string> CandidateUniverse,
    string CandidateUniverseDigest,
    string HistoryCutoff,
    VerificationBudget Budget,
    string VerificationProtocol,
    string ModelSnapshot,
    string SelectionMode,
    bool ScalarizationAllowed,
    bool BaseWriteAllowed,
    ConceptNeighborhood Neighborhood);

public sealed record DiscoveryLedger(CatalogStatus CatalogStatus, SemanticStatus SemanticStatus, CertificationStatus CertificationStatus);

public sealed record IntuitionProposal(
    string Schema,
    string ProposalId,
    string CandidateId,
    string NeighborhoodId,
    string TargetNodeId,
    IReadOnlyList<string> EndpointNodeIds,
    string ConjecturedBridge,
    string StateRef,
    string CandidateEditRef,
    string ProposerSeat,
    IReadOnlyList<string> EvidenceRefs,
    DiscoveryLedger Discovery,
    string ModelSnapshot,
    string Falsifier,
    long FrozenAtUnix);

public sealed record IntuitionProposalSet(string Schema, string StateRef, IReadOnlyList<string> ProposalRefs);

public sealed record IntuitionCritique(
    string Schema,
    string ProposalRef,
    string Lens,
    string Verdict,
    IReadOnlyList<string> Findings,
    IReadOnlyList<string> EvidenceRefs,
    string ReviewerIdentity);

public sealed record IntuitionCritiqueSet(string Schema, string StateRef, string ProposalSetRef, IReadOnlyList<string> CritiqueRefs);

public sealed record MetricEvidence(MetricStatus Status, double? Value, string? ReceiptRef);
public sealed record WorthVector(MetricEvidence Novelty, MetricEvidence Readiness, MetricEvidence Realization, MetricEvidence ReceiptPotential);

public sealed record OutcomeDistribution(
    double Proved,
    double Refuted,
    double Wall,
    double Duplicate,
    double Trivial,
    double Open,
    double InfrastructureFailure)
{
    public double Sum => Proved + Refuted + Wall + Duplicate + Trivial + Open + InfrastructureFailure;
    public double For(ResearchOutcome outcome) => outcome switch
    {
        ResearchOutcome.Proved => Proved,
        ResearchOutcome.Refuted => Refuted,
        ResearchOutcome.Wall => Wall,
        ResearchOutcome.Duplicate => Duplicate,
        ResearchOutcome.Trivial => Trivial,
        ResearchOutcome.Open => Open,
        ResearchOutcome.InfrastructureFailure => InfrastructureFailure,
        _ => throw new ArgumentOutOfRangeException(nameof(outcome))
    };

    public ResearchOutcome TopOutcome()
    {
        return Enum.GetValues<ResearchOutcome>()
            .OrderByDescending(For)
            .ThenBy(static value => value)
            .First();
    }
}

public sealed record IntuitionValuation(
    string Schema,
    string ProposalRef,
    WorthVector Worth,
    VerificationBudget PredictedCost,
    OutcomeDistribution PredictedOutcomes,
    double PredictedReachabilityGain,
    double PredictedPruningGain,
    double Uncertainty,
    IReadOnlyList<string> EvidenceRefs,
    string ValuerIdentity,
    long FrozenAtUnix);

public sealed record IntuitionValuationSet(string Schema, string StateRef, string ProposalSetRef, string CritiqueSetRef, IReadOnlyList<string> ValuationRefs);

public sealed record IntuitionAllocation(
    string Schema,
    string StateRef,
    string ValuationSetRef,
    string Policy,
    IReadOnlyList<string> ParetoFront,
    IReadOnlyList<string> Dominated,
    IReadOnlyList<string> Incomparable,
    IReadOnlyList<string> SelectedForExecution,
    long AllocatedAtUnix);

public sealed record OwnerAuthorization(
    string Schema,
    string AllocationRef,
    IReadOnlyList<string> AuthorizedProposalRefs,
    string Owner,
    string Reason,
    long AuthorizedAtUnix);

public sealed record ResearchAttempt(
    string Schema,
    string AttemptId,
    string StateRef,
    string ProposalRef,
    string ValuationRef,
    string AllocationRef,
    string AuthorizationRef,
    VerificationBudget Budget,
    string ExecutorIdentity,
    long StartedAtUnix);

public sealed record IntuitionSettlement(
    string Schema,
    string AttemptRef,
    ResearchOutcome Outcome,
    string SettlementAuthority,
    IReadOnlyList<string> ReceiptRefs,
    VerificationBudget ActualCost,
    double ObservedReachabilityGain,
    double ObservedPruningGain,
    string Notes,
    long SettledAtUnix);

public sealed record IndependentSettlement(
    string Schema,
    string StateRef,
    string ProposalRef,
    ResearchOutcome Outcome,
    string SettlementAuthority,
    IReadOnlyList<string> ReceiptRefs,
    string IntakeMode,
    string Notes,
    long SettledAtUnix);

public sealed record FormalizationRequest(
    string Schema,
    string RequestId,
    string StateRef,
    string ProposalRef,
    string CandidateEditRef,
    string SettlementRef,
    string TargetRepository,
    string TargetBaseBranch,
    string RequestedLemma,
    IReadOnlyList<string> EndpointRefs,
    IReadOnlyList<string> EvidenceRefs,
    bool MockWriteBack,
    bool PushAllowed,
    long RequestedAtUnix);

public sealed record CalibrationSummary(
    int ProvedCount,
    int RefutedCount,
    int OpenCount,
    int TotalCount,
    int EvaluatedCount,
    double HitRate);

public sealed record IntuitionLedgerEntry(
    string CandidateId,
    string NeighborhoodId,
    string TargetNodeId,
    string CandidateEditRef,
    string ProposalRef,
    string ValuationRef,
    IReadOnlyList<string> EndpointNodeIds,
    IReadOnlyList<string> EndpointRefs,
    string ConjecturedBridge,
    DiscoveryLedger Discovery,
    WorthVector Worth,
    bool OnParetoFront,
    bool Dominated,
    bool Incomparable,
    string SettlementRef,
    ResearchOutcome Outcome,
    string? FormalizationRequestRef);

public sealed record IntuitionLedger(
    string Schema,
    string StateRef,
    string AllocationRef,
    ConceptNeighborhood Neighborhood,
    IReadOnlyList<IntuitionLedgerEntry> Candidates,
    CalibrationSummary Calibration,
    string Advisory,
    long RecordedAtUnix);

public sealed record LocalDevFrozenTruthNode(
    string Schema,
    string NodeId,
    string SourceCommit,
    string SourceTree,
    string SourcePath,
    string StatementSummary);

public sealed record LocalDevMockTruthSubset(
    string Schema,
    LocalDevTruthSubsetKind Kind,
    string AdapterIdentity,
    string SourceRepository,
    string SourceBranch,
    string SourceCommit,
    string SourceTree,
    IReadOnlyList<string> NodeRefs,
    string Caveat);

public sealed record LocalDevMockTruthRelease(
    string Schema,
    string AdapterIdentity,
    string SourceCommit,
    string SourceTree,
    string TruthGraphRef,
    string TruthExportRef,
    string Caveat);

public sealed record LocalDevMockSettlementEvidence(
    string Schema,
    string ProposalRef,
    ResearchOutcome Outcome,
    string Finding,
    bool MockEvidence);

public sealed record IntuitionRelease(
    string Schema,
    string StateRef,
    string ProposalSetRef,
    string CritiqueSetRef,
    string ValuationSetRef,
    string AllocationRef,
    IReadOnlyList<string> AttemptRefs,
    IReadOnlyList<string> SettlementRefs,
    IReadOnlyList<string> IndependentSettlementRefs,
    IReadOnlyList<string> FormalizationRequestRefs,
    string LedgerRef,
    string SourceTruthReleaseDigest,
    long PublishedAtUnix);

public sealed record TemporalReplayCase(
    string Schema,
    string CaseId,
    string SourceTruthReleaseDigest,
    string FutureTruthReleaseDigest,
    long SourceCutoffUnix,
    long FutureCutoffUnix,
    string TargetInterfaceRef,
    IReadOnlyList<string> CandidateUniverse,
    VerificationBudget Budget,
    string VerificationProtocol,
    IReadOnlyList<string> SourceFeatureRefs,
    IReadOnlyList<string> ForbiddenFutureRefs,
    IReadOnlyList<string> FutureSettlementRefs);

public sealed record ReplayScore(
    string Schema,
    string CaseRef,
    string ModelSnapshot,
    double PremiseRecallAtK,
    double NdcgAtK,
    double VerifiedClosureRate,
    double VerifiedPruningRate,
    double BrierScore,
    VerificationBudget ObservedCost);

public sealed record CalibrationReport(
    string Schema,
    string ValuationSetRef,
    IReadOnlyList<string> SettlementRefs,
    int Count,
    double MulticlassBrierScore,
    double TopOutcomeAccuracy,
    double MeanAbsoluteWallSecondsError,
    IReadOnlyDictionary<string, int> PredictedTopCounts,
    IReadOnlyDictionary<string, int> ActualCounts);

public sealed record CoverageAssessment(CoverageLevel Level, int CutCount, int UniverseCount, IReadOnlyList<string> MissingWitnessRefs, string? FormalReceiptRef);
public sealed record ParetoResult(IReadOnlyList<string> ParetoFront, IReadOnlyList<string> Dominated, IReadOnlyList<string> Incomparable);
