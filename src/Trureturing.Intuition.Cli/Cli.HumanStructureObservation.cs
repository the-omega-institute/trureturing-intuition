using Trureturing.Intuition.Core;

internal static partial class Cli
{
    private static int RegisterHumanStructureObservation(
        ArtifactStore store,
        string inputPath)
    {
        HumanStructureObservation observation =
            CanonicalJson.DeserializeStrict<HumanStructureObservation>(
                File.ReadAllBytes(inputPath));
        HumanStructureObservationRegistration result =
            HumanStructureObservationRegistrar.Register(store, observation);
        WriteResult(new Dictionary<string, object?>
        {
            ["observation_ref"] = result.ObservationRef,
            ["receipt_ref"] = result.ReceiptRef,
            ["truth_release_digest"] = result.TruthReleaseDigest,
            ["topology_atlas_digest"] = result.TopologyAtlasDigest,
            ["privacy_class"] = result.PrivacyClass
        });
        return 0;
    }
}
