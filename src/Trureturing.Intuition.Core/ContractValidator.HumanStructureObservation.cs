using System.Globalization;

namespace Trureturing.Intuition.Core;

public static partial class ContractValidator
{
    private static readonly IReadOnlySet<string> HumanStructureGestureKinds =
        new HashSet<string>(StringComparer.Ordinal)
        {
            "selection",
            "compare",
            "bring-together",
            "cluster-peel",
            "path-inspection",
            "frontier-mark"
        };

    private static readonly IReadOnlySet<string> HumanStructurePrivacyClasses =
        new HashSet<string>(StringComparer.Ordinal)
        {
            "private-research",
            "team-research",
            "public-candidate"
        };

    public static void Validate(HumanStructureObservation observation)
    {
        ArgumentNullException.ThrowIfNull(observation);
        RequireSchema(
            observation.Schema,
            HumanStructureObservationSchemas.Observation);
        ArgumentNullException.ThrowIfNull(observation.ObservationContent);
        Validate(observation.ObservationContent);
        RequireArtifactRef(
            observation.ObservationId,
            nameof(observation.ObservationId));
        string expected = CanonicalJson.Sha256Reference(
            CanonicalJson.Serialize(observation.ObservationContent));
        if (!StringComparer.Ordinal.Equals(
                observation.ObservationId,
                expected))
        {
            throw new InvalidOperationException(
                "observation_id does not address canonical observation_content bytes.");
        }
    }

    public static void Validate(HumanStructureObservationContent content)
    {
        ArgumentNullException.ThrowIfNull(content);
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
        if (content.PagesResearchContextDigest is not null)
        {
            RequireArtifactRef(
                content.PagesResearchContextDigest,
                nameof(content.PagesResearchContextDigest));
        }
        RequireGitId(content.SourceCommit, nameof(content.SourceCommit));
        RequireGitId(content.SourceTree, nameof(content.SourceTree));
        if (content.SourceCommit.Length != content.SourceTree.Length)
        {
            throw new InvalidOperationException(
                "source_commit and source_tree use different Git object widths.");
        }
        if (!StringComparer.Ordinal.Equals(
                content.SourceSurface,
                "trureturing-pages"))
        {
            throw new InvalidOperationException(
                "Human structure observations must originate from trureturing-pages.");
        }
        RequireNonEmpty(content.HumanActor, nameof(content.HumanActor));
        if (content.HumanActor.Length > 256)
        {
            throw new InvalidOperationException(
                "human_actor exceeds 256 characters.");
        }
        ArgumentNullException.ThrowIfNull(content.Selection);
        ArgumentNullException.ThrowIfNull(content.Gesture);
        Validate(content.Selection);
        Validate(content.Gesture, content.Selection);
        RequireObservationText(content.HumanNote, nameof(content.HumanNote), 8000);
        if (!HumanStructurePrivacyClasses.Contains(content.PrivacyClass))
        {
            throw new InvalidOperationException(
                $"Unsupported privacy_class '{content.PrivacyClass}'.");
        }
        if (!content.ExplicitlySaved)
        {
            throw new InvalidOperationException(
                "A durable human structure observation must be explicitly saved.");
        }
        if (!DateTimeOffset.TryParse(
                content.CreatedAt,
                CultureInfo.InvariantCulture,
                DateTimeStyles.AssumeUniversal | DateTimeStyles.AdjustToUniversal,
                out _))
        {
            throw new InvalidOperationException(
                "created_at must be an RFC 3339 timestamp.");
        }
    }

    public static void Validate(HumanStructureSelection selection)
    {
        ArgumentNullException.ThrowIfNull(selection);
        RequireSortedUniqueStrings(
            selection.SelectedNodeIds,
            nameof(selection.SelectedNodeIds));
        RequireSortedUniqueClusters(
            selection.SelectedClusterIds,
            nameof(selection.SelectedClusterIds));
        RequireSortedUniqueEdges(
            selection.SelectedEdges,
            nameof(selection.SelectedEdges));
        if (selection.SelectedPathRef is not null)
        {
            RequireArtifactRef(
                selection.SelectedPathRef,
                nameof(selection.SelectedPathRef));
        }
        if (selection.SelectedNodeIds.Count == 0 &&
            selection.SelectedClusterIds.Count == 0 &&
            selection.SelectedEdges.Count == 0 &&
            selection.SelectedPathRef is null)
        {
            throw new InvalidOperationException(
                "A human structure observation must select at least one node, cluster, edge, or path.");
        }
    }

    public static void Validate(
        HumanStructureGesture gesture,
        HumanStructureSelection selection)
    {
        ArgumentNullException.ThrowIfNull(gesture);
        ArgumentNullException.ThrowIfNull(selection);
        if (!HumanStructureGestureKinds.Contains(gesture.Kind))
        {
            throw new InvalidOperationException(
                $"Unsupported structure gesture '{gesture.Kind}'.");
        }
        RequireSortedUniqueStrings(
            gesture.SourceNodeIds,
            nameof(gesture.SourceNodeIds));
        RequireSortedUniqueStrings(
            gesture.TargetNodeIds,
            nameof(gesture.TargetNodeIds));
        RequireSortedUniqueClusters(
            gesture.SourceClusterIds,
            nameof(gesture.SourceClusterIds));
        RequireSortedUniqueClusters(
            gesture.TargetClusterIds,
            nameof(gesture.TargetClusterIds));

        RequireSubset(
            gesture.SourceNodeIds,
            selection.SelectedNodeIds,
            nameof(gesture.SourceNodeIds));
        RequireSubset(
            gesture.TargetNodeIds,
            selection.SelectedNodeIds,
            nameof(gesture.TargetNodeIds));
        RequireSubset(
            gesture.SourceClusterIds,
            selection.SelectedClusterIds,
            nameof(gesture.SourceClusterIds));
        RequireSubset(
            gesture.TargetClusterIds,
            selection.SelectedClusterIds,
            nameof(gesture.TargetClusterIds));

        int sourceCount = gesture.SourceNodeIds.Count +
            gesture.SourceClusterIds.Count;
        int targetCount = gesture.TargetNodeIds.Count +
            gesture.TargetClusterIds.Count;
        if (gesture.Kind is "compare" or "bring-together")
        {
            if (sourceCount == 0 || targetCount == 0)
            {
                throw new InvalidOperationException(
                    $"Gesture '{gesture.Kind}' requires non-empty source and target selections.");
            }
        }
        if (StringComparer.Ordinal.Equals(
                gesture.Kind,
                "cluster-peel") &&
            gesture.SourceClusterIds.Count == 0)
        {
            throw new InvalidOperationException(
                "Gesture 'cluster-peel' requires a source cluster.");
        }
        if (StringComparer.Ordinal.Equals(
                gesture.Kind,
                "path-inspection") &&
            selection.SelectedPathRef is null)
        {
            throw new InvalidOperationException(
                "Gesture 'path-inspection' requires selected_path_ref.");
        }
    }

    public static void Validate(HumanStructureObservationReceipt receipt)
    {
        ArgumentNullException.ThrowIfNull(receipt);
        RequireSchema(
            receipt.Schema,
            HumanStructureObservationSchemas.Receipt);
        RequireArtifactRef(receipt.ObservationRef, nameof(receipt.ObservationRef));
        RequireArtifactRef(receipt.ObservationId, nameof(receipt.ObservationId));
        RequireArtifactRef(
            receipt.TopologyAtlasInputReceiptRef,
            nameof(receipt.TopologyAtlasInputReceiptRef));
        RequireArtifactRef(
            receipt.TruthReleaseDigest,
            nameof(receipt.TruthReleaseDigest));
        RequireArtifactRef(
            receipt.TopologyAtlasDigest,
            nameof(receipt.TopologyAtlasDigest));
        if (!StringComparer.Ordinal.Equals(
                receipt.SourceSurface,
                "trureturing-pages"))
        {
            throw new InvalidOperationException(
                "Observation receipt has an unsupported source surface.");
        }
        RequireNonEmpty(receipt.HumanActor, nameof(receipt.HumanActor));
        if (!HumanStructurePrivacyClasses.Contains(receipt.PrivacyClass))
        {
            throw new InvalidOperationException(
                $"Unsupported receipt privacy_class '{receipt.PrivacyClass}'.");
        }
        if (!DateTimeOffset.TryParse(
                receipt.RegisteredAt,
                CultureInfo.InvariantCulture,
                DateTimeStyles.AssumeUniversal | DateTimeStyles.AdjustToUniversal,
                out _))
        {
            throw new InvalidOperationException(
                "registered_at must be an RFC 3339 timestamp.");
        }
    }

    private static void RequireSortedUniqueClusters(
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

    private static void RequireSortedUniqueEdges(
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
            if (StringComparer.Ordinal.Equals(
                    edge.DependencyId,
                    edge.DependentId))
            {
                throw new InvalidOperationException(
                    $"{name} cannot contain a self edge.");
            }
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

    private static void RequireSubset(
        IReadOnlyList<string> values,
        IReadOnlyList<string> selected,
        string name)
    {
        var allowed = selected.ToHashSet(StringComparer.Ordinal);
        string? unknown = values.FirstOrDefault(value => !allowed.Contains(value));
        if (unknown is not null)
        {
            throw new InvalidOperationException(
                $"{name} contains '{unknown}' outside the saved selection.");
        }
    }

    private static void RequireObservationText(
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
