namespace Trureturing.Intuition.Core;

public sealed record IntakeRouterResult(
    string StateRef,
    string RequestRef,
    string ReceiptRef,
    string TargetRef,
    string UniverseRef,
    IReadOnlyList<string> CandidateRefs,
    string RunId,
    string AgentMode);

public static class IntakeRouter
{
    public static IntakeRouterResult Freeze(
        ArtifactStore store,
        IntakeEnvelope envelope,
        TruthReleaseVerificationReceipt receipt,
        TargetInterface target,
        ResidualUniverse universe,
        IReadOnlyList<CandidateEdit> candidates)
    {
        ContractValidator.Validate(envelope);
        ContractValidator.ValidateCandidateEditSet(candidates);
        ValidateNeighborhoodCandidates(envelope.Neighborhood, candidates);
        var receiptRef = store.Put(receipt);
        var targetRef = store.Put(target);
        var universeRef = store.Put(universe);
        var candidateRefs = candidates.Select(store.Put).Order(StringComparer.Ordinal).ToArray();
        var request = new IntuitionRunRequest(
            Schemas.RunRequest,
            envelope.RunId,
            receiptRef,
            targetRef,
            universeRef,
            candidateRefs,
            envelope.HistoryCutoff,
            envelope.Budget,
            envelope.VerificationProtocol,
            envelope.ModelSnapshot,
            "shadow-pareto-bootstrap-v1",
            envelope.Neighborhood);
        var requestRef = store.Put(request);
        var stateRef = store.Put(StateFactory.Create(request, receipt, receiptRef));
        return new IntakeRouterResult(stateRef, requestRef, receiptRef, targetRef, universeRef, candidateRefs, envelope.RunId, envelope.AgentMode);
    }

    private static void ValidateNeighborhoodCandidates(ConceptNeighborhood neighborhood, IReadOnlyList<CandidateEdit> candidates)
    {
        var byId = candidates.ToDictionary(static candidate => candidate.CandidateId, StringComparer.Ordinal);
        if (byId.Count != candidates.Count) throw new InvalidOperationException("Neighborhood candidate ids must be unique.");
        var expectedIds = neighborhood.Members.Select(static member => member.CandidateId).ToArray();
        if (!byId.Keys.Order(StringComparer.Ordinal).SequenceEqual(expectedIds, StringComparer.Ordinal))
        {
            throw new InvalidOperationException("Candidate edits must exactly cover the declared concept neighborhood.");
        }

        foreach (var member in neighborhood.Members)
        {
            var candidate = byId[member.CandidateId];
            if (candidate.CandidateKind != CandidateKind.Bridge)
            {
                throw new InvalidOperationException("Every concept neighborhood member must emit a bridge candidate.");
            }
            var endpoints = candidate.Inputs.Concat(candidate.Outputs).Distinct(StringComparer.Ordinal).Order(StringComparer.Ordinal).ToArray();
            var expectedEndpoints = new[] { neighborhood.TargetNodeRef, member.RelatedNodeRef }.Order(StringComparer.Ordinal).ToArray();
            if (!endpoints.SequenceEqual(expectedEndpoints, StringComparer.Ordinal))
            {
                throw new InvalidOperationException($"Candidate {candidate.CandidateId} endpoints do not match its target/related neighborhood nodes.");
            }
        }
    }
}
