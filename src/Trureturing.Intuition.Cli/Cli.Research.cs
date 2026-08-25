using System.Text;
using System.Text.Json;
using Trureturing.Intuition.Core;

internal static partial class Cli
{
private static int ProposalSet(ArtifactStore store, string stateRef, IReadOnlyList<string> inputs)
    {
        var state = store.Get<IntuitionState>(stateRef);
        var allowed = state.CandidateUniverse.ToHashSet(StringComparer.Ordinal);
        var neighborhoodMembers = state.Neighborhood.Members.ToDictionary(static member => member.CandidateId, StringComparer.Ordinal);
        var references = new List<string>();
        var seenCandidateIds = new HashSet<string>(StringComparer.Ordinal);
        foreach (var input in inputs)
        {
            var proposal = CanonicalJson.DeserializeStrict<IntuitionProposal>(File.ReadAllBytes(input));
            if (proposal.StateRef != stateRef) throw new InvalidOperationException("Proposal state_ref mismatch.");
            if (!allowed.Contains(proposal.CandidateEditRef)) throw new InvalidOperationException("Proposal candidate is outside frozen candidate universe.");
            if (!string.Equals(proposal.NeighborhoodId, state.Neighborhood.NeighborhoodId, StringComparison.Ordinal)
                || !string.Equals(proposal.TargetNodeId, state.Neighborhood.TargetNodeId, StringComparison.Ordinal))
            {
                throw new InvalidOperationException("Proposal neighborhood/target binding does not match frozen state.");
            }
            if (!neighborhoodMembers.TryGetValue(proposal.CandidateId, out var member))
            {
                throw new InvalidOperationException("Proposal candidate_id is outside the frozen neighborhood.");
            }
            var candidateEdit = store.Get<CandidateEdit>(proposal.CandidateEditRef);
            if (!string.Equals(candidateEdit.CandidateId, proposal.CandidateId, StringComparison.Ordinal))
            {
                throw new InvalidOperationException("Proposal candidate_id does not match its candidate edit.");
            }
            var expectedEndpointIds = new[] { state.Neighborhood.TargetNodeId, member.RelatedNodeId }.Order(StringComparer.Ordinal);
            if (!proposal.EndpointNodeIds.SequenceEqual(expectedEndpointIds, StringComparer.Ordinal))
            {
                throw new InvalidOperationException("Proposal endpoints do not match its frozen neighborhood member.");
            }
            var expectedEndpointRefs = new[] { state.Neighborhood.TargetNodeRef, member.RelatedNodeRef }.Order(StringComparer.Ordinal).ToArray();
            if (!candidateEdit.Inputs.Concat(candidateEdit.Outputs).Distinct(StringComparer.Ordinal).Order(StringComparer.Ordinal).SequenceEqual(expectedEndpointRefs, StringComparer.Ordinal))
            {
                throw new InvalidOperationException("Proposal candidate edit refs do not match its frozen neighborhood endpoints.");
            }
            if (!expectedEndpointRefs.All(reference => proposal.EvidenceRefs.Contains(reference, StringComparer.Ordinal)))
            {
                throw new InvalidOperationException("Proposal evidence must include both frozen endpoint artifacts.");
            }
            if (!string.Equals(proposal.ConjecturedBridge, candidateEdit.RepresentationMap, StringComparison.Ordinal))
            {
                throw new InvalidOperationException("Proposal conjectured_bridge does not match its candidate edit representation map.");
            }
            if (!seenCandidateIds.Add(proposal.CandidateId)) throw new InvalidOperationException("Neighborhood contains duplicate proposal candidate_id.");
            references.Add(store.Put(proposal));
        }
        if (seenCandidateIds.Count != neighborhoodMembers.Count)
        {
            throw new InvalidOperationException("Proposal batch must contain one bridge for every neighborhood member.");
        }
        var sorted = references.Order(StringComparer.Ordinal).Distinct(StringComparer.Ordinal).ToArray();
        var setRef = store.Put(new IntuitionProposalSet(Schemas.ProposalSet, stateRef, sorted));
        WriteResult(new Dictionary<string, object?> { ["proposal_set_ref"] = setRef, ["proposal_refs"] = sorted });
        return 0;
    }

    private static int CritiqueSet(ArtifactStore store, string stateRef, string proposalSetRef, IReadOnlyList<string> inputs)
    {
        var proposals = store.Get<IntuitionProposalSet>(proposalSetRef);
        if (proposals.StateRef != stateRef) throw new InvalidOperationException("Proposal set state mismatch.");
        var allowed = proposals.ProposalRefs.ToHashSet(StringComparer.Ordinal);
        var refs = new List<string>();
        foreach (var input in inputs)
        {
            var critique = CanonicalJson.DeserializeStrict<IntuitionCritique>(File.ReadAllBytes(input));
            if (!allowed.Contains(critique.ProposalRef)) throw new InvalidOperationException("Critique references unknown proposal.");
            refs.Add(store.Put(critique));
        }
        var sorted = refs.Order(StringComparer.Ordinal).Distinct(StringComparer.Ordinal).ToArray();
        var setRef = store.Put(new IntuitionCritiqueSet(Schemas.CritiqueSet, stateRef, proposalSetRef, sorted));
        WriteResult(new Dictionary<string, object?> { ["critique_set_ref"] = setRef, ["critique_refs"] = sorted });
        return 0;
    }

    private static int ValuationSet(ArtifactStore store, string stateRef, string proposalSetRef, string critiqueSetRef, IReadOnlyList<string> inputs)
    {
        var proposals = store.Get<IntuitionProposalSet>(proposalSetRef);
        var critiques = store.Get<IntuitionCritiqueSet>(critiqueSetRef);
        if (proposals.StateRef != stateRef || critiques.StateRef != stateRef || critiques.ProposalSetRef != proposalSetRef) throw new InvalidOperationException("Valuation input graph mismatch.");
        var allowed = proposals.ProposalRefs.ToHashSet(StringComparer.Ordinal);
        var refs = new List<string>();
        foreach (var input in inputs)
        {
            var valuation = CanonicalJson.DeserializeStrict<IntuitionValuation>(File.ReadAllBytes(input));
            if (!allowed.Contains(valuation.ProposalRef)) throw new InvalidOperationException("Valuation references unknown proposal.");
            refs.Add(store.Put(valuation));
        }
        var sorted = refs.Order(StringComparer.Ordinal).Distinct(StringComparer.Ordinal).ToArray();
        var setRef = store.Put(new IntuitionValuationSet(Schemas.ValuationSet, stateRef, proposalSetRef, critiqueSetRef, sorted));
        WriteResult(new Dictionary<string, object?> { ["valuation_set_ref"] = setRef, ["valuation_refs"] = sorted });
        return 0;
    }

    private static int Allocate(ArtifactStore store, string stateRef, string valuationSetRef)
    {
        var state = store.Get<IntuitionState>(stateRef);
        var set = store.Get<IntuitionValuationSet>(valuationSetRef);
        if (set.StateRef != stateRef) throw new InvalidOperationException("Valuation set state mismatch.");
        var values = set.ValuationRefs.Select(reference => (reference, store.Get<IntuitionValuation>(reference))).ToArray();
        var result = ParetoAnalyzer.Analyze(values);
        var allocation = new IntuitionAllocation(
            Schemas.Allocation,
            stateRef,
            valuationSetRef,
            state.SelectionMode,
            result.ParetoFront,
            result.Dominated,
            result.Incomparable,
            Array.Empty<string>(),
            DateTimeOffset.UtcNow.ToUnixTimeSeconds());
        var allocationRef = store.Put(allocation);
        WriteResult(new Dictionary<string, object?> { ["allocation_ref"] = allocationRef, ["pareto_front"] = result.ParetoFront });
        return 0;
    }

    private static int Coverage(ArtifactStore store, string universeRef, string candidateRef)
    {
        var universe = store.Get<ResidualUniverse>(universeRef);
        var candidate = store.Get<CandidateEdit>(candidateRef);
        WriteResult(ResidualCoverageAnalyzer.Analyze(universe, candidate));
        return 0;
    }
}
