using Trureturing.Intuition.Core;

if (args.Length < 1)
{
    return Usage();
}

try
{
    switch (args[0])
    {
        case "patch" when args.Length == 4:
        {
            StructureEditGraphPatch patch =
                CanonicalJson.DeserializeCanonical<StructureEditGraphPatch>(
                    File.ReadAllBytes(args[3]));
            StructureEditGraphPatchRegistration result =
                StructureCounterfactualRegistrar.RegisterPatch(
                    args[1],
                    args[2],
                    patch);
            await Console.OpenStandardOutput().WriteAsync(
                CanonicalJson.Serialize(result));
            return 0;
        }
        case "result" when args.Length == 5:
        {
            TopologyCounterfactualPublication publication =
                CanonicalJson.DeserializeCanonical<TopologyCounterfactualPublication>(
                    File.ReadAllBytes(args[3]));
            StructureCounterfactualRegistration result =
                StructureCounterfactualRegistrar.RegisterCounterfactual(
                    args[1],
                    args[2],
                    publication,
                    File.ReadAllBytes(args[4]));
            await Console.OpenStandardOutput().WriteAsync(
                CanonicalJson.Serialize(result));
            return 0;
        }
        default:
            return Usage();
    }
}
catch (Exception exception)
{
    Console.Error.WriteLine(exception.Message);
    return 1;
}

static int Usage()
{
    Console.Error.WriteLine(
        "usage:\n" +
        "  Trureturing.Intuition.CounterfactualCli patch " +
        "<artifact-store-root> <candidate-ref> <patch.json>\n" +
        "  Trureturing.Intuition.CounterfactualCli result " +
        "<artifact-store-root> <patch-ref> <publication.json> " +
        "<topology-counterfactual.json>");
    return 2;
}
