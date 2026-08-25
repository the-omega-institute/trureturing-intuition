namespace Trureturing.Intuition.Core;

public static partial class ContractValidator
{
    public static void Validate(IndependentSettlement value)
    {
        RequireSchema(value.Schema, Schemas.IndependentSettlement);
        RequireArtifactRef(value.StateRef, nameof(value.StateRef));
        RequireArtifactRef(value.ProposalRef, nameof(value.ProposalRef));
        if (value.SettlementAuthority is not SettlementAuthorities.IndependentVerifier
            and not SettlementAuthorities.LocalDevMockIndependentVerifier)
        {
            throw new InvalidOperationException("settlement_authority is not an accepted independent settlement authority.");
        }
        var requiresEvidence = value.Outcome is ResearchOutcome.Proved or ResearchOutcome.Refuted;
        RequireSortedUniqueRefs(value.ReceiptRefs, nameof(value.ReceiptRefs), requireNonEmpty: requiresEvidence);
        RequireNonEmpty(value.IntakeMode, nameof(value.IntakeMode));
        RequireNonEmpty(value.Notes, nameof(value.Notes));
        if (value.SettledAtUnix < 0) throw new InvalidOperationException("settled_at_unix is negative.");
    }

    internal static void Validate(IndependentSettlement value, Func<string, bool> receiptExists)
    {
        Validate(value);
        foreach (var receiptRef in value.ReceiptRefs)
        {
            if (!receiptExists(receiptRef))
            {
                throw new InvalidOperationException($"receipt_refs contains missing or digest-invalid artifact {receiptRef}.");
            }
        }
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
        Validate(value.Neighborhood);
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
            if (!string.Equals(candidate.NeighborhoodId, value.Neighborhood.NeighborhoodId, StringComparison.Ordinal))
            {
                throw new InvalidOperationException("Ledger candidate neighborhood_id does not match its group.");
            }
            if (!string.Equals(candidate.TargetNodeId, value.Neighborhood.TargetNodeId, StringComparison.Ordinal))
            {
                throw new InvalidOperationException("Ledger candidate target_node_id does not match its group.");
            }
            RequireArtifactRef(candidate.CandidateEditRef, nameof(candidate.CandidateEditRef));
            RequireArtifactRef(candidate.ProposalRef, nameof(candidate.ProposalRef));
            RequireArtifactRef(candidate.ValuationRef, nameof(candidate.ValuationRef));
            if (candidate.EndpointNodeIds.Count != 2) throw new InvalidOperationException("A bridge ledger entry requires two endpoint node ids.");
            RequireSortedUniqueStrings(candidate.EndpointNodeIds, nameof(candidate.EndpointNodeIds));
            if (candidate.EndpointRefs.Count != 2) throw new InvalidOperationException("A bridge ledger entry requires two endpoint refs.");
            RequireSortedUniqueRefs(candidate.EndpointRefs, nameof(candidate.EndpointRefs));
            RequireNonEmpty(candidate.ConjecturedBridge, nameof(candidate.ConjecturedBridge));
            if (!candidate.EndpointNodeIds.Contains(candidate.TargetNodeId, StringComparer.Ordinal))
            {
                throw new InvalidOperationException("A ledger bridge must include its neighborhood target endpoint.");
            }
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

        var groupedCandidateIds = value.Neighborhood.Members.Select(static member => member.CandidateId);
        if (value.Candidates.Count != 0
            && !value.Candidates.Select(static candidate => candidate.CandidateId).SequenceEqual(groupedCandidateIds, StringComparer.Ordinal))
        {
            throw new InvalidOperationException("Ledger candidates must exactly cover their neighborhood group.");
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
