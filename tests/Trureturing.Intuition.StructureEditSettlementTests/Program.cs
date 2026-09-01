using System.Numerics;
using System.Text;
using Trureturing.Intuition.Core;

var tests = new (string Name, Action Run)[]
{
    ("settles verified candidate realized in later release", SettlesVerifiedAndRealized),
    ("keeps verified candidate open until later release", SettlesVerifiedNotYetRealized),
    ("marks redundant candidate separately", SettlesRedundantCandidate),
    ("calibrates positive counterfactual against formal refutation", SettlesRefutedPrediction),
    ("isolates infrastructure failure", SettlesInfrastructureFailure),
    ("rejects mixed delta coordinates", RejectsMixedDeltaCoordinates),
    ("rejects inconsistent delta summary", RejectsInconsistentDeltaSummary),
    ("rejects tampered formalization bytes", RejectsTamperedFormalization),
    ("creates no Base write or Formalize request", CreatesNoExecutionArtifacts)
};

int failed = 0;
foreach ((string name, Action run) in tests)
{
    try
    {
        run();
        Console.WriteLine($"PASS {name}");
    }
    catch (Exception exception)
    {
        failed++;
        Console.Error.WriteLine($"FAIL {name}: {exception.Message}");
    }
}
Console.WriteLine($"{tests.Length - failed} passed, {failed} failed");
return failed == 0 ? 0 : 1;

static void SettlesVerifiedAndRealized()
{
    using var temp = new TempDirectory();
    Fixture fixture = Fixture.Create(temp.Path);
    SettlementInput input = fixture.Input(
        StructureFormalizationOutcomes.Verified,
        edgeRelation: "added");
    StructureEditSettlementRegistration result = fixture.Register(input);
    Assert.Equal(
        StructureEditSettlementStatuses.VerifiedAndRealized,
        result.SettlementStatus);
    Assert.Equal("confirmed-structural-transfer", result.CalibrationClass);
    Assert.Equal(BigInteger.One, result.Counts.RealizedCount);
    Assert.Equal(BigInteger.Zero, result.Counts.NotRealizedCount);
    StructureEditSettlement settlement =
        StructureEditSettlementRegistrar.ReadSettlement(
            fixture.Store,
            result.SettlementRef);
    Assert.Equal("realized",
        settlement.SettlementContent.OperationSettlements[0].Outcome);
    Assert.Equal(fixture.ToRelease,
        settlement.SettlementContent.ToTruthReleaseDigest);
}

static void SettlesVerifiedNotYetRealized()
{
    using var temp = new TempDirectory();
    Fixture fixture = Fixture.Create(temp.Path);
    SettlementInput input = fixture.Input(
        StructureFormalizationOutcomes.Verified,
        edgeRelation: null);
    StructureEditSettlementRegistration result = fixture.Register(input);
    Assert.Equal(
        StructureEditSettlementStatuses.VerifiedNotYetRealized,
        result.SettlementStatus);
    Assert.Equal("formal-proof-awaiting-release", result.CalibrationClass);
    Assert.Equal(BigInteger.One, result.Counts.NotRealizedCount);
}

static void SettlesRedundantCandidate()
{
    using var temp = new TempDirectory();
    Fixture fixture = Fixture.Create(temp.Path);
    SettlementInput input = fixture.Input(
        StructureFormalizationOutcomes.Verified,
        edgeRelation: "retained");
    StructureEditSettlementRegistration result = fixture.Register(input);
    Assert.Equal(
        StructureEditSettlementStatuses.VerifiedNotYetRealized,
        result.SettlementStatus);
    Assert.Equal("candidate-redundant", result.CalibrationClass);
    Assert.Equal(BigInteger.One, result.Counts.AlreadyPresentCount);
}

static void SettlesRefutedPrediction()
{
    using var temp = new TempDirectory();
    Fixture fixture = Fixture.Create(temp.Path);
    SettlementInput input = fixture.Input(
        StructureFormalizationOutcomes.Refuted,
        edgeRelation: null);
    StructureEditSettlementRegistration result = fixture.Register(input);
    Assert.Equal(StructureEditSettlementStatuses.Refuted,
        result.SettlementStatus);
    Assert.Equal("counterfactual-overpredicted", result.CalibrationClass);
}

static void SettlesInfrastructureFailure()
{
    using var temp = new TempDirectory();
    Fixture fixture = Fixture.Create(temp.Path);
    SettlementInput input = fixture.Input(
        StructureFormalizationOutcomes.InfrastructureFailure,
        edgeRelation: null);
    StructureEditSettlementRegistration result = fixture.Register(input);
    Assert.Equal(StructureEditSettlementStatuses.InfrastructureFailure,
        result.SettlementStatus);
    Assert.Equal("infrastructure-only", result.CalibrationClass);
}

static void RejectsMixedDeltaCoordinates()
{
    using var temp = new TempDirectory();
    Fixture fixture = Fixture.Create(temp.Path);
    SettlementInput input = fixture.Input(
        StructureFormalizationOutcomes.Verified,
        edgeRelation: "added");
    TopologyAtlasDeltaPublicationCoordinate hostile = input.DeltaPublication with
    {
        FromTopologyAtlasDigest = Digest('f')
    };
    Assert.Throws(() => StructureEditSettlementRegistrar.Register(
        fixture.Store,
        fixture.ValuationRef,
        input.FormalizationPublication,
        input.FormalizationBytes,
        hostile,
        input.DeltaBytes));
}

static void RejectsInconsistentDeltaSummary()
{
    using var temp = new TempDirectory();
    Fixture fixture = Fixture.Create(temp.Path);
    SettlementInput input = fixture.Input(
        StructureFormalizationOutcomes.Verified,
        edgeRelation: "added",
        summaryEdgesAdded: 2);
    Assert.Throws(() => fixture.Register(input));
}

static void RejectsTamperedFormalization()
{
    using var temp = new TempDirectory();
    Fixture fixture = Fixture.Create(temp.Path);
    SettlementInput input = fixture.Input(
        StructureFormalizationOutcomes.Verified,
        edgeRelation: "added");
    byte[] changed = input.FormalizationBytes.ToArray();
    changed[^2] = changed[^2] == (byte)'0' ? (byte)'1' : (byte)'0';
    Assert.Throws(() => StructureEditSettlementRegistrar.Register(
        fixture.Store,
        fixture.ValuationRef,
        input.FormalizationPublication,
        changed,
        input.DeltaPublication,
        input.DeltaBytes));
}

static void CreatesNoExecutionArtifacts()
{
    using var temp = new TempDirectory();
    Fixture fixture = Fixture.Create(temp.Path);
    SettlementInput input = fixture.Input(
        StructureFormalizationOutcomes.Verified,
        edgeRelation: "added");
    _ = fixture.Register(input);
    string storePath = Path.Combine(temp.Path, "artifacts", "sha256");
    foreach (string path in Directory.EnumerateFiles(
        storePath,
        "*.json",
        SearchOption.AllDirectories))
    {
        string text = File.ReadAllText(path);
        Assert.True(!text.Contains("\"base_write_allowed\":true", StringComparison.Ordinal));
        Assert.True(!text.Contains("\"schema\":\"research-attempt.v1\"", StringComparison.Ordinal));
    }
}

sealed record SettlementInput(
    StructureFormalizationResultPublicationCoordinate FormalizationPublication,
    byte[] FormalizationBytes,
    TopologyAtlasDeltaPublicationCoordinate DeltaPublication,
    byte[] DeltaBytes);

sealed record Fixture(
    string Root,
    ArtifactStore Store,
    string CandidateRef,
    StructureEditCandidate Candidate,
    string ValuationRef,
    StructureCounterfactualValuation Valuation,
    string FromRelease,
    string ToRelease,
    string FromAtlas,
    string ToAtlas,
    string FromEvidence,
    string ToEvidence,
    string DeltaProfile,
    string DeltaProducer,
    string FormalizationRequestRef,
    string VerificationReceiptRef,
    string FormalArtifactRef)
{
    public static Fixture Create(string root)
    {
        var store = new ArtifactStore(Path.Combine(root, "artifacts"));
        string fromRelease = Digest('1');
        string toRelease = Digest('2');
        string fromAtlas = Digest('3');
        string toAtlas = Digest('4');
        string fromEvidence = Digest('5');
        string toEvidence = Digest('6');
        string atlasReceipt = Digest('7');
        string evidenceReceipt = Digest('8');
        string episodeRef = Digest('9');
        string episodeId = Digest('a');
        string observationRef = Digest('b');
        var candidateContent = new StructureEditCandidateContent(
            episodeRef,
            episodeId,
            observationRef,
            atlasReceipt,
            evidenceReceipt,
            fromRelease,
            Digest('c'),
            fromAtlas,
            fromEvidence,
            StructureEditKinds.AddBridge,
            1,
            "Add an explicit stable bridge",
            "Test one missing stable dependency.",
            "Create or shorten reachability.",
            "A cycle, failed proof, or absent release edge refutes transfer.",
            [
                new StructureGraphPatchOperation(
                    StructureGraphPatchOperations.AddEdge,
                    null,
                    null,
                    "node-a",
                    "node-b",
                    "gid:node-a",
                    "gid:node-b",
                    null)
            ],
            "human:lexa",
            "human-authored",
            "advisory",
            "unsubmitted",
            "2026-09-01T00:00:00Z");
        string candidateId = CanonicalJson.Sha256Reference(
            CanonicalJson.Serialize(candidateContent));
        var candidate = new StructureEditCandidate(
            StructureEditCandidateSchemas.Candidate,
            candidateId,
            candidateContent);
        string candidateRef = PutCanonical(store, candidate);

        var metrics = new StructureCounterfactualMetrics(
            3,
            0,
            1,
            1,
            0,
            0,
            1,
            0,
            0,
            2,
            2,
            1);
        var benefit = new StructureCounterfactualBenefitVector(
            3,
            1,
            0,
            1,
            1);
        var risk = new StructureCounterfactualRiskVector(
            0,
            0,
            0,
            2,
            2,
            3,
            false);
        var valuationContent = new StructureCounterfactualValuationContent(
            candidateRef,
            candidateId,
            episodeRef,
            episodeId,
            Digest('d'),
            Digest('d'),
            fromRelease,
            fromAtlas,
            fromEvidence,
            Digest('e'),
            new string('c', 40),
            true,
            false,
            ["gid:node-a", "gid:node-b"],
            [Cluster('1'), Cluster('2')],
            metrics,
            benefit,
            risk,
            "structural-upside",
            "exact-topology-counterfactual-only",
            "advisory");
        string valuationId = CanonicalJson.Sha256Reference(
            CanonicalJson.Serialize(valuationContent));
        var valuation = new StructureCounterfactualValuation(
            StructureCounterfactualSchemas.Valuation,
            valuationId,
            valuationContent);
        string valuationRef = PutCanonical(store, valuation);

        string requestRef = PutBlob(
            store,
            Encoding.UTF8.GetBytes("formalization request"));
        string verificationRef = PutBlob(
            store,
            Encoding.UTF8.GetBytes("verification receipt"));
        string formalArtifactRef = PutBlob(
            store,
            Encoding.UTF8.GetBytes("formal artifact"));
        return new Fixture(
            root,
            store,
            candidateRef,
            candidate,
            valuationRef,
            valuation,
            fromRelease,
            toRelease,
            fromAtlas,
            toAtlas,
            fromEvidence,
            toEvidence,
            Digest('f'),
            new string('d', 40),
            requestRef,
            verificationRef,
            formalArtifactRef);
    }

    public SettlementInput Input(
        string formalizationOutcome,
        string? edgeRelation,
        int? summaryEdgesAdded = null)
    {
        string? verification = formalizationOutcome ==
            StructureFormalizationOutcomes.InfrastructureFailure
                ? null
                : VerificationReceiptRef;
        string? formalArtifact = formalizationOutcome ==
            StructureFormalizationOutcomes.Verified
                ? FormalArtifactRef
                : null;
        var resultContent = new StructureFormalizationResultContent(
            CandidateRef,
            Candidate.CandidateId,
            FormalizationRequestRef,
            FromRelease,
            FromAtlas,
            formalizationOutcome,
            "trureturing-formalize",
            verification,
            formalArtifact,
            formalArtifact is null ? null : Digest('0'),
            [],
            "formalize-execution-evidence",
            "2026-09-01T01:00:00Z");
        string resultId = CanonicalJson.Sha256Reference(
            CanonicalJson.Serialize(resultContent));
        var result = new StructureFormalizationResult(
            StructureFormalizationResultSchemas.Result,
            resultId,
            resultContent);
        byte[] resultBytes = CanonicalJson.Serialize(result);
        var resultPublication =
            new StructureFormalizationResultPublicationCoordinate(
                StructureFormalizationResultSchemas.Publication,
                CanonicalJson.Sha256Reference(resultBytes),
                CandidateRef,
                Candidate.CandidateId,
                FormalizationRequestRef,
                FromRelease,
                FromAtlas,
                "trureturing-formalize",
                new string('e', 40));

        byte[] deltaBytes = Delta(edgeRelation, summaryEdgesAdded);
        var deltaPublication = new TopologyAtlasDeltaPublicationCoordinate(
            StructureEditSettlementSchemas.DeltaPublication,
            CanonicalJson.Sha256Reference(deltaBytes),
            FromRelease,
            ToRelease,
            FromAtlas,
            ToAtlas,
            FromEvidence,
            ToEvidence,
            DeltaProfile,
            DeltaProducer);
        return new SettlementInput(
            resultPublication,
            resultBytes,
            deltaPublication,
            deltaBytes);
    }

    public StructureEditSettlementRegistration Register(
        SettlementInput input) =>
        StructureEditSettlementRegistrar.Register(
            Store,
            ValuationRef,
            input.FormalizationPublication,
            input.FormalizationBytes,
            input.DeltaPublication,
            input.DeltaBytes);

    private byte[] Delta(
        string? edgeRelation,
        int? summaryEdgesAdded)
    {
        object[] edges = edgeRelation is null
            ? []
            :
            [
                new Dictionary<string, object?>
                {
                    ["stable_dependency_id"] = "gid:node-a",
                    ["stable_dependent_id"] = "gid:node-b",
                    ["relation"] = edgeRelation,
                    ["from_dependency_id"] = edgeRelation == "added" ? null : "node-a",
                    ["from_dependent_id"] = edgeRelation == "added" ? null : "node-b",
                    ["to_dependency_id"] = edgeRelation == "removed" ? null : "node-a",
                    ["to_dependent_id"] = edgeRelation == "removed" ? null : "node-b"
                }
            ];
        int added = summaryEdgesAdded
            ?? (edgeRelation == "added" ? 1 : 0);
        int removed = edgeRelation == "removed" ? 1 : 0;
        int retained = edgeRelation == "retained" ? 1 : 0;
        var value = new Dictionary<string, object?>
        {
            ["schema_version"] = "topology-atlas-delta.v1",
            ["from_truth_release_digest"] = FromRelease,
            ["to_truth_release_digest"] = ToRelease,
            ["from_topology_atlas_digest"] = FromAtlas,
            ["to_topology_atlas_digest"] = ToAtlas,
            ["from_evidence_digest"] = FromEvidence,
            ["to_evidence_digest"] = ToEvidence,
            ["algorithm_profile_digest"] = DeltaProfile,
            ["producer_commit"] = DeltaProducer,
            ["node_transitions"] = Array.Empty<object>(),
            ["edge_transitions"] = edges,
            ["cluster_lineage"] = Array.Empty<object>(),
            ["frontier_delta"] = new Dictionary<string, object?>
            {
                ["entered_frontier"] = Array.Empty<string>(),
                ["left_frontier"] = Array.Empty<string>()
            },
            ["summary"] = new Dictionary<string, object?>
            {
                ["nodes_added"] = 0,
                ["nodes_retired"] = 0,
                ["nodes_retained"] = 0,
                ["edges_added"] = added,
                ["edges_removed"] = removed,
                ["edges_retained"] = retained,
                ["cluster_continuations"] = 0,
                ["cluster_splits"] = 0,
                ["cluster_merges"] = 0,
                ["cluster_reorganizations"] = 0,
                ["clusters_new"] = 0,
                ["clusters_retired"] = 0
            }
        };
        return CanonicalJson.Serialize(value);
    }
}

static string PutCanonical<T>(ArtifactStore store, T value)
{
    byte[] bytes = CanonicalJson.Serialize(value);
    string reference = CanonicalJson.Sha256Reference(bytes);
    string path = store.PathFor(reference);
    Directory.CreateDirectory(Path.GetDirectoryName(path)!);
    File.WriteAllBytes(path, bytes);
    return reference;
}

static string PutBlob(ArtifactStore store, byte[] bytes)
{
    string reference = CanonicalJson.Sha256Reference(bytes);
    string path = store.PathFor(reference);
    Directory.CreateDirectory(Path.GetDirectoryName(path)!);
    File.WriteAllBytes(path, bytes);
    return reference;
}

static string Digest(char value) =>
    "sha256:" + new string(value, 64);

static string Cluster(char value) =>
    "cluster:sha256:" + new string(value, 64);

sealed class TempDirectory : IDisposable
{
    public TempDirectory()
    {
        Path = System.IO.Path.Combine(
            System.IO.Path.GetTempPath(),
            "trureturing-structure-settlement-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(Path);
    }

    public string Path { get; }

    public void Dispose()
    {
        if (Directory.Exists(Path)) Directory.Delete(Path, recursive: true);
    }
}

static class Assert
{
    public static void True(bool value)
    {
        if (!value) throw new InvalidOperationException("Expected true.");
    }

    public static void Equal<T>(T expected, T actual)
    {
        if (!EqualityComparer<T>.Default.Equals(expected, actual))
        {
            throw new InvalidOperationException(
                $"Expected '{expected}', got '{actual}'.");
        }
    }

    public static void Throws(Action action)
    {
        try
        {
            action();
        }
        catch
        {
            return;
        }
        throw new InvalidOperationException("Expected an exception.");
    }
}
