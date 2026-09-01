using Trureturing.Intuition.Core;

internal static partial class Cli
{
    private static int RegisterTopologyAtlasEvidenceInput(
        ArtifactStore store,
        string publicationPath,
        string evidencePath,
        string cursorPath)
    {
        TopologyAtlasEvidencePublicationCoordinate publication =
            CanonicalJson.DeserializeStrict<
                TopologyAtlasEvidencePublicationCoordinate>(
                    File.ReadAllBytes(publicationPath));
        TopologyAtlasEvidenceResearchInputRegistration result =
            TopologyAtlasEvidenceResearchInputRegistrar.Register(
                RequiredStoreRoot(store),
                publication,
                File.ReadAllBytes(evidencePath),
                cursorPath);
        WriteResult(new Dictionary<string, object?>
        {
            ["publication_ref"] = result.PublicationRef,
            ["evidence_ref"] = result.EvidenceRef,
            ["receipt_ref"] = result.ReceiptRef,
            ["topology_atlas_input_receipt_ref"] =
                result.TopologyAtlasInputReceiptRef,
            ["cursor_path"] = result.CursorPath,
            ["replayed"] = result.Replayed,
            ["truth_release_digest"] = publication.TruthReleaseDigest,
            ["certified_topology_digest"] =
                publication.CertifiedTopologyDigest,
            ["topology_atlas_digest"] = publication.TopologyAtlasDigest,
            ["topology_atlas_evidence_digest"] =
                publication.TopologyAtlasEvidenceDigest,
            ["evidence_algorithm_profile_digest"] =
                publication.EvidenceAlgorithmProfileDigest,
            ["stable_node_count"] = result.StableNodeCount,
            ["trait_record_count"] = result.TraitRecordCount,
            ["cluster_interface_count"] = result.ClusterInterfaceCount,
            ["affinity_witness_count"] = result.AffinityWitnessCount
        });
        return 0;
    }
}
