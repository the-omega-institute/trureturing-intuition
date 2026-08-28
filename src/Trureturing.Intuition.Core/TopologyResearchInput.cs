using System.Security.Cryptography;

namespace Trureturing.Intuition.Core;

public static class TopologyResearchInputSchemas
{
    public const string Publication = "trureturing.topology-publication.v1";
    public const string Receipt = "intuition-topology-input-receipt.v1";
    public const string Cursor = "intuition-topology-input-cursor.v1";
}

public sealed record TopologyPublicationCoordinate(
    string Schema,
    string TruthReleaseDigest,
    string TopologyDigest,
    string SourceCommit,
    string SourceTree,
    string AlgorithmProfileDigest,
    string ProducerCommit);

public sealed record IntuitionTopologyInputReceipt(
    string Schema,
    string PublicationRef,
    string TopologyRef,
    string TruthReleaseDigest,
    string TopologyDigest,
    string SourceCommit,
    string SourceTree,
    string AlgorithmProfileDigest,
    string ProducerCommit);

public sealed record IntuitionTopologyInputCursor(
    string Schema,
    string ReceiptRef,
    string TruthReleaseDigest,
    string TopologyDigest,
    string SourceCommit,
    string SourceTree);

public sealed record TopologyResearchInputRegistration(
    string PublicationRef,
    string TopologyRef,
    string ReceiptRef,
    string CursorPath,
    bool Replayed);

public static class TopologyResearchInputRegistrar
{
    public static TopologyResearchInputRegistration Register(
        string durableRoot,
        TopologyPublicationCoordinate publication,
        ReadOnlySpan<byte> topologyBytes,
        string cursorPath)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(durableRoot);
        ArgumentException.ThrowIfNullOrWhiteSpace(cursorPath);
        ContractValidator.Validate(publication);

        string topologyRef = Sha256Reference(topologyBytes);
        if (!string.Equals(
                publication.TopologyDigest,
                topologyRef,
                StringComparison.Ordinal))
        {
            throw new InvalidDataException(
                "Topology bytes do not match publication.topology_digest.");
        }

        _ = CertifiedTopologyReader.Read(
            topologyBytes,
            new CertifiedTopologyBinding(
                publication.TruthReleaseDigest,
                publication.AlgorithmProfileDigest,
                publication.ProducerCommit));

        string root = Path.GetFullPath(durableRoot);
        var store = new ArtifactStore(root);
        string publicationRef = store.Put(publication);
        PersistTopologyBlob(root, topologyRef, topologyBytes);

        string fullCursorPath = Path.GetFullPath(cursorPath);
        if (File.Exists(fullCursorPath))
        {
            IntuitionTopologyInputCursor current =
                CanonicalJson.DeserializeCanonical<IntuitionTopologyInputCursor>(
                    File.ReadAllBytes(fullCursorPath));
            ContractValidator.Validate(current);

            if (string.Equals(
                    current.TruthReleaseDigest,
                    publication.TruthReleaseDigest,
                    StringComparison.Ordinal))
            {
                if (!string.Equals(
                        current.TopologyDigest,
                        topologyRef,
                        StringComparison.Ordinal))
                {
                    throw new InvalidDataException(
                        "An existing truth release cannot be rebound to different topology bytes.");
                }

                IntuitionTopologyInputReceipt existing =
                    store.Get<IntuitionTopologyInputReceipt>(current.ReceiptRef);
                return new TopologyResearchInputRegistration(
                    existing.PublicationRef,
                    existing.TopologyRef,
                    current.ReceiptRef,
                    fullCursorPath,
                    true);
            }
        }

        var receipt = new IntuitionTopologyInputReceipt(
            TopologyResearchInputSchemas.Receipt,
            publicationRef,
            topologyRef,
            publication.TruthReleaseDigest,
            topologyRef,
            publication.SourceCommit,
            publication.SourceTree,
            publication.AlgorithmProfileDigest,
            publication.ProducerCommit);
        string receiptRef = store.Put(receipt);

        var cursor = new IntuitionTopologyInputCursor(
            TopologyResearchInputSchemas.Cursor,
            receiptRef,
            publication.TruthReleaseDigest,
            topologyRef,
            publication.SourceCommit,
            publication.SourceTree);
        ContractValidator.Validate(cursor);
        WriteAtomic(fullCursorPath, CanonicalJson.Serialize(cursor));

        return new TopologyResearchInputRegistration(
            publicationRef,
            topologyRef,
            receiptRef,
            fullCursorPath,
            false);
    }

    public static string TopologyBlobPath(string durableRoot, string topologyRef)
    {
        ContractValidator.RequireArtifactRef(topologyRef, nameof(topologyRef));
        string hex = topologyRef["sha256:".Length..];
        return Path.Combine(
            Path.GetFullPath(durableRoot),
            "blobs",
            "sha256",
            hex[..2],
            hex + ".json");
    }

    private static void PersistTopologyBlob(
        string durableRoot,
        string topologyRef,
        ReadOnlySpan<byte> bytes)
    {
        string path = TopologyBlobPath(durableRoot, topologyRef);
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        if (File.Exists(path))
        {
            if (!File.ReadAllBytes(path).AsSpan().SequenceEqual(bytes))
            {
                throw new InvalidDataException(
                    $"Content-address collision at {topologyRef}.");
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
