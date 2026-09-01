using System.Numerics;

namespace Trureturing.Intuition.Core;

public static class StructureEditSettlementRegistrar
{
    public static StructureEditSettlementRegistration Register(
        ArtifactStore store,
        string counterfactualValuationRef,
        StructureFormalizationResultPublicationCoordinate formalizationPublication,
        ReadOnlySpan<byte> formalizationResultBytes,
        TopologyAtlasDeltaPublicationCoordinate deltaPublication,
        ReadOnlySpan<byte> atlasDeltaBytes)
    {
        ArgumentNullException.ThrowIfNull(store);
        ContractValidator.RequireArtifactRef(
            counterfactualValuationRef,
            nameof(counterfactualValuationRef));
        ArgumentNullException.ThrowIfNull(formalizationPublication);
        ArgumentNullException.ThrowIfNull(deltaPublication);
        ContractValidator.Validate(formalizationPublication);
        ContractValidator.Validate(deltaPublication);

        StructureCounterfactualValuation valuation =
            StructureCounterfactualValuator.ReadValuation(
                store,
                counterfactualValuationRef);
        StructureCounterfactualValuationContent valuationContent =
            valuation.ValuationContent;
        StructureEditCandidate candidate =
            StructureEditCandidateRegistrar.ReadCandidate(
                store,
                valuationContent.CandidateRef);
        StructureEditCandidateContent candidateContent =
            candidate.CandidateContent;
        RequireEqual(
            candidate.CandidateId,
            valuationContent.CandidateId,
            "valuation candidate_id");
        RequireEqual(
            candidateContent.EpisodeRef,
            valuationContent.EpisodeRef,
            "valuation episode_ref");
        RequireEqual(
            candidateContent.EpisodeId,
            valuationContent.EpisodeId,
            "valuation episode_id");

        string formalizationResultRef = CanonicalJson.Sha256Reference(
            formalizationResultBytes);
        RequireEqual(
            formalizationResultRef,
            formalizationPublication.ResultDigest,
            "formalization result_digest");
        StructureFormalizationResult formalization =
            CanonicalJson.DeserializeCanonical<StructureFormalizationResult>(
                formalizationResultBytes);
        ContractValidator.Validate(formalization);
        ValidateFormalizationBinding(
            store,
            candidate,
            formalization,
            formalizationPublication);
        PutExactArtifact(
            store.PathFor(formalizationResultRef),
            formalizationResultBytes,
            formalizationResultRef);
        _ = PutCanonical(store, formalizationPublication);

        string deltaRef = CanonicalJson.Sha256Reference(atlasDeltaBytes);
        RequireEqual(
            deltaRef,
            deltaPublication.DeltaDigest,
            "Atlas delta_digest");
        ValidateDeltaPublicationBinding(
            candidateContent,
            valuationContent,
            deltaPublication);
        TopologyAtlasDeltaReadModel delta = TopologyAtlasDeltaReader.Read(
            atlasDeltaBytes,
            new TopologyAtlasDeltaBinding(
                deltaPublication.FromTruthReleaseDigest,
                deltaPublication.ToTruthReleaseDigest,
                deltaPublication.FromTopologyAtlasDigest,
                deltaPublication.ToTopologyAtlasDigest,
                deltaPublication.FromEvidenceDigest,
                deltaPublication.ToEvidenceDigest,
                deltaPublication.AlgorithmProfileDigest,
                deltaPublication.ProducerCommit));
        PutExactBlob(
            AtlasDeltaBlobPath(StoreRoot(store), deltaRef),
            atlasDeltaBytes,
            deltaRef);
        _ = PutCanonical(store, deltaPublication);

        StructureEditOperationSettlement[] operationSettlements =
            SettleOperations(candidateContent.GraphPatch, delta);
        var counts = new StructureEditSettlementCounts(
            new BigInteger(operationSettlements.Length),
            new BigInteger(operationSettlements.Count(
                value => value.Outcome == "realized")),
            new BigInteger(operationSettlements.Count(
                value => value.Outcome == "not-realized")),
            new BigInteger(operationSettlements.Count(
                value => value.Outcome == "already-present")),
            new BigInteger(operationSettlements.Count(
                value => value.Outcome == "contradicted")));
        string status = ContractValidator.ClassifyStructureSettlement(
            formalization.ResultContent.Outcome,
            valuationContent.Classification,
            counts);
        string calibration = ContractValidator.ClassifyStructureCalibration(
            status,
            valuationContent.Classification,
            counts,
            valuationContent.BenefitVector);
        var realizedSummary = new StructureEditRealizedDeltaSummary(
            delta.Summary.NodesAdded,
            delta.Summary.NodesRetired,
            delta.Summary.EdgesAdded,
            delta.Summary.EdgesRemoved,
            delta.Summary.ClusterSplits,
            delta.Summary.ClusterMerges,
            delta.Summary.ClusterReorganizations,
            new BigInteger(delta.FrontierDelta.EnteredFrontier.Count),
            new BigInteger(delta.FrontierDelta.LeftFrontier.Count));
        var content = new StructureEditSettlementContent(
            valuationContent.CandidateRef,
            valuationContent.CandidateId,
            valuationContent.EpisodeRef,
            valuationContent.EpisodeId,
            counterfactualValuationRef,
            valuation.ValuationId,
            valuationContent.CounterfactualRef,
            formalizationResultRef,
            formalization.ResultId,
            formalization.ResultContent.FormalizationRequestRef,
            deltaRef,
            deltaRef,
            delta.Binding.FromTruthReleaseDigest,
            delta.Binding.ToTruthReleaseDigest,
            delta.Binding.FromTopologyAtlasDigest,
            delta.Binding.ToTopologyAtlasDigest,
            delta.Binding.FromEvidenceDigest,
            delta.Binding.ToEvidenceDigest,
            formalization.ResultContent.Outcome,
            valuationContent.Classification,
            operationSettlements,
            counts,
            valuationContent.BenefitVector,
            valuationContent.RiskVector,
            realizedSummary,
            status,
            calibration,
            "independent-structure-settlement");
        string settlementId = CanonicalJson.Sha256Reference(
            CanonicalJson.Serialize(content));
        var settlement = new StructureEditSettlement(
            StructureEditSettlementSchemas.Settlement,
            settlementId,
            content);
        ContractValidator.Validate(settlement);
        string settlementRef = PutCanonical(store, settlement);
        var receipt = new StructureEditSettlementReceipt(
            StructureEditSettlementSchemas.Receipt,
            settlementRef,
            settlementId,
            valuationContent.CandidateRef,
            valuationContent.CandidateId,
            counterfactualValuationRef,
            formalizationResultRef,
            deltaRef,
            delta.Binding.FromTruthReleaseDigest,
            delta.Binding.ToTruthReleaseDigest,
            status,
            calibration,
            "independent-structure-settlement");
        ContractValidator.Validate(receipt);
        string receiptRef = PutCanonical(store, receipt);
        return new StructureEditSettlementRegistration(
            formalizationResultRef,
            deltaRef,
            settlementRef,
            receiptRef,
            settlementId,
            valuationContent.CandidateRef,
            delta.Binding.FromTruthReleaseDigest,
            delta.Binding.ToTruthReleaseDigest,
            status,
            calibration,
            counts);
    }

    public static StructureEditSettlement ReadSettlement(
        ArtifactStore store,
        string settlementRef)
    {
        byte[] bytes = ReadVerified(store, settlementRef);
        StructureEditSettlement value =
            CanonicalJson.DeserializeCanonical<StructureEditSettlement>(bytes);
        ContractValidator.Validate(value);
        return value;
    }

    public static string AtlasDeltaBlobPath(
        string storeRoot,
        string deltaRef)
    {
        ContractValidator.RequireArtifactRef(deltaRef, nameof(deltaRef));
        string hex = deltaRef[7..];
        return Path.Combine(
            Path.GetFullPath(storeRoot),
            "topology-atlas-delta",
            "sha256",
            hex[..2],
            hex + ".json");
    }

    private static void ValidateFormalizationBinding(
        ArtifactStore store,
        StructureEditCandidate candidate,
        StructureFormalizationResult result,
        StructureFormalizationResultPublicationCoordinate publication)
    {
        StructureEditCandidateContent candidateContent =
            candidate.CandidateContent;
        StructureFormalizationResultContent content = result.ResultContent;
        RequireEqual(
            candidate.CandidateId,
            publication.CandidateId,
            "formalization publication candidate_id");
        RequireEqual(
            candidate.CandidateId,
            content.CandidateId,
            "formalization result candidate_id");
        RequireEqual(
            publication.CandidateRef,
            content.CandidateRef,
            "formalization candidate_ref");
        RequireEqual(
            publication.CandidateRef,
            CandidateReference(store, candidate),
            "formalization publication candidate_ref");
        RequireEqual(
            publication.FormalizationRequestRef,
            content.FormalizationRequestRef,
            "formalization_request_ref");
        RequireEqual(
            publication.TruthReleaseDigest,
            content.TruthReleaseDigest,
            "formalization truth_release_digest");
        RequireEqual(
            candidateContent.TruthReleaseDigest,
            content.TruthReleaseDigest,
            "candidate formalization truth_release_digest");
        RequireEqual(
            publication.TopologyAtlasDigest,
            content.TopologyAtlasDigest,
            "formalization topology_atlas_digest");
        RequireEqual(
            candidateContent.TopologyAtlasDigest,
            content.TopologyAtlasDigest,
            "candidate formalization topology_atlas_digest");
        RequireArtifactExists(
            store,
            content.FormalizationRequestRef,
            "formalization request");
        if (content.VerificationReceiptRef is not null)
        {
            RequireArtifactExists(
                store,
                content.VerificationReceiptRef,
                "verification receipt");
        }
        if (content.FormalArtifactRef is not null)
        {
            RequireArtifactExists(
                store,
                content.FormalArtifactRef,
                "formal artifact");
        }
        foreach (string reference in content.DiagnosticArtifactRefs)
        {
            RequireArtifactExists(store, reference, "diagnostic artifact");
        }
    }

    private static void ValidateDeltaPublicationBinding(
        StructureEditCandidateContent candidate,
        StructureCounterfactualValuationContent valuation,
        TopologyAtlasDeltaPublicationCoordinate delta)
    {
        RequireEqual(
            candidate.TruthReleaseDigest,
            delta.FromTruthReleaseDigest,
            "delta from_truth_release_digest");
        RequireEqual(
            candidate.TopologyAtlasDigest,
            delta.FromTopologyAtlasDigest,
            "delta from_topology_atlas_digest");
        RequireEqual(
            candidate.TopologyAtlasEvidenceDigest,
            delta.FromEvidenceDigest,
            "delta from_evidence_digest");
        RequireEqual(
            valuation.TruthReleaseDigest,
            delta.FromTruthReleaseDigest,
            "valuation delta truth release");
        RequireEqual(
            valuation.TopologyAtlasDigest,
            delta.FromTopologyAtlasDigest,
            "valuation delta Topology Atlas");
        RequireEqual(
            valuation.TopologyAtlasEvidenceDigest,
            delta.FromEvidenceDigest,
            "valuation delta Atlas evidence");
    }

    private static StructureEditOperationSettlement[] SettleOperations(
        IReadOnlyList<StructureGraphPatchOperation> operations,
        TopologyAtlasDeltaReadModel delta)
    {
        var nodes = delta.NodeTransitions.ToDictionary(
            value => value.StableNodeId,
            StringComparer.Ordinal);
        var edges = delta.EdgeTransitions.ToDictionary(
            value => (value.StableDependencyId, value.StableDependentId));
        return operations.Select((operation, index) =>
        {
            if (operation.Operation == StructureGraphPatchOperations.AddNode)
            {
                nodes.TryGetValue(
                    operation.StableNodeId!,
                    out TopologyAtlasDeltaNodeTransitionReadModel? transition);
                string? observed = transition?.Relation;
                return new StructureEditOperationSettlement(
                    index + 1,
                    operation.Operation,
                    operation.StableNodeId!,
                    null,
                    "added",
                    observed,
                    NodeOutcome(observed),
                    transition?.FromNodeId,
                    transition?.ToNodeId,
                    null,
                    null);
            }

            edges.TryGetValue(
                (operation.StableDependencyId!, operation.StableDependentId!),
                out TopologyAtlasDeltaEdgeTransitionReadModel? edge);
            string expected = operation.Operation ==
                StructureGraphPatchOperations.AddEdge
                    ? "added"
                    : "removed";
            string? observedEdge = edge?.Relation;
            return new StructureEditOperationSettlement(
                index + 1,
                operation.Operation,
                operation.StableDependencyId!,
                operation.StableDependentId!,
                expected,
                observedEdge,
                EdgeOutcome(operation.Operation, observedEdge),
                edge?.FromDependencyId,
                edge?.ToDependencyId,
                edge?.FromDependentId,
                edge?.ToDependentId);
        }).ToArray();
    }

    private static string NodeOutcome(string? relation) => relation switch
    {
        "added" => "realized",
        "retained" => "already-present",
        "retired" => "contradicted",
        _ => "not-realized"
    };

    private static string EdgeOutcome(string operation, string? relation)
    {
        if (operation == StructureGraphPatchOperations.AddEdge)
        {
            return relation switch
            {
                "added" => "realized",
                "retained" => "already-present",
                "removed" => "contradicted",
                _ => "not-realized"
            };
        }
        return relation switch
        {
            "removed" => "realized",
            "added" => "contradicted",
            "retained" => "not-realized",
            null => "already-present",
            _ => "not-realized"
        };
    }

    private static string CandidateReference(
        ArtifactStore store,
        StructureEditCandidate candidate)
    {
        byte[] bytes = CanonicalJson.Serialize(candidate);
        string reference = CanonicalJson.Sha256Reference(bytes);
        if (!File.Exists(store.PathFor(reference)))
        {
            throw new InvalidDataException(
                "The formalization result candidate is absent from the artifact store.");
        }
        return reference;
    }

    private static void RequireArtifactExists(
        ArtifactStore store,
        string reference,
        string name)
    {
        ContractValidator.RequireArtifactRef(reference, name);
        string path = store.PathFor(reference);
        if (!File.Exists(path))
        {
            throw new InvalidDataException(
                $"The referenced {name} is absent from the artifact store.");
        }
        byte[] bytes = File.ReadAllBytes(path);
        if (!StringComparer.Ordinal.Equals(
                CanonicalJson.Sha256Reference(bytes),
                reference))
        {
            throw new InvalidDataException(
                $"The referenced {name} failed digest verification.");
        }
    }

    private static byte[] ReadVerified(
        ArtifactStore store,
        string reference)
    {
        ContractValidator.RequireArtifactRef(reference, nameof(reference));
        byte[] bytes = File.ReadAllBytes(store.PathFor(reference));
        if (!StringComparer.Ordinal.Equals(
                CanonicalJson.Sha256Reference(bytes),
                reference))
        {
            throw new InvalidDataException(
                $"Artifact {reference} failed digest verification.");
        }
        return bytes;
    }

    private static string PutCanonical<T>(ArtifactStore store, T value)
    {
        byte[] bytes = CanonicalJson.Serialize(value);
        string reference = CanonicalJson.Sha256Reference(bytes);
        PutExactArtifact(store.PathFor(reference), bytes, reference);
        return reference;
    }

    private static void PutExactArtifact(
        string path,
        ReadOnlySpan<byte> bytes,
        string expectedRef)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        if (File.Exists(path))
        {
            byte[] existing = File.ReadAllBytes(path);
            if (!existing.AsSpan().SequenceEqual(bytes) ||
                !StringComparer.Ordinal.Equals(
                    CanonicalJson.Sha256Reference(existing),
                    expectedRef))
            {
                throw new InvalidDataException(
                    $"Content-address collision at {expectedRef}.");
            }
            return;
        }
        string temporary = path + ".tmp-" + Guid.NewGuid().ToString("N");
        File.WriteAllBytes(temporary, bytes);
        try
        {
            File.Move(temporary, path, overwrite: false);
        }
        catch (IOException) when (File.Exists(path))
        {
            if (!File.ReadAllBytes(path).AsSpan().SequenceEqual(bytes)) throw;
            File.Delete(temporary);
        }
        finally
        {
            if (File.Exists(temporary)) File.Delete(temporary);
        }
    }

    private static void PutExactBlob(
        string path,
        ReadOnlySpan<byte> bytes,
        string expectedRef) =>
        PutExactArtifact(path, bytes, expectedRef);

    private static string StoreRoot(ArtifactStore store)
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

    private static void RequireEqual(
        string actual,
        string expected,
        string name)
    {
        if (!StringComparer.Ordinal.Equals(actual, expected))
        {
            throw new InvalidDataException(
                $"Structure settlement {name} does not match its bound evidence.");
        }
    }
}
