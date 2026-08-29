namespace Trureturing.Intuition.Core;

public static class HumanResearchCandidateSchemas
{
    public const string Candidate = "human-intuition-candidate.v1";
    public const string Receipt = "human-intuition-candidate-receipt.v1";
}

public sealed record HumanResearchCandidateContent(
    string TruthReleaseDigest,
    string TopologyDigest,
    string SourceCommit,
    string SourceTree,
    string SourceSurface,
    string HumanActor,
    IReadOnlyList<string> SelectedNodeIds,
    IReadOnlyList<string> SelectedEdgeIds,
    string HumanPrompt,
    string AgentResponseRef,
    string CandidateKind,
    string CandidateStatement,
    string Falsifier,
    string CreatedAt);

public sealed record HumanResearchCandidate(
    string Schema,
    string CandidateId,
    HumanResearchCandidateContent CandidateContent);

public sealed record HumanResearchCandidateReceipt(
    string Schema,
    string CandidateRef,
    string CandidateId,
    string TruthReleaseDigest,
    string TopologyDigest,
    string SourceSurface,
    string HumanActor,
    string RegisteredAt);

public sealed record HumanResearchCandidateRegistration(
    string CandidateRef,
    string ReceiptRef,
    string TruthReleaseDigest,
    string TopologyDigest);

public static class HumanResearchCandidateRegistrar
{
    public static HumanResearchCandidateRegistration Register(
        ArtifactStore store,
        HumanResearchCandidate candidate)
    {
        ArgumentNullException.ThrowIfNull(store);
        ContractValidator.Validate(candidate);
        string candidateRef = store.Put(candidate);
        var receipt = new HumanResearchCandidateReceipt(
            HumanResearchCandidateSchemas.Receipt,
            candidateRef,
            candidate.CandidateId,
            candidate.CandidateContent.TruthReleaseDigest,
            candidate.CandidateContent.TopologyDigest,
            candidate.CandidateContent.SourceSurface,
            candidate.CandidateContent.HumanActor,
            candidate.CandidateContent.CreatedAt);
        string receiptRef = store.Put(receipt);
        return new HumanResearchCandidateRegistration(
            candidateRef,
            receiptRef,
            candidate.CandidateContent.TruthReleaseDigest,
            candidate.CandidateContent.TopologyDigest);
    }
}
