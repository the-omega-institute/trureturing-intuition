using System.Text.Json;
using Trureturing.Intuition.Core;

return Run(args);

static int Run(string[] arguments)
{
    try
    {
        IReadOnlyDictionary<string, string> options = Parse(arguments);
        var store = new ArtifactStore(Required(options, "root"));
        StructureFormalizationResultPublicationCoordinate formalization =
            CanonicalJson.DeserializeStrict<
                StructureFormalizationResultPublicationCoordinate>(
                    File.ReadAllBytes(Required(options, "formalization-publication")));
        TopologyAtlasDeltaPublicationCoordinate delta =
            CanonicalJson.DeserializeStrict<TopologyAtlasDeltaPublicationCoordinate>(
                File.ReadAllBytes(Required(options, "delta-publication")));
        StructureEditSettlementRegistration result =
            StructureEditSettlementRegistrar.Register(
                store,
                Required(options, "valuation-ref"),
                formalization,
                File.ReadAllBytes(Required(options, "formalization-result")),
                delta,
                File.ReadAllBytes(Required(options, "atlas-delta")));
        Console.WriteLine(JsonSerializer.Serialize(
            new Dictionary<string, object?>
            {
                ["formalization_result_ref"] = result.FormalizationResultRef,
                ["atlas_delta_ref"] = result.AtlasDeltaRef,
                ["settlement_ref"] = result.SettlementRef,
                ["receipt_ref"] = result.ReceiptRef,
                ["settlement_id"] = result.SettlementId,
                ["candidate_ref"] = result.CandidateRef,
                ["from_truth_release_digest"] = result.FromTruthReleaseDigest,
                ["to_truth_release_digest"] = result.ToTruthReleaseDigest,
                ["settlement_status"] = result.SettlementStatus,
                ["calibration_class"] = result.CalibrationClass,
                ["counts"] = result.Counts
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
                "usage: --root <artifact-store> --valuation-ref <sha256> " +
                "--formalization-publication <json> --formalization-result <json> " +
                "--delta-publication <json> --atlas-delta <json>");
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
