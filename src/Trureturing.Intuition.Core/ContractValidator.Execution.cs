using System.Security.Cryptography;
using System.Text;
using System.Text.Encodings.Web;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace Trureturing.Intuition.Core;

public static partial class ContractValidator
{
public static void Validate(OwnerAuthorization value)
    {
        RequireSchema(value.Schema, Schemas.Authorization);
        RequireArtifactRef(value.AllocationRef, nameof(value.AllocationRef));
        RequireSortedUniqueRefs(value.AuthorizedProposalRefs, nameof(value.AuthorizedProposalRefs));
        RequireNonEmpty(value.Owner, nameof(value.Owner));
        RequireNonEmpty(value.Reason, nameof(value.Reason));
    }

    public static void Validate(ResearchAttempt value)
    {
        RequireSchema(value.Schema, Schemas.Attempt);
        RequireIdentifier(value.AttemptId, nameof(value.AttemptId));
        RequireArtifactRef(value.StateRef, nameof(value.StateRef));
        RequireArtifactRef(value.ProposalRef, nameof(value.ProposalRef));
        RequireArtifactRef(value.ValuationRef, nameof(value.ValuationRef));
        RequireArtifactRef(value.AllocationRef, nameof(value.AllocationRef));
        RequireArtifactRef(value.AuthorizationRef, nameof(value.AuthorizationRef));
        ValidateBudget(value.Budget);
        RequireNonEmpty(value.ExecutorIdentity, nameof(value.ExecutorIdentity));
    }

    public static void Validate(IntuitionSettlement value)
    {
        RequireSchema(value.Schema, Schemas.Settlement);
        RequireArtifactRef(value.AttemptRef, nameof(value.AttemptRef));
        RequireNonEmpty(value.SettlementAuthority, nameof(value.SettlementAuthority));
        if (string.Equals(value.SettlementAuthority, "agent", StringComparison.OrdinalIgnoreCase)) throw new InvalidOperationException("An agent cannot be settlement authority.");
        RequireSortedUniqueRefs(value.ReceiptRefs, nameof(value.ReceiptRefs));
        ValidateBudget(value.ActualCost);
        RequireFiniteNonNegative(value.ObservedReachabilityGain, nameof(value.ObservedReachabilityGain));
        RequireFiniteNonNegative(value.ObservedPruningGain, nameof(value.ObservedPruningGain));
        if (value.Outcome == ResearchOutcome.InfrastructureFailure && (value.ObservedReachabilityGain != 0 || value.ObservedPruningGain != 0))
        {
            throw new InvalidOperationException("Infrastructure failure cannot claim mathematical knowledge gain.");
        }
    }

    public static void Validate(IntuitionRelease value)
    {
        RequireSchema(value.Schema, Schemas.Release);
        RequireArtifactRef(value.StateRef, nameof(value.StateRef));
        RequireArtifactRef(value.ProposalSetRef, nameof(value.ProposalSetRef));
        RequireArtifactRef(value.CritiqueSetRef, nameof(value.CritiqueSetRef));
        RequireArtifactRef(value.ValuationSetRef, nameof(value.ValuationSetRef));
        RequireArtifactRef(value.AllocationRef, nameof(value.AllocationRef));
        RequireSortedUniqueRefs(value.AttemptRefs, nameof(value.AttemptRefs));
        RequireSortedUniqueRefs(value.SettlementRefs, nameof(value.SettlementRefs));
        RequireSortedUniqueRefs(value.IndependentSettlementRefs, nameof(value.IndependentSettlementRefs));
        RequireSortedUniqueRefs(value.FormalizationRequestRefs, nameof(value.FormalizationRequestRefs));
        RequireArtifactRef(value.LedgerRef, nameof(value.LedgerRef));
        RequireArtifactRef(value.SourceTruthReleaseDigest, nameof(value.SourceTruthReleaseDigest));
    }

    public static void Validate(TemporalReplayCase value)
    {
        RequireSchema(value.Schema, Schemas.ReplayCase);
        RequireIdentifier(value.CaseId, nameof(value.CaseId));
        RequireArtifactRef(value.SourceTruthReleaseDigest, nameof(value.SourceTruthReleaseDigest));
        RequireArtifactRef(value.FutureTruthReleaseDigest, nameof(value.FutureTruthReleaseDigest));
        if (value.SourceTruthReleaseDigest == value.FutureTruthReleaseDigest) throw new InvalidOperationException("Replay source and future release must differ.");
        if (value.FutureCutoffUnix <= value.SourceCutoffUnix) throw new InvalidOperationException("Future cutoff must be after source cutoff.");
        RequireArtifactRef(value.TargetInterfaceRef, nameof(value.TargetInterfaceRef));
        RequireSortedUniqueRefs(value.CandidateUniverse, nameof(value.CandidateUniverse));
        ValidateBudget(value.Budget);
        RequireNonEmpty(value.VerificationProtocol, nameof(value.VerificationProtocol));
        RequireSortedUniqueRefs(value.SourceFeatureRefs, nameof(value.SourceFeatureRefs));
        RequireSortedUniqueRefs(value.ForbiddenFutureRefs, nameof(value.ForbiddenFutureRefs));
        RequireSortedUniqueRefs(value.FutureSettlementRefs, nameof(value.FutureSettlementRefs));
        var forbidden = value.ForbiddenFutureRefs.ToHashSet(StringComparer.Ordinal);
        var leak = value.SourceFeatureRefs.FirstOrDefault(forbidden.Contains);
        if (leak is not null) throw new InvalidOperationException($"Temporal leakage: source feature {leak} is future-owned.");
    }

    public static void Validate(ReplayScore value)
    {
        RequireSchema(value.Schema, Schemas.ReplayScore);
        RequireArtifactRef(value.CaseRef, nameof(value.CaseRef));
        RequireArtifactRef(value.ModelSnapshot, nameof(value.ModelSnapshot));
        foreach (var metric in new[] { value.PremiseRecallAtK, value.NdcgAtK, value.VerifiedClosureRate, value.VerifiedPruningRate })
        {
            if (!double.IsFinite(metric) || metric < 0 || metric > 1) throw new InvalidOperationException("Replay rate metric must be in [0,1].");
        }
        RequireFiniteNonNegative(value.BrierScore, nameof(value.BrierScore));
        ValidateBudget(value.ObservedCost);
    }

    public static void Validate(CalibrationReport value)
    {
        RequireSchema(value.Schema, Schemas.Calibration);
        RequireArtifactRef(value.ValuationSetRef, nameof(value.ValuationSetRef));
        RequireSortedUniqueRefs(value.SettlementRefs, nameof(value.SettlementRefs));
        if (value.Count < 0) throw new InvalidOperationException("Calibration count is negative.");
        RequireFiniteNonNegative(value.MulticlassBrierScore, nameof(value.MulticlassBrierScore));
        if (!double.IsFinite(value.TopOutcomeAccuracy) || value.TopOutcomeAccuracy < 0 || value.TopOutcomeAccuracy > 1) throw new InvalidOperationException("Accuracy must be in [0,1].");
        RequireFiniteNonNegative(value.MeanAbsoluteWallSecondsError, nameof(value.MeanAbsoluteWallSecondsError));
    }

    
}
