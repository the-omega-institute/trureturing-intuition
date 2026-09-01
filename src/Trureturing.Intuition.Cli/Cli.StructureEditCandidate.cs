using Trureturing.Intuition.Core;

internal static partial class Cli
{
    private static int PrepareStructureEditCandidateContext(
        ArtifactStore store,
        string episodeRef,
        string episodeReceiptRef,
        string evidenceReceiptRef)
    {
        StructureEditCandidateContext context =
            StructureEditCandidateRegistrar.PrepareContext(
                store,
                episodeRef,
                episodeReceiptRef,
                evidenceReceiptRef);
        Console.WriteLine(
            System.Text.Encoding.UTF8.GetString(
                CanonicalJson.Serialize(context)).TrimEnd());
        return 0;
    }

    private static int RegisterStructureEditCandidateSet(
        ArtifactStore store,
        string inputPath)
    {
        StructureEditCandidateDraftSet draftSet =
            CanonicalJson.DeserializeStrict<StructureEditCandidateDraftSet>(
                File.ReadAllBytes(inputPath));
        StructureEditCandidateSetRegistration result =
            StructureEditCandidateRegistrar.Register(store, draftSet);
        WriteResult(new Dictionary<string, object?>
        {
            ["candidate_set_ref"] = result.CandidateSetRef,
            ["receipt_ref"] = result.ReceiptRef,
            ["candidate_set_id"] = result.CandidateSetId,
            ["candidate_ids"] = result.CandidateIds,
            ["candidate_count"] = result.CandidateCount,
            ["truth_release_digest"] = result.TruthReleaseDigest,
            ["topology_atlas_evidence_digest"] =
                result.TopologyAtlasEvidenceDigest
        });
        return 0;
    }
}
