namespace Trureturing.Intuition.Core;

public static partial class ContractValidator
{
    public static void Validate(IndependentSettlement value)
    {
        RequireSchema(value.Schema, Schemas.IndependentSettlement);
        RequireArtifactRef(value.StateRef, nameof(value.StateRef));
        RequireArtifactRef(value.ProposalRef, nameof(value.ProposalRef));
        RequireNonEmpty(value.SettlementAuthority, nameof(value.SettlementAuthority));
        if (string.Equals(value.SettlementAuthority, "agent", StringComparison.OrdinalIgnoreCase))
        {
            throw new InvalidOperationException("An agent cannot be settlement authority.");
        }
        RequireSortedUniqueRefs(value.ReceiptRefs, nameof(value.ReceiptRefs));
        RequireNonEmpty(value.IntakeMode, nameof(value.IntakeMode));
        RequireNonEmpty(value.Notes, nameof(value.Notes));
        if (value.SettledAtUnix < 0) throw new InvalidOperationException("settled_at_unix is negative.");
    }

    public static void Validate(FormalizationRequest value)
    {
        RequireSchema(value.Schema, Schemas.FormalizationRequest);
        RequireIdentifier(value.RequestId, nameof(value.RequestId));
        RequireArtifactRef(value.StateRef, nameof(value.StateRef));
        RequireArtifactRef(value.ProposalRef, nameof(value.ProposalRef));
        RequireArtifactRef(value.CandidateEditRef, nameof(value.CandidateEditRef));
        RequireArtifactRef(value.SettlementRef, nameof(value.SettlementRef));
        RequireNonEmpty(value.TargetRepository, nameof(value.TargetRepository));
        RequireNonEmpty(value.TargetBaseBranch, nameof(value.TargetBaseBranch));
        RequireNonEmpty(value.RequestedLemma, nameof(value.RequestedLemma));
        if (value.EndpointRefs.Count != 2) throw new InvalidOperationException("A bridge formalization request requires exactly two endpoints.");
        RequireSortedUniqueRefs(value.EndpointRefs, nameof(value.EndpointRefs));
        RequireSortedUniqueRefs(value.EvidenceRefs, nameof(value.EvidenceRefs));
        if (!value.MockWriteBack || value.PushAllowed)
        {
            throw new InvalidOperationException("v1 formalization requests are mock write-back artifacts and cannot push.");
        }
        if (value.RequestedAtUnix < 0) throw new InvalidOperationException("requested_at_unix is negative.");
    }

    public static void Validate(IntuitionLedger value)
    {
        RequireSchema(value.Schema, Schemas.Ledger);
        RequireArtifactRef(value.StateRef, nameof(value.StateRef));
        RequireArtifactRef(value.AllocationRef, nameof(value.AllocationRef));
        RequireNonEmpty(value.Advisory, nameof(value.Advisory));
        if (value.RecordedAtUnix < 0) throw new InvalidOperationException("recorded_at_unix is negative.");

        var previousId = string.Empty;
        foreach (var candidate in value.Candidates)
        {
            RequireIdentifier(candidate.CandidateId, nameof(candidate.CandidateId));
            if (previousId.Length != 0 && StringComparer.Ordinal.Compare(previousId, candidate.CandidateId) >= 0)
            {
                throw new InvalidOperationException("Ledger candidates must be strictly candidate-id sorted and unique.");
            }
            previousId = candidate.CandidateId;
            RequireArtifactRef(candidate.CandidateEditRef, nameof(candidate.CandidateEditRef));
            RequireArtifactRef(candidate.ProposalRef, nameof(candidate.ProposalRef));
            RequireArtifactRef(candidate.ValuationRef, nameof(candidate.ValuationRef));
            if (candidate.EndpointNodeIds.Count != 2) throw new InvalidOperationException("A bridge ledger entry requires two endpoint node ids.");
            RequireSortedUniqueStrings(candidate.EndpointNodeIds, nameof(candidate.EndpointNodeIds));
            if (candidate.EndpointRefs.Count != 2) throw new InvalidOperationException("A bridge ledger entry requires two endpoint refs.");
            RequireSortedUniqueRefs(candidate.EndpointRefs, nameof(candidate.EndpointRefs));
            RequireNonEmpty(candidate.ConjecturedBridge, nameof(candidate.ConjecturedBridge));
            if (candidate.OnParetoFront == candidate.Dominated)
            {
                throw new InvalidOperationException("A ledger candidate must be either Pareto-front or dominated, but not both.");
            }
            ValidateMetric(candidate.Worth.Novelty, "worth.novelty");
            ValidateMetric(candidate.Worth.Readiness, "worth.readiness");
            ValidateMetric(candidate.Worth.Realization, "worth.realization");
            ValidateMetric(candidate.Worth.ReceiptPotential, "worth.receipt_potential");
            RequireArtifactRef(candidate.SettlementRef, nameof(candidate.SettlementRef));
            if (candidate.FormalizationRequestRef is not null) RequireArtifactRef(candidate.FormalizationRequestRef, nameof(candidate.FormalizationRequestRef));
            if (candidate.Outcome == ResearchOutcome.Proved && candidate.FormalizationRequestRef is null)
            {
                throw new InvalidOperationException("Every proved ledger candidate requires a formalization request.");
            }
            if (candidate.Outcome != ResearchOutcome.Proved && candidate.FormalizationRequestRef is not null)
            {
                throw new InvalidOperationException("Only proved ledger candidates may carry formalization requests.");
            }
        }

        var calibration = value.Calibration;
        if (calibration.ProvedCount < 0 || calibration.RefutedCount < 0 || calibration.OpenCount < 0 || calibration.TotalCount < 0 || calibration.EvaluatedCount < 0)
        {
            throw new InvalidOperationException("Calibration counts cannot be negative.");
        }
        if (calibration.TotalCount != value.Candidates.Count || calibration.TotalCount != calibration.ProvedCount + calibration.RefutedCount + calibration.OpenCount)
        {
            throw new InvalidOperationException("Calibration counts do not cover the ledger candidates.");
        }
        if (calibration.EvaluatedCount != calibration.ProvedCount + calibration.RefutedCount)
        {
            throw new InvalidOperationException("Calibration evaluated count must exclude open candidates.");
        }
        var expectedHitRate = calibration.EvaluatedCount == 0 ? 0 : (double)calibration.ProvedCount / calibration.EvaluatedCount;
        if (!double.IsFinite(calibration.HitRate) || Math.Abs(calibration.HitRate - expectedHitRate) > 1e-12)
        {
            throw new InvalidOperationException("Calibration hit rate must equal proved / (proved + refuted).");
        }
    }

    public static void Validate(LocalDevFrozenTruthNode value)
    {
        RequireSchema(value.Schema, Schemas.LocalDevFrozenNode);
        RequireNonEmpty(value.NodeId, nameof(value.NodeId));
        RequireGitId(value.SourceCommit, nameof(value.SourceCommit));
        RequireGitId(value.SourceTree, nameof(value.SourceTree));
        RequireNonEmpty(value.SourcePath, nameof(value.SourcePath));
        RequireNonEmpty(value.StatementSummary, nameof(value.StatementSummary));
    }

    public static void Validate(LocalDevMockTruthSubset value)
    {
        RequireSchema(value.Schema, Schemas.LocalDevTruthSubset);
        RequireNonEmpty(value.AdapterIdentity, nameof(value.AdapterIdentity));
        RequireNonEmpty(value.SourceRepository, nameof(value.SourceRepository));
        RequireNonEmpty(value.SourceBranch, nameof(value.SourceBranch));
        RequireGitId(value.SourceCommit, nameof(value.SourceCommit));
        RequireGitId(value.SourceTree, nameof(value.SourceTree));
        RequireSortedUniqueRefs(value.NodeRefs, nameof(value.NodeRefs));
        if (value.NodeRefs.Count is < 8 or > 12) throw new InvalidOperationException("Local dev truth subset must contain 8-12 frozen nodes.");
        RequireNonEmpty(value.Caveat, nameof(value.Caveat));
    }

    public static void Validate(LocalDevMockTruthRelease value)
    {
        RequireSchema(value.Schema, Schemas.LocalDevTruthRelease);
        RequireNonEmpty(value.AdapterIdentity, nameof(value.AdapterIdentity));
        RequireGitId(value.SourceCommit, nameof(value.SourceCommit));
        RequireGitId(value.SourceTree, nameof(value.SourceTree));
        RequireArtifactRef(value.TruthGraphRef, nameof(value.TruthGraphRef));
        RequireArtifactRef(value.TruthExportRef, nameof(value.TruthExportRef));
        RequireNonEmpty(value.Caveat, nameof(value.Caveat));
    }

    public static void Validate(LocalDevMockSettlementEvidence value)
    {
        RequireSchema(value.Schema, Schemas.LocalDevSettlementEvidence);
        RequireArtifactRef(value.ProposalRef, nameof(value.ProposalRef));
        RequireNonEmpty(value.Finding, nameof(value.Finding));
        if (!value.MockEvidence) throw new InvalidOperationException("Local dev settlement evidence must remain explicitly mock.");
    }
}
