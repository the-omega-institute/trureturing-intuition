using System.Text.Json;
using Trureturing.Intuition.Core;

return Run(args);

static int Run(string[] arguments)
{
    try
    {
        IReadOnlyDictionary<string, string> options = Parse(arguments);
        string root = Required(options, "root");
        string publicationPath = Required(options, "publication");
        string evidencePath = Required(options, "evidence");
        string atlasPath = Required(options, "atlas");
        string cursorPath = Required(options, "cursor");
        TopologyAtlasEvidencePublicationCoordinate publication =
            CanonicalJson.DeserializeStrict<
                TopologyAtlasEvidencePublicationCoordinate>(
                    File.ReadAllBytes(publicationPath));
        TopologyAtlasEvidenceResearchInputRegistration result =
            TopologyAtlasEvidenceResearchInputRegistrar.Register(
                root,
                publication,
                File.ReadAllBytes(evidencePath),
                File.ReadAllBytes(atlasPath),
                cursorPath);
        Console.WriteLine(JsonSerializer.Serialize(
            new Dictionary<string, object?>
            {
                ["publication_ref"] = result.PublicationRef,
                ["evidence_ref"] = result.EvidenceRef,
                ["receipt_ref"] = result.ReceiptRef,
                ["cursor_path"] = result.CursorPath,
                ["replayed"] = result.Replayed,
                ["truth_release_digest"] = result.TruthReleaseDigest,
                ["stable_identity_count"] = result.StableIdentityCount,
                ["cluster_interface_count"] = result.ClusterInterfaceCount,
                ["affinity_witness_count"] = result.AffinityWitnessCount
            },
            CanonicalJson.Options));
        return 0;
    }
    catch (Exception exception)
    {
        Console.Error.WriteLine(exception.Message);
        return 2;
    }
}

static IReadOnlyDictionary<string, string> Parse(string[] arguments)
{
    var result = new Dictionary<string, string>(StringComparer.Ordinal);
    for (int index = 0; index < arguments.Length; index += 2)
    {
        if (index + 1 >= arguments.Length ||
            !arguments[index].StartsWith("--", StringComparison.Ordinal))
        {
            throw new InvalidOperationException(
                "usage: --root <path> --publication <json> --evidence <json> " +
                "--atlas <json> --cursor <json>");
        }
        string name = arguments[index][2..];
        if (!result.TryAdd(name, arguments[index + 1]))
        {
            throw new InvalidOperationException($"Duplicate option --{name}.");
        }
    }
    return result;
}

static string Required(
    IReadOnlyDictionary<string, string> options,
    string name) =>
    options.TryGetValue(name, out string? value) &&
    !string.IsNullOrWhiteSpace(value)
        ? value
        : throw new InvalidOperationException($"Missing --{name}.");
