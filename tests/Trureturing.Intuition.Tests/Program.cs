using Trureturing.Intuition.Core;

var tests = new (string Name, Action Run)[]
{
    ("artifact roundtrip and tamper refusal", ArtifactRoundtrip),
    ("truth receipt identity is bound", TruthReceiptIdentityBound),
    ("duplicate JSON property rejected", DuplicatePropertyRejected),
    ("candidate edit self-cycle rejected", CandidateSelfCycleRejected),
    ("candidate edit set cycle rejected", CandidateSetCycleRejected),
    ("open worth dimension cannot carry a value", OpenMetricCannotCarryValue),
    ("shadow allocation selects nothing", ShadowAllocationSelectsNothing),
    ("Pareto dominance respects vector cost", ParetoDominance),
    ("partial residual cut is witness-cut", ResidualWitnessCut),
    ("finite observed universe cannot become formal cover", FiniteCoverBoundary),
    ("formal complete universe yields formal cover", FormalCover),
    ("temporal future leakage rejected", TemporalLeakageRejected),
    ("agent cannot settle", AgentCannotSettle),
    ("infrastructure failure cannot claim gain", InfrastructureFailureHasNoGain),
    ("state factory binds upstream receipt", StateBindsReceipt),
    ("CLI blocks bootstrap attempts and release graph mismatches", CliSafetyInvariants)
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

static void TruthReceiptIdentityBound()
{
    var fabricated = new TruthReleaseVerificationReceipt(Schemas.TruthReceipt, Hash('1'), Git('a'), Git('b'), Hash('2'), Hash('3'), "local-script", 1);
    Assert.Throws(() => ContractValidator.Validate(fabricated));
    ContractValidator.Validate(fabricated with { VerifiedBy = Schemas.TruthVerifierIdentity });
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

static void CandidateSelfCycleRejected()
{
    var reference = Hash('a');
    var candidate = new CandidateEdit(Schemas.CandidateEdit, "self", CandidateKind.Bridge, new[] { reference }, new[] { reference }, "map", Array.Empty<string>(), Array.Empty<string>(), Array.Empty<string>(), "falsifier", "Lean");
    Assert.Throws(() => ContractValidator.Validate(candidate));
}

static void CandidateSetCycleRejected()
{
    var first = new CandidateEdit(Schemas.CandidateEdit, "first", CandidateKind.Bridge, new[] { Hash('a') }, new[] { Hash('b') }, "map", Array.Empty<string>(), Array.Empty<string>(), Array.Empty<string>(), "falsifier", "Lean");
    var second = new CandidateEdit(Schemas.CandidateEdit, "second", CandidateKind.Bridge, new[] { Hash('b') }, new[] { Hash('a') }, "map", Array.Empty<string>(), Array.Empty<string>(), Array.Empty<string>(), "falsifier", "Lean");
    Assert.Throws(() => ContractValidator.ValidateCandidateEditSet(new[] { first, second }));
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

static void CliSafetyInvariants()
{
    using var temp = new TempDirectory();
    var store = new ArtifactStore(temp.Path);
    var state = new IntuitionState(Schemas.State, "run", Hash('4'), Hash('5'), Git('a'), Git('b'), Hash('6'), Hash('7'), Hash('8'), Hash('9'), new[] { Hash('a') }, Hash('c'), "cutoff", Budget(), "protocol", Hash('d'), "shadow-pareto-bootstrap-v1", false, false);
    var stateRef = store.Put(state);
    var proposalRef = store.Put(new IntuitionProposal(Schemas.Proposal, "proposal", stateRef, Hash('a'), "seat", Array.Empty<string>(), new DiscoveryLedger(CatalogStatus.Unsearched, SemanticStatus.Unknown, CertificationStatus.Unattempted), Hash('2'), "falsifier", 1));
    var valuationRef = store.Put(new IntuitionValuation(Schemas.Valuation, proposalRef, Worth(1, 1, 1, 1), Budget(), Distribution(), 0, 0, .1, Array.Empty<string>(), "valuer", 1));
    var allocationRef = store.Put(new IntuitionAllocation(Schemas.Allocation, stateRef, Hash('3'), "shadow-pareto-bootstrap-v1", new[] { valuationRef }, Array.Empty<string>(), Array.Empty<string>(), Array.Empty<string>(), 1));
    var authorizationRef = store.Put(new OwnerAuthorization(Schemas.Authorization, allocationRef, new[] { proposalRef }, "owner", "test", 1));
    var before = Directory.EnumerateFiles(temp.Path, "*.json", SearchOption.AllDirectories).Count();
    var attemptExit = Cli.RunAsync(new[] { "attempt", "--root", temp.Path, "--state-ref", stateRef, "--proposal-ref", proposalRef, "--valuation-ref", valuationRef, "--allocation-ref", allocationRef, "--authorization-ref", authorizationRef, "--attempt-id", "attempt", "--executor", "executor" }).GetAwaiter().GetResult();
    Assert.Equal(2, attemptExit);
    Assert.Equal(before, Directory.EnumerateFiles(temp.Path, "*.json", SearchOption.AllDirectories).Count());

    var proposalSetRef = store.Put(new IntuitionProposalSet(Schemas.ProposalSet, stateRef, new[] { proposalRef }));
    var critiqueRef = store.Put(new IntuitionCritique(Schemas.Critique, proposalRef, "lens", "approve", Array.Empty<string>(), Array.Empty<string>(), "reviewer"));
    var critiqueSetRef = store.Put(new IntuitionCritiqueSet(Schemas.CritiqueSet, stateRef, proposalSetRef, new[] { critiqueRef }));
    var valuationSetRef = store.Put(new IntuitionValuationSet(Schemas.ValuationSet, stateRef, proposalSetRef, critiqueSetRef, new[] { valuationRef }));
    var linkedAllocationRef = store.Put(new IntuitionAllocation(Schemas.Allocation, stateRef, valuationSetRef, "shadow-pareto-bootstrap-v1", new[] { valuationRef }, Array.Empty<string>(), Array.Empty<string>(), Array.Empty<string>(), 1));
    var releaseArgs = new[] { "build-release", "--root", temp.Path, "--state-ref", stateRef, "--proposal-set-ref", proposalSetRef, "--critique-set-ref", critiqueSetRef, "--valuation-set-ref", valuationSetRef, "--allocation-ref", linkedAllocationRef };
    Assert.Equal(0, Cli.RunAsync(releaseArgs).GetAwaiter().GetResult());
    var mismatchedSetRef = store.Put(new IntuitionProposalSet(Schemas.ProposalSet, Hash('e'), new[] { proposalRef }));
    var mismatchArgs = releaseArgs.Select(value => value == proposalSetRef ? mismatchedSetRef : value).ToArray();
    Assert.Equal(2, Cli.RunAsync(mismatchArgs).GetAwaiter().GetResult());
    var missingArgs = releaseArgs.Select(value => value == linkedAllocationRef ? Hash('f') : value).ToArray();
    Assert.Equal(2, Cli.RunAsync(missingArgs).GetAwaiter().GetResult());
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
