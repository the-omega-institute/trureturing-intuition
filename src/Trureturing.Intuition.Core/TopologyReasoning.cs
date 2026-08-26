using System.Collections.Immutable;
using System.Numerics;

namespace Trureturing.Intuition.Core;

public sealed record TopologyReasoningSignal(
    string NodeId,
    string? CandidateId,
    ConceptRelation? Relation,
    BigInteger InDegree,
    BigInteger OutDegree,
    BigInteger MinDepth,
    BigInteger MaxDepth,
    BigInteger AncestorCount,
    BigInteger DescendantCount,
    BigInteger DescendantCost,
    ExactNonNegativeRational NormalizedReach,
    ExactNonNegativeRational DependencyBetweenness,
    bool IsLoadBearing,
    bool IsFrontier);

public sealed record TopologyBridgeReasoningContext(
    CertifiedTopologyBinding Binding,
    string NeighborhoodId,
    string TargetNodeId,
    IReadOnlyList<TopologyReasoningSignal> Signals,
    string Advisory);

public static class TopologyReasoningAdvisor
{
    public static TopologyBridgeReasoningContext Build(
        CertifiedTopologyReadModel topology,
        IntuitionState state)
    {
        ArgumentNullException.ThrowIfNull(topology);
        ArgumentNullException.ThrowIfNull(state);
        ContractValidator.Validate(state);
        if (!StringComparer.Ordinal.Equals(
                topology.Binding.TruthReleaseDigest,
                state.ReleaseDigest))
        {
            throw new InvalidDataException(
                "Certified topology is bound to a different frozen truth release.");
        }

        if (topology.CycleCertificate.Status != "acyclic" ||
            topology.DanglingReferenceCertificate.Status != "complete")
        {
            throw new InvalidDataException(
                "Topology advisory input requires acyclic and complete certificates.");
        }

        HashSet<string> highCost = TopQuartile(
            topology.Nodes,
            static node => node.DescendantCost,
            static value => value > 0);
        HashSet<string> highBetweenness = TopQuartile(
            topology.Nodes,
            static node => node.DependencyBetweenness,
            static value => value.Numerator > 0);
        BigInteger maximumDepth = topology.Nodes.Count == 0
            ? BigInteger.Zero
            : topology.Nodes.Max(node => node.MaxDepth);

        var inputs = new List<(string NodeId, string? CandidateId, ConceptRelation? Relation)>
        {
            (state.Neighborhood.TargetNodeId, null, null)
        };
        inputs.AddRange(state.Neighborhood.Members.Select(member =>
            (member.RelatedNodeId, (string?)member.CandidateId, (ConceptRelation?)member.Relation)));

        var seen = new HashSet<string>(StringComparer.Ordinal);
        IReadOnlyList<TopologyReasoningSignal> signals = inputs
            .Where(input => seen.Add(input.NodeId))
            .Select(input =>
            {
                CertifiedTopologyNodeMetrics node = topology.GetNode(input.NodeId);
                return new TopologyReasoningSignal(
                    node.NodeId,
                    input.CandidateId,
                    input.Relation,
                    node.InDegree,
                    node.OutDegree,
                    node.MinDepth,
                    node.MaxDepth,
                    node.AncestorCount,
                    node.DescendantCount,
                    node.DescendantCost,
                    node.NormalizedReach,
                    node.DependencyBetweenness,
                    highCost.Contains(node.NodeId) || highBetweenness.Contains(node.NodeId),
                    node.OutDegree == 0 || node.MaxDepth == maximumDepth);
            })
            .OrderByDescending(signal => signal.IsLoadBearing)
            .ThenByDescending(signal => signal.DescendantCost)
            .ThenByDescending(signal => signal.DependencyBetweenness)
            .ThenBy(signal => signal.NodeId, StringComparer.Ordinal)
            .ToImmutableArray();

        return new TopologyBridgeReasoningContext(
            topology.Binding,
            state.Neighborhood.NeighborhoodId,
            state.Neighborhood.TargetNodeId,
            signals,
            "Advisory structural inputs for bridge proposal and vector valuation only; " +
            "they confer no truth authority and do not change certified metrics.");
    }

    public static IReadOnlyList<TopologyReasoningSignal> ForProposal(
        TopologyBridgeReasoningContext context,
        IntuitionProposal proposal)
    {
        ArgumentNullException.ThrowIfNull(context);
        ArgumentNullException.ThrowIfNull(proposal);
        if (!StringComparer.Ordinal.Equals(
                context.NeighborhoodId,
                proposal.NeighborhoodId) ||
            !StringComparer.Ordinal.Equals(
                context.TargetNodeId,
                proposal.TargetNodeId))
        {
            throw new InvalidDataException(
                "Topology reasoning context does not match the proposal neighborhood.");
        }

        var endpointIds = proposal.EndpointNodeIds.ToHashSet(StringComparer.Ordinal);
        IReadOnlyList<TopologyReasoningSignal> result = context.Signals
            .Where(signal => endpointIds.Contains(signal.NodeId))
            .OrderBy(signal => signal.NodeId, StringComparer.Ordinal)
            .ToImmutableArray();
        if (result.Count != endpointIds.Count)
        {
            throw new InvalidDataException(
                "Topology reasoning context does not cover every proposal endpoint.");
        }

        return result;
    }

    private static HashSet<string> TopQuartile<T>(
        IReadOnlyList<CertifiedTopologyNodeMetrics> nodes,
        Func<CertifiedTopologyNodeMetrics, T> selector,
        Func<T, bool> positive)
        where T : IComparable<T>
    {
        int count = Math.Max(1, (nodes.Count + 3) / 4);
        return nodes
            .Where(node => positive(selector(node)))
            .OrderByDescending(selector)
            .ThenBy(node => node.NodeId, StringComparer.Ordinal)
            .Take(count)
            .Select(node => node.NodeId)
            .ToHashSet(StringComparer.Ordinal);
    }
}
