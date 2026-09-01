using Trureturing.Intuition.Core;

if (args.Length != 4)
{
    Console.Error.WriteLine(
        "usage: Trureturing.Intuition.AtlasEvidenceCli " +
        "<artifact-store-root> <publication.json> <topology-atlas-evidence.json> <cursor.json>");
    return 2;
}

try
{
    string root = Path.GetFullPath(args[0]);
    TopologyAtlasEvidencePublicationCoordinate publication =
        CanonicalJson.DeserializeCanonical<TopologyAtlasEvidencePublicationCoordinate>(
            File.ReadAllBytes(args[1]));
    byte[] evidence = File.ReadAllBytes(args[2]);
    TopologyAtlasEvidenceResearchInputRegistration result =
        TopologyAtlasEvidenceResearchInputRegistrar.Register(
            root,
            publication,
            evidence,
            args[3]);
    await Console.OpenStandardOutput().WriteAsync(CanonicalJson.Serialize(result));
    return 0;
}
catch (Exception exception)
{
    Console.Error.WriteLine(exception.Message);
    return 1;
}
