using System.Numerics;

namespace Trureturing.Intuition.Core;

public static class StructureCounterfactualValuator
{
    public static StructureCounterfactualValuationRegistration Register(
        ArtifactStore store,
        TopologyCounterfactualPublicationCoordinate publication,
        ReadOnlySpan<byte> counterfactualBytes)
    {
        ArgumentNullException.ThrowIfNull(store);
        ArgumentNullException.ThrowIfNull(publication);
        ContractValidator.Validate(publication);

        string counterfactualRef = CanonicalJson.Sha256Reference(
            counterfactualBytes);
        if (!StringComparer.Ordinal.Equals(
                counterfactualRef,
                publication.CounterfactualDigest))
        {
            throw new InvalidDataException(
                "Topology counterfactual bytes do not match counterfactual_digest.");
        }
        StructureEditCandidate candidate =
            StructureEditCandidateRegistrar.ReadCandidate(
                store,
                publication.CandidateRef);
        RequireEqual(
            candidate.CandidateId,
            publication.CandidateId,
            "candidate_id");
        StructureEditCandidateContent candidateContent =
            candidate.CandidateContent;
        RequireEqual(
            candidateContent.TruthReleaseDigest,
            publication.TruthReleaseDigest,
            "truth_release_digest");
        RequireEqual(
            candidateContent.TopologyAtlasDigest,
            publication.TopologyAtlasDigest,
            "topology_atlas_digest");
        RequireEqual(
            candidateContent.TopologyAtlasEvidenceDigest,
            publication.TopologyAtlasEvidenceDigest,
            "topology_atlas_evidence_digest");

        TopologyCounterfactualProjection projection =
            TopologyCounterfactualReader.Read(
                counterfactualBytes,
                new TopologyCounterfactualBinding(
                    publication.TruthReleaseDigest,
                    publication.TopologyAtlasDigest,
                    publication.TopologyAtlasEvidenceDigest,
                    publication.AlgorithmProfileDigest,
                    publication.ProducerCommit));
        if (projection.EditOperationCount > 0 &&
            projection.EditOperationCount !=
                new BigInteger(candidateContent.GraphPatch.Count))
        {
            throw new InvalidDataException(
                "Topology counterfactual edit operation count does not match the candidate graph patch.");
        }

        PutExactBlob(
            CounterfactualBlobPath(StoreRoot(store), counterfactualRef),
            counterfactualBytes,
            counterfactualRef);
        _ = PutCanonical(store, publication);

        var metrics = new StructureCounterfactualMetrics(
            projection.ReachabilityGain,
            projection.ReachabilityLoss,
            projection.PathCompression,
            projection.ShortestPathChangeCount,
            projection.NewCutBridgeCount,
            projection.RemovedCutBridgeCount,
            projection.NewInterfaceCount,
            projection.RemovedInterfaceCount,
            projection.CycleWitnessCount,
            new BigInteger(projection.AffectedStableNodeIds.Count),
            new BigInteger(projection.TouchedClusterIds.Count),
            new BigInteger(candidateContent.GraphPatch.Count));
        var benefit = new StructureCounterfactualBenefitVector(
            metrics.ReachabilityGain,
            metrics.PathCompression,
            metrics.RemovedCutBridgeCount,
            metrics.NewInterfaceCount,
            metrics.ShortestPathChangeCount);
        var risk = new StructureCounterfactualRiskVector(
            metrics.ReachabilityLoss,
            metrics.NewCutBridgeCount,
            metrics.RemovedInterfaceCount,
            metrics.AffectedStableNodeCount,
            metrics.TouchedClusterCount,
            metrics.EditOperationCount + metrics.AffectedStableNodeCount,
            projection.CycleRisk);
        string classification = ContractValidator.ClassifyCounterfactual(
            projection.Accepted,
            projection.CycleRisk,
            benefit,
            risk);
        var content = new StructureCounterfactualValuationContent(
            publication.CandidateRef,
            publication.CandidateId,
            candidateContent.EpisodeRef,
            candidateContent.EpisodeId,
            counterfactualRef,
            counterfactualRef,
            publication.TruthReleaseDigest,
            publication.TopologyAtlasDigest,
            publication.TopologyAtlasEvidenceDigest,
            publication.AlgorithmProfileDigest,
            publication.ProducerCommit,
            projection.Accepted,
            projection.CycleRisk,
            projection.AffectedStableNodeIds,
            projection.TouchedClusterIds,
            metrics,
            benefit,
            risk,
            classification,
            "exact-topology-counterfactual-only",
            "advisory");
        string valuationId = CanonicalJson.Sha256Reference(
            CanonicalJson.Serialize(content));
        var valuation = new StructureCounterfactualValuation(
            StructureCounterfactualSchemas.Valuation,
            valuationId,
            content);
        ContractValidator.Validate(valuation);
        string valuationRef = PutCanonical(store, valuation);
        var receipt = new StructureCounterfactualValuationReceipt(
            StructureCounterfactualSchemas.Receipt,
            valuationRef,
            valuationId,
            publication.CandidateRef,
            publication.CandidateId,
            candidateContent.EpisodeRef,
            counterfactualRef,
            counterfactualRef,
            publication.TruthReleaseDigest,
            publication.TopologyAtlasDigest,
            classification,
            "advisory");
        ContractValidator.Validate(receipt);
        string receiptRef = PutCanonical(store, receipt);
        return new StructureCounterfactualValuationRegistration(
            counterfactualRef,
            valuationRef,
            receiptRef,
            valuationId,
            publication.CandidateRef,
            classification,
            projection.Accepted,
            projection.CycleRisk,
            benefit,
            risk);
    }

    public static StructureCounterfactualValuation ReadValuation(
        ArtifactStore store,
        string valuationRef)
    {
        byte[] bytes = ReadVerified(store, valuationRef);
        StructureCounterfactualValuation value =
            CanonicalJson.DeserializeCanonical<
                StructureCounterfactualValuation>(bytes);
        ContractValidator.Validate(value);
        return value;
    }

    public static string CounterfactualBlobPath(
        string storeRoot,
        string counterfactualRef)
    {
        ContractValidator.RequireArtifactRef(
            counterfactualRef,
            nameof(counterfactualRef));
        string hex = counterfactualRef[7..];
        return Path.Combine(
            Path.GetFullPath(storeRoot),
            "topology-counterfactual",
            "sha256",
            hex[..2],
            hex + ".json");
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
        string path = store.PathFor(reference);
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        if (File.Exists(path))
        {
            if (!File.ReadAllBytes(path).AsSpan().SequenceEqual(bytes))
            {
                throw new InvalidDataException(
                    $"Content-address collision at {reference}.");
            }
            return reference;
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
        return reference;
    }

    private static void PutExactBlob(
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
                $"Topology counterfactual {name} does not match its candidate.");
        }
    }
}
