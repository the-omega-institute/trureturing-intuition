using System.Globalization;

namespace Trureturing.Intuition.Core;

public static class StructureEditCandidateRegistrar
{
    public static StructureEditCandidateSetRegistration Register(
        ArtifactStore store,
        StructureEditCandidateDraftSet draftSet)
    {
        ArgumentNullException.ThrowIfNull(store);
        ArgumentNullException.ThrowIfNull(draftSet);
        ContractValidator.Validate(draftSet);

        StructureEditCandidateResearchState state =
            StructureEditCandidateResearchLoader.Load(
                store,
                draftSet.EpisodeRef,
                draftSet.EpisodeReceiptRef,
                draftSet.TopologyAtlasEvidenceInputReceiptRef);
        StructureEditEpisodeContent episode = state.Episode.EpisodeContent;
        if (draftSet.Candidates.Count > episode.CandidateLimit)
        {
            throw new InvalidDataException(
                "Structure edit draft set exceeds the episode candidate limit.");
        }

        StructureEditCandidate[] candidates = draftSet.Candidates
            .Select((draft, index) => BuildCandidate(state, draft, index))
            .OrderBy(candidate => candidate.CandidateId, StringComparer.Ordinal)
            .ToArray();
        for (int index = 1; index < candidates.Length; index++)
        {
            if (StringComparer.Ordinal.Equals(
                    candidates[index - 1].CandidateId,
                    candidates[index].CandidateId))
            {
                throw new InvalidDataException(
                    "Structure edit draft set contains duplicate semantic candidates.");
            }
        }

        var content = new StructureEditCandidateSetContent(
            draftSet.EpisodeRef,
            draftSet.EpisodeReceiptRef,
            draftSet.TopologyAtlasEvidenceInputReceiptRef,
            episode.TruthReleaseDigest,
            episode.CertifiedTopologyDigest,
            episode.TopologyAtlasDigest,
            state.EvidenceReceipt.TopologyAtlasEvidenceDigest,
            NormalizeText(draftSet.GeneratedBy, nameof(draftSet.GeneratedBy), 256),
            NormalizeText(draftSet.ModelSnapshot, nameof(draftSet.ModelSnapshot), 512),
            StructureEditCandidateSchemas.GenerationProfile,
            candidates,
            NormalizeTimestamp(draftSet.GeneratedAt, nameof(draftSet.GeneratedAt)));
        string candidateSetId = CanonicalJson.Sha256Reference(
            CanonicalJson.Serialize(content));
        var candidateSet = new StructureEditCandidateSet(
            StructureEditCandidateSchemas.CandidateSet,
            candidateSetId,
            content);
        ContractValidator.Validate(candidateSet);
        string candidateSetRef = store.Put(candidateSet);

        string[] candidateIds = candidates
            .Select(candidate => candidate.CandidateId)
            .ToArray();
        var receipt = new StructureEditCandidateSetReceipt(
            StructureEditCandidateSchemas.Receipt,
            candidateSetRef,
            candidateSetId,
            draftSet.EpisodeRef,
            draftSet.EpisodeReceiptRef,
            draftSet.TopologyAtlasEvidenceInputReceiptRef,
            episode.TruthReleaseDigest,
            episode.TopologyAtlasDigest,
            state.EvidenceReceipt.TopologyAtlasEvidenceDigest,
            candidateIds,
            StructureEditCandidateSchemas.GenerationProfile);
        ContractValidator.Validate(receipt);
        string receiptRef = store.Put(receipt);
        return new StructureEditCandidateSetRegistration(
            candidateSetRef,
            receiptRef,
            candidateSetId,
            candidateIds,
            candidates.Length,
            episode.TruthReleaseDigest,
            state.EvidenceReceipt.TopologyAtlasEvidenceDigest);
    }

    public static StructureEditCandidateContext PrepareContext(
        ArtifactStore store,
        string episodeRef,
        string episodeReceiptRef,
        string evidenceReceiptRef)
    {
        StructureEditCandidateContext context =
            StructureEditCandidateContextBuilder.Build(
                store,
                episodeRef,
                episodeReceiptRef,
                evidenceReceiptRef);
        ContractValidator.Validate(context);
        return context;
    }

    private static StructureEditCandidate BuildCandidate(
        StructureEditCandidateResearchState state,
        StructureEditCandidateDraft draft,
        int index)
    {
        ArgumentNullException.ThrowIfNull(draft);
        string path = $"candidates[{index}]";
        string editKind = NormalizeText(
            draft.EditKind,
            path + ".edit_kind",
            128);
        if (!state.Episode.EpisodeContent.AllowedEditKinds.Contains(
                editKind,
                StringComparer.Ordinal))
        {
            throw new InvalidDataException(
                $"{path}.edit_kind is outside the episode edit algebra.");
        }
        string candidateKind = StructureEditCandidateMappings.CandidateKind(
            editKind);

        string[] anchorNodeIds = NormalizeStrings(
            draft.AnchorNodeIds,
            path + ".anchor_node_ids",
            512);
        string[] anchorClusterIds = NormalizeClusterIds(
            draft.AnchorClusterIds,
            path + ".anchor_cluster_ids");
        if (anchorNodeIds.Length == 0 && anchorClusterIds.Length == 0)
        {
            throw new InvalidDataException(
                $"{path} must retain at least one episode anchor.");
        }
        string? unknownNode = anchorNodeIds.FirstOrDefault(
            nodeId => !state.AllowedAnchorNodeIds.Contains(nodeId));
        if (unknownNode is not null)
        {
            throw new InvalidDataException(
                $"{path} uses node '{unknownNode}' outside the episode scope.");
        }
        string? unknownCluster = anchorClusterIds.FirstOrDefault(
            clusterId => !state.AllowedAnchorClusterIds.Contains(clusterId));
        if (unknownCluster is not null)
        {
            throw new InvalidDataException(
                $"{path} uses cluster '{unknownCluster}' outside the episode scope.");
        }

        string[] stableNodeIds = anchorNodeIds
            .Select(state.Evidence.StableNodeId)
            .Distinct(StringComparer.Ordinal)
            .Order(StringComparer.Ordinal)
            .ToArray();
        if (stableNodeIds.Length != anchorNodeIds.Length)
        {
            throw new InvalidDataException(
                $"{path} contains node anchors with colliding stable identities.");
        }
        var anchorNodeSet = anchorNodeIds.ToHashSet(StringComparer.Ordinal);
        var anchorClusterSet = anchorClusterIds.ToHashSet(StringComparer.Ordinal);
        string[] interfaceIds = ValidateInterfaces(
            state,
            draft.InterfaceEvidenceIds,
            anchorNodeSet,
            anchorClusterSet,
            path);
        StructureAffinityEvidenceRef[] affinities = ValidateAffinities(
            state,
            draft.AffinityEvidence,
            anchorNodeSet,
            path);

        var content = new StructureEditCandidateContent(
            editKind,
            candidateKind,
            "advisory",
            anchorNodeIds,
            stableNodeIds,
            anchorClusterIds,
            interfaceIds,
            affinities,
            NormalizeText(
                draft.CandidateStatement,
                path + ".candidate_statement",
                8000),
            NormalizeText(
                draft.RepresentationMap,
                path + ".representation_map",
                4000),
            NormalizeStrings(
                draft.AssumptionMap,
                path + ".assumption_map",
                1000),
            NormalizeStrings(
                draft.PreservedInvariants,
                path + ".preserved_invariants",
                1000),
            NormalizeText(draft.Falsifier, path + ".falsifier", 4000),
            NormalizeText(
                draft.VerificationRoute,
                path + ".verification_route",
                4000),
            "unlowered");
        string candidateId = CanonicalJson.Sha256Reference(
            CanonicalJson.Serialize(content));
        return new StructureEditCandidate(candidateId, content);
    }

    private static string[] ValidateInterfaces(
        StructureEditCandidateResearchState state,
        IReadOnlyList<string> values,
        IReadOnlySet<string> anchorNodes,
        IReadOnlySet<string> anchorClusters,
        string path)
    {
        string[] result = NormalizePrefixedIds(
            values,
            path + ".interface_evidence_ids",
            "interface:sha256:");
        var byId = state.Evidence.ClusterInterfaces.ToDictionary(
            item => item.InterfaceId,
            StringComparer.Ordinal);
        foreach (string interfaceId in result)
        {
            if (!byId.TryGetValue(
                    interfaceId,
                    out TopologyAtlasClusterInterfaceEvidence? evidence))
            {
                throw new InvalidDataException(
                    $"{path} references unknown interface evidence '{interfaceId}'.");
            }
            bool relevant = anchorClusters.Contains(evidence.SourceClusterId) ||
                anchorClusters.Contains(evidence.TargetClusterId) ||
                evidence.SourceBoundaryNodeIds.Any(anchorNodes.Contains) ||
                evidence.TargetBoundaryNodeIds.Any(anchorNodes.Contains);
            if (!relevant)
            {
                throw new InvalidDataException(
                    $"{path} references interface evidence outside its anchors.");
            }
        }
        return result;
    }

    private static StructureAffinityEvidenceRef[] ValidateAffinities(
        StructureEditCandidateResearchState state,
        IReadOnlyList<StructureAffinityEvidenceRef> values,
        IReadOnlySet<string> anchorNodes,
        string path)
    {
        ArgumentNullException.ThrowIfNull(values);
        var result = values.Select((value, index) =>
        {
            ArgumentNullException.ThrowIfNull(value);
            string source = NormalizeText(
                value.SourceNodeId,
                $"{path}.affinity_evidence[{index}].source_node_id",
                512);
            string neighbor = NormalizeText(
                value.NeighborNodeId,
                $"{path}.affinity_evidence[{index}].neighbor_node_id",
                512);
            if (value.Rank is < 1 or > 64)
            {
                throw new InvalidDataException(
                    $"{path}.affinity_evidence[{index}].rank is out of range.");
            }
            if (state.Evidence.FindAffinityWitness(
                    source,
                    neighbor,
                    value.Rank) is null)
            {
                throw new InvalidDataException(
                    $"{path} references absent affinity evidence.");
            }
            if (!anchorNodes.Contains(source) && !anchorNodes.Contains(neighbor))
            {
                throw new InvalidDataException(
                    $"{path} references affinity evidence outside its node anchors.");
            }
            return new StructureAffinityEvidenceRef(
                source,
                neighbor,
                value.Rank);
        }).OrderBy(value => value.SourceNodeId, StringComparer.Ordinal)
            .ThenBy(value => value.Rank)
            .ThenBy(value => value.NeighborNodeId, StringComparer.Ordinal)
            .ToArray();
        for (int index = 1; index < result.Length; index++)
        {
            StructureAffinityEvidenceRef previous = result[index - 1];
            StructureAffinityEvidenceRef current = result[index];
            if (StringComparer.Ordinal.Equals(
                    previous.SourceNodeId,
                    current.SourceNodeId) &&
                StringComparer.Ordinal.Equals(
                    previous.NeighborNodeId,
                    current.NeighborNodeId) &&
                previous.Rank == current.Rank)
            {
                throw new InvalidDataException(
                    $"{path}.affinity_evidence must be unique.");
            }
        }
        return result;
    }

    private static string NormalizeTimestamp(string value, string name)
    {
        string normalized = NormalizeText(value, name, 128);
        if (!DateTimeOffset.TryParse(
                normalized,
                CultureInfo.InvariantCulture,
                DateTimeStyles.AssumeUniversal | DateTimeStyles.AdjustToUniversal,
                out DateTimeOffset parsed))
        {
            throw new InvalidDataException(
                $"{name} must be an RFC 3339 timestamp.");
        }
        return parsed.ToUniversalTime().ToString("O", CultureInfo.InvariantCulture);
    }

    private static string NormalizeText(
        string? value,
        string name,
        int maximum)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            throw new InvalidDataException($"{name} must not be empty.");
        }
        string normalized = value.Trim();
        if (normalized.Length > maximum)
        {
            throw new InvalidDataException(
                $"{name} exceeds {maximum} characters.");
        }
        return normalized;
    }

    private static string[] NormalizeStrings(
        IReadOnlyList<string> values,
        string name,
        int maximumLength)
    {
        ArgumentNullException.ThrowIfNull(values);
        string[] result = values
            .Select((value, index) => NormalizeText(
                value,
                $"{name}[{index}]",
                maximumLength))
            .Distinct(StringComparer.Ordinal)
            .Order(StringComparer.Ordinal)
            .ToArray();
        if (result.Length != values.Count)
        {
            throw new InvalidDataException($"{name} must be unique.");
        }
        return result;
    }

    private static string[] NormalizeClusterIds(
        IReadOnlyList<string> values,
        string name) =>
        NormalizePrefixedIds(values, name, "cluster:sha256:");

    private static string[] NormalizePrefixedIds(
        IReadOnlyList<string> values,
        string name,
        string prefix)
    {
        string[] result = NormalizeStrings(values, name, prefix.Length + 64);
        foreach (string value in result)
        {
            if (value.Length != prefix.Length + 64 ||
                !value.StartsWith(prefix, StringComparison.Ordinal) ||
                value[prefix.Length..].Any(character =>
                    character is not (>= '0' and <= '9') and
                    not (>= 'a' and <= 'f')))
            {
                throw new InvalidDataException(
                    $"{name} contains invalid content-addressed ID '{value}'.");
            }
        }
        return result;
    }
}
