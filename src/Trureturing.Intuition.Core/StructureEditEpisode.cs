namespace Trureturing.Intuition.Core;

public static class StructureEditEpisodeSchemas
{
    public const string Episode = "structure-edit-episode.v1";
    public const string Receipt = "structure-edit-episode-receipt.v1";
    public const string NormalizationProfile = "human-structure-episode-v1";
}

public static class StructureEditKinds
{
    public const string AcquireEvidence = "acquire-evidence";
    public const string AddAbstraction = "add-abstraction";
    public const string AddBridge = "add-bridge";
    public const string AddCounterexample = "add-counterexample";
    public const string AddDefinitionPackage = "add-definition-package";
    public const string AddPremise = "add-premise";
    public const string AddSubgoal = "add-subgoal";
    public const string ChangeRepresentation = "change-representation";
    public const string RegisterOpenQuestion = "register-open-question";
    public const string Reroot = "reroot";
}

public sealed record StructureEditEpisodeContent(
    string ObservationRef,
    string ObservationReceiptRef,
    string TopologyAtlasInputReceiptRef,
    string TruthReleaseDigest,
    string CertifiedTopologyDigest,
    string TopologyAtlasDigest,
    string PagesConformationDigest,
    string SelectionKind,
    IReadOnlyList<string> SelectedNodeIds,
    IReadOnlyList<string> SelectedClusterIds,
    IReadOnlyList<HumanStructureSelectedEdge> SelectedEdges,
    string? SelectedPathRef,
    string GestureKind,
    IReadOnlyList<string> AllowedEditKinds,
    int CandidateLimit,
    string HumanIntent,
    string PrivacyClass,
    string NormalizationProfile,
    string SourceObservedAt);

public sealed record StructureEditEpisode(
    string Schema,
    string EpisodeId,
    StructureEditEpisodeContent EpisodeContent);

public sealed record StructureEditEpisodeReceipt(
    string Schema,
    string EpisodeRef,
    string EpisodeId,
    string ObservationRef,
    string ObservationReceiptRef,
    string TruthReleaseDigest,
    string TopologyAtlasDigest,
    string PrivacyClass,
    string NormalizationProfile);

public sealed record StructureEditEpisodeRegistration(
    string EpisodeRef,
    string ReceiptRef,
    string EpisodeId,
    string SelectionKind,
    IReadOnlyList<string> AllowedEditKinds,
    int CandidateLimit,
    string PrivacyClass);

public static class StructureEditEpisodeNormalizer
{
    public static StructureEditEpisodeRegistration Normalize(
        ArtifactStore store,
        string observationRef,
        string observationReceiptRef)
    {
        ArgumentNullException.ThrowIfNull(store);
        ContractValidator.RequireArtifactRef(
            observationRef,
            nameof(observationRef));
        ContractValidator.RequireArtifactRef(
            observationReceiptRef,
            nameof(observationReceiptRef));

        HumanStructureObservation observation =
            store.Get<HumanStructureObservation>(observationRef);
        HumanStructureObservationReceipt observationReceipt =
            store.Get<HumanStructureObservationReceipt>(observationReceiptRef);
        ContractValidator.Validate(observation);
        ContractValidator.Validate(observationReceipt);
        ValidateReceipt(
            observation,
            observationRef,
            observationReceipt,
            observationReceiptRef);

        HumanStructureObservationContent source =
            observation.ObservationContent;
        string selectionKind = SelectionKind(
            source.Selection,
            source.Gesture.Kind);
        string[] allowed = AllowedEditKinds(
            source.Gesture.Kind,
            selectionKind);
        int selectedCount = source.Selection.SelectedNodeIds.Count +
            source.Selection.SelectedClusterIds.Count +
            source.Selection.SelectedEdges.Count +
            (source.Selection.SelectedPathRef is null ? 0 : 1);
        int candidateLimit = Math.Clamp(
            Math.Max(3, selectedCount * 2),
            1,
            12);

        var content = new StructureEditEpisodeContent(
            observationRef,
            observationReceiptRef,
            source.TopologyAtlasInputReceiptRef,
            source.TruthReleaseDigest,
            source.CertifiedTopologyDigest,
            source.TopologyAtlasDigest,
            source.PagesConformationDigest,
            selectionKind,
            source.Selection.SelectedNodeIds,
            source.Selection.SelectedClusterIds,
            source.Selection.SelectedEdges,
            source.Selection.SelectedPathRef,
            source.Gesture.Kind,
            allowed,
            candidateLimit,
            source.HumanNote,
            source.PrivacyClass,
            StructureEditEpisodeSchemas.NormalizationProfile,
            source.CreatedAt);
        string episodeId = CanonicalJson.Sha256Reference(
            CanonicalJson.Serialize(content));
        var episode = new StructureEditEpisode(
            StructureEditEpisodeSchemas.Episode,
            episodeId,
            content);
        ContractValidator.Validate(episode);
        string episodeRef = store.Put(episode);

        var receipt = new StructureEditEpisodeReceipt(
            StructureEditEpisodeSchemas.Receipt,
            episodeRef,
            episodeId,
            observationRef,
            observationReceiptRef,
            source.TruthReleaseDigest,
            source.TopologyAtlasDigest,
            source.PrivacyClass,
            StructureEditEpisodeSchemas.NormalizationProfile);
        ContractValidator.Validate(receipt);
        string receiptRef = store.Put(receipt);
        return new StructureEditEpisodeRegistration(
            episodeRef,
            receiptRef,
            episodeId,
            selectionKind,
            allowed,
            candidateLimit,
            source.PrivacyClass);
    }

    private static void ValidateReceipt(
        HumanStructureObservation observation,
        string observationRef,
        HumanStructureObservationReceipt receipt,
        string receiptRef)
    {
        if (!StringComparer.Ordinal.Equals(
                receipt.ObservationRef,
                observationRef) ||
            !StringComparer.Ordinal.Equals(
                receipt.ObservationId,
                observation.ObservationId) ||
            !StringComparer.Ordinal.Equals(
                receipt.TruthReleaseDigest,
                observation.ObservationContent.TruthReleaseDigest) ||
            !StringComparer.Ordinal.Equals(
                receipt.TopologyAtlasDigest,
                observation.ObservationContent.TopologyAtlasDigest) ||
            !StringComparer.Ordinal.Equals(
                receipt.PrivacyClass,
                observation.ObservationContent.PrivacyClass))
        {
            throw new InvalidDataException(
                $"Observation receipt {receiptRef} does not bind the supplied observation.");
        }
    }

    private static string SelectionKind(
        HumanStructureSelection selection,
        string gestureKind)
    {
        if (StringComparer.Ordinal.Equals(
                gestureKind,
                "frontier-mark"))
        {
            return "frontier-region";
        }
        if (selection.SelectedPathRef is not null)
        {
            return "certified-path";
        }
        int nodes = selection.SelectedNodeIds.Count;
        int clusters = selection.SelectedClusterIds.Count;
        bool hasOther = selection.SelectedEdges.Count > 0;
        if (clusters == 0 && !hasOther)
        {
            return nodes switch
            {
                1 => "single-node",
                2 => "node-pair",
                _ when nodes > 2 => "node-set",
                _ => "mixed-selection"
            };
        }
        if (nodes == 0 && !hasOther)
        {
            return clusters switch
            {
                1 => "single-cluster",
                2 => "cluster-pair",
                _ when clusters > 2 => "cluster-set",
                _ => "mixed-selection"
            };
        }
        return "mixed-selection";
    }

    private static string[] AllowedEditKinds(
        string gestureKind,
        string selectionKind)
    {
        IEnumerable<string> values = gestureKind switch
        {
            "compare" or "bring-together" =>
            [
                StructureEditKinds.AddAbstraction,
                StructureEditKinds.AddBridge,
                StructureEditKinds.AddCounterexample,
                StructureEditKinds.ChangeRepresentation,
                StructureEditKinds.RegisterOpenQuestion
            ],
            "cluster-peel" =>
            [
                StructureEditKinds.AddAbstraction,
                StructureEditKinds.AddBridge,
                StructureEditKinds.AddSubgoal,
                StructureEditKinds.Reroot
            ],
            "path-inspection" =>
            [
                StructureEditKinds.AddAbstraction,
                StructureEditKinds.AddCounterexample,
                StructureEditKinds.AddSubgoal,
                StructureEditKinds.Reroot
            ],
            "frontier-mark" =>
            [
                StructureEditKinds.AcquireEvidence,
                StructureEditKinds.AddCounterexample,
                StructureEditKinds.AddSubgoal,
                StructureEditKinds.RegisterOpenQuestion
            ],
            _ when selectionKind is "single-node" or "node-set" =>
            [
                StructureEditKinds.AddDefinitionPackage,
                StructureEditKinds.AddPremise,
                StructureEditKinds.AddSubgoal,
                StructureEditKinds.ChangeRepresentation,
                StructureEditKinds.RegisterOpenQuestion
            ],
            _ =>
            [
                StructureEditKinds.AddAbstraction,
                StructureEditKinds.AddBridge,
                StructureEditKinds.AddCounterexample,
                StructureEditKinds.RegisterOpenQuestion
            ]
        };
        return values.Distinct(StringComparer.Ordinal)
            .Order(StringComparer.Ordinal)
            .ToArray();
    }
}
