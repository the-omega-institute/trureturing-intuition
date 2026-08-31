using System.Security.Cryptography;

namespace Trureturing.Intuition.Core;

public static class TopologyAtlasResearchInputSchemas
{
    public const string Publication = "trureturing.topology-atlas-publication.v1";
    public const string Receipt = "intuition-topology-atlas-input-receipt.v1";
    public const string Cursor = "intuition-topology-atlas-input-cursor.v1";
}

public sealed record TopologyAtlasPublicationCoordinate(
    string Schema,
    string TruthReleaseDigest,
    string CertifiedTopologyDigest,
    string TopologyAtlasDigest,
    string SourceCommit,
    string SourceTree,
    string CertifiedAlgorithmProfileDigest,
    string AtlasAlgorithmProfileDigest,
    string ProducerCommit);

public sealed record IntuitionTopologyAtlasInputReceipt(
    string Schema,
    string PublicationRef,
    string AtlasRef,
    string TruthReleaseDigest,
    string CertifiedTopologyDigest,
    string TopologyAtlasDigest,
    string SourceCommit,
    string SourceTree,
    string CertifiedAlgorithmProfileDigest,
    string AtlasAlgorithmProfileDigest,
    string ProducerCommit);

public sealed record IntuitionTopologyAtlasInputCursor(
    string Schema,
    string ReceiptRef,
    string TruthReleaseDigest,
    string CertifiedTopologyDigest,
    string TopologyAtlasDigest,
    string SourceCommit,
    string SourceTree,
    string AtlasAlgorithmProfileDigest);

public sealed record TopologyAtlasResearchInputRegistration(
    string PublicationRef,
    string AtlasRef,
    string ReceiptRef,
    string CursorPath,
    bool Replayed);

public static class TopologyAtlasResearchInputRegistrar
{
    public static TopologyAtlasResearchInputRegistration Register(
        string durableRoot,
        TopologyAtlasPublicationCoordinate publication,
        ReadOnlySpan<byte> atlasBytes,
        string cursorPath)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(durableRoot);
        ArgumentException.ThrowIfNullOrWhiteSpace(cursorPath);
        ContractValidator.Validate(publication);

        string atlasRef = Sha256Reference(atlasBytes);
        if (!StringComparer.Ordinal.Equals(
                publication.TopologyAtlasDigest,
                atlasRef))
        {
            throw new InvalidDataException(
                "Topology Atlas bytes do not match publication.topology_atlas_digest.");
        }

        var binding = new TopologyAtlasBinding(
            publication.TruthReleaseDigest,
            publication.CertifiedTopologyDigest,
            publication.CertifiedAlgorithmProfileDigest,
            publication.AtlasAlgorithmProfileDigest,
            publication.ProducerCommit);
        _ = TopologyAtlasReader.Read(atlasBytes, binding);

        string root = Path.GetFullPath(durableRoot);
        var store = new ArtifactStore(root);
        string publicationRef = store.Put(publication);
        PersistAtlasBlob(root, atlasRef, atlasBytes);

        string fullCursorPath = Path.GetFullPath(cursorPath);
        if (File.Exists(fullCursorPath))
        {
            IntuitionTopologyAtlasInputCursor current =
                CanonicalJson.DeserializeCanonical<IntuitionTopologyAtlasInputCursor>(
                    File.ReadAllBytes(fullCursorPath));
            ContractValidator.Validate(current);

            if (StringComparer.Ordinal.Equals(
                    current.TruthReleaseDigest,
                    publication.TruthReleaseDigest))
            {
                if (!StringComparer.Ordinal.Equals(
                        current.CertifiedTopologyDigest,
                        publication.CertifiedTopologyDigest) ||
                    !StringComparer.Ordinal.Equals(
                        current.TopologyAtlasDigest,
                        atlasRef) ||
                    !StringComparer.Ordinal.Equals(
                        current.AtlasAlgorithmProfileDigest,
                        publication.AtlasAlgorithmProfileDigest) ||
                    !StringComparer.Ordinal.Equals(
                        current.SourceCommit,
                        publication.SourceCommit) ||
                    !StringComparer.Ordinal.Equals(
                        current.SourceTree,
                        publication.SourceTree))
                {
                    throw new InvalidDataException(
                        "An existing truth release cannot be rebound to different Topology Atlas coordinates.");
                }

                IntuitionTopologyAtlasInputReceipt existing =
                    store.Get<IntuitionTopologyAtlasInputReceipt>(current.ReceiptRef);
                ValidateReceiptAgainstPublication(
                    existing,
                    publication,
                    publicationRef,
                    atlasRef);
                return new TopologyAtlasResearchInputRegistration(
                    existing.PublicationRef,
                    existing.AtlasRef,
                    current.ReceiptRef,
                    fullCursorPath,
                    true);
            }
        }

        var receipt = new IntuitionTopologyAtlasInputReceipt(
            TopologyAtlasResearchInputSchemas.Receipt,
            publicationRef,
            atlasRef,
            publication.TruthReleaseDigest,
            publication.CertifiedTopologyDigest,
            atlasRef,
            publication.SourceCommit,
            publication.SourceTree,
            publication.CertifiedAlgorithmProfileDigest,
            publication.AtlasAlgorithmProfileDigest,
            publication.ProducerCommit);
        ContractValidator.Validate(receipt);
        string receiptRef = store.Put(receipt);

        var cursor = new IntuitionTopologyAtlasInputCursor(
            TopologyAtlasResearchInputSchemas.Cursor,
            receiptRef,
            publication.TruthReleaseDigest,
            publication.CertifiedTopologyDigest,
            atlasRef,
            publication.SourceCommit,
            publication.SourceTree,
            publication.AtlasAlgorithmProfileDigest);
        ContractValidator.Validate(cursor);
        WriteAtomic(fullCursorPath, CanonicalJson.Serialize(cursor));

        return new TopologyAtlasResearchInputRegistration(
            publicationRef,
            atlasRef,
            receiptRef,
            fullCursorPath,
            false);
    }

    public static string AtlasBlobPath(
        string durableRoot,
        string atlasRef)
    {
        ContractValidator.RequireArtifactRef(atlasRef, nameof(atlasRef));
        string hex = atlasRef["sha256:".Length..];
        return Path.Combine(
            Path.GetFullPath(durableRoot),
            "blobs",
            "sha256",
            hex[..2],
            hex + ".json");
    }

    private static void ValidateReceiptAgainstPublication(
        IntuitionTopologyAtlasInputReceipt receipt,
        TopologyAtlasPublicationCoordinate publication,
        string publicationRef,
        string atlasRef)
    {
        ContractValidator.Validate(receipt);
        if (!StringComparer.Ordinal.Equals(receipt.PublicationRef, publicationRef) ||
            !StringComparer.Ordinal.Equals(receipt.AtlasRef, atlasRef) ||
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
                receipt.AtlasAlgorithmProfileDigest,
                publication.AtlasAlgorithmProfileDigest))
        {
            throw new InvalidDataException(
                "Stored Topology Atlas receipt does not match the replayed publication.");
        }
    }

    private static void PersistAtlasBlob(
        string durableRoot,
        string atlasRef,
        ReadOnlySpan<byte> bytes)
    {
        string path = AtlasBlobPath(durableRoot, atlasRef);
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        if (File.Exists(path))
        {
            if (!File.ReadAllBytes(path).AsSpan().SequenceEqual(bytes))
            {
                throw new InvalidDataException(
                    $"Content-address collision at {atlasRef}.");
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
