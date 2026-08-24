using System.Security.Cryptography;
using System.Text;
using System.Text.Encodings.Web;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace Trureturing.Intuition.Core;

public static partial class ContractValidator
{
    private const string LowerHex = "0123456789abcdef";

public static void RequireArtifactRef(string? value, string name)
    {
        if (value is null || !value.StartsWith("sha256:", StringComparison.Ordinal) || value.Length != 71 || !IsLowerHex(value.AsSpan(7)))
        {
            throw new InvalidOperationException($"{name} is not sha256:<64 lowercase hex>.");
        }
    }

    private static void RequireSchema(string actual, string expected)
    {
        if (!string.Equals(actual, expected, StringComparison.Ordinal)) throw new InvalidOperationException($"Expected schema {expected}, got {actual}.");
    }

    private static void RequireGitId(string value, string name)
    {
        if (value.Length is not (40 or 64) || !IsLowerHex(value.AsSpan())) throw new InvalidOperationException($"{name} is not a canonical lowercase Git object id.");
    }

    private static bool IsLowerHex(ReadOnlySpan<char> value)
    {
        foreach (var character in value)
        {
            if (LowerHex.IndexOf(character) < 0) return false;
        }
        return true;
    }

    private static void RequireIdentifier(string value, string name)
    {
        RequireNonEmpty(value, name);
        if (value.Any(static character => !(char.IsAsciiLetterOrDigit(character) || character is '-' or '_' or '.' or ':')))
        {
            throw new InvalidOperationException($"{name} contains unsupported characters.");
        }
    }

    private static void RequireNonEmpty(string? value, string name)
    {
        if (string.IsNullOrWhiteSpace(value)) throw new InvalidOperationException($"{name} is empty.");
    }

    private static void RequireSortedUniqueRefs(IReadOnlyList<string> values, string name)
    {
        foreach (var value in values) RequireArtifactRef(value, name);
        RequireSortedUniqueStrings(values, name);
    }

    private static void RequireSortedUniqueStrings(IReadOnlyList<string> values, string name)
    {
        for (var index = 1; index < values.Count; index++)
        {
            if (StringComparer.Ordinal.Compare(values[index - 1], values[index]) >= 0)
            {
                throw new InvalidOperationException($"{name} must be strictly ordinal-sorted and unique.");
            }
        }
    }

    private static void ValidateMetric(MetricEvidence metric, string name)
    {
        if (metric.Status == MetricStatus.Open)
        {
            if (metric.Value is not null || metric.ReceiptRef is not null) throw new InvalidOperationException($"{name}: open metric must not carry value or receipt.");
        }
        else
        {
            if (metric.Value is null || !double.IsFinite(metric.Value.Value)) throw new InvalidOperationException($"{name}: measured metric requires a finite value.");
            RequireArtifactRef(metric.ReceiptRef, name + ".receipt_ref");
        }
    }

    private static void ValidateDistribution(OutcomeDistribution distribution)
    {
        foreach (var outcome in Enum.GetValues<ResearchOutcome>())
        {
            var probability = distribution.For(outcome);
            if (!double.IsFinite(probability) || probability < 0 || probability > 1) throw new InvalidOperationException("Outcome probabilities must be in [0,1].");
        }
        if (Math.Abs(distribution.Sum - 1.0) > 1e-9) throw new InvalidOperationException("Outcome probabilities must sum to one.");
    }

    private static void ValidateBudget(VerificationBudget budget)
    {
        if (budget.VerifierCalls < 0 || budget.ExpandedStates < 0 || budget.GeneratedLemmas < 0 || budget.Tokens < 0) throw new InvalidOperationException("Budget integer component is negative.");
        RequireFiniteNonNegative(budget.GpuSeconds, nameof(budget.GpuSeconds));
        RequireFiniteNonNegative(budget.WallSeconds, nameof(budget.WallSeconds));
        RequireFiniteNonNegative(budget.HumanReviewMinutes, nameof(budget.HumanReviewMinutes));
    }

    private static void RequireFiniteNonNegative(double value, string name)
    {
        if (!double.IsFinite(value) || value < 0) throw new InvalidOperationException($"{name} must be finite and non-negative.");
    }
}
