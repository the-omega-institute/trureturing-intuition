namespace Trureturing.Intuition.Core;

public static partial class ContractValidator
{
    public static void Validate(TopologyPublicationCoordinate value)
    {
        RequireSchema(value.Schema, TopologyResearchInputSchemas.Publication);
        RequireArtifactRef(value.TruthReleaseDigest, nameof(value.TruthReleaseDigest));
        RequireArtifactRef(value.TopologyDigest, nameof(value.TopologyDigest));
        RequireArtifactRef(
            value.AlgorithmProfileDigest,
            nameof(value.AlgorithmProfileDigest));
        RequireGitId(value.SourceCommit, nameof(value.SourceCommit));
        RequireGitId(value.SourceTree, nameof(value.SourceTree));
        RequireGitId(value.ProducerCommit, nameof(value.ProducerCommit));
        if (value.SourceCommit.Length != value.SourceTree.Length)
        {
            throw new InvalidOperationException(
                "source_commit and source_tree must use the same Git object format.");
        }
    }

    public static void Validate(IntuitionTopologyInputReceipt value)
    {
        RequireSchema(value.Schema, TopologyResearchInputSchemas.Receipt);
        RequireArtifactRef(value.PublicationRef, nameof(value.PublicationRef));
        RequireArtifactRef(value.TopologyRef, nameof(value.TopologyRef));
        RequireArtifactRef(value.TruthReleaseDigest, nameof(value.TruthReleaseDigest));
        RequireArtifactRef(value.TopologyDigest, nameof(value.TopologyDigest));
        RequireArtifactRef(
            value.AlgorithmProfileDigest,
            nameof(value.AlgorithmProfileDigest));
        RequireGitId(value.SourceCommit, nameof(value.SourceCommit));
        RequireGitId(value.SourceTree, nameof(value.SourceTree));
        RequireGitId(value.ProducerCommit, nameof(value.ProducerCommit));
        if (!string.Equals(
                value.TopologyRef,
                value.TopologyDigest,
                StringComparison.Ordinal))
        {
            throw new InvalidOperationException(
                "topology_ref must equal topology_digest.");
        }
        if (value.SourceCommit.Length != value.SourceTree.Length)
        {
            throw new InvalidOperationException(
                "source_commit and source_tree must use the same Git object format.");
        }
    }

    public static void Validate(IntuitionTopologyInputCursor value)
    {
        RequireSchema(value.Schema, TopologyResearchInputSchemas.Cursor);
        RequireArtifactRef(value.ReceiptRef, nameof(value.ReceiptRef));
        RequireArtifactRef(value.TruthReleaseDigest, nameof(value.TruthReleaseDigest));
        RequireArtifactRef(value.TopologyDigest, nameof(value.TopologyDigest));
        RequireGitId(value.SourceCommit, nameof(value.SourceCommit));
        RequireGitId(value.SourceTree, nameof(value.SourceTree));
        if (value.SourceCommit.Length != value.SourceTree.Length)
        {
            throw new InvalidOperationException(
                "source_commit and source_tree must use the same Git object format.");
        }
    }
}
