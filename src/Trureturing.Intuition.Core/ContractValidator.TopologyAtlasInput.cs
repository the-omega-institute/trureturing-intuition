namespace Trureturing.Intuition.Core;

public static partial class ContractValidator
{
    public static void Validate(TopologyAtlasPublicationCoordinate value)
    {
        ArgumentNullException.ThrowIfNull(value);
        RequireSchema(
            value.Schema,
            TopologyAtlasResearchInputSchemas.Publication);
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
            value.CertifiedAlgorithmProfileDigest,
            nameof(value.CertifiedAlgorithmProfileDigest));
        RequireArtifactRef(
            value.AtlasAlgorithmProfileDigest,
            nameof(value.AtlasAlgorithmProfileDigest));
        RequireGitId(value.SourceCommit, nameof(value.SourceCommit));
        RequireGitId(value.SourceTree, nameof(value.SourceTree));
        RequireGitId(value.ProducerCommit, nameof(value.ProducerCommit));
        if (value.SourceCommit.Length != value.SourceTree.Length)
        {
            throw new InvalidOperationException(
                "source_commit and source_tree must use the same Git object format.");
        }
    }

    public static void Validate(IntuitionTopologyAtlasInputReceipt value)
    {
        ArgumentNullException.ThrowIfNull(value);
        RequireSchema(
            value.Schema,
            TopologyAtlasResearchInputSchemas.Receipt);
        RequireArtifactRef(value.PublicationRef, nameof(value.PublicationRef));
        RequireArtifactRef(value.AtlasRef, nameof(value.AtlasRef));
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
            value.CertifiedAlgorithmProfileDigest,
            nameof(value.CertifiedAlgorithmProfileDigest));
        RequireArtifactRef(
            value.AtlasAlgorithmProfileDigest,
            nameof(value.AtlasAlgorithmProfileDigest));
        RequireGitId(value.SourceCommit, nameof(value.SourceCommit));
        RequireGitId(value.SourceTree, nameof(value.SourceTree));
        RequireGitId(value.ProducerCommit, nameof(value.ProducerCommit));
        if (!StringComparer.Ordinal.Equals(
                value.AtlasRef,
                value.TopologyAtlasDigest))
        {
            throw new InvalidOperationException(
                "atlas_ref must equal topology_atlas_digest.");
        }
        if (value.SourceCommit.Length != value.SourceTree.Length)
        {
            throw new InvalidOperationException(
                "source_commit and source_tree must use the same Git object format.");
        }
    }

    public static void Validate(IntuitionTopologyAtlasInputCursor value)
    {
        ArgumentNullException.ThrowIfNull(value);
        RequireSchema(
            value.Schema,
            TopologyAtlasResearchInputSchemas.Cursor);
        RequireArtifactRef(value.ReceiptRef, nameof(value.ReceiptRef));
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
            value.AtlasAlgorithmProfileDigest,
            nameof(value.AtlasAlgorithmProfileDigest));
        RequireGitId(value.SourceCommit, nameof(value.SourceCommit));
        RequireGitId(value.SourceTree, nameof(value.SourceTree));
        if (value.SourceCommit.Length != value.SourceTree.Length)
        {
            throw new InvalidOperationException(
                "source_commit and source_tree must use the same Git object format.");
        }
    }
}
