using Trureturing.Intuition.Core;

internal static partial class Cli
{
    private static int RegisterHumanCandidate(
        ArtifactStore store,
        string input)
    {
        HumanResearchCandidate candidate =
            CanonicalJson.DeserializeStrict<HumanResearchCandidate>(
                File.ReadAllBytes(input));
        HumanResearchCandidateRegistration result =
            HumanResearchCandidateRegistrar.Register(store, candidate);
        WriteResult(new Dictionary<string, object?>
        {
            ["schema"] = "intuition-human-candidate-registered.v1",
            ["candidate_ref"] = result.CandidateRef,
            ["receipt_ref"] = result.ReceiptRef,
            ["truth_release_digest"] = result.TruthReleaseDigest,
            ["topology_digest"] = result.TopologyDigest,
        });
        return 0;
    }
}
