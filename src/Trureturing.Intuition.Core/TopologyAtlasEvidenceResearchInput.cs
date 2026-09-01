using System.Security.Cryptography;

namespace Trureturing.Intuition.Core;

public static class TopologyAtlasEvidenceResearchInputSchemas
{
    public const string Publication =
        "trureturing.topology-atlas-evidence-publication.v1";
    public const string Receipt =
        "intuition-topology-atlas-evidence-input-receipt.v1";
    public const string Cursor =
        "intuition-topology-atlas-evidence-input-cursor.v1";
}

public sealed record TopologyAtlasEvidencePublicationCoordinate(
    string Schema,
    string TopologyAtlasInputReceiptRef,
    string TruthReleaseDigest,
    string CertifiedTopologyDigest,
    string TopologyAtlasDigest,
    string TopologyAtlasEvidenceDigest,
    string SourceCommit,
    string SourceTree,
    string CertifiedAlgorithmProfileDigest,
    string AtlasAlgorithmProfileDigest,
    string EvidenceAlgorithmProfileDigest,
    string ProducerCommit);

public sealed record IntuitionTopologyAtlasEvidenceInputReceipt(
    string Schema,
    string PublicationRef,
    string EvidenceRef,
    string TopologyAtlasInputReceiptRef,
    string TruthReleaseDigest,
    string CertifiedTopologyDigest,
    string TopologyAtlasDigest,
    string TopologyAtlasEvidenceDigest,
    string SourceCommit,
    string SourceTree,
    string CertifiedAlgorithmProfileDigest,
    string AtlasAlgorithmProfileDigest,
    string EvidenceAlgorithmProfileDigest,
    string ProducerCommit);

public sealed record IntuitionTopologyAtlasEvidenceInputCursor(
    string Schema,
    string ReceiptRef,
    string TopologyAtlasInputReceiptRef,
    string TruthReleaseDigest,
    string CertifiedTopologyDigest,
    string TopologyAtlasDigest,
    string TopologyAtlasEvidenceDigest,
    string SourceCommit,
    string SourceTree,
    string EvidenceAlgorithmProfileDigest,
    string ProducerCommit);

public sealed record TopologyAtlasEvidenceResearchInputRegistration(
    string PublicationRef,
    string EvidenceRef,
    string ReceiptRef,
    string TopologyAtlasInputReceiptRef,
    string CursorPath,
    bool Replayed,
    int StableNodeCount,
    int TraitRecordCount,
    int ClusterInterfaceCount,
    int AffinityWitnessCount);

public static class TopologyAtlasEvidenceResearchInputRegistrar
{
    public static TopologyAtlasEvidenceResearchInputRegistration Register(
        string durableRoot,
        TopologyAtlasEvidencePublicationCoordinate publication,
        ReadOnlySpan<byte> evidenceBytes,
        string cursorPath)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(durableRoot);
        ArgumentException.ThrowIfNullOrWhiteSpace(cursorPath);
        ArgumentNullException.ThrowIfNull(publication);
        ContractValidator.Validate(publication);

        string root = Path.GetFullPath(durableRoot);
        var store = new ArtifactStore(root);
        IntuitionTopologyAtlasInputReceipt atlasReceipt =
            store.Get<IntuitionTopologyAtlasInputReceipt>(
                publication.TopologyAtlasInputReceiptRef);
        ValidateAtlasReceipt(publication, atlasReceipt);

        string atlasPath = TopologyAtlasResearchInputRegistrar.AtlasBlobPath(
            root,
            atlasReceipt.AtlasRef);
        byte[] atlasBytes = File.ReadAllBytes(atlasPath);
        if (!StringComparer.Ordinal.Equals(
                Sha256Reference(atlasBytes),
                atlasReceipt.AtlasRef))
        {
            throw new InvalidDataException(
                "The registered Topology Atlas blob failed digest verification.");
        }
        var atlasBinding = new TopologyAtlasBinding(
            atlasReceipt.TruthReleaseDigest,
            atlasReceipt.CertifiedTopologyDigest,
            atlasReceipt.CertifiedAlgorithmProfileDigest,
            atlasReceipt.AtlasAlgorithmProfileDigest,
            atlasReceipt.ProducerCommit);
        TopologyAtlasReadModel atlas = TopologyAtlasReader.Read(
            atlasBytes,
            atlasBinding);

        string evidenceRef = Sha256Reference(evidenceBytes);
        if (!StringComparer.Ordinal.Equals(
                evidenceRef,
                publication.TopologyAtlasEvidenceDigest))
        {
            throw new InvalidDataException(
                "Topology Atlas evidence bytes do not match the publication digest.");
        }
        var evidenceBinding = new TopologyAtlasEvidenceBinding(
            publication.TruthReleaseDigest,
            publication.CertifiedTopologyDigest,
            publication.TopologyAtlasDigest,
            publication.EvidenceAlgorithmProfileDigest,
            publication.ProducerCommit);
        TopologyAtlasEvidenceReadModel evidence =
            TopologyAtlasEvidenceReader.Read(
                evidenceBytes,
                evidenceBinding,
                atlas);

        string publicationRef = store.Put(publication);
        PersistEvidenceBlob(root, evidenceRef, evidenceBytes);
        string fullCursorPath = Path.GetFullPath(cursorPath);

        if (File.Exists(fullCursorPath))
        {
            IntuitionTopologyAtlasEvidenceInputCursor current =
                CanonicalJson.DeserializeCanonical<
                    IntuitionTopologyAtlasEvidenceInputCursor>(
                        File.ReadAllBytes(fullCursorPath));
            ContractValidator.Validate(current);
            if (StringComparer.Ordinal.Equals(
                    current.TruthReleaseDigest,
                    publication.TruthReleaseDigest))
            {
                if (!StringComparer.Ordinal.Equals(
                        current.TopologyAtlasInputReceiptRef,
                        publication.TopologyAtlasInputReceiptRef) ||
                    !StringComparer.Ordinal.Equals(
                        current.CertifiedTopologyDigest,
                        publication.CertifiedTopologyDigest) ||
                    !StringComparer.Ordinal.Equals(
                        current.TopologyAtlasDigest,
                        publication.TopologyAtlasDigest) ||
                    !StringComparer.Ordinal.Equals(
                        current.TopologyAtlasEvidenceDigest,
                        evidenceRef) ||
                    !StringComparer.Ordinal.Equals(
                        current.EvidenceAlgorithmProfileDigest,
                        publication.EvidenceAlgorithmProfileDigest) ||
                    !StringComparer.Ordinal.Equals(
                        current.ProducerCommit,
                        publication.ProducerCommit) ||
                    !StringComparer.Ordinal.Equals(
                        current.SourceCommit,
                        publication.SourceCommit) ||
                    !StringComparer.Ordinal.Equals(
                        current.SourceTree,
                        publication.SourceTree))
                {
                    throw new InvalidDataException(
                        "An existing truth release cannot be rebound to different Topology Atlas evidence coordinates.");
                }

                IntuitionTopologyAtlasEvidenceInputReceipt existing =
                    store.Get<IntuitionTopologyAtlasEvidenceInputReceipt>(
                        current.ReceiptRef);
                ValidateReceiptAgainstPublication(
                    existing,
                    publication,
                    publicationRef,
                    evidenceRef);
                return Registration(
                    existing.PublicationRef,
                    existing.EvidenceRef,
                    current.ReceiptRef,
                    publication,
                    fullCursorPath,
                    true,
                    evidence);
            }
        }

        var receipt = new IntuitionTopologyAtlasEvidenceInputReceipt(
            TopologyAtlasEvidenceResearchInputSchemas.Receipt,
            publicationRef,
            evidenceRef,
            publication.TopologyAtlasInputReceiptRef,
            publication.TruthReleaseDigest,
            publication.CertifiedTopologyDigest,
            publication.TopologyAtlasDigest,
            evidenceRef,
            publication.SourceCommit,
            publication.SourceTree,
            publication.CertifiedAlgorithmProfileDigest,
            publication.AtlasAlgorithmProfileDigest,
            publication.EvidenceAlgorithmProfileDigest,
            publication.ProducerCommit);
        ContractValidator.Validate(receipt);
        string receiptRef = store.Put(receipt);

        var cursor = new IntuitionTopologyAtlasEvidenceInputCursor(
            TopologyAtlasEvidenceResearchInputSchemas.Cursor,
            receiptRef,
            publication.TopologyAtlasInputReceiptRef,
            publication.TruthReleaseDigest,
            publication.CertifiedTopologyDigest,
            publication.TopologyAtlasDigest,
            evidenceRef,
            publication.SourceCommit,
            publication.SourceTree,
            publication.EvidenceAlgorithmProfileDigest,
            publication.ProducerCommit);
        ContractValidator.Validate(cursor);
        WriteAtomic(fullCursorPath, CanonicalJson.Serialize(cursor));

        return Registration(
            publicationRef,
            evidenceRef,
            receiptRef,
            publication,
            fullCursorPath,
            false,
            evidence);
    }

    public static string EvidenceBlobPath(
        string durableRoot,
        string evidenceRef)
    {
        ContractValidator.RequireArtifactRef(evidenceRef, nameof(evidenceRef));
        string hex = evidenceRef["sha256:".Length..];
        return Path.Combine(
            Path.GetFullPath(durableRoot),
            "blobs",
            "sha256",
            hex[..2],
            hex + ".json");
    }

    private static void ValidateAtlasReceipt(
        TopologyAtlasEvidencePublicationCoordinate publication,
        IntuitionTopologyAtlasInputReceipt receipt)
    {
        ContractValidator.Validate(receipt);
        if (!StringComparer.Ordinal.Equals(
                receipt.TruthReleaseDigest,
                publication.TruthReleaseDigest) ||
            !StringComparer.Ordinal.Equals(
                receipt.CertifiedTopologyDigest,
                publication.CertifiedTopologyDigest) ||
            !StringComparer.Ordinal.Equals(
                receipt.TopologyAtlasDigest,
                publication.TopologyAtlasDigest) ||
            !StringComparer.Ordinal.Equals(
                receipt.CertifiedAlgorithmProfileDigest,
                publication.CertifiedAlgorithmProfileDigest) ||
            !StringComparer.Ordinal.Equals(
                receipt.AtlasAlgorithmProfileDigest,
                publication.AtlasAlgorithmProfileDigest) ||
            !StringComparer.Ordinal.Equals(
                receipt.ProducerCommit,
                publication.ProducerCommit) ||
            !StringComparer.Ordinal.Equals(
                receipt.SourceCommit,
                publication.SourceCommit) ||
            !StringComparer.Ordinal.Equals(
                receipt.SourceTree,
                publication.SourceTree))
        {
            throw new InvalidDataException(
                "Topology Atlas evidence publication does not match its registered Atlas receipt.");
        }
    }

    private static void ValidateReceiptAgainstPublication(
        IntuitionTopologyAtlasEvidenceInputReceipt receipt,
        TopologyAtlasEvidencePublicationCoordinate publication,
        string publicationRef,
        string evidenceRef)
    {
        ContractValidator.Validate(receipt);
        if (!StringComparer.Ordinal.Equals(receipt.PublicationRef, publicationRef) ||
            !StringComparer.Ordinal.Equals(receipt.EvidenceRef, evidenceRef) ||
            !StringComparer.Ordinal.Equals(
                receipt.TopologyAtlasInputReceiptRef,
                publication.TopologyAtlasInputReceiptRef) ||
            !StringComparer.Ordinal.Equals(
                receipt.TruthReleaseDigest,
                publication.TruthReleaseDigest) ||
            !StringComparer.Ordinal.Equals(
                receipt.CertifiedTopologyDigest,
                publication.CertifiedTopologyDigest) ||
            !StringComparer.Ordinal.Equals(
                receipt.TopologyAtlasDigest,
                publication.TopologyAtlasDigest) ||
            !StringComparer.Ordinal.Equals(
                receipt.TopologyAtlasEvidenceDigest,
                evidenceRef) ||
            !StringComparer.Ordinal.Equals(
                receipt.EvidenceAlgorithmProfileDigest,
                publication.EvidenceAlgorithmProfileDigest) ||
            !StringComparer.Ordinal.Equals(
                receipt.ProducerCommit,
                publication.ProducerCommit) ||
            !StringComparer.Ordinal.Equals(
                receipt.SourceCommit,
                publication.SourceCommit) ||
            !StringComparer.Ordinal.Equals(
                receipt.SourceTree,
                publication.SourceTree))
        {
            throw new InvalidDataException(
                "Stored Topology Atlas evidence receipt does not match the replayed publication.");
        }
    }

    private static TopologyAtlasEvidenceResearchInputRegistration Registration(
        string publicationRef,
        string evidenceRef,
        string receiptRef,
        TopologyAtlasEvidencePublicationCoordinate publication,
        string cursorPath,
        bool replayed,
        TopologyAtlasEvidenceReadModel evidence) =>
        new(
            publicationRef,
            evidenceRef,
            receiptRef,
            publication.TopologyAtlasInputReceiptRef,
            cursorPath,
            replayed,
            evidence.NodeIdentities.Count,
            evidence.NodeTraits.Count,
            evidence.ClusterInterfaces.Count,
            evidence.AffinityWitnesses.Count);

    private static void PersistEvidenceBlob(
        string durableRoot,
        string evidenceRef,
        ReadOnlySpan<byte> bytes)
    {
        string path = EvidenceBlobPath(durableRoot, evidenceRef);
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        if (File.Exists(path))
        {
            if (!File.ReadAllBytes(path).AsSpan().SequenceEqual(bytes))
            {
                throw new InvalidDataException(
                    $"Content-address collision at {evidenceRef}.");
            }
            return;
        }

        string temporary = path + ".tmp-" + Guid.NewGuid().ToString("N");
        File.WriteAllBytes(temporary, bytes.ToArray());
        try
        {
            File.Move(temporary, path, overwrite: false);
        }
        catch (IOException) when (File.Exists(path))
        {
            if (!File.ReadAllBytes(path).AsSpan().SequenceEqual(bytes))
            {
                throw;
            }
            File.Delete(temporary);
        }
    }

    private static string Sha256Reference(ReadOnlySpan<byte> bytes) =>
        "sha256:" + Convert.ToHexStringLower(SHA256.HashData(bytes));

    private static void WriteAtomic(string path, ReadOnlySpan<byte> bytes)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        string temporary = path + ".tmp-" + Guid.NewGuid().ToString("N");
        try
        {
            File.WriteAllBytes(temporary, bytes.ToArray());
            File.Move(temporary, path, overwrite: true);
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
