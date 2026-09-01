using System.Text.Json;
using Trureturing.Intuition.Core;

return Run(args);

static int Run(string[] arguments)
{
    try
    {
        IReadOnlyDictionary<string, string> options = Parse(arguments);
        var store = new ArtifactStore(Required(options, "root"));
        TopologyCounterfactualPublicationCoordinate publication =
            CanonicalJson.DeserializeStrict<
                TopologyCounterfactualPublicationCoordinate>(
                    File.ReadAllBytes(Required(options, "publication")));
        StructureCounterfactualValuationRegistration result =
            StructureCounterfactualValuator.Register(
                store,
                publication,
                File.ReadAllBytes(Required(options, "counterfactual")));
        Console.WriteLine(JsonSerializer.Serialize(
            new Dictionary<string, object?>
            {
                ["counterfactual_ref"] = result.CounterfactualRef,
                ["valuation_ref"] = result.ValuationRef,
                ["receipt_ref"] = result.ReceiptRef,
                ["valuation_id"] = result.ValuationId,
                ["candidate_ref"] = result.CandidateRef,
                ["classification"] = result.Classification,
                ["accepted"] = result.Accepted,
                ["cycle_risk"] = result.CycleRisk,
                ["benefit_vector"] = result.BenefitVector,
                ["risk_vector"] = result.RiskVector
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
                "usage: --root <artifact-store> --publication <json> " +
                "--counterfactual <json>");
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
