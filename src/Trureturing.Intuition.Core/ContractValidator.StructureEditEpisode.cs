using System.Globalization;

namespace Trureturing.Intuition.Core;

public static partial class ContractValidator
{
    private static readonly IReadOnlySet<string> StructureSelectionKinds =
        new HashSet<string>(StringComparer.Ordinal)
        {
            "single-node",
            "node-pair",
            "node-set",
            "single-cluster",
            "cluster-pair",
            "cluster-set",
            "mixed-selection",
            "certified-path",
            "frontier-region"
        };

    private static readonly IReadOnlySet<string> StructureEditKindSet =
        new HashSet<string>(StringComparer.Ordinal)
        {
            StructureEditKinds.AcquireEvidence,
            StructureEditKinds.AddAbstraction,
            StructureEditKinds.AddBridge,
            StructureEditKinds.AddCounterexample,
            StructureEditKinds.AddDefinitionPackage,
            StructureEditKinds.AddPremise,
            StructureEditKinds.AddSubgoal,
            StructureEditKinds.ChangeRepresentation,
            StructureEditKinds.RegisterOpenQuestion,
            StructureEditKinds.Reroot
        };

    public static void Validate(StructureEditEpisode episode)
    {
        ArgumentNullException.ThrowIfNull(episode);
        RequireSchema(episode.Schema, StructureEditEpisodeSchemas.Episode);
        ArgumentNullException.ThrowIfNull(episode.EpisodeContent);
        Validate(episode.EpisodeContent);
        RequireArtifactRef(episode.EpisodeId, nameof(episode.EpisodeId));
        string expected = CanonicalJson.Sha256Reference(
            CanonicalJson.Serialize(episode.EpisodeContent));
        if (!StringComparer.Ordinal.Equals(episode.EpisodeId, expected))
        {
            throw new InvalidOperationException(
                "episode_id does not address canonical episode_content bytes.");
        }
    }

    public static void Validate(StructureEditEpisodeContent content)
    {
        ArgumentNullException.ThrowIfNull(content);
        RequireArtifactRef(content.ObservationRef, nameof(content.ObservationRef));
        RequireArtifactRef(
            content.ObservationReceiptRef,
            nameof(content.ObservationReceiptRef));
        RequireArtifactRef(
            content.TopologyAtlasInputReceiptRef,
            nameof(content.TopologyAtlasInputReceiptRef));
        RequireArtifactRef(
            content.TruthReleaseDigest,
            nameof(content.TruthReleaseDigest));
        RequireArtifactRef(
            content.CertifiedTopologyDigest,
            nameof(content.CertifiedTopologyDigest));
        RequireArtifactRef(
            content.TopologyAtlasDigest,
            nameof(content.TopologyAtlasDigest));
        RequireArtifactRef(
            content.PagesConformationDigest,
            nameof(content.PagesConformationDigest));
        if (!StructureSelectionKinds.Contains(content.SelectionKind))
        {
            throw new InvalidOperationException(
                $"Unsupported selection_kind '{content.SelectionKind}'.");
        }
        RequireSortedUniqueStrings(
            content.SelectedNodeIds,
            nameof(content.SelectedNodeIds));
        RequireSortedUniqueEpisodeClusters(
            content.SelectedClusterIds,
            nameof(content.SelectedClusterIds));
        RequireSortedUniqueEpisodeEdges(
            content.SelectedEdges,
            nameof(content.SelectedEdges));
        if (content.SelectedPathRef is not null)
        {
            RequireArtifactRef(
                content.SelectedPathRef,
                nameof(content.SelectedPathRef));
        }
        if (content.SelectionKind == "certified-path" &&
            content.SelectedPathRef is null)
        {
            throw new InvalidOperationException(
                "certified-path episodes require selected_path_ref.");
        }
        if (content.SelectionKind == "frontier-region" &&
            !StringComparer.Ordinal.Equals(content.GestureKind, "frontier-mark"))
        {
            throw new InvalidOperationException(
                "frontier-region episodes require frontier-mark gesture.");
        }
        ValidateSelectionCardinality(content);
        if (!HumanStructureGestureKinds.Contains(content.GestureKind))
        {
            throw new InvalidOperationException(
                $"Unsupported gesture_kind '{content.GestureKind}'.");
        }
        RequireSortedUniqueStrings(
            content.AllowedEditKinds,
            nameof(content.AllowedEditKinds));
        if (content.AllowedEditKinds.Count == 0 ||
            content.AllowedEditKinds.Any(kind => !StructureEditKindSet.Contains(kind)))
        {
            throw new InvalidOperationException(
                "allowed_edit_kinds must contain supported edit algebra members.");
        }
        if (content.CandidateLimit is < 1 or > 12)
        {
            throw new InvalidOperationException(
                "candidate_limit must be from 1 through 12.");
        }
        RequireEpisodeText(content.HumanIntent, nameof(content.HumanIntent), 8000);
        if (!HumanStructurePrivacyClasses.Contains(content.PrivacyClass))
        {
            throw new InvalidOperationException(
                $"Unsupported privacy_class '{content.PrivacyClass}'.");
        }
        if (!StringComparer.Ordinal.Equals(
                content.NormalizationProfile,
                StructureEditEpisodeSchemas.NormalizationProfile))
        {
            throw new InvalidOperationException(
                "normalization_profile is unsupported.");
        }
        if (!DateTimeOffset.TryParse(
                content.SourceObservedAt,
                CultureInfo.InvariantCulture,
                DateTimeStyles.AssumeUniversal | DateTimeStyles.AdjustToUniversal,
                out _))
        {
            throw new InvalidOperationException(
                "source_observed_at must be an RFC 3339 timestamp.");
        }
    }

    public static void Validate(StructureEditEpisodeReceipt receipt)
    {
        ArgumentNullException.ThrowIfNull(receipt);
        RequireSchema(receipt.Schema, StructureEditEpisodeSchemas.Receipt);
        RequireArtifactRef(receipt.EpisodeRef, nameof(receipt.EpisodeRef));
        RequireArtifactRef(receipt.EpisodeId, nameof(receipt.EpisodeId));
        RequireArtifactRef(receipt.ObservationRef, nameof(receipt.ObservationRef));
        RequireArtifactRef(
            receipt.ObservationReceiptRef,
            nameof(receipt.ObservationReceiptRef));
        RequireArtifactRef(
            receipt.TruthReleaseDigest,
            nameof(receipt.TruthReleaseDigest));
        RequireArtifactRef(
            receipt.TopologyAtlasDigest,
            nameof(receipt.TopologyAtlasDigest));
        if (!HumanStructurePrivacyClasses.Contains(receipt.PrivacyClass))
        {
            throw new InvalidOperationException(
                $"Unsupported receipt privacy_class '{receipt.PrivacyClass}'.");
        }
        if (!StringComparer.Ordinal.Equals(
                receipt.NormalizationProfile,
                StructureEditEpisodeSchemas.NormalizationProfile))
        {
            throw new InvalidOperationException(
                "receipt normalization_profile is unsupported.");
        }
    }

    private static void ValidateSelectionCardinality(
        StructureEditEpisodeContent content)
    {
        int nodes = content.SelectedNodeIds.Count;
        int clusters = content.SelectedClusterIds.Count;
        switch (content.SelectionKind)
        {
            case "single-node" when nodes != 1 || clusters != 0:
            case "node-pair" when nodes != 2 || clusters != 0:
            case "node-set" when nodes < 3 || clusters != 0:
            case "single-cluster" when clusters != 1 || nodes != 0:
            case "cluster-pair" when clusters != 2 || nodes != 0:
            case "cluster-set" when clusters < 3 || nodes != 0:
                throw new InvalidOperationException(
                    $"selection_kind '{content.SelectionKind}' disagrees with selected identities.");
        }
    }

    private static void RequireSortedUniqueEpisodeClusters(
        IReadOnlyList<string> values,
        string name)
    {
        RequireSortedUniqueStrings(values, name);
        foreach (string value in values)
        {
            if (value.Length != "cluster:sha256:".Length + 64 ||
                !value.StartsWith("cluster:sha256:", StringComparison.Ordinal) ||
                value["cluster:sha256:".Length..].Any(character =>
                    character is not (>= '0' and <= '9') and
                    not (>= 'a' and <= 'f')))
            {
                throw new InvalidOperationException(
                    $"{name} contains invalid cluster id '{value}'.");
            }
        }
    }

    private static void RequireSortedUniqueEpisodeEdges(
        IReadOnlyList<HumanStructureSelectedEdge> edges,
        string name)
    {
        ArgumentNullException.ThrowIfNull(edges);
        string? previous = null;
        foreach (HumanStructureSelectedEdge edge in edges)
        {
            ArgumentNullException.ThrowIfNull(edge);
            RequireNonEmpty(edge.DependencyId, $"{name}.dependency_id");
            RequireNonEmpty(edge.DependentId, $"{name}.dependent_id");
            string key = edge.DependencyId + "\u0000" + edge.DependentId;
            if (previous is not null &&
                StringComparer.Ordinal.Compare(previous, key) >= 0)
            {
                throw new InvalidOperationException(
                    $"{name} must be sorted and unique.");
            }
            previous = key;
        }
    }

    private static void RequireEpisodeText(
        string? value,
        string name,
        int maximum)
    {
        RequireNonEmpty(value, name);
        if (value!.Length > maximum)
        {
            throw new InvalidOperationException(
                $"{name} exceeds {maximum} characters.");
        }
    }
}
