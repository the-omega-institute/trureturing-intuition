using Trureturing.Intuition.Core;

internal static partial class Cli
{
    private static int RegisterTopologyAtlasInput(
        ArtifactStore store,
        string publicationPath,
        string atlasPath,
        string cursorPath)
    {
        TopologyAtlasPublicationCoordinate publication =
            CanonicalJson.DeserializeStrict<TopologyAtlasPublicationCoordinate>(
                File.ReadAllBytes(publicationPath));
        TopologyAtlasResearchInputRegistration result =
            TopologyAtlasResearchInputRegistrar.Register(
                RequiredStoreRoot(store),
                publication,
                File.ReadAllBytes(atlasPath),
                cursorPath);
        WriteResult(new Dictionary<string, object?>
        {
            ["publication_ref"] = result.PublicationRef,
            ["atlas_ref"] = result.AtlasRef,
            ["receipt_ref"] = result.ReceiptRef,
            ["cursor_path"] = result.CursorPath,
            ["replayed"] = result.Replayed,
            ["truth_release_digest"] = publication.TruthReleaseDigest,
            ["certified_topology_digest"] =
                publication.CertifiedTopologyDigest,
            ["topology_atlas_digest"] = publication.TopologyAtlasDigest,
            ["atlas_algorithm_profile_digest"] =
                publication.AtlasAlgorithmProfileDigest
        });
        return 0;
    }
}
