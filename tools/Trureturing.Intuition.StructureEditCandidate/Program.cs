using System.Text.Json;
using Trureturing.Intuition.Core;

return Run(args);

static int Run(string[] arguments)
{
    try
    {
        IReadOnlyDictionary<string, string> options = Parse(arguments);
        var store = new ArtifactStore(Required(options, "root"));
        StructureEditCandidateContent content =
            CanonicalJson.DeserializeStrict<StructureEditCandidateContent>(
                File.ReadAllBytes(Required(options, "input")));
        StructureEditCandidateRegistration result =
            StructureEditCandidateRegistrar.Register(store, content);
        Console.WriteLine(JsonSerializer.Serialize(
            new Dictionary<string, object?>
            {
                ["candidate_ref"] = result.CandidateRef,
                ["receipt_ref"] = result.ReceiptRef,
                ["candidate_id"] = result.CandidateId,
                ["episode_ref"] = result.EpisodeRef,
                ["candidate_kind"] = result.CandidateKind,
                ["candidate_ordinal"] = result.CandidateOrdinal,
                ["graph_patch_operation_count"] = result.GraphPatchOperationCount,
                ["authority"] = result.Authority
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
                "usage: --root <artifact-store> --input <candidate-content.json>");
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
