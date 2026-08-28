using Trureturing.Intuition.Core;

internal static partial class Cli
{
    private static int RegisterTopologyInput(
        ArtifactStore store,
        string publicationPath,
        string topologyPath,
        string cursorPath)
    {
        TopologyPublicationCoordinate publication =
            CanonicalJson.DeserializeStrict<TopologyPublicationCoordinate>(
                File.ReadAllBytes(publicationPath));
        TopologyResearchInputRegistration result =
            TopologyResearchInputRegistrar.Register(
                RequiredStoreRoot(store),
                publication,
                File.ReadAllBytes(topologyPath),
                cursorPath);
        WriteResult(new Dictionary<string, object?>
        {
            ["publication_ref"] = result.PublicationRef,
            ["topology_ref"] = result.TopologyRef,
            ["receipt_ref"] = result.ReceiptRef,
            ["cursor_path"] = result.CursorPath,
            ["replayed"] = result.Replayed,
            ["truth_release_digest"] = publication.TruthReleaseDigest
        });
        return 0;
    }

    private static string RequiredStoreRoot(ArtifactStore store)
    {
        string probe = store.PathFor(
            "sha256:" + new string('0', 64));
        DirectoryInfo? sha256 = Directory.GetParent(
            Directory.GetParent(probe)!.FullName);
        if (sha256?.Parent is null)
        {
            throw new InvalidOperationException(
                "Cannot derive the artifact store root.");
        }
        return sha256.Parent.FullName;
    }
}
