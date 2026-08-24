using System.Security.Cryptography;
using System.Text;
using System.Text.Encodings.Web;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace Trureturing.Intuition.Core;

public static class ResidualCoverageAnalyzer
{
    public static CoverageAssessment Analyze(ResidualUniverse universe, CandidateEdit candidate)
    {
        ContractValidator.Validate(universe);
        ContractValidator.Validate(candidate);
        var universeSet = universe.WitnessRefs.ToHashSet(StringComparer.Ordinal);
        foreach (var claimed in candidate.ClaimedResidualCuts)
        {
            if (!universeSet.Contains(claimed)) throw new InvalidOperationException($"Candidate claims residual cut outside frozen universe: {claimed}.");
        }
        var missing = universe.WitnessRefs.Except(candidate.ClaimedResidualCuts, StringComparer.Ordinal).Order(StringComparer.Ordinal).ToArray();
        CoverageLevel level;
        if (candidate.ClaimedResidualCuts.Count == 0) level = CoverageLevel.None;
        else if (missing.Length != 0) level = CoverageLevel.WitnessCut;
        else if (universe.Kind == ResidualUniverseKind.FormalComplete) level = CoverageLevel.FormalCover;
        else level = CoverageLevel.FiniteObservedCover;
        return new CoverageAssessment(level, candidate.ClaimedResidualCuts.Count, universe.WitnessRefs.Count, missing, universe.FormalCompletenessReceiptRef);
    }
}

public static class ParetoAnalyzer
{
    public static ParetoResult Analyze(IReadOnlyList<(string Ref, IntuitionValuation Value)> valuations)
    {
        var front = new SortedSet<string>(StringComparer.Ordinal);
        var dominated = new SortedSet<string>(StringComparer.Ordinal);
        var incomparable = new SortedSet<string>(StringComparer.Ordinal);
        foreach (var candidate in valuations)
        {
            var comparableToAll = true;
            var isDominated = false;
            foreach (var other in valuations)
            {
                if (candidate.Ref == other.Ref) continue;
                var relation = Compare(other.Value, candidate.Value);
                if (relation is null) comparableToAll = false;
                else if (relation > 0) isDominated = true;
            }
            if (isDominated) dominated.Add(candidate.Ref);
            else front.Add(candidate.Ref);
            if (!comparableToAll) incomparable.Add(candidate.Ref);
        }
        return new ParetoResult(front.ToArray(), dominated.ToArray(), incomparable.ToArray());
    }

    private static int? Compare(IntuitionValuation left, IntuitionValuation right)
    {
        var leftMetrics = new[] { left.Worth.Novelty, left.Worth.Readiness, left.Worth.Realization, left.Worth.ReceiptPotential };
        var rightMetrics = new[] { right.Worth.Novelty, right.Worth.Readiness, right.Worth.Realization, right.Worth.ReceiptPotential };
        if (leftMetrics.Any(static metric => metric.Status == MetricStatus.Open) || rightMetrics.Any(static metric => metric.Status == MetricStatus.Open)) return null;
        var atLeast = true;
        var strict = false;
        for (var i = 0; i < leftMetrics.Length; i++)
        {
            var l = leftMetrics[i].Value!.Value;
            var r = rightMetrics[i].Value!.Value;
            if (l < r) atLeast = false;
            if (l > r) strict = true;
        }
        var leftCost = CostComponents(left.PredictedCost);
        var rightCost = CostComponents(right.PredictedCost);
        for (var i = 0; i < leftCost.Length; i++)
        {
            if (leftCost[i] > rightCost[i]) atLeast = false;
            if (leftCost[i] < rightCost[i]) strict = true;
        }
        return atLeast && strict ? 1 : 0;
    }

    private static double[] CostComponents(VerificationBudget budget) =>
    [budget.VerifierCalls, budget.ExpandedStates, budget.GeneratedLemmas, budget.Tokens, budget.GpuSeconds, budget.WallSeconds, budget.HumanReviewMinutes];
}

public static class StateFactory
{
    public static IntuitionState Create(
        IntuitionRunRequest request,
        TruthReleaseVerificationReceipt receipt,
        string requestRef)
    {
        ContractValidator.Validate(request);
        ContractValidator.Validate(receipt);
        if (request.TruthReleaseReceiptRef != requestRef) throw new InvalidOperationException("Run request receipt ref does not match supplied receipt artifact.");
        var universeBytes = Encoding.UTF8.GetBytes(string.Join("\n", request.CandidateUniverse) + "\n");
        var digest = "sha256:" + Convert.ToHexString(SHA256.HashData(universeBytes)).ToLowerInvariant();
        return new IntuitionState(
            Schemas.State,
            request.RunId,
            request.TruthReleaseReceiptRef,
            receipt.ReleaseDigest,
            receipt.SourceCommit,
            receipt.SourceTree,
            receipt.TruthGraphRef,
            receipt.TruthExportRef,
            request.TargetInterfaceRef,
            request.ResidualUniverseRef,
            request.CandidateUniverse,
            digest,
            request.HistoryCutoff,
            request.Budget,
            request.VerificationProtocol,
            request.ModelSnapshot,
            request.SelectionMode,
            ScalarizationAllowed: false,
            BaseWriteAllowed: false);
    }
}

public static class Calibration
{
    public static CalibrationReport Build(
        string valuationSetRef,
        IReadOnlyList<(string Ref, IntuitionValuation Value)> valuations,
        IReadOnlyList<(string Ref, IntuitionSettlement Value)> settlements)
    {
        var byProposal = valuations.ToDictionary(static item => item.Value.ProposalRef, static item => item.Value, StringComparer.Ordinal);
        var brier = 0.0;
        var correct = 0;
        var wallError = 0.0;
        var predicted = Enum.GetValues<ResearchOutcome>().ToDictionary(static key => key.ToString(), static _ => 0, StringComparer.Ordinal);
        var actual = Enum.GetValues<ResearchOutcome>().ToDictionary(static key => key.ToString(), static _ => 0, StringComparer.Ordinal);
        var count = 0;
        foreach (var settlementPair in settlements)
        {
            var settlement = settlementPair.Value;
            var attemptProposal = settlement.Notes.StartsWith("proposal_ref=", StringComparison.Ordinal)
                ? settlement.Notes[13..].Split(';', 2)[0]
                : throw new InvalidOperationException("Settlement notes must bind proposal_ref=<artifact-ref>; for calibration.");
            if (!byProposal.TryGetValue(attemptProposal, out var valuation)) continue;
            count++;
            foreach (var outcome in Enum.GetValues<ResearchOutcome>())
            {
                var expected = outcome == settlement.Outcome ? 1.0 : 0.0;
                var delta = valuation.PredictedOutcomes.For(outcome) - expected;
                brier += delta * delta;
            }
            var top = valuation.PredictedOutcomes.TopOutcome();
            if (top == settlement.Outcome) correct++;
            predicted[top.ToString()]++;
            actual[settlement.Outcome.ToString()]++;
            wallError += Math.Abs(valuation.PredictedCost.WallSeconds - settlement.ActualCost.WallSeconds);
        }
        var refs = settlements.Select(static item => item.Ref).Order(StringComparer.Ordinal).ToArray();
        var report = new CalibrationReport(
            Schemas.Calibration,
            valuationSetRef,
            refs,
            count,
            count == 0 ? 0 : brier / count,
            count == 0 ? 0 : (double)correct / count,
            count == 0 ? 0 : wallError / count,
            predicted,
            actual);
        ContractValidator.Validate(report);
        return report;
    }

    public static CalibrationReport BuildIndependent(
        string valuationSetRef,
        IReadOnlyList<(string Ref, IntuitionValuation Value)> valuations,
        IReadOnlyList<(string Ref, IndependentSettlement Value)> settlements)
    {
        var byProposal = valuations.ToDictionary(static item => item.Value.ProposalRef, static item => item.Value, StringComparer.Ordinal);
        var brier = 0.0;
        var correct = 0;
        var predicted = Enum.GetValues<ResearchOutcome>().ToDictionary(static key => key.ToString(), static _ => 0, StringComparer.Ordinal);
        var actual = Enum.GetValues<ResearchOutcome>().ToDictionary(static key => key.ToString(), static _ => 0, StringComparer.Ordinal);
        var count = 0;
        foreach (var settlementPair in settlements)
        {
            var settlement = settlementPair.Value;
            if (!byProposal.TryGetValue(settlement.ProposalRef, out var valuation)) continue;
            count++;
            foreach (var outcome in Enum.GetValues<ResearchOutcome>())
            {
                var expected = outcome == settlement.Outcome ? 1.0 : 0.0;
                var delta = valuation.PredictedOutcomes.For(outcome) - expected;
                brier += delta * delta;
            }
            var top = valuation.PredictedOutcomes.TopOutcome();
            if (top == settlement.Outcome) correct++;
            predicted[top.ToString()]++;
            actual[settlement.Outcome.ToString()]++;
        }
        var refs = settlements.Select(static item => item.Ref).Order(StringComparer.Ordinal).ToArray();
        var report = new CalibrationReport(
            Schemas.Calibration,
            valuationSetRef,
            refs,
            count,
            count == 0 ? 0 : brier / count,
            count == 0 ? 0 : (double)correct / count,
            0,
            predicted,
            actual);
        ContractValidator.Validate(report);
        return report;
    }
}
