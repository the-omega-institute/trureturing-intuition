using System.Security.Cryptography;
using System.Text;
using System.Text.Encodings.Web;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace Trureturing.Intuition.Core;

public static partial class ContractValidator
{
public static void Validate(IntuitionProposal value)
    {
        RequireSchema(value.Schema, Schemas.Proposal);
        RequireIdentifier(value.ProposalId, nameof(value.ProposalId));
        RequireArtifactRef(value.StateRef, nameof(value.StateRef));
        RequireArtifactRef(value.CandidateEditRef, nameof(value.CandidateEditRef));
        RequireNonEmpty(value.ProposerSeat, nameof(value.ProposerSeat));
        RequireSortedUniqueRefs(value.EvidenceRefs, nameof(value.EvidenceRefs));
        RequireArtifactRef(value.ModelSnapshot, nameof(value.ModelSnapshot));
        RequireNonEmpty(value.Falsifier, nameof(value.Falsifier));
        if (value.FrozenAtUnix < 0) throw new InvalidOperationException("frozen_at_unix is negative.");
    }

    public static void Validate(IntuitionProposalSet value)
    {
        RequireSchema(value.Schema, Schemas.ProposalSet);
        RequireArtifactRef(value.StateRef, nameof(value.StateRef));
        RequireSortedUniqueRefs(value.ProposalRefs, nameof(value.ProposalRefs));
    }

    public static void Validate(IntuitionCritique value)
    {
        RequireSchema(value.Schema, Schemas.Critique);
        RequireArtifactRef(value.ProposalRef, nameof(value.ProposalRef));
        RequireNonEmpty(value.Lens, nameof(value.Lens));
        if (value.Verdict is not ("approve" or "comment" or "reject")) throw new InvalidOperationException("Unknown critique verdict.");
        RequireSortedUniqueStrings(value.Findings, nameof(value.Findings));
        RequireSortedUniqueRefs(value.EvidenceRefs, nameof(value.EvidenceRefs));
        RequireNonEmpty(value.ReviewerIdentity, nameof(value.ReviewerIdentity));
    }

    public static void Validate(IntuitionCritiqueSet value)
    {
        RequireSchema(value.Schema, Schemas.CritiqueSet);
        RequireArtifactRef(value.StateRef, nameof(value.StateRef));
        RequireArtifactRef(value.ProposalSetRef, nameof(value.ProposalSetRef));
        RequireSortedUniqueRefs(value.CritiqueRefs, nameof(value.CritiqueRefs));
    }

    public static void Validate(IntuitionValuation value)
    {
        RequireSchema(value.Schema, Schemas.Valuation);
        RequireArtifactRef(value.ProposalRef, nameof(value.ProposalRef));
        ValidateMetric(value.Worth.Novelty, "worth.novelty");
        ValidateMetric(value.Worth.Readiness, "worth.readiness");
        ValidateMetric(value.Worth.Realization, "worth.realization");
        ValidateMetric(value.Worth.ReceiptPotential, "worth.receipt_potential");
        ValidateBudget(value.PredictedCost);
        ValidateDistribution(value.PredictedOutcomes);
        RequireFiniteNonNegative(value.PredictedReachabilityGain, nameof(value.PredictedReachabilityGain));
        RequireFiniteNonNegative(value.PredictedPruningGain, nameof(value.PredictedPruningGain));
        if (!double.IsFinite(value.Uncertainty) || value.Uncertainty < 0 || value.Uncertainty > 1) throw new InvalidOperationException("uncertainty must be in [0,1].");
        RequireSortedUniqueRefs(value.EvidenceRefs, nameof(value.EvidenceRefs));
        RequireNonEmpty(value.ValuerIdentity, nameof(value.ValuerIdentity));
    }

    public static void Validate(IntuitionValuationSet value)
    {
        RequireSchema(value.Schema, Schemas.ValuationSet);
        RequireArtifactRef(value.StateRef, nameof(value.StateRef));
        RequireArtifactRef(value.ProposalSetRef, nameof(value.ProposalSetRef));
        RequireArtifactRef(value.CritiqueSetRef, nameof(value.CritiqueSetRef));
        RequireSortedUniqueRefs(value.ValuationRefs, nameof(value.ValuationRefs));
    }

    public static void Validate(IntuitionAllocation value)
    {
        RequireSchema(value.Schema, Schemas.Allocation);
        RequireArtifactRef(value.StateRef, nameof(value.StateRef));
        RequireArtifactRef(value.ValuationSetRef, nameof(value.ValuationSetRef));
        if (value.Policy != "shadow-pareto-bootstrap-v1") throw new InvalidOperationException("Unknown allocation policy.");
        RequireSortedUniqueRefs(value.ParetoFront, nameof(value.ParetoFront));
        RequireSortedUniqueRefs(value.Dominated, nameof(value.Dominated));
        RequireSortedUniqueRefs(value.Incomparable, nameof(value.Incomparable));
        if (value.SelectedForExecution.Count != 0) throw new InvalidOperationException("Shadow allocation cannot select execution.");
    }
}
