using Trureturing.Intuition.Core;

internal static partial class Cli
{
    private static int NormalizeStructureEditEpisode(
        ArtifactStore store,
        string observationRef,
        string observationReceiptRef)
    {
        StructureEditEpisodeRegistration result =
            StructureEditEpisodeNormalizer.Normalize(
                store,
                observationRef,
                observationReceiptRef);
        WriteResult(new Dictionary<string, object?>
        {
            ["episode_ref"] = result.EpisodeRef,
            ["receipt_ref"] = result.ReceiptRef,
            ["episode_id"] = result.EpisodeId,
            ["selection_kind"] = result.SelectionKind,
            ["allowed_edit_kinds"] = result.AllowedEditKinds,
            ["candidate_limit"] = result.CandidateLimit,
            ["privacy_class"] = result.PrivacyClass
        });
        return 0;
    }
}
