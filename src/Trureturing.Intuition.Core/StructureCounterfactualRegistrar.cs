using System.Security.Cryptography;
using System.Text.Json;

namespace Trureturing.Intuition.Core;

public static class StructureCounterfactualRegistrar
{
    public static StructureEditGraphPatchRegistration RegisterPatch(
        string artifactStoreRoot,
        string candidateRef,
        StructureEditGraphPatch patch)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(artifactStoreRoot);
        ContractValidator.RequireArtifactRef(candidateRef, nameof(candidateRef));
        ArgumentNullException.ThrowIfNull(patch);
        string root = Path.GetFullPath(artifactStoreRoot);
        var store = new ArtifactStore(root);
        StructureEditCandidate candidate =
            store.Get<StructureEditCandidate>(candidateRef);
        ContractValidator.Validate(patch);
        ValidatePatchBinding(candidateRef, candidate, patch);
        ValidatePatchClosure(candidate, patch.PatchContent);

        string patchRef = store.Put(patch);
        StructureEditGraphPatchReceipt receipt = PatchReceipt(
            patchRef,
            patch);
        string receiptRef = store.Put(receipt);
        return new StructureEditGraphPatchRegistration(
            patchRef,
            receiptRef,
            patch.PatchId,
            candidateRef,
            candidate.CandidateId,
            patch.PatchContent.Operations.Count);
    }

    public static StructureCounterfactualRegistration RegisterCounterfactual(
        string artifactStoreRoot,
        string patchRef,
        TopologyCounterfactualPublication publication,
        ReadOnlySpan<byte> resultBytes)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(artifactStoreRoot);
        ContractValidator.RequireArtifactRef(patchRef, nameof(patchRef));
        ArgumentNullException.ThrowIfNull(publication);
        string root = Path.GetFullPath(artifactStoreRoot);
        var store = new ArtifactStore(root);
        StructureEditGraphPatch patch =
            store.Get<StructureEditGraphPatch>(patchRef);
        StructureEditCandidate candidate =
            store.Get<StructureEditCandidate>(patch.PatchContent.CandidateRef);
        ContractValidator.Validate(publication);
        ValidatePublicationBinding(
            patchRef,
            patch,
            candidate,
            publication);

        byte[] exactResultBytes = resultBytes.ToArray();
        string resultRef = Digest(exactResultBytes);
        if (!StringComparer.Ordinal.Equals(
                resultRef,
                publication.PublicationContent.TopologyCounterfactualResultDigest))
        {
            throw new InvalidDataException(
                "Topology counterfactual bytes do not match the publication digest.");
        }
        ValidateRawCounterfactual(
            exactResultBytes,
            publication.PublicationContent);
        WriteBlob(ResultBlobPath(root, resultRef), exactResultBytes, resultRef);

        string publicationRef = store.Put(publication);
        StructureEditGraphPatchReceipt patchReceipt = PatchReceipt(
            patchRef,
            patch);
        string patchReceiptRef = store.Put(patchReceipt);
        var publicationReceipt = new TopologyCounterfactualPublicationReceipt(
            StructureCounterfactualSchemas.PublicationReceipt,
            publicationRef,
            publication.PublicationId,
            resultRef,
            patchRef,
            patch.PatchId,
            candidate.CandidateId == publication.PublicationContent.CandidateId
                ? publication.PublicationContent.CandidateRef
                : throw new InvalidDataException(
                    "Counterfactual candidate_id changed after binding."),
            publication.PublicationContent.CandidateId,
            publication.PublicationContent.TruthReleaseDigest,
            publication.PublicationContent.TopologyAtlasDigest,
            publication.PublicationContent.TopologyAtlasEvidenceDigest,
            publication.PublicationContent.TopologyCounterfactualProfileDigest,
            publication.PublicationContent.TopologyProducerCommit,
            publication.PublicationContent.ProjectionProfile,
            StructureCounterfactualSchemas.Authority);
        string publicationReceiptRef = store.Put(publicationReceipt);

        TopologyCounterfactualProjection projection =
            publication.PublicationContent.Projection;
        bool eligible = projection.Accepted &&
            !projection.CycleRisk &&
            projection.AnalysisAvailable;
        string blockingReason = projection.CycleRisk
            ? "cycle-risk"
            : !projection.Accepted
                ? "topology-rejected"
                : !projection.AnalysisAvailable
                    ? "analysis-unavailable"
                    : "none";
        var vector = new StructureCounterfactualWorthVector(
            projection.ReachablePairGain,
            projection.ReachablePairLoss,
            projection.TotalPathCompression,
            projection.NewInterfaceHypothesisCount,
            projection.RemovedInterfaceHypothesisCount,
            projection.RemovedCutBridgeCount,
            projection.NewCutBridgeCount,
            projection.AffectedStableNodeIds.Count +
                projection.TouchedClusterIds.Count,
            patch.PatchContent.Operations.Count,
            projection.CycleRisk ? 1 : 0,
            FormalVerificationOpen: true);
        var valuationContent = new StructureCounterfactualValuationContent(
            candidate.CandidateContent.EpisodeRef ==
                publication.PublicationContent.CandidateRef
                ? throw new InvalidDataException(
                    "Candidate reference cannot alias the source episode reference.")
                : publication.PublicationContent.CandidateRef,
            publication.PublicationContent.CandidateId,
            patchRef,
            patch.PatchId,
            publicationRef,
            publication.PublicationId,
            resultRef,
            publication.PublicationContent.TruthReleaseDigest,
            publication.PublicationContent.TopologyAtlasDigest,
            publication.PublicationContent.TopologyAtlasEvidenceDigest,
            projection,
            vector,
            eligible,
            blockingReason,
            StructureCounterfactualSchemas.ValuationProfile,
            StructureCounterfactualSchemas.Authority);
        string valuationId = CanonicalJson.Sha256Reference(
            CanonicalJson.Serialize(valuationContent));
        var valuation = new StructureCounterfactualValuation(
            StructureCounterfactualSchemas.Valuation,
            valuationId,
            valuationContent);
        ContractValidator.Validate(valuation);
        string valuationRef = store.Put(valuation);

        return new StructureCounterfactualRegistration(
            patchRef,
            patchReceiptRef,
            publicationRef,
            publicationReceiptRef,
            resultRef,
            valuationRef,
            valuationId,
            eligible,
            blockingReason);
    }

    public static string ResultBlobPath(
        string artifactStoreRoot,
        string resultRef)
    {
        ContractValidator.RequireArtifactRef(resultRef, nameof(resultRef));
        string hex = resultRef[7..];
        return Path.Combine(
            Path.GetFullPath(artifactStoreRoot),
            "inputs",
            "topology-counterfactuals",
            "sha256",
            hex[..2],
            hex + ".json");
    }

    private static void ValidatePatchBinding(
        string candidateRef,
        StructureEditCandidate candidate,
        StructureEditGraphPatch patch)
    {
        StructureEditCandidateContent source = candidate.CandidateContent;
        StructureEditGraphPatchContent target = patch.PatchContent;
        if (!StringComparer.Ordinal.Equals(target.CandidateRef, candidateRef) ||
            !StringComparer.Ordinal.Equals(target.CandidateId, candidate.CandidateId) ||
            !StringComparer.Ordinal.Equals(
                target.TruthReleaseDigest,
                source.TruthReleaseDigest) ||
            !StringComparer.Ordinal.Equals(
                target.TopologyAtlasDigest,
                source.TopologyAtlasDigest) ||
            !StringComparer.Ordinal.Equals(
                target.TopologyAtlasEvidenceDigest,
                source.TopologyAtlasEvidenceDigest))
        {
            throw new InvalidDataException(
                "Graph patch is bound to different candidate or release coordinates.");
        }
        if (!StringComparer.Ordinal.Equals(
                source.CounterfactualEligibility,
                StructureEditCounterfactualEligibility.GraphPatchRequired))
        {
            throw new InvalidDataException(
                "Candidate is not eligible for graph-patch counterfactual evaluation.");
        }
        string expected = source.SuggestedPatchShape;
        IReadOnlySet<string> kinds = target.Operations
            .Select(operation => operation.Kind)
            .ToHashSet(StringComparer.Ordinal);
        bool shapeMatches = expected switch
        {
            StructureEditPatchShapes.AddNode =>
                kinds.Contains(StructureGraphPatchOperationKinds.AddNode),
            StructureEditPatchShapes.AddEdge =>
                kinds.Contains(StructureGraphPatchOperationKinds.AddEdge),
            StructureEditPatchShapes.RemoveEdge =>
                kinds.Contains(StructureGraphPatchOperationKinds.RemoveEdge),
            StructureEditPatchShapes.Mixed => kinds.Count >= 2,
            _ => false
        };
        if (!shapeMatches)
        {
            throw new InvalidDataException(
                $"Graph patch does not satisfy suggested patch shape '{expected}'.");
        }
    }

    private static void ValidatePatchClosure(
        StructureEditCandidate candidate,
        StructureEditGraphPatchContent patch)
    {
        var releaseToStable = candidate.CandidateContent.NodeIdentities
            .ToDictionary(
                identity => identity.ReleaseNodeId,
                identity => identity.StableNodeId,
                StringComparer.Ordinal);
        var stableToRelease = candidate.CandidateContent.NodeIdentities
            .ToDictionary(
                identity => identity.StableNodeId,
                identity => identity.ReleaseNodeId,
                StringComparer.Ordinal);
        var addedReleaseToStable = new Dictionary<string, string>(
            StringComparer.Ordinal);
        var addedStableToRelease = new Dictionary<string, string>(
            StringComparer.Ordinal);
        foreach (StructureEditGraphPatchOperation operation in patch.Operations)
        {
            if (operation.Kind != StructureGraphPatchOperationKinds.AddNode)
            {
                continue;
            }
            string nodeId = operation.NodeId!;
            string stableId = operation.StableNodeId!;
            if (releaseToStable.ContainsKey(nodeId) ||
                stableToRelease.ContainsKey(stableId) ||
                !addedReleaseToStable.TryAdd(nodeId, stableId) ||
                !addedStableToRelease.TryAdd(stableId, nodeId))
            {
                throw new InvalidDataException(
                    "add-node operation collides with an existing or added identity.");
            }
        }

        var removable = candidate.CandidateContent.SelectedEdges
            .Select(edge => StableEdgeKey(
                edge.StableDependencyId,
                edge.StableDependentId))
            .ToHashSet(StringComparer.Ordinal);
        var addedEdges = new HashSet<string>(StringComparer.Ordinal);
        foreach (StructureEditGraphPatchOperation operation in patch.Operations)
        {
            if (operation.Kind == StructureGraphPatchOperationKinds.AddNode)
            {
                continue;
            }
            RequireEndpoint(
                operation.DependencyId!,
                operation.StableDependencyId!,
                releaseToStable,
                stableToRelease,
                addedReleaseToStable,
                addedStableToRelease);
            RequireEndpoint(
                operation.DependentId!,
                operation.StableDependentId!,
                releaseToStable,
                stableToRelease,
                addedReleaseToStable,
                addedStableToRelease);
            string key = StableEdgeKey(
                operation.StableDependencyId!,
                operation.StableDependentId!);
            if (operation.Kind == StructureGraphPatchOperationKinds.RemoveEdge)
            {
                if (!removable.Contains(key))
                {
                    throw new InvalidDataException(
                        "remove-edge operation is outside the candidate's selected certified edges.");
                }
            }
            else if (!addedEdges.Add(key))
            {
                throw new InvalidDataException(
                    "Graph patch repeats an add-edge operation.");
            }
        }
    }

    private static void RequireEndpoint(
        string releaseId,
        string stableId,
        IReadOnlyDictionary<string, string> releaseToStable,
        IReadOnlyDictionary<string, string> stableToRelease,
        IReadOnlyDictionary<string, string> addedReleaseToStable,
        IReadOnlyDictionary<string, string> addedStableToRelease)
    {
        bool existing = releaseToStable.TryGetValue(
                releaseId,
                out string? expectedStable) &&
            stableToRelease.TryGetValue(
                stableId,
                out string? expectedRelease) &&
            StringComparer.Ordinal.Equals(expectedStable, stableId) &&
            StringComparer.Ordinal.Equals(expectedRelease, releaseId);
        bool added = addedReleaseToStable.TryGetValue(
                releaseId,
                out string? addedStable) &&
            addedStableToRelease.TryGetValue(
                stableId,
                out string? addedRelease) &&
            StringComparer.Ordinal.Equals(addedStable, stableId) &&
            StringComparer.Ordinal.Equals(addedRelease, releaseId);
        if (!existing && !added)
        {
            throw new InvalidDataException(
                $"Graph patch endpoint '{releaseId}' / '{stableId}' is outside candidate closure.");
        }
    }

    private static void ValidatePublicationBinding(
        string patchRef,
        StructureEditGraphPatch patch,
        StructureEditCandidate candidate,
        TopologyCounterfactualPublication publication)
    {
        TopologyCounterfactualPublicationContent value =
            publication.PublicationContent;
        if (!StringComparer.Ordinal.Equals(value.PatchRef, patchRef) ||
            !StringComparer.Ordinal.Equals(value.PatchId, patch.PatchId) ||
            !StringComparer.Ordinal.Equals(
                value.CandidateRef,
                patch.PatchContent.CandidateRef) ||
            !StringComparer.Ordinal.Equals(
                value.CandidateId,
                candidate.CandidateId) ||
            !StringComparer.Ordinal.Equals(
                value.TruthReleaseDigest,
                patch.PatchContent.TruthReleaseDigest) ||
            !StringComparer.Ordinal.Equals(
                value.TopologyAtlasDigest,
                patch.PatchContent.TopologyAtlasDigest) ||
            !StringComparer.Ordinal.Equals(
                value.TopologyAtlasEvidenceDigest,
                patch.PatchContent.TopologyAtlasEvidenceDigest))
        {
            throw new InvalidDataException(
                "Counterfactual publication is bound to different patch or release coordinates.");
        }
    }

    private static void ValidateRawCounterfactual(
        ReadOnlySpan<byte> bytes,
        TopologyCounterfactualPublicationContent publication)
    {
        StrictJson.Preflight(bytes);
        try
        {
            using JsonDocument document = JsonDocument.Parse(bytes);
            JsonElement root = document.RootElement;
            if (root.ValueKind != JsonValueKind.Object)
            {
                throw new InvalidDataException(
                    "Topology counterfactual result must be a JSON object.");
            }
            string schema = RequiredString(root, "schema_version");
            if (!StringComparer.Ordinal.Equals(
                    schema,
                    "topology-counterfactual.v1"))
            {
                throw new InvalidDataException(
                    "Topology counterfactual result uses an unsupported schema.");
            }
            string release = RequiredString(root, "truth_release_digest");
            if (!StringComparer.Ordinal.Equals(
                    release,
                    publication.TruthReleaseDigest))
            {
                throw new InvalidDataException(
                    "Topology counterfactual result uses a different truth release.");
            }
            bool accepted = RequiredBoolean(root, "accepted");
            bool cycleRisk = RequiredBoolean(root, "cycle_risk");
            bool analysisAvailable = root.TryGetProperty(
                    "analysis",
                    out JsonElement analysis) &&
                analysis.ValueKind != JsonValueKind.Null;
            if (accepted != publication.Projection.Accepted ||
                cycleRisk != publication.Projection.CycleRisk ||
                analysisAvailable != publication.Projection.AnalysisAvailable)
            {
                throw new InvalidDataException(
                    "Counterfactual projection disagrees with exact result status.");
            }
            ValidateOptionalReference(
                root,
                "patch_id",
                publication.PatchId);
            ValidateOptionalReference(
                root,
                "candidate_id",
                publication.CandidateId);
            ValidateOptionalReference(
                root,
                "topology_atlas_digest",
                publication.TopologyAtlasDigest);
        }
        catch (JsonException exception)
        {
            throw new InvalidDataException(
                "Topology counterfactual result is malformed JSON.",
                exception);
        }
    }

    private static string RequiredString(JsonElement parent, string name)
    {
        if (!parent.TryGetProperty(name, out JsonElement value) ||
            value.ValueKind != JsonValueKind.String ||
            string.IsNullOrWhiteSpace(value.GetString()))
        {
            throw new InvalidDataException(
                $"Topology counterfactual result is missing string field '{name}'.");
        }
        return value.GetString()!;
    }

    private static bool RequiredBoolean(JsonElement parent, string name)
    {
        if (!parent.TryGetProperty(name, out JsonElement value) ||
            value.ValueKind is not (JsonValueKind.True or JsonValueKind.False))
        {
            throw new InvalidDataException(
                $"Topology counterfactual result is missing boolean field '{name}'.");
        }
        return value.GetBoolean();
    }

    private static void ValidateOptionalReference(
        JsonElement parent,
        string name,
        string expected)
    {
        if (!parent.TryGetProperty(name, out JsonElement value))
        {
            return;
        }
        if (value.ValueKind != JsonValueKind.String ||
            !StringComparer.Ordinal.Equals(value.GetString(), expected))
        {
            throw new InvalidDataException(
                $"Topology counterfactual result field '{name}' disagrees with publication coordinates.");
        }
    }

    private static StructureEditGraphPatchReceipt PatchReceipt(
        string patchRef,
        StructureEditGraphPatch patch) =>
        new(
            StructureCounterfactualSchemas.PatchReceipt,
            patchRef,
            patch.PatchId,
            patch.PatchContent.CandidateRef,
            patch.PatchContent.CandidateId,
            patch.PatchContent.TruthReleaseDigest,
            patch.PatchContent.TopologyAtlasDigest,
            patch.PatchContent.TopologyAtlasEvidenceDigest,
            patch.PatchContent.Operations.Count,
            StructureCounterfactualSchemas.Authority);

    private static string StableEdgeKey(
        string dependency,
        string dependent) =>
        dependency + "\u0000" + dependent;

    private static string Digest(ReadOnlySpan<byte> bytes) =>
        "sha256:" + Convert.ToHexStringLower(SHA256.HashData(bytes));

    private static void WriteBlob(
        string destination,
        byte[] bytes,
        string expectedRef)
    {
        if (File.Exists(destination))
        {
            byte[] existing = File.ReadAllBytes(destination);
            if (!existing.AsSpan().SequenceEqual(bytes) ||
                !StringComparer.Ordinal.Equals(Digest(existing), expectedRef))
            {
                throw new InvalidDataException(
                    $"Topology counterfactual content-address collision at {expectedRef}.");
            }
            return;
        }
        Directory.CreateDirectory(Path.GetDirectoryName(destination)!);
        string temporary = destination + ".tmp-" + Guid.NewGuid().ToString("N");
        File.WriteAllBytes(temporary, bytes);
        try
        {
            File.Move(temporary, destination, overwrite: false);
        }
        catch (IOException) when (File.Exists(destination))
        {
            byte[] existing = File.ReadAllBytes(destination);
            if (!existing.AsSpan().SequenceEqual(bytes))
            {
                throw;
            }
            File.Delete(temporary);
        }
        finally
        {
            if (File.Exists(temporary))
            {
                File.Delete(temporary);
            }
        }
    }
}
