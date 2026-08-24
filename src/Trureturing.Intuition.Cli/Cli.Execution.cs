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
        var release = new IntuitionRelease(
            Schemas.Release,
            stateRef,
            Required(options, "proposal-set-ref"),
            Required(options, "critique-set-ref"),
            Required(options, "valuation-set-ref"),
            Required(options, "allocation-ref"),
            Many(options, "attempt-ref").Order(StringComparer.Ordinal).Distinct(StringComparer.Ordinal).ToArray(),
            Many(options, "settlement-ref").Order(StringComparer.Ordinal).Distinct(StringComparer.Ordinal).ToArray(),
            state.ReleaseDigest,
            DateTimeOffset.UtcNow.ToUnixTimeSeconds());
        var reference = store.Put(release);
        WriteResult(new Dictionary<string, object?> { ["intuition_release_ref"] = reference });
        return 0;
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
