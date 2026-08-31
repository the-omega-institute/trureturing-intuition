namespace Trureturing.Intuition.Core;

public static class HumanStructureObservationSchemas
{
    public const string Observation = "human-structure-observation.v1";
    public const string Receipt = "human-structure-observation-receipt.v1";
}

public sealed record HumanStructureSelectedEdge(
    string DependencyId,
    string DependentId);

public sealed record HumanStructureSelection(
    IReadOnlyList<string> SelectedNodeIds,
    IReadOnlyList<string> SelectedClusterIds,
    IReadOnlyList<HumanStructureSelectedEdge> SelectedEdges,
    string? SelectedPathRef);

public sealed record HumanStructureGesture(
    string Kind,
    IReadOnlyList<string> SourceNodeIds,
    IReadOnlyList<string> TargetNodeIds,
    IReadOnlyList<string> SourceClusterIds,
    IReadOnlyList<string> TargetClusterIds);

public sealed record HumanStructureObservationContent(
    string TopologyAtlasInputReceiptRef,
    string TruthReleaseDigest,
    string CertifiedTopologyDigest,
    string TopologyAtlasDigest,
    string PagesConformationDigest,
    string? PagesResearchContextDigest,
    string SourceCommit,
    string SourceTree,
    string SourceSurface,
    string HumanActor,
    HumanStructureSelection Selection,
    HumanStructureGesture Gesture,
    string HumanNote,
    string PrivacyClass,
    bool ExplicitlySaved,
    string CreatedAt);

public sealed record HumanStructureObservation(
    string Schema,
    string ObservationId,
    HumanStructureObservationContent ObservationContent);

public sealed record HumanStructureObservationReceipt(
    string Schema,
    string ObservationRef,
    string ObservationId,
    string TopologyAtlasInputReceiptRef,
    string TruthReleaseDigest,
    string TopologyAtlasDigest,
    string SourceSurface,
    string HumanActor,
    string PrivacyClass,
    string RegisteredAt);

public sealed record HumanStructureObservationRegistration(
    string ObservationRef,
    string ReceiptRef,
    string TruthReleaseDigest,
    string TopologyAtlasDigest,
    string PrivacyClass);

public static class HumanStructureObservationRegistrar
{
    public static HumanStructureObservationRegistration Register(
        ArtifactStore store,
        HumanStructureObservation observation)
    {
        ArgumentNullException.ThrowIfNull(store);
        ContractValidator.Validate(observation);

        HumanStructureObservationContent content =
            observation.ObservationContent;
        IntuitionTopologyAtlasInputReceipt atlasReceipt =
            store.Get<IntuitionTopologyAtlasInputReceipt>(
                content.TopologyAtlasInputReceiptRef);
        ContractValidator.Validate(atlasReceipt);
        ValidateBinding(content, atlasReceipt);

        string root = RequiredStoreRoot(store);
        string atlasPath = TopologyAtlasResearchInputRegistrar.AtlasBlobPath(
            root,
            atlasReceipt.AtlasRef);
        if (!File.Exists(atlasPath))
        {
            throw new InvalidDataException(
                "The registered Topology Atlas blob is unavailable.");
        }
        TopologyAtlasReadModel atlas = TopologyAtlasReader.Read(
            File.ReadAllBytes(atlasPath),
            new TopologyAtlasBinding(
                atlasReceipt.TruthReleaseDigest,
                atlasReceipt.CertifiedTopologyDigest,
                atlasReceipt.CertifiedAlgorithmProfileDigest,
                atlasReceipt.AtlasAlgorithmProfileDigest,
                atlasReceipt.ProducerCommit));
        ValidateSelection(content.Selection, atlas);

        string observationRef = store.Put(observation);
        var receipt = new HumanStructureObservationReceipt(
            HumanStructureObservationSchemas.Receipt,
            observationRef,
            observation.ObservationId,
            content.TopologyAtlasInputReceiptRef,
            content.TruthReleaseDigest,
            content.TopologyAtlasDigest,
            content.SourceSurface,
            content.HumanActor,
            content.PrivacyClass,
            content.CreatedAt);
        ContractValidator.Validate(receipt);
        string receiptRef = store.Put(receipt);
        return new HumanStructureObservationRegistration(
            observationRef,
            receiptRef,
            content.TruthReleaseDigest,
            content.TopologyAtlasDigest,
            content.PrivacyClass);
    }

    private static void ValidateBinding(
        HumanStructureObservationContent content,
        IntuitionTopologyAtlasInputReceipt receipt)
    {
        if (!StringComparer.Ordinal.Equals(
                content.TruthReleaseDigest,
                receipt.TruthReleaseDigest) ||
            !StringComparer.Ordinal.Equals(
                content.CertifiedTopologyDigest,
                receipt.CertifiedTopologyDigest) ||
            !StringComparer.Ordinal.Equals(
                content.TopologyAtlasDigest,
                receipt.TopologyAtlasDigest) ||
            !StringComparer.Ordinal.Equals(
                content.SourceCommit,
                receipt.SourceCommit) ||
            !StringComparer.Ordinal.Equals(
                content.SourceTree,
                receipt.SourceTree))
        {
            throw new InvalidDataException(
                "Human structure observation is bound to different Atlas or source coordinates.");
        }
    }

    private static void ValidateSelection(
        HumanStructureSelection selection,
        TopologyAtlasReadModel atlas)
    {
        var nodeIds = atlas.Nodes
            .Select(node => node.NodeId)
            .ToHashSet(StringComparer.Ordinal);
        var clusterIds = atlas.Clusters
            .Select(cluster => cluster.ClusterId)
            .ToHashSet(StringComparer.Ordinal);
        var edgeIds = atlas.Edges
            .Select(edge => EdgeKey(edge.DependencyId, edge.DependentId))
            .ToHashSet(StringComparer.Ordinal);

        string? unknownNode = selection.SelectedNodeIds
            .FirstOrDefault(node => !nodeIds.Contains(node));
        if (unknownNode is not null)
        {
            throw new InvalidDataException(
                $"Human structure observation selects unknown Atlas node '{unknownNode}'.");
        }
        string? unknownCluster = selection.SelectedClusterIds
            .FirstOrDefault(cluster => !clusterIds.Contains(cluster));
        if (unknownCluster is not null)
        {
            throw new InvalidDataException(
                $"Human structure observation selects unknown Atlas cluster '{unknownCluster}'.");
        }
        HumanStructureSelectedEdge? unknownEdge = selection.SelectedEdges
            .FirstOrDefault(edge => !edgeIds.Contains(
                EdgeKey(edge.DependencyId, edge.DependentId)));
        if (unknownEdge is not null)
        {
            throw new InvalidDataException(
                "Human structure observation selects an edge absent from the exact Topology Atlas.");
        }
    }

    private static string EdgeKey(string dependencyId, string dependentId) =>
        dependencyId + "\u0000" + dependentId;

    private static string RequiredStoreRoot(ArtifactStore store)
    {
        string probe = store.PathFor("sha256:" + new string('0', 64));
        DirectoryInfo? directory = new FileInfo(probe).Directory;
        while (directory is not null &&
            !StringComparer.Ordinal.Equals(directory.Name, "sha256"))
        {
            directory = directory.Parent;
        }
        return directory?.Parent?.Parent?.FullName
            ?? throw new InvalidOperationException(
                "Cannot establish the Intuition artifact-store root.");
    }
}
