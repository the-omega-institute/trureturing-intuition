using System.Security.Cryptography;
using System.Text.Json;

namespace Trureturing.Intuition.Core;

public static class FormalizationWorthGateSchemas
{
    public const string Policy = "formalization-gate-policy.v1";
    public const string Evidence = "formalization-worth-evidence.v1";
    public const string Assessment = "formalization-gate-assessment.v1";
}

public static class FormalizationContributionRoutes
{
    public const string GithubUser = "github-user";
    public const string AnonymousService = "anonymous-service";
}

public static class FormalizationGateDecisions
{
    public const string Accept = "accept";
    public const string NeedsClarification = "needs-clarification";
    public const string Defer = "defer";
    public const string Duplicate = "duplicate";
    public const string Reject = "reject";
}

public sealed record FormalizationGateBudget(
    int MaximumVerifierCalls,
    int MaximumGeneratedLemmas,
    int MaximumWallSeconds,
    int MaximumHumanReviewMinutes);

public sealed record FormalizationGatePolicy(
    string Schema,
    string PolicyId,
    bool GithubUserEnabled,
    bool AnonymousServiceEnabled,
    bool RequireDuplicateSearch,
    bool RequireVerificationPlan,
    bool RequireFalsifier,
    bool RequireCounterfactualForBridge,
    int MaximumUncertaintyBasisPoints,
    FormalizationGateBudget Budget,
    IReadOnlyList<string> AcceptedNoveltyStatuses,
    IReadOnlyList<string> AcceptedStructuralLeverageStatuses);

public sealed record FormalizationGateRequestBinding(
    string RequestId,
    string TruthReleaseDigest,
    string ContributionRoute,
    string Action,
    string PrivacyClass);

public sealed record FormalizationWorthEvidence(
    string Schema,
    string EvidenceId,
    FormalizationGateRequestBinding Request,
    string AnalysisRef,
    string RouteAuthorizationRef,
    bool DuplicateSearchComplete,
    IReadOnlyList<string> DuplicateMatches,
    string? VerificationPlan,
    string? Falsifier,
    string? TopologyCounterfactualRef,
    string NoveltyStatus,
    string StructuralLeverageStatus,
    int? NoveltyBasisPoints,
    int? StructuralLeverageBasisPoints,
    int? ReuseValueBasisPoints,
    int? FrontierClosureBasisPoints,
    int? VerificationReadinessBasisPoints,
    int UncertaintyBasisPoints,
    int EstimatedVerifierCalls,
    int EstimatedGeneratedLemmas,
    int EstimatedWallSeconds,
    int EstimatedHumanReviewMinutes,
    string ScopeStatus,
    string PrivacyStatus,
    IReadOnlyList<string> EvidenceRefs);

public sealed record FormalizationWorthVector(
    int? Novelty,
    int? StructuralLeverage,
    int? ReuseValue,
    int? FrontierClosure,
    int? VerificationReadiness,
    int Uncertainty);

public sealed record FormalizationGateAssessmentContent(
    string PolicyId,
    string RequestId,
    string EvidenceId,
    string TruthReleaseDigest,
    string ContributionRoute,
    string Decision,
    bool FormalizationAllowed,
    FormalizationWorthVector ValueVector,
    IReadOnlyList<string> Reasons,
    IReadOnlyList<string> MissingInputs,
    IReadOnlyList<string> DuplicateMatches,
    IReadOnlyList<string> AllowedContributionRoutes,
    IReadOnlyList<string> EvidenceRefs,
    string EvaluatedAt,
    string ExpiresAt);

public sealed record FormalizationGateAssessment(
    string Schema,
    string AssessmentId,
    FormalizationGateAssessmentContent AssessmentContent);

public static class FormalizationWorthGate
{
    private static readonly JsonSerializerOptions CanonicalOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.SnakeCaseLower,
        WriteIndented = false
    };

    private static readonly HashSet<string> Routes = new(StringComparer.Ordinal)
    {
        FormalizationContributionRoutes.GithubUser,
        FormalizationContributionRoutes.AnonymousService
    };

    private static readonly HashSet<string> NoveltyStatuses = new(StringComparer.Ordinal)
    {
        "demonstrated",
        "plausible",
        "open",
        "duplicate"
    };

    private static readonly HashSet<string> StructuralStatuses = new(StringComparer.Ordinal)
    {
        "demonstrated",
        "plausible",
        "open"
    };

    public static FormalizationGateAssessment Evaluate(
        FormalizationGatePolicy policy,
        FormalizationGateRequestBinding request,
        FormalizationWorthEvidence evidence,
        DateTimeOffset evaluatedAt,
        TimeSpan lifetime)
    {
        ValidatePolicy(policy);
        ValidateRequest(request);
        ValidateEvidence(evidence);
        RequireEqual(evidence.Request.RequestId, request.RequestId, "request_id");
        RequireEqual(
            evidence.Request.TruthReleaseDigest,
            request.TruthReleaseDigest,
            "truth_release_digest");
        RequireEqual(
            evidence.Request.ContributionRoute,
            request.ContributionRoute,
            "contribution_route");
        RequireEqual(evidence.Request.Action, request.Action, "action");
        RequireEqual(
            evidence.Request.PrivacyClass,
            request.PrivacyClass,
            "privacy_class");
        if (lifetime <= TimeSpan.Zero || lifetime > TimeSpan.FromHours(24))
        {
            throw new InvalidDataException(
                "Gate assessment lifetime must be positive and no longer than 24 hours.");
        }

        var reasons = new List<string>();
        var missing = new List<string>();
        string decision;

        bool routeEnabled = request.ContributionRoute switch
        {
            FormalizationContributionRoutes.GithubUser => policy.GithubUserEnabled,
            FormalizationContributionRoutes.AnonymousService => policy.AnonymousServiceEnabled,
            _ => false
        };

        if (!routeEnabled)
        {
            decision = FormalizationGateDecisions.Reject;
            reasons.Add("The requested contribution route is disabled by policy.");
        }
        else if (evidence.ScopeStatus != "supported")
        {
            decision = FormalizationGateDecisions.Reject;
            reasons.Add("The proposed work is outside the supported formalization scope.");
        }
        else if (evidence.PrivacyStatus != "safe")
        {
            decision = FormalizationGateDecisions.Reject;
            reasons.Add("The evidence does not satisfy the selected privacy boundary.");
        }
        else if (evidence.DuplicateMatches.Count > 0 ||
                 evidence.NoveltyStatus == "duplicate")
        {
            decision = FormalizationGateDecisions.Duplicate;
            reasons.Add("Existing registered work already covers the proposed contribution.");
        }
        else
        {
            if (policy.RequireDuplicateSearch && !evidence.DuplicateSearchComplete)
            {
                missing.Add("Complete duplicate and existing-library search.");
            }
            if (policy.RequireVerificationPlan &&
                string.IsNullOrWhiteSpace(evidence.VerificationPlan))
            {
                missing.Add("Provide a bounded verification plan.");
            }
            if (policy.RequireFalsifier &&
                string.IsNullOrWhiteSpace(evidence.Falsifier))
            {
                missing.Add("Provide a falsifier or explicit failure boundary.");
            }
            if (policy.RequireCounterfactualForBridge &&
                request.Action == "add-bridge" &&
                string.IsNullOrWhiteSpace(evidence.TopologyCounterfactualRef))
            {
                missing.Add("Provide a Topology counterfactual for the proposed bridge.");
            }

            if (missing.Count > 0)
            {
                decision = FormalizationGateDecisions.NeedsClarification;
                reasons.Add("Required evidence is incomplete.");
            }
            else if (ExceedsBudget(policy, evidence))
            {
                decision = FormalizationGateDecisions.Defer;
                reasons.Add("Estimated verification cost exceeds the current policy budget.");
            }
            else if (evidence.UncertaintyBasisPoints >
                     policy.MaximumUncertaintyBasisPoints)
            {
                decision = FormalizationGateDecisions.Defer;
                reasons.Add("Uncertainty exceeds the current admission ceiling.");
            }
            else if (!policy.AcceptedNoveltyStatuses.Contains(
                         evidence.NoveltyStatus,
                         StringComparer.Ordinal) ||
                     !policy.AcceptedStructuralLeverageStatuses.Contains(
                         evidence.StructuralLeverageStatus,
                         StringComparer.Ordinal))
            {
                decision = FormalizationGateDecisions.Defer;
                reasons.Add(
                    "Novelty or structural leverage has not reached an admitted evidence status.");
            }
            else
            {
                decision = FormalizationGateDecisions.Accept;
                reasons.Add("The request has a bounded verification route.");
                reasons.Add("The evidence satisfies the current novelty and structural-value policy.");
            }
        }

        string[] orderedReasons = reasons
            .Distinct(StringComparer.Ordinal)
            .Order(StringComparer.Ordinal)
            .ToArray();
        string[] orderedMissing = missing
            .Distinct(StringComparer.Ordinal)
            .Order(StringComparer.Ordinal)
            .ToArray();
        string[] duplicates = evidence.DuplicateMatches
            .Distinct(StringComparer.Ordinal)
            .Order(StringComparer.Ordinal)
            .ToArray();
        string[] allowedRoutes = decision == FormalizationGateDecisions.Accept
            ? [request.ContributionRoute]
            : [];
        string[] evidenceRefs = evidence.EvidenceRefs
            .Append(evidence.AnalysisRef)
            .Append(evidence.RouteAuthorizationRef)
            .Distinct(StringComparer.Ordinal)
            .Order(StringComparer.Ordinal)
            .ToArray();
        string evaluated = evaluatedAt.ToUniversalTime().ToString("O");
        string expires = evaluatedAt.Add(lifetime).ToUniversalTime().ToString("O");
        var content = new FormalizationGateAssessmentContent(
            policy.PolicyId,
            request.RequestId,
            evidence.EvidenceId,
            request.TruthReleaseDigest,
            request.ContributionRoute,
            decision,
            decision == FormalizationGateDecisions.Accept,
            new FormalizationWorthVector(
                evidence.NoveltyBasisPoints,
                evidence.StructuralLeverageBasisPoints,
                evidence.ReuseValueBasisPoints,
                evidence.FrontierClosureBasisPoints,
                evidence.VerificationReadinessBasisPoints,
                evidence.UncertaintyBasisPoints),
            orderedReasons,
            orderedMissing,
            duplicates,
            allowedRoutes,
            evidenceRefs,
            evaluated,
            expires);
        string assessmentId = Digest(JsonSerializer.SerializeToUtf8Bytes(
            content,
            CanonicalOptions));
        return new FormalizationGateAssessment(
            FormalizationWorthGateSchemas.Assessment,
            assessmentId,
            content);
    }

    public static void ValidatePolicy(FormalizationGatePolicy policy)
    {
        ArgumentNullException.ThrowIfNull(policy);
        RequireEqual(
            policy.Schema,
            FormalizationWorthGateSchemas.Policy,
            "policy.schema");
        RequireDigest(policy.PolicyId, "policy_id");
        ArgumentNullException.ThrowIfNull(policy.Budget);
        if (policy.MaximumUncertaintyBasisPoints is < 0 or > 10_000)
        {
            throw new InvalidDataException(
                "maximum_uncertainty_basis_points must be from 0 through 10000.");
        }
        if (policy.Budget.MaximumVerifierCalls < 0 ||
            policy.Budget.MaximumGeneratedLemmas < 0 ||
            policy.Budget.MaximumWallSeconds < 0 ||
            policy.Budget.MaximumHumanReviewMinutes < 0)
        {
            throw new InvalidDataException("Formalization gate budgets must be non-negative.");
        }
        RequireSortedUnique(policy.AcceptedNoveltyStatuses, "accepted novelty statuses");
        RequireSortedUnique(
            policy.AcceptedStructuralLeverageStatuses,
            "accepted structural leverage statuses");
        if (policy.AcceptedNoveltyStatuses.Any(value => !NoveltyStatuses.Contains(value)) ||
            policy.AcceptedStructuralLeverageStatuses.Any(
                value => !StructuralStatuses.Contains(value)))
        {
            throw new InvalidDataException("Gate policy contains an unsupported evidence status.");
        }
    }

    public static void ValidateRequest(FormalizationGateRequestBinding request)
    {
        ArgumentNullException.ThrowIfNull(request);
        RequireText(request.RequestId, "request_id", 128);
        RequireDigest(request.TruthReleaseDigest, "truth_release_digest");
        if (!Routes.Contains(request.ContributionRoute))
        {
            throw new InvalidDataException("Unsupported contribution route.");
        }
        RequireText(request.Action, "action", 64);
        if (request.PrivacyClass is not "private-research" and
            not "public-contribution")
        {
            throw new InvalidDataException("Unsupported privacy class.");
        }
    }

    public static void ValidateEvidence(FormalizationWorthEvidence evidence)
    {
        ArgumentNullException.ThrowIfNull(evidence);
        RequireEqual(
            evidence.Schema,
            FormalizationWorthGateSchemas.Evidence,
            "evidence.schema");
        RequireDigest(evidence.EvidenceId, "evidence_id");
        ValidateRequest(evidence.Request);
        RequireDigest(evidence.AnalysisRef, "analysis_ref");
        RequireDigest(evidence.RouteAuthorizationRef, "route_authorization_ref");
        RequireSortedUnique(evidence.DuplicateMatches, "duplicate matches");
        OptionalText(evidence.VerificationPlan, "verification_plan", 8_000);
        OptionalText(evidence.Falsifier, "falsifier", 8_000);
        OptionalDigest(evidence.TopologyCounterfactualRef, "topology_counterfactual_ref");
        if (!NoveltyStatuses.Contains(evidence.NoveltyStatus) ||
            !StructuralStatuses.Contains(evidence.StructuralLeverageStatus))
        {
            throw new InvalidDataException("Evidence contains an unsupported value status.");
        }
        ValidateBasisPoints(evidence.NoveltyBasisPoints, "novelty_basis_points");
        ValidateBasisPoints(
            evidence.StructuralLeverageBasisPoints,
            "structural_leverage_basis_points");
        ValidateBasisPoints(evidence.ReuseValueBasisPoints, "reuse_value_basis_points");
        ValidateBasisPoints(
            evidence.FrontierClosureBasisPoints,
            "frontier_closure_basis_points");
        ValidateBasisPoints(
            evidence.VerificationReadinessBasisPoints,
            "verification_readiness_basis_points");
        if (evidence.UncertaintyBasisPoints is < 0 or > 10_000)
        {
            throw new InvalidDataException("uncertainty_basis_points is outside 0..10000.");
        }
        if (evidence.EstimatedVerifierCalls < 0 ||
            evidence.EstimatedGeneratedLemmas < 0 ||
            evidence.EstimatedWallSeconds < 0 ||
            evidence.EstimatedHumanReviewMinutes < 0)
        {
            throw new InvalidDataException("Estimated formalization costs must be non-negative.");
        }
        if ((evidence.ScopeStatus != "supported" &&
             evidence.ScopeStatus != "unsupported") ||
            (evidence.PrivacyStatus != "safe" &&
             evidence.PrivacyStatus != "unsafe"))
        {
            throw new InvalidDataException("Evidence scope or privacy status is unsupported.");
        }
        RequireSortedUnique(evidence.EvidenceRefs, "evidence refs");
        foreach (string reference in evidence.EvidenceRefs)
        {
            RequireDigest(reference, "evidence_ref");
        }
    }

    private static bool ExceedsBudget(
        FormalizationGatePolicy policy,
        FormalizationWorthEvidence evidence) =>
        evidence.EstimatedVerifierCalls > policy.Budget.MaximumVerifierCalls ||
        evidence.EstimatedGeneratedLemmas > policy.Budget.MaximumGeneratedLemmas ||
        evidence.EstimatedWallSeconds > policy.Budget.MaximumWallSeconds ||
        evidence.EstimatedHumanReviewMinutes > policy.Budget.MaximumHumanReviewMinutes;

    private static void ValidateBasisPoints(int? value, string name)
    {
        if (value is < 0 or > 10_000)
        {
            throw new InvalidDataException($"{name} is outside 0..10000.");
        }
    }

    private static void RequireSortedUnique(
        IReadOnlyList<string> values,
        string name)
    {
        ArgumentNullException.ThrowIfNull(values);
        string? previous = null;
        foreach (string value in values)
        {
            RequireText(value, name, 1_000);
            if (previous is not null &&
                StringComparer.Ordinal.Compare(previous, value) >= 0)
            {
                throw new InvalidDataException($"{name} must be ordinal-sorted and unique.");
            }
            previous = value;
        }
    }

    private static void OptionalText(string? value, string name, int maximum)
    {
        if (value is not null) RequireText(value, name, maximum);
    }

    private static void OptionalDigest(string? value, string name)
    {
        if (value is not null) RequireDigest(value, name);
    }

    private static void RequireText(string value, string name, int maximum)
    {
        if (string.IsNullOrWhiteSpace(value) || value.Length > maximum)
        {
            throw new InvalidDataException($"{name} is empty or exceeds {maximum} characters.");
        }
    }

    private static void RequireDigest(string value, string name)
    {
        if (value.Length != 71 ||
            !value.StartsWith("sha256:", StringComparison.Ordinal) ||
            value[7..].Any(character => character is not (>= '0' and <= '9') and
                not (>= 'a' and <= 'f')))
        {
            throw new InvalidDataException($"{name} must be sha256:<64 lowercase hex>.");
        }
    }

    private static void RequireEqual(string actual, string expected, string name)
    {
        if (!StringComparer.Ordinal.Equals(actual, expected))
        {
            throw new InvalidDataException($"{name} does not match its bound value.");
        }
    }

    private static string Digest(ReadOnlySpan<byte> bytes) =>
        "sha256:" + Convert.ToHexStringLower(SHA256.HashData(bytes));
}
