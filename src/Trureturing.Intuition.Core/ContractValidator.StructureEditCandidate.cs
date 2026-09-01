using System.Globalization;

namespace Trureturing.Intuition.Core;

public static partial class ContractValidator
{
    public static void Validate(StructureEditCandidateDraftSet value)
    {
        ArgumentNullException.ThrowIfNull(value);
        RequireSchema(value.Schema, StructureEditCandidateSchemas.DraftSet);
        RequireArtifactRef(value.EpisodeRef, nameof(value.EpisodeRef));
        RequireArtifactRef(
            value.EpisodeReceiptRef,
            nameof(value.EpisodeReceiptRef));
        RequireArtifactRef(
            value.TopologyAtlasEvidenceInputReceiptRef,
            nameof(value.TopologyAtlasEvidenceInputReceiptRef));
        RequireCandidateText(value.GeneratedBy, nameof(value.GeneratedBy), 256);
        RequireCandidateText(
            value.ModelSnapshot,
            nameof(value.ModelSnapshot),
            512);
        ArgumentNullException.ThrowIfNull(value.Candidates);
        if (value.Candidates.Count is < 1 or > 12)
        {
            throw new InvalidOperationException(
                "candidate draft set must contain from 1 through 12 candidates.");
        }
        foreach (StructureEditCandidateDraft candidate in value.Candidates)
        {
            Validate(candidate);
        }
        RequireCandidateTimestamp(
            value.GeneratedAt,
            nameof(value.GeneratedAt),
            canonical: false);
    }

    public static void Validate(StructureEditCandidateDraft value)
    {
        ArgumentNullException.ThrowIfNull(value);
        RequireCandidateEditKind(value.EditKind, nameof(value.EditKind));
        RequireUniqueCandidateStrings(
            value.AnchorNodeIds,
            nameof(value.AnchorNodeIds),
            512,
            sorted: false);
        RequireUniquePrefixedCandidateIds(
            value.AnchorClusterIds,
            nameof(value.AnchorClusterIds),
            "cluster:sha256:",
            sorted: false);
        RequireUniquePrefixedCandidateIds(
            value.InterfaceEvidenceIds,
            nameof(value.InterfaceEvidenceIds),
            "interface:sha256:",
            sorted: false);
        RequireAffinityRefs(
            value.AffinityEvidence,
            nameof(value.AffinityEvidence),
            sorted: false);
        RequireCandidateText(
            value.CandidateStatement,
            nameof(value.CandidateStatement),
            8000);
        RequireCandidateText(
            value.RepresentationMap,
            nameof(value.RepresentationMap),
            4000);
        RequireUniqueCandidateStrings(
            value.AssumptionMap,
            nameof(value.AssumptionMap),
            1000,
            sorted: false);
        RequireUniqueCandidateStrings(
            value.PreservedInvariants,
            nameof(value.PreservedInvariants),
            1000,
            sorted: false);
        RequireCandidateText(value.Falsifier, nameof(value.Falsifier), 4000);
        RequireCandidateText(
            value.VerificationRoute,
            nameof(value.VerificationRoute),
            4000);
    }

    public static void Validate(StructureEditCandidate value)
    {
        ArgumentNullException.ThrowIfNull(value);
        RequireArtifactRef(value.CandidateId, nameof(value.CandidateId));
        ArgumentNullException.ThrowIfNull(value.CandidateContent);
        Validate(value.CandidateContent);
        string expected = CanonicalJson.Sha256Reference(
            CanonicalJson.Serialize(value.CandidateContent));
        if (!StringComparer.Ordinal.Equals(value.CandidateId, expected))
        {
            throw new InvalidOperationException(
                "candidate_id does not address canonical candidate_content bytes.");
        }
    }

    public static void Validate(StructureEditCandidateContent value)
    {
        ArgumentNullException.ThrowIfNull(value);
        RequireCandidateEditKind(value.EditKind, nameof(value.EditKind));
        string expectedKind = StructureEditCandidateMappings.CandidateKind(
            value.EditKind);
        if (!StringComparer.Ordinal.Equals(value.CandidateKind, expectedKind))
        {
            throw new InvalidOperationException(
                "candidate_kind does not match edit_kind.");
        }
        if (!StringComparer.Ordinal.Equals(value.ClaimStatus, "advisory"))
        {
            throw new InvalidOperationException(
                "structure edit candidates must remain advisory.");
        }
        RequireUniqueCandidateStrings(
            value.AnchorNodeIds,
            nameof(value.AnchorNodeIds),
            512,
            sorted: true);
        RequireUniqueCandidateStrings(
            value.AnchorStableNodeIds,
            nameof(value.AnchorStableNodeIds),
            512,
            sorted: true);
        RequireUniquePrefixedCandidateIds(
            value.AnchorClusterIds,
            nameof(value.AnchorClusterIds),
            "cluster:sha256:",
            sorted: true);
        if (value.AnchorNodeIds.Count == 0 && value.AnchorClusterIds.Count == 0)
        {
            throw new InvalidOperationException(
                "candidate must retain at least one structural anchor.");
        }
        if (value.AnchorStableNodeIds.Count != value.AnchorNodeIds.Count)
        {
            throw new InvalidOperationException(
                "every anchor node must have one stable identity.");
        }
        RequireUniquePrefixedCandidateIds(
            value.InterfaceEvidenceIds,
            nameof(value.InterfaceEvidenceIds),
            "interface:sha256:",
            sorted: true);
        RequireAffinityRefs(
            value.AffinityEvidence,
            nameof(value.AffinityEvidence),
            sorted: true);
        RequireCandidateText(
            value.CandidateStatement,
            nameof(value.CandidateStatement),
            8000);
        RequireCandidateText(
            value.RepresentationMap,
            nameof(value.RepresentationMap),
            4000);
        RequireUniqueCandidateStrings(
            value.AssumptionMap,
            nameof(value.AssumptionMap),
            1000,
            sorted: true);
        RequireUniqueCandidateStrings(
            value.PreservedInvariants,
            nameof(value.PreservedInvariants),
            1000,
            sorted: true);
        RequireCandidateText(value.Falsifier, nameof(value.Falsifier), 4000);
        RequireCandidateText(
            value.VerificationRoute,
            nameof(value.VerificationRoute),
            4000);
        if (!StringComparer.Ordinal.Equals(
                value.TopologyLoweringStatus,
                "unlowered"))
        {
            throw new InvalidOperationException(
                "semantic candidates must remain unlowered in this contract.");
        }
    }

    public static void Validate(StructureEditCandidateSet value)
    {
        ArgumentNullException.ThrowIfNull(value);
        RequireSchema(value.Schema, StructureEditCandidateSchemas.CandidateSet);
        RequireArtifactRef(value.CandidateSetId, nameof(value.CandidateSetId));
        ArgumentNullException.ThrowIfNull(value.CandidateSetContent);
        Validate(value.CandidateSetContent);
        string expected = CanonicalJson.Sha256Reference(
            CanonicalJson.Serialize(value.CandidateSetContent));
        if (!StringComparer.Ordinal.Equals(value.CandidateSetId, expected))
        {
            throw new InvalidOperationException(
                "candidate_set_id does not address canonical candidate_set_content bytes.");
        }
    }

    public static void Validate(StructureEditCandidateSetContent value)
    {
        ArgumentNullException.ThrowIfNull(value);
        RequireArtifactRef(value.EpisodeRef, nameof(value.EpisodeRef));
        RequireArtifactRef(
            value.EpisodeReceiptRef,
            nameof(value.EpisodeReceiptRef));
        RequireArtifactRef(
            value.TopologyAtlasEvidenceInputReceiptRef,
            nameof(value.TopologyAtlasEvidenceInputReceiptRef));
        RequireArtifactRef(
            value.TruthReleaseDigest,
            nameof(value.TruthReleaseDigest));
        RequireArtifactRef(
            value.CertifiedTopologyDigest,
            nameof(value.CertifiedTopologyDigest));
        RequireArtifactRef(
            value.TopologyAtlasDigest,
            nameof(value.TopologyAtlasDigest));
        RequireArtifactRef(
            value.TopologyAtlasEvidenceDigest,
            nameof(value.TopologyAtlasEvidenceDigest));
        RequireCandidateText(value.GeneratedBy, nameof(value.GeneratedBy), 256);
        RequireCandidateText(
            value.ModelSnapshot,
            nameof(value.ModelSnapshot),
            512);
        if (!StringComparer.Ordinal.Equals(
                value.GenerationProfile,
                StructureEditCandidateSchemas.GenerationProfile))
        {
            throw new InvalidOperationException(
                "candidate generation profile is unsupported.");
        }
        ArgumentNullException.ThrowIfNull(value.Candidates);
        if (value.Candidates.Count is < 1 or > 12)
        {
            throw new InvalidOperationException(
                "candidate set must contain from 1 through 12 candidates.");
        }
        string? previous = null;
        foreach (StructureEditCandidate candidate in value.Candidates)
        {
            Validate(candidate);
            if (previous is not null &&
                StringComparer.Ordinal.Compare(previous, candidate.CandidateId) >= 0)
            {
                throw new InvalidOperationException(
                    "candidates must be sorted by candidate_id and unique.");
            }
            previous = candidate.CandidateId;
        }
        RequireCandidateTimestamp(
            value.GeneratedAt,
            nameof(value.GeneratedAt),
            canonical: true);
    }

    public static void Validate(StructureEditCandidateSetReceipt value)
    {
        ArgumentNullException.ThrowIfNull(value);
        RequireSchema(value.Schema, StructureEditCandidateSchemas.Receipt);
        RequireArtifactRef(
            value.CandidateSetRef,
            nameof(value.CandidateSetRef));
        RequireArtifactRef(value.CandidateSetId, nameof(value.CandidateSetId));
        RequireArtifactRef(value.EpisodeRef, nameof(value.EpisodeRef));
        RequireArtifactRef(
            value.EpisodeReceiptRef,
            nameof(value.EpisodeReceiptRef));
        RequireArtifactRef(
            value.TopologyAtlasEvidenceInputReceiptRef,
            nameof(value.TopologyAtlasEvidenceInputReceiptRef));
        RequireArtifactRef(
            value.TruthReleaseDigest,
            nameof(value.TruthReleaseDigest));
        RequireArtifactRef(
            value.TopologyAtlasDigest,
            nameof(value.TopologyAtlasDigest));
        RequireArtifactRef(
            value.TopologyAtlasEvidenceDigest,
            nameof(value.TopologyAtlasEvidenceDigest));
        RequireSortedUniqueRefs(
            value.CandidateIds,
            nameof(value.CandidateIds),
            requireNonEmpty: true);
        if (!StringComparer.Ordinal.Equals(
                value.GenerationProfile,
                StructureEditCandidateSchemas.GenerationProfile))
        {
            throw new InvalidOperationException(
                "candidate receipt generation profile is unsupported.");
        }
    }

    public static void Validate(StructureEditCandidateContext value)
    {
        ArgumentNullException.ThrowIfNull(value);
        RequireSchema(value.Schema, StructureEditCandidateSchemas.Context);
        RequireArtifactRef(value.EpisodeRef, nameof(value.EpisodeRef));
        RequireArtifactRef(
            value.EpisodeReceiptRef,
            nameof(value.EpisodeReceiptRef));
        RequireArtifactRef(
            value.TopologyAtlasEvidenceInputReceiptRef,
            nameof(value.TopologyAtlasEvidenceInputReceiptRef));
        RequireArtifactRef(
            value.TruthReleaseDigest,
            nameof(value.TruthReleaseDigest));
        RequireArtifactRef(
            value.TopologyAtlasDigest,
            nameof(value.TopologyAtlasDigest));
        RequireArtifactRef(
            value.TopologyAtlasEvidenceDigest,
            nameof(value.TopologyAtlasEvidenceDigest));
        RequireCandidateText(value.HumanIntent, nameof(value.HumanIntent), 8000);
        if (!StructureSelectionKinds.Contains(value.SelectionKind))
        {
            throw new InvalidOperationException(
                "candidate context selection_kind is unsupported.");
        }
        if (!HumanStructureGestureKinds.Contains(value.GestureKind))
        {
            throw new InvalidOperationException(
                "candidate context gesture_kind is unsupported.");
        }
        RequireSortedUniqueStrings(
            value.AllowedEditKinds,
            nameof(value.AllowedEditKinds));
        if (value.AllowedEditKinds.Count == 0 ||
            value.AllowedEditKinds.Any(kind =>
                !StructureEditCandidateMappings.EditKinds.Contains(kind)))
        {
            throw new InvalidOperationException(
                "candidate context contains unsupported edit kinds.");
        }
        if (value.CandidateLimit is < 1 or > 12)
        {
            throw new InvalidOperationException(
                "candidate context limit must be from 1 through 12.");
        }
        ValidateContextNodes(value.AnchorNodes);
        RequireUniquePrefixedCandidateIds(
            value.AnchorClusterIds,
            nameof(value.AnchorClusterIds),
            "cluster:sha256:",
            sorted: true);
        ValidateContextInterfaces(value.RelevantInterfaces);
        RequireAffinityWitnessOrder(value.RelevantAffinityWitnesses);
    }

    private static void ValidateContextNodes(
        IReadOnlyList<StructureEditCandidateContextNode> values)
    {
        ArgumentNullException.ThrowIfNull(values);
        string? previous = null;
        foreach (StructureEditCandidateContextNode value in values)
        {
            ArgumentNullException.ThrowIfNull(value);
            RequireCandidateText(value.NodeId, "anchor_nodes.node_id", 512);
            RequireCandidateText(
                value.StableNodeId,
                "anchor_nodes.stable_node_id",
                512);
            RequireCandidateText(
                value.PrimaryRole,
                "anchor_nodes.primary_role",
                128);
            RequireUniqueCandidateStrings(
                value.StructuralTraits,
                "anchor_nodes.structural_traits",
                128,
                sorted: false);
            RequireUniquePrefixedCandidateIds(
                value.ClusterPath,
                "anchor_nodes.cluster_path",
                "cluster:sha256:",
                sorted: false);
            if (previous is not null &&
                StringComparer.Ordinal.Compare(previous, value.NodeId) >= 0)
            {
                throw new InvalidOperationException(
                    "anchor_nodes must be sorted by node_id and unique.");
            }
            previous = value.NodeId;
        }
    }

    private static void ValidateContextInterfaces(
        IReadOnlyList<StructureEditCandidateContextInterface> values)
    {
        ArgumentNullException.ThrowIfNull(values);
        string? previous = null;
        foreach (StructureEditCandidateContextInterface value in values)
        {
            ArgumentNullException.ThrowIfNull(value);
            RequirePrefixedCandidateId(
                value.InterfaceId,
                "relevant_interfaces.interface_id",
                "interface:sha256:");
            RequirePrefixedCandidateId(
                value.SourceClusterId,
                "relevant_interfaces.source_cluster_id",
                "cluster:sha256:");
            RequirePrefixedCandidateId(
                value.TargetClusterId,
                "relevant_interfaces.target_cluster_id",
                "cluster:sha256:");
            RequireUniqueCandidateStrings(
                value.SourceBoundaryNodeIds,
                "relevant_interfaces.source_boundary_node_ids",
                512,
                sorted: true);
            RequireUniqueCandidateStrings(
                value.TargetBoundaryNodeIds,
                "relevant_interfaces.target_boundary_node_ids",
                512,
                sorted: true);
            RequireUniquePrefixedCandidateIds(
                value.CutBridgeEdgeIds,
                "relevant_interfaces.cut_bridge_edge_ids",
                "edge:sha256:",
                sorted: true);
            if (previous is not null &&
                StringComparer.Ordinal.Compare(previous, value.InterfaceId) >= 0)
            {
                throw new InvalidOperationException(
                    "relevant_interfaces must be sorted and unique.");
            }
            previous = value.InterfaceId;
        }
    }

    private static void RequireAffinityWitnessOrder(
        IReadOnlyList<TopologyAtlasAffinityWitnessEvidence> values)
    {
        ArgumentNullException.ThrowIfNull(values);
        string? previous = null;
        foreach (TopologyAtlasAffinityWitnessEvidence value in values)
        {
            ArgumentNullException.ThrowIfNull(value);
            string key = CandidateAffinityKey(
                value.SourceNodeId,
                value.NeighborNodeId,
                checked((int)value.Rank));
            if (previous is not null &&
                StringComparer.Ordinal.Compare(previous, key) >= 0)
            {
                throw new InvalidOperationException(
                    "relevant_affinity_witnesses must use canonical order.");
            }
            previous = key;
        }
    }

    private static void RequireAffinityRefs(
        IReadOnlyList<StructureAffinityEvidenceRef> values,
        string name,
        bool sorted)
    {
        ArgumentNullException.ThrowIfNull(values);
        var keys = new HashSet<string>(StringComparer.Ordinal);
        string? previous = null;
        foreach (StructureAffinityEvidenceRef value in values)
        {
            ArgumentNullException.ThrowIfNull(value);
            RequireCandidateText(
                value.SourceNodeId,
                name + ".source_node_id",
                512);
            RequireCandidateText(
                value.NeighborNodeId,
                name + ".neighbor_node_id",
                512);
            if (value.Rank is < 1 or > 64)
            {
                throw new InvalidOperationException(
                    $"{name}.rank must be from 1 through 64.");
            }
            string key = CandidateAffinityKey(
                value.SourceNodeId,
                value.NeighborNodeId,
                value.Rank);
            if (!keys.Add(key))
            {
                throw new InvalidOperationException($"{name} must be unique.");
            }
            if (sorted && previous is not null &&
                StringComparer.Ordinal.Compare(previous, key) >= 0)
            {
                throw new InvalidOperationException(
                    $"{name} must use canonical order.");
            }
            previous = key;
        }
    }

    private static string CandidateAffinityKey(
        string sourceNodeId,
        string neighborNodeId,
        int rank) =>
        sourceNodeId.Length.ToString(CultureInfo.InvariantCulture) + ":" +
        sourceNodeId + ":" + rank.ToString(CultureInfo.InvariantCulture) + ":" +
        neighborNodeId;

    private static void RequireCandidateEditKind(string value, string name)
    {
        RequireCandidateText(value, name, 128);
        if (!StructureEditCandidateMappings.EditKinds.Contains(value))
        {
            throw new InvalidOperationException(
                $"{name} contains unsupported structure edit kind '{value}'.");
        }
    }

    private static void RequireUniqueCandidateStrings(
        IReadOnlyList<string> values,
        string name,
        int maximumLength,
        bool sorted)
    {
        ArgumentNullException.ThrowIfNull(values);
        var seen = new HashSet<string>(StringComparer.Ordinal);
        string? previous = null;
        foreach (string value in values)
        {
            RequireCandidateText(value, name, maximumLength);
            if (!seen.Add(value))
            {
                throw new InvalidOperationException($"{name} must be unique.");
            }
            if (sorted && previous is not null &&
                StringComparer.Ordinal.Compare(previous, value) >= 0)
            {
                throw new InvalidOperationException(
                    $"{name} must be strictly ordinal-sorted.");
            }
            previous = value;
        }
    }

    private static void RequireUniquePrefixedCandidateIds(
        IReadOnlyList<string> values,
        string name,
        string prefix,
        bool sorted)
    {
        RequireUniqueCandidateStrings(
            values,
            name,
            prefix.Length + 64,
            sorted);
        foreach (string value in values)
        {
            RequirePrefixedCandidateId(value, name, prefix);
        }
    }

    private static void RequirePrefixedCandidateId(
        string value,
        string name,
        string prefix)
    {
        if (value.Length != prefix.Length + 64 ||
            !value.StartsWith(prefix, StringComparison.Ordinal) ||
            value[prefix.Length..].Any(character =>
                character is not (>= '0' and <= '9') and
                not (>= 'a' and <= 'f')))
        {
            throw new InvalidOperationException(
                $"{name} must use {prefix}<64 lowercase hex>.");
        }
    }

    private static void RequireCandidateText(
        string? value,
        string name,
        int maximumLength)
    {
        RequireNonEmpty(value, name);
        if (value!.Length > maximumLength ||
            !StringComparer.Ordinal.Equals(value, value.Trim()))
        {
            throw new InvalidOperationException(
                $"{name} must be trimmed and at most {maximumLength} characters.");
        }
    }

    private static void RequireCandidateTimestamp(
        string value,
        string name,
        bool canonical)
    {
        RequireCandidateText(value, name, 128);
        if (!DateTimeOffset.TryParse(
                value,
                CultureInfo.InvariantCulture,
                DateTimeStyles.AssumeUniversal | DateTimeStyles.AdjustToUniversal,
                out DateTimeOffset parsed))
        {
            throw new InvalidOperationException(
                $"{name} must be an RFC 3339 timestamp.");
        }
        if (canonical && !StringComparer.Ordinal.Equals(
                value,
                parsed.ToUniversalTime().ToString("O", CultureInfo.InvariantCulture)))
        {
            throw new InvalidOperationException(
                $"{name} must use canonical UTC round-trip form.");
        }
    }
}
