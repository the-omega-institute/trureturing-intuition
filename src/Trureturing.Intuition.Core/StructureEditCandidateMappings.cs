namespace Trureturing.Intuition.Core;

internal static class StructureEditCandidateMappings
{
    private static readonly IReadOnlyDictionary<string, string> CandidateKindByEditKind =
        new Dictionary<string, string>(StringComparer.Ordinal)
        {
            [StructureEditKinds.AcquireEvidence] =
                StructureCandidateKinds.EvidenceAcquisition,
            [StructureEditKinds.AddAbstraction] =
                StructureCandidateKinds.Abstraction,
            [StructureEditKinds.AddBridge] =
                StructureCandidateKinds.Bridge,
            [StructureEditKinds.AddCounterexample] =
                StructureCandidateKinds.Counterexample,
            [StructureEditKinds.AddDefinitionPackage] =
                StructureCandidateKinds.DefinitionPackage,
            [StructureEditKinds.AddPremise] =
                StructureCandidateKinds.PremiseSet,
            [StructureEditKinds.AddSubgoal] =
                StructureCandidateKinds.Subgoal,
            [StructureEditKinds.ChangeRepresentation] =
                StructureCandidateKinds.RepresentationChange,
            [StructureEditKinds.RegisterOpenQuestion] =
                StructureCandidateKinds.OpenQuestion,
            [StructureEditKinds.Reroot] =
                StructureCandidateKinds.Reroot
        };

    public static IReadOnlySet<string> EditKinds { get; } =
        CandidateKindByEditKind.Keys.ToHashSet(StringComparer.Ordinal);

    public static IReadOnlySet<string> CandidateKinds { get; } =
        CandidateKindByEditKind.Values.ToHashSet(StringComparer.Ordinal);

    public static string CandidateKind(string editKind) =>
        CandidateKindByEditKind.TryGetValue(editKind, out string? value)
            ? value
            : throw new InvalidDataException(
                $"Structure edit kind '{editKind}' has no candidate-kind mapping.");
}
