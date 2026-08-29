using System.Globalization;

namespace Trureturing.Intuition.Core;

public static partial class ContractValidator
{
    private static readonly IReadOnlySet<string> HumanCandidateKinds =
        new HashSet<string>(StringComparer.Ordinal)
        {
            "bridge",
            "subgoal",
            "abstraction",
            "counterexample",
            "representation-change",
            "open-question",
        };

    public static void Validate(HumanResearchCandidate candidate)
    {
        ArgumentNullException.ThrowIfNull(candidate);
        RequireSchema(candidate.Schema, HumanResearchCandidateSchemas.Candidate);
        ArgumentNullException.ThrowIfNull(candidate.CandidateContent);
        Validate(candidate.CandidateContent);
        RequireArtifactRef(candidate.CandidateId, nameof(candidate.CandidateId));
        string expected = CanonicalJson.Sha256Reference(
            CanonicalJson.Serialize(candidate.CandidateContent));
        if (!string.Equals(candidate.CandidateId, expected, StringComparison.Ordinal))
        {
            throw new InvalidOperationException(
                "candidate_id does not address canonical candidate_content bytes.");
        }
    }

    public static void Validate(HumanResearchCandidateContent content)
    {
        ArgumentNullException.ThrowIfNull(content);
        RequireArtifactRef(
            content.TruthReleaseDigest,
            nameof(content.TruthReleaseDigest));
        RequireArtifactRef(content.TopologyDigest, nameof(content.TopologyDigest));
        RequireGitId(content.SourceCommit, nameof(content.SourceCommit));
        RequireGitId(content.SourceTree, nameof(content.SourceTree));
        if (content.SourceCommit.Length != content.SourceTree.Length)
        {
            throw new InvalidOperationException(
                "source_commit and source_tree use different Git object widths.");
        }
        if (!string.Equals(
                content.SourceSurface,
                "trureturing-pages",
                StringComparison.Ordinal))
        {
            throw new InvalidOperationException(
                "Human candidates must originate from trureturing-pages.");
        }
        RequireNonEmpty(content.HumanActor, nameof(content.HumanActor));
        if (content.HumanActor.Length > 256)
        {
            throw new InvalidOperationException("human_actor exceeds 256 characters.");
        }
        if (content.SelectedNodeIds.Count == 0)
        {
            throw new InvalidOperationException(
                "selected_node_ids must contain at least one node.");
        }
        RequireSortedUniqueStrings(
            content.SelectedNodeIds,
            nameof(content.SelectedNodeIds));
        RequireSortedUniqueStrings(
            content.SelectedEdgeIds,
            nameof(content.SelectedEdgeIds));
        RequireText(content.HumanPrompt, nameof(content.HumanPrompt), 8000);
        RequireArtifactRef(
            content.AgentResponseRef,
            nameof(content.AgentResponseRef));
        if (!HumanCandidateKinds.Contains(content.CandidateKind))
        {
            throw new InvalidOperationException(
                $"Unsupported human candidate kind '{content.CandidateKind}'.");
        }
        RequireText(
            content.CandidateStatement,
            nameof(content.CandidateStatement),
            16384);
        RequireText(content.Falsifier, nameof(content.Falsifier), 8192);
        if (!DateTimeOffset.TryParse(
                content.CreatedAt,
                CultureInfo.InvariantCulture,
                DateTimeStyles.AssumeUniversal | DateTimeStyles.AdjustToUniversal,
                out _))
        {
            throw new InvalidOperationException(
                "created_at must be an RFC 3339 timestamp.");
        }
    }

    public static void Validate(HumanResearchCandidateReceipt receipt)
    {
        ArgumentNullException.ThrowIfNull(receipt);
        RequireSchema(receipt.Schema, HumanResearchCandidateSchemas.Receipt);
        RequireArtifactRef(receipt.CandidateRef, nameof(receipt.CandidateRef));
        RequireArtifactRef(receipt.CandidateId, nameof(receipt.CandidateId));
        RequireArtifactRef(
            receipt.TruthReleaseDigest,
            nameof(receipt.TruthReleaseDigest));
        RequireArtifactRef(receipt.TopologyDigest, nameof(receipt.TopologyDigest));
        if (!string.Equals(
                receipt.SourceSurface,
                "trureturing-pages",
                StringComparison.Ordinal))
        {
            throw new InvalidOperationException(
                "Human candidate receipt has an unsupported source surface.");
        }
        RequireNonEmpty(receipt.HumanActor, nameof(receipt.HumanActor));
        if (!DateTimeOffset.TryParse(
                receipt.RegisteredAt,
                CultureInfo.InvariantCulture,
                DateTimeStyles.AssumeUniversal | DateTimeStyles.AdjustToUniversal,
                out _))
        {
            throw new InvalidOperationException(
                "registered_at must be an RFC 3339 timestamp.");
        }
    }

    private static void RequireText(string? value, string name, int maximum)
    {
        RequireNonEmpty(value, name);
        if (value!.Length > maximum)
        {
            throw new InvalidOperationException(
                $"{name} exceeds {maximum} characters.");
        }
    }
}
