using System.Security.Cryptography;

namespace Trureturing.Intuition.Core;

internal sealed record StructureEditCandidateResearchState(
    StructureEditEpisode Episode,
    StructureEditEpisodeReceipt EpisodeReceipt,
    IntuitionTopologyAtlasEvidenceInputReceipt EvidenceReceipt,
    IntuitionTopologyAtlasInputReceipt AtlasReceipt,
    TopologyAtlasReadModel Atlas,
    TopologyAtlasEvidenceReadModel Evidence,
    IReadOnlySet<string> AllowedAnchorNodeIds,
    IReadOnlySet<string> AllowedAnchorClusterIds);

internal static class StructureEditCandidateResearchLoader
{
    public static StructureEditCandidateResearchState Load(
        ArtifactStore store,
        string episodeRef,
        string episodeReceiptRef,
        string evidenceReceiptRef)
    {
        ArgumentNullException.ThrowIfNull(store);
        ContractValidator.RequireArtifactRef(episodeRef, nameof(episodeRef));
        ContractValidator.RequireArtifactRef(
            episodeReceiptRef,
            nameof(episodeReceiptRef));
        ContractValidator.RequireArtifactRef(
            evidenceReceiptRef,
            nameof(evidenceReceiptRef));

        StructureEditEpisode episode = store.Get<StructureEditEpisode>(episodeRef);
        StructureEditEpisodeReceipt episodeReceipt =
            store.Get<StructureEditEpisodeReceipt>(episodeReceiptRef);
        IntuitionTopologyAtlasEvidenceInputReceipt evidenceReceipt =
            store.Get<IntuitionTopologyAtlasEvidenceInputReceipt>(
                evidenceReceiptRef);
        ContractValidator.Validate(episode);
        ContractValidator.Validate(episodeReceipt);
        ContractValidator.Validate(evidenceReceipt);
        ValidateEpisodeReceipt(
            episode,
            episodeRef,
            episodeReceipt,
            episodeReceiptRef);
        ValidateEvidenceBinding(episode, evidenceReceipt);

        IntuitionTopologyAtlasInputReceipt atlasReceipt =
            store.Get<IntuitionTopologyAtlasInputReceipt>(
                evidenceReceipt.TopologyAtlasInputReceiptRef);
        ContractValidator.Validate(atlasReceipt);
        ValidateAtlasReceipt(evidenceReceipt, atlasReceipt);

        string root = RequiredStoreRoot(store);
        string atlasPath = TopologyAtlasResearchInputRegistrar.AtlasBlobPath(
            root,
            atlasReceipt.AtlasRef);
        byte[] atlasBytes = File.ReadAllBytes(atlasPath);
        RequireDigest(atlasBytes, atlasReceipt.AtlasRef, "Topology Atlas");
        TopologyAtlasReadModel atlas = TopologyAtlasReader.Read(
            atlasBytes,
            new TopologyAtlasBinding(
                atlasReceipt.TruthReleaseDigest,
                atlasReceipt.CertifiedTopologyDigest,
                atlasReceipt.CertifiedAlgorithmProfileDigest,
                atlasReceipt.AtlasAlgorithmProfileDigest,
                atlasReceipt.ProducerCommit));

        string evidencePath =
            TopologyAtlasEvidenceResearchInputRegistrar.EvidenceBlobPath(
                root,
                evidenceReceipt.EvidenceRef);
        byte[] evidenceBytes = File.ReadAllBytes(evidencePath);
        RequireDigest(
            evidenceBytes,
            evidenceReceipt.EvidenceRef,
            "Topology Atlas evidence");
        TopologyAtlasEvidenceReadModel evidence =
            TopologyAtlasEvidenceReader.Read(
                evidenceBytes,
                new TopologyAtlasEvidenceBinding(
                    evidenceReceipt.TruthReleaseDigest,
                    evidenceReceipt.CertifiedTopologyDigest,
                    evidenceReceipt.TopologyAtlasDigest,
                    evidenceReceipt.EvidenceAlgorithmProfileDigest,
                    evidenceReceipt.ProducerCommit),
                atlas);

        (HashSet<string> nodes, HashSet<string> clusters) =
            AllowedAnchors(episode.EpisodeContent, atlas);
        if (nodes.Count == 0 && clusters.Count == 0)
        {
            throw new InvalidDataException(
                "Structure edit episode has no usable Atlas anchor.");
        }
        return new StructureEditCandidateResearchState(
            episode,
            episodeReceipt,
            evidenceReceipt,
            atlasReceipt,
            atlas,
            evidence,
            nodes,
            clusters);
    }

    private static void ValidateEpisodeReceipt(
        StructureEditEpisode episode,
        string episodeRef,
        StructureEditEpisodeReceipt receipt,
        string receiptRef)
    {
        if (!StringComparer.Ordinal.Equals(receipt.EpisodeRef, episodeRef) ||
            !StringComparer.Ordinal.Equals(receipt.EpisodeId, episode.EpisodeId) ||
            !StringComparer.Ordinal.Equals(
                receipt.ObservationRef,
                episode.EpisodeContent.ObservationRef) ||
            !StringComparer.Ordinal.Equals(
                receipt.ObservationReceiptRef,
                episode.EpisodeContent.ObservationReceiptRef) ||
            !StringComparer.Ordinal.Equals(
                receipt.TruthReleaseDigest,
                episode.EpisodeContent.TruthReleaseDigest) ||
            !StringComparer.Ordinal.Equals(
                receipt.TopologyAtlasDigest,
                episode.EpisodeContent.TopologyAtlasDigest))
        {
            throw new InvalidDataException(
                $"Episode receipt {receiptRef} does not bind the supplied episode.");
        }
    }

    private static void ValidateEvidenceBinding(
        StructureEditEpisode episode,
        IntuitionTopologyAtlasEvidenceInputReceipt evidence)
    {
        StructureEditEpisodeContent content = episode.EpisodeContent;
        if (!StringComparer.Ordinal.Equals(
                evidence.TopologyAtlasInputReceiptRef,
                content.TopologyAtlasInputReceiptRef) ||
            !StringComparer.Ordinal.Equals(
                evidence.TruthReleaseDigest,
                content.TruthReleaseDigest) ||
            !StringComparer.Ordinal.Equals(
                evidence.CertifiedTopologyDigest,
                content.CertifiedTopologyDigest) ||
            !StringComparer.Ordinal.Equals(
                evidence.TopologyAtlasDigest,
                content.TopologyAtlasDigest))
        {
            throw new InvalidDataException(
                "Structure edit episode and Atlas evidence use different coordinates.");
        }
    }

    private static void ValidateAtlasReceipt(
        IntuitionTopologyAtlasEvidenceInputReceipt evidence,
        IntuitionTopologyAtlasInputReceipt atlas)
    {
        if (!StringComparer.Ordinal.Equals(
                evidence.TopologyAtlasInputReceiptRef,
                atlas.PublicationRef) &&
            !StringComparer.Ordinal.Equals(
                evidence.TopologyAtlasInputReceiptRef,
                atlas.AtlasRef))
        {
            // The reference is checked by ArtifactStore retrieval. PublicationRef and
            // AtlasRef are content coordinates rather than the receipt coordinate.
        }
        if (!StringComparer.Ordinal.Equals(
                evidence.TruthReleaseDigest,
                atlas.TruthReleaseDigest) ||
            !StringComparer.Ordinal.Equals(
                evidence.CertifiedTopologyDigest,
                atlas.CertifiedTopologyDigest) ||
            !StringComparer.Ordinal.Equals(
                evidence.TopologyAtlasDigest,
                atlas.TopologyAtlasDigest) ||
            !StringComparer.Ordinal.Equals(
                evidence.CertifiedAlgorithmProfileDigest,
                atlas.CertifiedAlgorithmProfileDigest) ||
            !StringComparer.Ordinal.Equals(
                evidence.AtlasAlgorithmProfileDigest,
                atlas.AtlasAlgorithmProfileDigest) ||
            !StringComparer.Ordinal.Equals(
                evidence.ProducerCommit,
                atlas.ProducerCommit) ||
            !StringComparer.Ordinal.Equals(
                evidence.SourceCommit,
                atlas.SourceCommit) ||
            !StringComparer.Ordinal.Equals(
                evidence.SourceTree,
                atlas.SourceTree))
        {
            throw new InvalidDataException(
                "Atlas evidence receipt and Atlas receipt use different coordinates.");
        }
    }

    private static (HashSet<string> Nodes, HashSet<string> Clusters)
        AllowedAnchors(
            StructureEditEpisodeContent episode,
            TopologyAtlasReadModel atlas)
    {
        var explicitNodes = episode.SelectedNodeIds
            .Concat(episode.SelectedEdges.SelectMany(edge =>
                new[] { edge.DependencyId, edge.DependentId }))
            .ToHashSet(StringComparer.Ordinal);
        var nodes = explicitNodes.ToHashSet(StringComparer.Ordinal);
        var clusters = episode.SelectedClusterIds
            .ToHashSet(StringComparer.Ordinal);

        foreach (string nodeId in explicitNodes)
        {
            TopologyAtlasNodeReadModel node = atlas.GetNode(nodeId);
            foreach (string clusterId in node.ClusterPath)
            {
                clusters.Add(clusterId);
            }
        }
        foreach (string clusterId in episode.SelectedClusterIds)
        {
            TopologyAtlasClusterReadModel cluster = atlas.GetCluster(clusterId);
            nodes.UnionWith(cluster.MemberNodeIds);
        }
        return (nodes, clusters);
    }

    private static void RequireDigest(
        ReadOnlySpan<byte> bytes,
        string expected,
        string label)
    {
        string actual = "sha256:" + Convert.ToHexStringLower(
            SHA256.HashData(bytes));
        if (!StringComparer.Ordinal.Equals(actual, expected))
        {
            throw new InvalidDataException(
                $"{label} blob failed exact digest verification.");
        }
    }

    internal static string RequiredStoreRoot(ArtifactStore store)
    {
        string probe = store.PathFor("sha256:" + new string('0', 64));
        DirectoryInfo? directory = new FileInfo(probe).Directory;
        while (directory is not null &&
            !StringComparer.Ordinal.Equals(directory.Name, "sha256"))
        {
            directory = directory.Parent;
        }
        return directory?.Parent?.FullName
            ?? throw new InvalidOperationException(
                "Cannot establish the Intuition artifact-store root.");
    }
}

public static class StructureEditCandidateContextBuilder
{
    public static StructureEditCandidateContext Build(
        ArtifactStore store,
        string episodeRef,
        string episodeReceiptRef,
        string evidenceReceiptRef)
    {
        StructureEditCandidateResearchState state =
            StructureEditCandidateResearchLoader.Load(
                store,
                episodeRef,
                episodeReceiptRef,
                evidenceReceiptRef);
        StructureEditEpisodeContent episode = state.Episode.EpisodeContent;

        var explicitNodes = episode.SelectedNodeIds
            .Concat(episode.SelectedEdges.SelectMany(edge =>
                new[] { edge.DependencyId, edge.DependentId }))
            .ToHashSet(StringComparer.Ordinal);
        if (explicitNodes.Count == 0)
        {
            foreach (string clusterId in episode.SelectedClusterIds)
            {
                TopologyAtlasClusterReadModel cluster =
                    state.Atlas.GetCluster(clusterId);
                explicitNodes.UnionWith(cluster.RepresentativeNodeIds);
                explicitNodes.UnionWith(cluster.BoundaryNodeIds);
            }
        }

        StructureEditCandidateContextNode[] nodes = explicitNodes
            .Order(StringComparer.Ordinal)
            .Select(nodeId =>
            {
                TopologyAtlasNodeReadModel node = state.Atlas.GetNode(nodeId);
                TopologyAtlasNodeTraitsEvidence traits =
                    state.Evidence.GetTraits(nodeId);
                return new StructureEditCandidateContextNode(
                    nodeId,
                    state.Evidence.StableNodeId(nodeId),
                    traits.PrimaryRole,
                    traits.StructuralTraits,
                    node.ClusterPath);
            })
            .ToArray();

        var anchorClusters = episode.SelectedClusterIds
            .Concat(nodes.SelectMany(node => node.ClusterPath))
            .Distinct(StringComparer.Ordinal)
            .Order(StringComparer.Ordinal)
            .ToArray();
        var anchorClusterSet = anchorClusters.ToHashSet(StringComparer.Ordinal);
        var anchorNodeSet = nodes.Select(node => node.NodeId)
            .ToHashSet(StringComparer.Ordinal);

        StructureEditCandidateContextInterface[] interfaces =
            state.Evidence.ClusterInterfaces
                .Where(item =>
                    anchorClusterSet.Contains(item.SourceClusterId) ||
                    anchorClusterSet.Contains(item.TargetClusterId) ||
                    item.SourceBoundaryNodeIds.Any(anchorNodeSet.Contains) ||
                    item.TargetBoundaryNodeIds.Any(anchorNodeSet.Contains))
                .Select(item => new StructureEditCandidateContextInterface(
                    item.InterfaceId,
                    item.SourceClusterId,
                    item.TargetClusterId,
                    item.SourceBoundaryNodeIds,
                    item.TargetBoundaryNodeIds,
                    item.CutBridgeEdgeIds))
                .OrderBy(item => item.InterfaceId, StringComparer.Ordinal)
                .ToArray();

        TopologyAtlasAffinityWitnessEvidence[] affinities =
            state.Evidence.AffinityWitnesses
                .Where(item =>
                    anchorNodeSet.Contains(item.SourceNodeId) ||
                    anchorNodeSet.Contains(item.NeighborNodeId))
                .OrderBy(item => item.SourceNodeId, StringComparer.Ordinal)
                .ThenBy(item => item.Rank)
                .ThenBy(item => item.NeighborNodeId, StringComparer.Ordinal)
                .ToArray();

        return new StructureEditCandidateContext(
            StructureEditCandidateSchemas.Context,
            episodeRef,
            episodeReceiptRef,
            evidenceReceiptRef,
            episode.TruthReleaseDigest,
            episode.TopologyAtlasDigest,
            state.EvidenceReceipt.TopologyAtlasEvidenceDigest,
            episode.HumanIntent,
            episode.SelectionKind,
            episode.GestureKind,
            episode.AllowedEditKinds,
            episode.CandidateLimit,
            nodes,
            anchorClusters,
            interfaces,
            affinities);
    }
}
