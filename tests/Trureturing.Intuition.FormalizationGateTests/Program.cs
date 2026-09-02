using Trureturing.Intuition.Core;

var tests = new (string Name, Action Run)[]
{
    ("accept github user", AcceptGithubUser),
    ("accept anonymous service", AcceptAnonymousService),
    ("duplicate is separated from admission", Duplicate),
    ("missing evidence asks for clarification", NeedsClarification),
    ("high cost is deferred", DeferCost),
    ("disabled route is rejected", RejectDisabledRoute),
    ("request substitution is rejected", RejectSubstitution),
    ("assessment identity is deterministic", DeterministicIdentity),
    ("assessment has no scalar worth", NoScalarWorth)
};

int failed = 0;
foreach (var test in tests)
{
    try
    {
        test.Run();
        Console.WriteLine($"PASS {test.Name}");
    }
    catch (Exception exception)
    {
        failed++;
        Console.Error.WriteLine($"FAIL {test.Name}: {exception.Message}");
    }
}
Console.WriteLine($"{tests.Length - failed} passed, {failed} failed");
return failed == 0 ? 0 : 1;

static string Hash(char value) => "sha256:" + new string(value, 64);

static FormalizationGatePolicy Policy(
    bool github = true,
    bool anonymous = true) => new(
        FormalizationWorthGateSchemas.Policy,
        Hash('1'),
        github,
        anonymous,
        true,
        true,
        true,
        true,
        2_500,
        new FormalizationGateBudget(100, 20, 3_600, 120),
        ["demonstrated", "plausible"],
        ["demonstrated", "plausible"]);

static FormalizationGateRequestBinding Request(
    string route = FormalizationContributionRoutes.GithubUser,
    string action = "add-bridge") => new(
        "gate_request_example",
        Hash('2'),
        route,
        action,
        "public-contribution");

static FormalizationWorthEvidence Evidence(
    FormalizationGateRequestBinding? request = null,
    IReadOnlyList<string>? duplicates = null,
    bool duplicateSearchComplete = true,
    string? verificationPlan = "Run library discovery, construct one lemma, and check the exact target.",
    string? falsifier = "A model satisfying the prerequisites while refuting the target.",
    string? counterfactual = null,
    int uncertainty = 1_200,
    int verifierCalls = 20,
    string novelty = "demonstrated",
    string leverage = "demonstrated",
    string scope = "supported",
    string privacy = "safe")
{
    FormalizationGateRequestBinding binding = request ?? Request();
    return new FormalizationWorthEvidence(
        FormalizationWorthGateSchemas.Evidence,
        Hash('3'),
        binding,
        Hash('4'),
        Hash('5'),
        duplicateSearchComplete,
        duplicates ?? [],
        verificationPlan,
        falsifier,
        counterfactual ?? (binding.Action == "add-bridge" ? Hash('6') : null),
        novelty,
        leverage,
        7_500,
        8_000,
        6_000,
        7_000,
        7_200,
        uncertainty,
        verifierCalls,
        4,
        900,
        30,
        scope,
        privacy,
        [Hash('7')]);
}

static FormalizationGateAssessment Assess(
    FormalizationGatePolicy? policy = null,
    FormalizationGateRequestBinding? request = null,
    FormalizationWorthEvidence? evidence = null) =>
    FormalizationWorthGate.Evaluate(
        policy ?? Policy(),
        request ?? Request(),
        evidence ?? Evidence(),
        DateTimeOffset.Parse("2026-09-02T00:00:00Z"),
        TimeSpan.FromHours(1));

static void AcceptGithubUser()
{
    FormalizationGateAssessment result = Assess();
    Equal(FormalizationGateDecisions.Accept, result.AssessmentContent.Decision);
    True(result.AssessmentContent.FormalizationAllowed);
    Sequence(
        [FormalizationContributionRoutes.GithubUser],
        result.AssessmentContent.AllowedContributionRoutes);
}

static void AcceptAnonymousService()
{
    FormalizationGateRequestBinding request = Request(
        FormalizationContributionRoutes.AnonymousService,
        "add-subgoal");
    FormalizationGateAssessment result = Assess(
        request: request,
        evidence: Evidence(request));
    Equal(FormalizationGateDecisions.Accept, result.AssessmentContent.Decision);
    Sequence(
        [FormalizationContributionRoutes.AnonymousService],
        result.AssessmentContent.AllowedContributionRoutes);
}

static void Duplicate()
{
    FormalizationGateAssessment result = Assess(
        evidence: Evidence(duplicates: ["candidate:existing"]));
    Equal(FormalizationGateDecisions.Duplicate, result.AssessmentContent.Decision);
    True(!result.AssessmentContent.FormalizationAllowed);
    Sequence(["candidate:existing"], result.AssessmentContent.DuplicateMatches);
}

static void NeedsClarification()
{
    FormalizationGateAssessment result = Assess(
        evidence: Evidence(
            duplicateSearchComplete: false,
            verificationPlan: null,
            falsifier: null,
            counterfactual: null));
    Equal(
        FormalizationGateDecisions.NeedsClarification,
        result.AssessmentContent.Decision);
    True(result.AssessmentContent.MissingInputs.Count >= 3);
}

static void DeferCost()
{
    FormalizationGateAssessment result = Assess(
        evidence: Evidence(verifierCalls: 101));
    Equal(FormalizationGateDecisions.Defer, result.AssessmentContent.Decision);
}

static void RejectDisabledRoute()
{
    FormalizationGateAssessment result = Assess(policy: Policy(github: false));
    Equal(FormalizationGateDecisions.Reject, result.AssessmentContent.Decision);
}

static void RejectSubstitution()
{
    FormalizationGateRequestBinding request = Request();
    FormalizationWorthEvidence evidence = Evidence(
        request with { TruthReleaseDigest = Hash('9') });
    Throws(() => Assess(request: request, evidence: evidence));
}

static void DeterministicIdentity()
{
    FormalizationGateAssessment first = Assess();
    FormalizationGateAssessment second = Assess();
    Equal(first.AssessmentId, second.AssessmentId);
}

static void NoScalarWorth()
{
    string[] names = typeof(FormalizationGateAssessmentContent)
        .GetProperties()
        .Select(property => property.Name)
        .ToArray();
    True(!names.Contains("ScalarScore", StringComparer.Ordinal));
    True(names.Contains("ValueVector", StringComparer.Ordinal));
}

static void Equal<T>(T expected, T actual)
{
    if (!EqualityComparer<T>.Default.Equals(expected, actual))
    {
        throw new InvalidOperationException($"Expected {expected}, observed {actual}.");
    }
}

static void True(bool value)
{
    if (!value) throw new InvalidOperationException("Expected true.");
}

static void Sequence<T>(IEnumerable<T> expected, IEnumerable<T> actual)
{
    if (!expected.SequenceEqual(actual))
    {
        throw new InvalidOperationException("Sequences differ.");
    }
}

static void Throws(Action action)
{
    try
    {
        action();
    }
    catch (InvalidDataException)
    {
        return;
    }
    throw new InvalidOperationException("Expected InvalidDataException.");
}
