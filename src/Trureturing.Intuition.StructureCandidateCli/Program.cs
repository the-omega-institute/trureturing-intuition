using Trureturing.Intuition.Core;

if (args.Length != 4)
{
    Console.Error.WriteLine(
        "usage: Trureturing.Intuition.StructureCandidateCli " +
        "<artifact-store-root> <episode-ref> <episode-receipt-ref> " +
        "<topology-atlas-evidence-input-receipt-ref>");
    return 2;
}

try
{
    StructureEditCandidateGeneration result =
        StructureEditCandidateGenerator.Generate(
            args[0],
            args[1],
            args[2],
            args[3]);
    await Console.OpenStandardOutput().WriteAsync(CanonicalJson.Serialize(result));
    return 0;
}
catch (Exception exception)
{
    Console.Error.WriteLine(exception.Message);
    return 1;
}
