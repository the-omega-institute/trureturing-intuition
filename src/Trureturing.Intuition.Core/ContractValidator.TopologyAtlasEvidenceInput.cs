namespace Trureturing.Intuition.Core;

public static partial class ContractValidator
{
    public static void Validate(
        TopologyAtlasEvidencePublicationCoordinate value)
    {
        ArgumentNullException.ThrowIfNull(value);
        RequireSchema(
            value.Schema,
            TopologyAtlasEvidenceResearchInputSchemas.Publication);
        RequireArtifactRef(
            value.TopologyAtlasInputReceiptRef,
            nameof(value.TopologyAtlasInputReceiptRef));
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
        RequireArtifactRef(
            value.CertifiedAlgorithmProfileDigest,
            nameof(value.CertifiedAlgorithmProfileDigest));
        RequireArtifactRef(
            value.AtlasAlgorithmProfileDigest,
            nameof(value.AtlasAlgorithmProfileDigest));
        RequireArtifactRef(
            value.EvidenceAlgorithmProfileDigest,
            nameof(value.EvidenceAlgorithmProfileDigest));
        RequireGitId(value.SourceCommit, nameof(value.SourceCommit));
        RequireGitId(value.SourceTree, nameof(value.SourceTree));
        RequireTopologyEvidenceProducerCommit(value.ProducerCommit);
        RequireMatchingGitObjectFormats(
            value.SourceCommit,
            value.SourceTree);
    }

    public static void Validate(
        IntuitionTopologyAtlasEvidenceInputReceipt value)
    {
        ArgumentNullException.ThrowIfNull(value);
        RequireSchema(
            value.Schema,
            TopologyAtlasEvidenceResearchInputSchemas.Receipt);
        RequireArtifactRef(value.PublicationRef, nameof(value.PublicationRef));
        RequireArtifactRef(value.EvidenceRef, nameof(value.EvidenceRef));
        RequireArtifactRef(
            value.TopologyAtlasInputReceiptRef,
            nameof(value.TopologyAtlasInputReceiptRef));
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
        RequireArtifactRef(
            value.CertifiedAlgorithmProfileDigest,
            nameof(value.CertifiedAlgorithmProfileDigest));
        RequireArtifactRef(
            value.AtlasAlgorithmProfileDigest,
            nameof(value.AtlasAlgorithmProfileDigest));
        RequireArtifactRef(
            value.EvidenceAlgorithmProfileDigest,
            nameof(value.EvidenceAlgorithmProfileDigest));
        RequireGitId(value.SourceCommit, nameof(value.SourceCommit));
        RequireGitId(value.SourceTree, nameof(value.SourceTree));
        RequireTopologyEvidenceProducerCommit(value.ProducerCommit);
        RequireMatchingGitObjectFormats(
            value.SourceCommit,
            value.SourceTree);
        if (!StringComparer.Ordinal.Equals(
                value.EvidenceRef,
                value.TopologyAtlasEvidenceDigest))
        {
            throw new InvalidOperationException(
                "evidence_ref must equal topology_atlas_evidence_digest.");
        }
    }

    public static void Validate(
        IntuitionTopologyAtlasEvidenceInputCursor value)
    {
        ArgumentNullException.ThrowIfNull(value);
        RequireSchema(
            value.Schema,
            TopologyAtlasEvidenceResearchInputSchemas.Cursor);
        RequireArtifactRef(value.ReceiptRef, nameof(value.ReceiptRef));
        RequireArtifactRef(
            value.TopologyAtlasInputReceiptRef,
            nameof(value.TopologyAtlasInputReceiptRef));
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
        RequireArtifactRef(
            value.EvidenceAlgorithmProfileDigest,
            nameof(value.EvidenceAlgorithmProfileDigest));
        RequireGitId(value.SourceCommit, nameof(value.SourceCommit));
        RequireGitId(value.SourceTree, nameof(value.SourceTree));
        RequireTopologyEvidenceProducerCommit(value.ProducerCommit);
        RequireMatchingGitObjectFormats(
            value.SourceCommit,
            value.SourceTree);
    }

    private static void RequireTopologyEvidenceProducerCommit(string value)
    {
        RequireGitId(value, "producer_commit");
        if (value.Length != 40)
        {
            throw new InvalidOperationException(
                "producer_commit must use a 40-character lowercase Git commit ID.");
        }
    }

    private static void RequireMatchingGitObjectFormats(
        string sourceCommit,
        string sourceTree)
    {
        if (sourceCommit.Length != sourceTree.Length)
        {
            throw new InvalidOperationException(
                "source_commit and source_tree must use the same Git object format.");
        }
    }
}
