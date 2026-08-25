using System.Security.Cryptography;
using System.Text.Json.Serialization;

namespace Trureturing.Intuition.Core;

public static class SharedServiceSchemas
{
    public const string CertifiedTopology = "trureturing.certified-topology.v1";
    public const string CertifiedTopologyAlgorithm = "trureturing-certified-topology-v1";
    public const string TopologyBinding = "intuition-topology-binding.v1";
    public const string FormalizationWorkRequest = "formalization-work-request.v1";
}

public sealed record SharedTopologySemantics(
    [property: JsonRequired] string NodeSemantics,
    [property: JsonRequired] string EdgeSemantics,
    [property: JsonRequired] string DepthSemantics,
    [property: JsonRequired] string ComponentSemantics,
    [property: JsonRequired] string DominatorSemantics);

public sealed record SharedTopologySummary(
    [property: JsonRequired] int NodeCount,
    [property: JsonRequired] int EdgeCount,
    [property: JsonRequired] int RootCount,
    [property: JsonRequired] int LeafCount,
    [property: JsonRequired] int ComponentCount,
    [property: JsonRequired] int MaximumDepth);

public sealed record SharedTopologyNode(
    [property: JsonRequired] string Id,
    [property: JsonRequired] string RepoPath,
    [property: JsonRequired] IReadOnlyList<string> Declarations,
    [property: JsonRequired] IReadOnlyList<string> PrerequisiteIds,
    [property: JsonRequired] IReadOnlyList<string> AxiomClosure,
    string? AxiomTier,
    string? DocumentAnchor,
    [property: JsonRequired] string ComponentId,
    [property: JsonRequired] int Depth,
    [property: JsonRequired] int Height,
    [property: JsonRequired] int InDegree,
    [property: JsonRequired] int OutDegree,
    [property: JsonRequired] int AncestorCount,
    [property: JsonRequired] int DescendantCount,
    [property: JsonRequired] int DominatedNodeCount,
    [property: JsonRequired] int StructuralBlastRadius,
    [property: JsonRequired] bool IsRoot,
    [property: JsonRequired] bool IsLeaf);

public sealed record SharedTopologyEdge(
    [property: JsonRequired] string PrerequisiteId,
    [property: JsonRequired] string DependentId);

public sealed record SharedTopologyComponent(
    [property: JsonRequired] string Id,
    [property: JsonRequired] int NodeCount,
    [property: JsonRequired] int EdgeCount,
    [property: JsonRequired] int MaximumDepth);

public sealed record SharedCertifiedTopology(
    [property: JsonRequired] string Schema,
    [property: JsonRequired] string SourceTruthReleaseDigest,
    [property: JsonRequired] string SourceCommit,
    [property: JsonRequired] string SourceTree,
    [property: JsonRequired] string Algorithm,
    [property: JsonRequired] SharedTopologySemantics Semantics,
    [property: JsonRequired] SharedTopologySummary Summary,
    [property: JsonRequired] IReadOnlyList<SharedTopologyNode> Nodes,
    [property: JsonRequired] IReadOnlyList<SharedTopologyEdge> Edges,
    [property: JsonRequired] IReadOnlyList<SharedTopologyComponent> Components);

public sealed record IntuitionTopologyNode(
    string Id,
    string RepoPath,
    IReadOnlyList<string> Declarations,
    IReadOnlyList<string> PrerequisiteIds,
    string ComponentId,
    int Depth,
    int Height,
    int AncestorCount,
    int DescendantCount,
    int DominatedNodeCount,
    int StructuralBlastRadius,
    string? AxiomTier,
    IReadOnlyList<string> AxiomClosure);

public sealed record IntuitionTopologyBinding(
    string Schema,
    string StateRef,
    string TopologyDigest,
    string SourceTruthReleaseDigest,
    string SourceCommit,
    string SourceTree,
    string Algorithm,
    IReadOnlyList<IntuitionTopologyNode> Nodes,
    IReadOnlyList<SharedTopologyEdge> Edges,
    long BoundAtUnix);

public sealed record FormalizationWorkSource(
    string Organ,
    string ArtifactRef);

public sealed record FormalizationWorkSnapshot(
    string SourceRepo,
    string SourceCommit,
    string SourceTree,
    string TruthReleaseDigest);

public sealed record FormalizationWorkTarget(
    string? AtomId,
    string? PreferredGid,
    string? ProblemText,
    IReadOnlyList<string> KnownDependencies,
    IReadOnlyList<string> AllowedAssumptions,
    IReadOnlyList<string> ForbiddenWeakenings);

public sealed record FormalizationWorkScope(
    IReadOnlyList<string> AllowedPathPrefixes);

public sealed record FormalizationWorkBudget(
    int MaxAgentRuns,
    int MaxLeanCalls,
    int WallSeconds);

public sealed record FormalizationFailureSemantics(
    bool CounterexampleIsUseful,
    bool MissingPrerequisiteIsReportable);

public sealed record FormalizationWorkRequestV1(
    string Schema,
    string RequestKey,
    FormalizationWorkSource Source,
    FormalizationWorkSnapshot Snapshot,
    string Lane,
    FormalizationWorkTarget Target,
    FormalizationWorkScope Scope,
    FormalizationWorkBudget Budget,
    FormalizationFailureSemantics FailureSemantics);

public static class SharedServiceSeams
{
    public static IntuitionTopologyBinding BindTopology(
        string stateRef,
        IntuitionState state,
        ReadOnlySpan<byte> topologyBytes,
        long boundAtUnix)
    {
        ContractValidator.RequireArtifactRef(stateRef, nameof(stateRef));
        SharedCertifiedTopology topology =
            CanonicalJson.DeserializeCanonical<SharedCertifiedTopology>(topologyBytes);
        ValidateCertifiedTopology(topology);

        if (!string.Equals(topology.SourceTruthReleaseDigest, state.ReleaseDigest, StringComparison.Ordinal) ||
            !string.Equals(topology.SourceCommit, state.SourceCommit, StringComparison.Ordinal) ||
            !string.Equals(topology.SourceTree, state.SourceTree, StringComparison.Ordinal))
        {
            throw new InvalidOperationException(
                "Certified topology and Intuition state are bound to different truth snapshots.");
        }

        string topologyDigest = "sha256:" +
            Convert.ToHexStringLower(SHA256.HashData(topologyBytes));
        IntuitionTopologyNode[] nodes = topology.Nodes
            .OrderBy(node => node.Id, StringComparer.Ordinal)
            .Select(node => new IntuitionTopologyNode(
                node.Id,
                node.RepoPath,
                node.Declarations.Order(StringComparer.Ordinal).ToArray(),
                node.PrerequisiteIds.Order(StringComparer.Ordinal).ToArray(),
                node.ComponentId,
                node.Depth,
                node.Height,
                node.AncestorCount,
                node.DescendantCount,
                node.DominatedNodeCount,
                node.StructuralBlastRadius,
                node.AxiomTier,
                node.AxiomClosure.Order(StringComparer.Ordinal).ToArray()))
            .ToArray();
        SharedTopologyEdge[] edges = topology.Edges
            .OrderBy(edge => edge.PrerequisiteId, StringComparer.Ordinal)
            .ThenBy(edge => edge.DependentId, StringComparer.Ordinal)
            .ToArray();

        var binding = new IntuitionTopologyBinding(
            SharedServiceSchemas.TopologyBinding,
            stateRef,
            topologyDigest,
            topology.SourceTruthReleaseDigest,
            topology.SourceCommit,
            topology.SourceTree,
            topology.Algorithm,
            nodes,
            edges,
            boundAtUnix);
        ContractValidator.Validate(binding);
        return binding;
    }

    public static FormalizationWorkRequestV1 BuildFormalizationRequest(
        string stateRef,
        IntuitionState state,
        string topologyBindingRef,
        IntuitionTopologyBinding topology,
        string proposalRef,
        IntuitionProposal proposal,
        CandidateEdit candidate,
        IntuitionAllocation allocation,
        OwnerAuthorization authorization,
        string requestKey,
        string problemText,
        IReadOnlyList<string> allowedPathPrefixes,
        int maxAgentRuns,
        string? preferredGid = null)
    {
        ContractValidator.RequireArtifactRef(stateRef, nameof(stateRef));
        ContractValidator.RequireArtifactRef(topologyBindingRef, nameof(topologyBindingRef));
        ContractValidator.RequireArtifactRef(proposalRef, nameof(proposalRef));
        if (topology.StateRef != stateRef ||
            topology.SourceTruthReleaseDigest != state.ReleaseDigest ||
            topology.SourceCommit != state.SourceCommit ||
            topology.SourceTree != state.SourceTree)
        {
            throw new InvalidOperationException("Topology binding does not belong to the Intuition state.");
        }
        if (proposal.StateRef != stateRef || proposal.CandidateEditRef != candidate.CandidateId &&
            proposal.CandidateEditRef != topologyBindingRef)
        {
            // CandidateEditRef is normally the content-address of candidate, checked by the CLI.
            // This guard only rejects an obviously unrelated state here.
            if (proposal.StateRef != stateRef)
            {
                throw new InvalidOperationException("Proposal does not belong to the Intuition state.");
            }
        }
        if (authorization.AllocationRef != allocation.ValuationSetRef &&
            authorization.AllocationRef.Length == 0)
        {
            throw new InvalidOperationException("Owner authorization has no allocation coordinate.");
        }
        if (!authorization.AuthorizedProposalRefs.Contains(proposalRef, StringComparer.Ordinal))
        {
            throw new InvalidOperationException("Proposal is not owner-authorized for formalization dispatch.");
        }
        if (string.IsNullOrWhiteSpace(problemText))
        {
            throw new InvalidOperationException("Formalization problem text must be non-empty.");
        }

        int maxLeanCalls = checked((int)Math.Clamp(state.Budget.VerifierCalls, 1, int.MaxValue));
        int wallSeconds = checked((int)Math.Max(60, Math.Ceiling(state.Budget.WallSeconds)));
        var request = new FormalizationWorkRequestV1(
            SharedServiceSchemas.FormalizationWorkRequest,
            requestKey,
            new FormalizationWorkSource("intuition", proposalRef),
            new FormalizationWorkSnapshot(
                "the-omega-institute/trureturing",
                state.SourceCommit,
                state.SourceTree,
                state.ReleaseDigest),
            "theorize",
            new FormalizationWorkTarget(
                null,
                preferredGid,
                problemText,
                candidate.Inputs
                    .Concat(candidate.Outputs)
                    .Distinct(StringComparer.Ordinal)
                    .Order(StringComparer.Ordinal)
                    .ToArray(),
                candidate.AssumptionMap
                    .Distinct(StringComparer.Ordinal)
                    .Order(StringComparer.Ordinal)
                    .ToArray(),
                candidate.PreservedInvariants
                    .Distinct(StringComparer.Ordinal)
                    .Order(StringComparer.Ordinal)
                    .ToArray()),
            new FormalizationWorkScope(
                allowedPathPrefixes
                    .Distinct(StringComparer.Ordinal)
                    .Order(StringComparer.Ordinal)
                    .ToArray()),
            new FormalizationWorkBudget(maxAgentRuns, maxLeanCalls, wallSeconds),
            new FormalizationFailureSemantics(true, true));
        ContractValidator.Validate(request);
        return request;
    }

    public static void ValidateCertifiedTopology(SharedCertifiedTopology topology)
    {
        if (topology.Schema != SharedServiceSchemas.CertifiedTopology ||
            topology.Algorithm != SharedServiceSchemas.CertifiedTopologyAlgorithm)
        {
            throw new InvalidOperationException("Unexpected certified topology dialect.");
        }
        ContractValidator.RequireArtifactRef(topology.SourceTruthReleaseDigest,
            nameof(topology.SourceTruthReleaseDigest));
        RequireGitObject(topology.SourceCommit, nameof(topology.SourceCommit));
        RequireGitObject(topology.SourceTree, nameof(topology.SourceTree));
        if (topology.SourceCommit.Length != topology.SourceTree.Length)
        {
            throw new InvalidOperationException("Topology source commit/tree object widths differ.");
        }

        var ids = topology.Nodes.Select(node => node.Id)
            .ToHashSet(StringComparer.Ordinal);
        if (ids.Count != topology.Nodes.Count)
        {
            throw new InvalidOperationException("Certified topology node ids are not unique.");
        }
        var edgePairs = new HashSet<(string, string)>();
        foreach (SharedTopologyNode node in topology.Nodes)
        {
            ContractValidator.RequireArtifactRef(node.Id, "topology node id");
            if (node.StructuralBlastRadius != node.DescendantCount + 1 ||
                node.Depth < 0 || node.Height < 0 ||
                node.AncestorCount < 0 || node.DescendantCount < 0 ||
                node.DominatedNodeCount < 0)
            {
                throw new InvalidOperationException($"Certified topology metrics are invalid for {node.Id}.");
            }
        }
        foreach (SharedTopologyEdge edge in topology.Edges)
        {
            if (!ids.Contains(edge.PrerequisiteId) || !ids.Contains(edge.DependentId) ||
                !edgePairs.Add((edge.PrerequisiteId, edge.DependentId)))
            {
                throw new InvalidOperationException("Certified topology edge closure or uniqueness failed.");
            }
        }
        if (topology.Summary.NodeCount != topology.Nodes.Count ||
            topology.Summary.EdgeCount != topology.Edges.Count)
        {
            throw new InvalidOperationException("Certified topology summary disagrees with its members.");
        }
    }

    private static void RequireGitObject(string value, string name)
    {
        if (value.Length is not (40 or 64) ||
            value.Any(character =>
                character is not (>= '0' and <= '9') and not (>= 'a' and <= 'f')))
        {
            throw new InvalidOperationException(
                $"{name} must be a lowercase 40- or 64-hex Git object id.");
        }
    }
}
