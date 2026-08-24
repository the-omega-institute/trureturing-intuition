using Trureturing.Intuition.Core;

var tests = new (string Name, Action Run)[]
{
    ("artifact roundtrip and tamper refusal", ArtifactRoundtrip),
    ("duplicate JSON property rejected", DuplicatePropertyRejected),
    ("open worth dimension cannot carry a value", OpenMetricCannotCarryValue),
    ("shadow allocation selects nothing", ShadowAllocationSelectsNothing),
    ("Pareto dominance respects vector cost", ParetoDominance),
    ("partial residual cut is witness-cut", ResidualWitnessCut),
    ("finite observed universe cannot become formal cover", FiniteCoverBoundary),
    ("formal complete universe yields formal cover", FormalCover),
    ("temporal future leakage rejected", TemporalLeakageRejected),
    ("agent cannot settle", AgentCannotSettle),
    ("infrastructure failure cannot claim gain", InfrastructureFailureHasNoGain),
    ("state factory binds upstream receipt", StateBindsReceipt)
};

var failed = 0;
foreach (var test in tests)
{
    try { test.Run(); Console.WriteLine($"PASS {test.Name}"); }
    catch (Exception exception) { failed++; Console.Error.WriteLine($"FAIL {test.Name}: {exception.Message}"); }
}
Console.WriteLine($"{tests.Length - failed} passed, {failed} failed");
return failed == 0 ? 0 : 1;

static string Hash(char value) => "sha256:" + new string(value, 64);
static string Git(char value) => new(value, 40);
static VerificationBudget Budget(double wall = 10) => new(10, 20, 2, 1000, 0, wall, 1);
static MetricEvidence Measured(double value, char receipt) => new(MetricStatus.Measured, value, Hash(receipt));
static MetricEvidence Open() => new(MetricStatus.Open, null, null);
static WorthVector Worth(double n, double d, double s, double r) => new(Measured(n, 'a'), Measured(d, 'b'), Measured(s, 'c'), Measured(r, 'd'));
static OutcomeDistribution Distribution() => new(.4, .2, .1, .1, .05, .1, .05);

static void ArtifactRoundtrip()
{
    using var temp = new TempDirectory();
    var store = new ArtifactStore(temp.Path);
    var receipt = new TruthReleaseVerificationReceipt(Schemas.TruthReceipt, Hash('1'), Git('a'), Git('b'), Hash('2'), Hash('3'), "Trureturing.Truth", 1);
    var reference = store.Put(receipt);
    Assert.Equal(receipt, store.Get<TruthReleaseVerificationReceipt>(reference));
    var path = store.PathFor(reference);
    File.AppendAllText(path, " ");
    Assert.Throws(() => store.Get<TruthReleaseVerificationReceipt>(reference));
}

static void DuplicatePropertyRejected()
{
    var bytes = System.Text.Encoding.UTF8.GetBytes("{\"schema\":\"x\",\"schema\":\"y\"}\n");
    Assert.Throws(() => CanonicalJson.DeserializeStrict<Dictionary<string, string>>(bytes));
}

static void OpenMetricCannotCarryValue()
{
    var bad = new IntuitionValuation(Schemas.Valuation, Hash('1'), new WorthVector(new MetricEvidence(MetricStatus.Open, 1, null), Open(), Open(), Open()), Budget(), Distribution(), 0, 0, .5, Array.Empty<string>(), "valuer", 1);
    Assert.Throws(() => ContractValidator.Validate(bad));
}

static void ShadowAllocationSelectsNothing()
{
    var allocation = new IntuitionAllocation(Schemas.Allocation, Hash('1'), Hash('2'), "shadow-pareto-bootstrap-v1", new[] { Hash('3') }, Array.Empty<string>(), Array.Empty<string>(), Array.Empty<string>(), 1);
    ContractValidator.Validate(allocation);
    Assert.Equal(0, allocation.SelectedForExecution.Count);
}

static void ParetoDominance()
{
    var better = new IntuitionValuation(Schemas.Valuation, Hash('1'), Worth(1, 1, 1, 1), Budget(5), Distribution(), 2, 2, .1, Array.Empty<string>(), "v", 1);
    var worse = new IntuitionValuation(Schemas.Valuation, Hash('2'), Worth(.5, .5, .5, .5), Budget(10), Distribution(), 1, 1, .2, Array.Empty<string>(), "v", 1);
    var result = ParetoAnalyzer.Analyze(new[] { (Hash('a'), better), (Hash('b'), worse) });
    Assert.SequenceEqual(new[] { Hash('a') }, result.ParetoFront);
    Assert.SequenceEqual(new[] { Hash('b') }, result.Dominated);
}

static ResidualUniverse Universe(ResidualUniverseKind kind, string? formal = null) =>
    new(Schemas.ResidualUniverse, "u", Hash('a'), kind, new[] { Hash('1'), Hash('2') }, formal);

static CandidateEdit Candidate(params string[] cuts) =>
    new(Schemas.CandidateEdit, "c", CandidateKind.Bridge, new[] { Hash('a') }, new[] { Hash('b') }, "map", Array.Empty<string>(), Array.Empty<string>(), cuts.Order(StringComparer.Ordinal).ToArray(), "falsifier", "Lean");

static void ResidualWitnessCut()
{
    var result = ResidualCoverageAnalyzer.Analyze(Universe(ResidualUniverseKind.FiniteObserved), Candidate(Hash('1')));
    Assert.Equal(CoverageLevel.WitnessCut, result.Level);
}

static void FiniteCoverBoundary()
{
    var result = ResidualCoverageAnalyzer.Analyze(Universe(ResidualUniverseKind.FiniteObserved), Candidate(Hash('1'), Hash('2')));
    Assert.Equal(CoverageLevel.FiniteObservedCover, result.Level);
}

static void FormalCover()
{
    var result = ResidualCoverageAnalyzer.Analyze(Universe(ResidualUniverseKind.FormalComplete, Hash('f')), Candidate(Hash('1'), Hash('2')));
    Assert.Equal(CoverageLevel.FormalCover, result.Level);
}

static void TemporalLeakageRejected()
{
    var replay = new TemporalReplayCase(Schemas.ReplayCase, "case", Hash('1'), Hash('2'), 1, 2, Hash('3'), Array.Empty<string>(), Budget(), "p", new[] { Hash('a') }, new[] { Hash('a') }, Array.Empty<string>());
    Assert.Throws(() => ContractValidator.Validate(replay));
}

static void AgentCannotSettle()
{
    var settlement = new IntuitionSettlement(Schemas.Settlement, Hash('1'), ResearchOutcome.Proved, "agent", new[] { Hash('2') }, Budget(), 1, 0, "", 1);
    Assert.Throws(() => ContractValidator.Validate(settlement));
}

static void InfrastructureFailureHasNoGain()
{
    var settlement = new IntuitionSettlement(Schemas.Settlement, Hash('1'), ResearchOutcome.InfrastructureFailure, "ci", new[] { Hash('2') }, Budget(), 1, 0, "", 1);
    Assert.Throws(() => ContractValidator.Validate(settlement));
}

static void StateBindsReceipt()
{
    var receipt = new TruthReleaseVerificationReceipt(Schemas.TruthReceipt, Hash('1'), Git('a'), Git('b'), Hash('2'), Hash('3'), "Trureturing.Truth", 1);
    var request = new IntuitionRunRequest(Schemas.RunRequest, "run", Hash('4'), Hash('5'), Hash('6'), new[] { Hash('7') }, "cutoff", Budget(), "protocol", Hash('8'), "shadow-pareto-bootstrap-v1");
    var state = StateFactory.Create(request, receipt, Hash('4'));
    Assert.Equal(receipt.ReleaseDigest, state.ReleaseDigest);
    Assert.True(!state.BaseWriteAllowed);
}

static class Assert
{
    public static void True(bool condition) { if (!condition) throw new InvalidOperationException("Expected true."); }
    public static void Equal<T>(T expected, T actual) where T : notnull { if (!EqualityComparer<T>.Default.Equals(expected, actual)) throw new InvalidOperationException($"Expected {expected}, got {actual}."); }
    public static void SequenceEqual<T>(IEnumerable<T> expected, IEnumerable<T> actual) { if (!expected.SequenceEqual(actual)) throw new InvalidOperationException("Sequences differ."); }
    public static void Throws(Action action) { try { action(); } catch { return; } throw new InvalidOperationException("Expected exception."); }
}

sealed class TempDirectory : IDisposable
{
    public string Path { get; } = System.IO.Path.Combine(System.IO.Path.GetTempPath(), "intuition-test-" + Guid.NewGuid().ToString("N"));
    public TempDirectory() => Directory.CreateDirectory(Path);
    public void Dispose() => Directory.Delete(Path, recursive: true);
}
