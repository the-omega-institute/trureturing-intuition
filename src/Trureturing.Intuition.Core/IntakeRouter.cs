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
        if (envelope.Schema != Schemas.IntakeEnvelope) throw new InvalidOperationException("Unexpected intake envelope schema.");
        ContractValidator.ValidateCandidateEditSet(candidates);
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
            "shadow-pareto-bootstrap-v1");
        var requestRef = store.Put(request);
        var stateRef = store.Put(StateFactory.Create(request, receipt, receiptRef));
        return new IntakeRouterResult(stateRef, requestRef, receiptRef, targetRef, universeRef, candidateRefs, envelope.RunId, envelope.AgentMode);
    }
}
