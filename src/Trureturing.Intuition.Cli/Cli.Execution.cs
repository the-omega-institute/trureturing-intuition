using System.Text;
using System.Text.Json;
using Trureturing.Intuition.Core;

internal static partial class Cli
{
private static int Attempt(ArtifactStore store, string stateRef, string proposalRef, string valuationRef, string allocationRef, string authorizationRef, string attemptId, string executor)
    {
        var state = store.Get<IntuitionState>(stateRef);
        var proposal = store.Get<IntuitionProposal>(proposalRef);
        var valuation = store.Get<IntuitionValuation>(valuationRef);
        var allocation = store.Get<IntuitionAllocation>(allocationRef);
        var authorization = store.Get<OwnerAuthorization>(authorizationRef);
        if (proposal.StateRef != stateRef || valuation.ProposalRef != proposalRef || allocation.StateRef != stateRef || authorization.AllocationRef != allocationRef) throw new InvalidOperationException("Attempt graph mismatch.");
        if (state.BaseWriteAllowed) throw new InvalidOperationException("Research attempts cannot run with base_write enabled.");
        if (allocation.Policy == "shadow-pareto-bootstrap-v1" || state.SelectionMode == "shadow-pareto-bootstrap-v1") throw new InvalidOperationException("shadow-pareto-bootstrap-v1 forbids execution attempts.");
        if (allocation.SelectedForExecution.Count == 0 || !allocation.SelectedForExecution.Contains(valuationRef, StringComparer.Ordinal)) throw new InvalidOperationException("Valuation was not selected for execution.");
        if (!allocation.ParetoFront.Contains(valuationRef, StringComparer.Ordinal)) throw new InvalidOperationException("Authorized attempt valuation is outside Pareto front.");
        if (!authorization.AuthorizedProposalRefs.Contains(proposalRef, StringComparer.Ordinal)) throw new InvalidOperationException("Proposal is not owner-authorized.");
        var attempt = new ResearchAttempt(Schemas.Attempt, attemptId, stateRef, proposalRef, valuationRef, allocationRef, authorizationRef, state.Budget, executor, DateTimeOffset.UtcNow.ToUnixTimeSeconds());
        var reference = store.Put(attempt);
        WriteResult(new Dictionary<string, object?> { ["attempt_ref"] = reference });
        return 0;
    }

    private static int Settle(ArtifactStore store, string input)
    {
        var settlement = CanonicalJson.DeserializeStrict<IntuitionSettlement>(File.ReadAllBytes(input));
        store.Get<ResearchAttempt>(settlement.AttemptRef);
        var reference = store.Put(settlement);
        WriteResult(new Dictionary<string, object?> { ["settlement_ref"] = reference, ["attempt_ref"] = settlement.AttemptRef });
        return 0;
    }

    private static int BuildRelease(ArtifactStore store, IReadOnlyDictionary<string, List<string>> options)
    {
        var stateRef = Required(options, "state-ref");
        var state = store.Get<IntuitionState>(stateRef);
        var proposalSetRef = Required(options, "proposal-set-ref");
        var critiqueSetRef = Required(options, "critique-set-ref");
        var valuationSetRef = Required(options, "valuation-set-ref");
        var allocationRef = Required(options, "allocation-ref");
        var attemptRefs = Many(options, "attempt-ref").Order(StringComparer.Ordinal).Distinct(StringComparer.Ordinal).ToArray();
        var settlementRefs = Many(options, "settlement-ref").Order(StringComparer.Ordinal).Distinct(StringComparer.Ordinal).ToArray();

        ValidateReleaseGraph(store, stateRef, state, proposalSetRef, critiqueSetRef, valuationSetRef, allocationRef, attemptRefs, settlementRefs);
        var release = new IntuitionRelease(
            Schemas.Release,
            stateRef,
            proposalSetRef,
            critiqueSetRef,
            valuationSetRef,
            allocationRef,
            attemptRefs,
            settlementRefs,
            state.ReleaseDigest,
            DateTimeOffset.UtcNow.ToUnixTimeSeconds());
        var reference = store.Put(release);
        WriteResult(new Dictionary<string, object?> { ["intuition_release_ref"] = reference });
        return 0;
    }

    private static void ValidateReleaseGraph(
        ArtifactStore store,
        string stateRef,
        IntuitionState state,
        string proposalSetRef,
        string critiqueSetRef,
        string valuationSetRef,
        string allocationRef,
        IReadOnlyList<string> attemptRefs,
        IReadOnlyList<string> settlementRefs)
    {
        var proposalSet = store.Get<IntuitionProposalSet>(proposalSetRef);
        if (proposalSet.StateRef != stateRef) throw new InvalidOperationException("Release proposal set belongs to a different state.");
        var proposals = proposalSet.ProposalRefs.ToDictionary(reference => reference, store.Get<IntuitionProposal>, StringComparer.Ordinal);
        if (proposals.Values.Any(proposal => proposal.StateRef != stateRef || !state.CandidateUniverse.Contains(proposal.CandidateEditRef, StringComparer.Ordinal)))
        {
            throw new InvalidOperationException("Release proposal graph does not belong to the state candidate universe.");
        }

        var critiqueSet = store.Get<IntuitionCritiqueSet>(critiqueSetRef);
        if (critiqueSet.StateRef != stateRef || critiqueSet.ProposalSetRef != proposalSetRef) throw new InvalidOperationException("Release critique set graph mismatch.");
        foreach (var critiqueRef in critiqueSet.CritiqueRefs)
        {
            var critique = store.Get<IntuitionCritique>(critiqueRef);
            if (!proposals.ContainsKey(critique.ProposalRef)) throw new InvalidOperationException("Release critique references a proposal outside its proposal set.");
        }

        var valuationSet = store.Get<IntuitionValuationSet>(valuationSetRef);
        if (valuationSet.StateRef != stateRef || valuationSet.ProposalSetRef != proposalSetRef || valuationSet.CritiqueSetRef != critiqueSetRef) throw new InvalidOperationException("Release valuation set graph mismatch.");
        var valuations = valuationSet.ValuationRefs.ToDictionary(reference => reference, store.Get<IntuitionValuation>, StringComparer.Ordinal);
        if (valuations.Values.Any(valuation => !proposals.ContainsKey(valuation.ProposalRef))) throw new InvalidOperationException("Release valuation references a proposal outside its proposal set.");

        var allocation = store.Get<IntuitionAllocation>(allocationRef);
        if (allocation.StateRef != stateRef || allocation.ValuationSetRef != valuationSetRef) throw new InvalidOperationException("Release allocation graph mismatch.");
        var allocatedValuations = allocation.ParetoFront.Concat(allocation.Dominated).Concat(allocation.Incomparable).Concat(allocation.SelectedForExecution);
        if (allocatedValuations.Any(reference => !valuations.ContainsKey(reference))) throw new InvalidOperationException("Release allocation references a valuation outside its valuation set.");

        if (state.SelectionMode == "shadow-pareto-bootstrap-v1" && (attemptRefs.Count != 0 || settlementRefs.Count != 0))
        {
            throw new InvalidOperationException("A shadow bootstrap release cannot contain attempts or settlements.");
        }

        var attempts = new Dictionary<string, ResearchAttempt>(StringComparer.Ordinal);
        foreach (var attemptRef in attemptRefs)
        {
            var attempt = store.Get<ResearchAttempt>(attemptRef);
            if (attempt.StateRef != stateRef || attempt.AllocationRef != allocationRef || !proposals.ContainsKey(attempt.ProposalRef) || !valuations.TryGetValue(attempt.ValuationRef, out var valuation) || valuation.ProposalRef != attempt.ProposalRef)
            {
                throw new InvalidOperationException("Release attempt graph mismatch.");
            }
            var authorization = store.Get<OwnerAuthorization>(attempt.AuthorizationRef);
            if (authorization.AllocationRef != allocationRef || !authorization.AuthorizedProposalRefs.Contains(attempt.ProposalRef, StringComparer.Ordinal)) throw new InvalidOperationException("Release attempt authorization graph mismatch.");
            attempts.Add(attemptRef, attempt);
        }

        foreach (var settlementRef in settlementRefs)
        {
            var settlement = store.Get<IntuitionSettlement>(settlementRef);
            if (!attempts.ContainsKey(settlement.AttemptRef)) throw new InvalidOperationException("Release settlement references an attempt outside the release.");
        }
    }

    private static int Calibrate(ArtifactStore store, string valuationSetRef, IReadOnlyList<string> settlementRefs)
    {
        var set = store.Get<IntuitionValuationSet>(valuationSetRef);
        var valuations = set.ValuationRefs.Select(reference => (reference, store.Get<IntuitionValuation>(reference))).ToArray();
        var settlements = settlementRefs.Order(StringComparer.Ordinal).Select(reference => (reference, store.Get<IntuitionSettlement>(reference))).ToArray();
        var report = Calibration.Build(valuationSetRef, valuations, settlements);
        var reference = store.Put(report);
        WriteResult(new Dictionary<string, object?> { ["calibration_ref"] = reference });
        return 0;
    }

    private static int Verify(ArtifactStore store, string kind, string reference)
    {
        _ = kind switch
        {
            "state" => (object)store.Get<IntuitionState>(reference),
            "proposal-set" => store.Get<IntuitionProposalSet>(reference),
            "critique-set" => store.Get<IntuitionCritiqueSet>(reference),
            "valuation-set" => store.Get<IntuitionValuationSet>(reference),
            "allocation" => store.Get<IntuitionAllocation>(reference),
            "attempt" => store.Get<ResearchAttempt>(reference),
            "settlement" => store.Get<IntuitionSettlement>(reference),
            "release" => store.Get<IntuitionRelease>(reference),
            _ => throw new InvalidOperationException($"Unknown verify kind '{kind}'.")
        };
        WriteResult(new Dictionary<string, object?> { ["verified_ref"] = reference, ["kind"] = kind });
        return 0;
    }

    private static IReadOnlyDictionary<string, List<string>> Parse(string[] args)
    {
        var result = new Dictionary<string, List<string>>(StringComparer.Ordinal);
        for (var index = 0; index < args.Length; index += 2)
        {
            if (!args[index].StartsWith("--", StringComparison.Ordinal) || index + 1 >= args.Length) throw new InvalidOperationException("Options use --name value pairs.");
            var name = args[index][2..];
            if (!result.TryGetValue(name, out var values)) result[name] = values = [];
            values.Add(args[index + 1]);
        }
        return result;
    }

    private static string Required(IReadOnlyDictionary<string, List<string>> options, string key) =>
        options.TryGetValue(key, out var values) && values.Count == 1 ? values[0] : throw new InvalidOperationException($"Expected exactly one --{key}.");

    private static IReadOnlyList<string> Many(IReadOnlyDictionary<string, List<string>> options, string key) =>
        options.TryGetValue(key, out var values) ? values : Array.Empty<string>();

    private static void WriteResult(object value) => Console.Write(Encoding.UTF8.GetString(CanonicalJson.Serialize(value)));
    private static int Fail(string message) { Console.Error.WriteLine(message); return 2; }
}
