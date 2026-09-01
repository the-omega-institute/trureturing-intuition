using System.Text;
using System.Text.Json;
using Trureturing.Intuition.Core;

var tests = new (string Name, Action Run)[]
{
    ("values accepted structural upside without scalarization", ValuesStructuralUpside),
    ("rejects cycle counterfactual categorically", RejectsCycle),
    ("preserves mixed structural risk", PreservesMixedRisk),
    ("rejects counterfactual digest mismatch", RejectsDigestMismatch),
    ("rejects counterfactual binding mismatch", RejectsBindingMismatch),
    ("rejects graph patch operation count mismatch", RejectsOperationCountMismatch),
    ("stored valuation contains no weighted score", ContainsNoWeightedScore)
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

static void ValuesStructuralUpside()
{
    using var temp = new TempDirectory();
    Fixture fixture = Fixture.Create(temp.Path);
    byte[] bytes = fixture.Counterfactual(
        accepted: true,
        cycleRisk: false,
        gain: 4,
        loss: 0,
        compression: 2,
        newCutBridges: 0,
        removedCutBridges: 1,
        newInterfaces: 1,
        removedInterfaces: 0,
        operations: 1);
    StructureCounterfactualValuationRegistration result =
        fixture.Register(bytes);
    Assert.Equal("structural-upside", result.Classification);
    Assert.True(result.Accepted);
    Assert.True(!result.CycleRisk);
    Assert.Equal(new System.Numerics.BigInteger(4),
        result.BenefitVector.ReachabilityGain);
    Assert.Equal(new System.Numerics.BigInteger(2),
        result.BenefitVector.PathCompression);
    Assert.Equal(System.Numerics.BigInteger.Zero,
        result.RiskVector.ReachabilityLoss);
    StructureCounterfactualValuation value =
        StructureCounterfactualValuator.ReadValuation(
            fixture.Store,
            result.ValuationRef);
    Assert.Equal(result.ValuationId, value.ValuationId);
}

static void RejectsCycle()
{
    using var temp = new TempDirectory();
    Fixture fixture = Fixture.Create(temp.Path);
    byte[] bytes = fixture.Counterfactual(
        accepted: false,
        cycleRisk: true,
        gain: 0,
        loss: 0,
        compression: 0,
        newCutBridges: 0,
        removedCutBridges: 0,
        newInterfaces: 0,
        removedInterfaces: 0,
        operations: 1,
        includeAnalysis: false);
    StructureCounterfactualValuationRegistration result =
        fixture.Register(bytes);
    Assert.Equal("rejected-cycle", result.Classification);
    Assert.True(!result.Accepted);
    Assert.True(result.CycleRisk);
}

static void PreservesMixedRisk()
{
    using var temp = new TempDirectory();
    Fixture fixture = Fixture.Create(temp.Path);
    byte[] bytes = fixture.Counterfactual(
        accepted: true,
        cycleRisk: false,
        gain: 3,
        loss: 1,
        compression: 1,
        newCutBridges: 1,
        removedCutBridges: 0,
        newInterfaces: 1,
        removedInterfaces: 1,
        operations: 1);
    StructureCounterfactualValuationRegistration result =
        fixture.Register(bytes);
    Assert.Equal("mixed-structural-risk", result.Classification);
    Assert.Equal(new System.Numerics.BigInteger(1),
        result.RiskVector.ReachabilityLoss);
    Assert.Equal(new System.Numerics.BigInteger(1),
        result.RiskVector.NewCutBridges);
    Assert.Equal(new System.Numerics.BigInteger(1),
        result.RiskVector.RemovedInterfaces);
}

static void RejectsDigestMismatch()
{
    using var temp = new TempDirectory();
    Fixture fixture = Fixture.Create(temp.Path);
    byte[] bytes = fixture.Counterfactual(
        true, false, 1, 0, 0, 0, 0, 0, 0, 1);
    TopologyCounterfactualPublicationCoordinate publication =
        fixture.Publication(bytes) with
        {
            CounterfactualDigest = Digest('f')
        };
    Assert.Throws(() => StructureCounterfactualValuator.Register(
        fixture.Store,
        publication,
        bytes));
}

static void RejectsBindingMismatch()
{
    using var temp = new TempDirectory();
    Fixture fixture = Fixture.Create(temp.Path);
    byte[] bytes = fixture.Counterfactual(
        true,
        false,
        1,
        0,
        0,
        0,
        0,
        0,
        0,
        1,
        evidenceOverride: Digest('e'));
    Assert.Throws(() => fixture.Register(bytes));
}

static void RejectsOperationCountMismatch()
{
    using var temp = new TempDirectory();
    Fixture fixture = Fixture.Create(temp.Path);
    byte[] bytes = fixture.Counterfactual(
        true, false, 1, 0, 0, 0, 0, 0, 0, 2);
    Assert.Throws(() => fixture.Register(bytes));
}

static void ContainsNoWeightedScore()
{
    using var temp = new TempDirectory();
    Fixture fixture = Fixture.Create(temp.Path);
    byte[] bytes = fixture.Counterfactual(
        true, false, 2, 0, 1, 0, 0, 1, 0, 1);
    StructureCounterfactualValuationRegistration result =
        fixture.Register(bytes);
    string json = File.ReadAllText(fixture.Store.PathFor(result.ValuationRef));
    Assert.True(!json.Contains("weighted_score", StringComparison.Ordinal));
    Assert.True(!json.Contains("scalar_score", StringComparison.Ordinal));
    Assert.True(json.Contains("benefit_vector", StringComparison.Ordinal));
    Assert.True(json.Contains("risk_vector", StringComparison.Ordinal));
}

sealed record Fixture(
    string Root,
    ArtifactStore Store,
    StructureEditCandidate Candidate,
    string CandidateRef,
    string Release,
    string Certified,
    string Atlas,
    string Evidence,
    string CounterfactualProfile,
    string Producer)
{
    public static Fixture Create(string root)
    {
        var store = new ArtifactStore(Path.Combine(root, "artifacts"));
        string release = Digest('1');
        string certified = Digest('2');
        string atlas = Digest('3');
        string atlasReceipt = Digest('4');
        string evidenceProfile = Digest('5');
        string evidence = Digest('6');
        string producer = new string('c', 40);
        string sourceCommit = new string('7', 40);
        string sourceTree = new string('8', 40);
        byte[] evidenceBytes = EvidenceBytes(
            release,
            certified,
            atlas,
            evidenceProfile,
            producer);
        evidence = CanonicalJson.Sha256Reference(evidenceBytes);
        string evidencePath =
            TopologyAtlasEvidenceResearchInputRegistrar.EvidenceBlobPath(
                Path.Combine(root, "artifacts"),
                evidence);
        Directory.CreateDirectory(Path.GetDirectoryName(evidencePath)!);
        File.WriteAllBytes(evidencePath, evidenceBytes);
        var evidenceReceipt = new IntuitionTopologyAtlasEvidenceInputReceipt(
            TopologyAtlasEvidenceResearchInputSchemas.Receipt,
            Digest('9'),
            evidence,
            atlasReceipt,
            release,
            certified,
            atlas,
            evidence,
            evidenceProfile,
            sourceCommit,
            sourceTree,
            producer);
        string evidenceReceiptRef = store.Put(evidenceReceipt);

        var episodeContent = new StructureEditEpisodeContent(
            Digest('a'),
            Digest('b'),
            atlasReceipt,
            release,
            certified,
            atlas,
            Digest('d'),
            "node-pair",
            ["node-a", "node-b"],
            [],
            [],
            null,
            "compare",
            [
                StructureEditKinds.AddAbstraction,
                StructureEditKinds.AddBridge,
                StructureEditKinds.AddCounterexample,
                StructureEditKinds.ChangeRepresentation,
                StructureEditKinds.RegisterOpenQuestion
            ],
            3,
            "Could these nodes share an explicit bridge?",
            "private-research",
            StructureEditEpisodeSchemas.NormalizationProfile,
            "2026-09-01T00:00:00Z");
        string episodeId = CanonicalJson.Sha256Reference(
            CanonicalJson.Serialize(episodeContent));
        string episodeRef = store.Put(new StructureEditEpisode(
            StructureEditEpisodeSchemas.Episode,
            episodeId,
            episodeContent));
        var candidateContent = new StructureEditCandidateContent(
            episodeRef,
            episodeId,
            Digest('a'),
            atlasReceipt,
            evidenceReceiptRef,
            release,
            certified,
            atlas,
            evidence,
            StructureEditKinds.AddBridge,
            1,
            "Add an explicit bridge",
            "Test one missing dependency as an exact graph patch.",
            "Create or shorten reachability between the selected concepts.",
            "Reject when the patch creates a cycle or yields no exact structural gain.",
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
            "2026-09-01T00:01:00Z");
        StructureEditCandidateRegistration candidateRegistration =
            StructureEditCandidateRegistrar.Register(store, candidateContent);
        StructureEditCandidate candidate =
            StructureEditCandidateRegistrar.ReadCandidate(
                store,
                candidateRegistration.CandidateRef);
        return new Fixture(
            root,
            store,
            candidate,
            candidateRegistration.CandidateRef,
            release,
            certified,
            atlas,
            evidence,
            Digest('e'),
            producer);
    }

    public StructureCounterfactualValuationRegistration Register(byte[] bytes) =>
        StructureCounterfactualValuator.Register(
            Store,
            Publication(bytes),
            bytes);

    public TopologyCounterfactualPublicationCoordinate Publication(byte[] bytes) =>
        new(
            StructureCounterfactualSchemas.Publication,
            CandidateRef,
            Candidate.CandidateId,
            CanonicalJson.Sha256Reference(bytes),
            Release,
            Atlas,
            Evidence,
            CounterfactualProfile,
            Producer);

    public byte[] Counterfactual(
        bool accepted,
        bool cycleRisk,
        int gain,
        int loss,
        int compression,
        int newCutBridges,
        int removedCutBridges,
        int newInterfaces,
        int removedInterfaces,
        int operations,
        bool includeAnalysis = true,
        string? evidenceOverride = null)
    {
        object? analysis = includeAnalysis
            ? new Dictionary<string, object?>
            {
                ["affected_stable_node_ids"] = new[]
                {
                    "gid:node-a",
                    "gid:node-b"
                },
                ["touched_cluster_ids"] = new[]
                {
                    Cluster('1'),
                    Cluster('2')
                },
                ["reachable_pair_gain"] = gain,
                ["reachable_pair_loss"] = loss,
                ["path_compression"] = compression,
                ["shortest_path_changes"] = new object[]
                {
                    new Dictionary<string, object?>
                    {
                        ["stable_source_id"] = "gid:node-a",
                        ["stable_target_id"] = "gid:node-b",
                        ["before_distance"] = 3,
                        ["after_distance"] = 2
                    }
                },
                ["new_cut_bridges"] = Enumerable.Range(0, newCutBridges)
                    .Select(index => $"new-{index}")
                    .ToArray(),
                ["removed_cut_bridges"] = Enumerable.Range(0, removedCutBridges)
                    .Select(index => $"removed-{index}")
                    .ToArray(),
                ["new_interface_hypotheses"] = Enumerable.Range(0, newInterfaces)
                    .Select(index => $"new-interface-{index}")
                    .ToArray(),
                ["removed_interface_hypotheses"] = Enumerable.Range(0, removedInterfaces)
                    .Select(index => $"removed-interface-{index}")
                    .ToArray()
            }
            : null;
        var value = new Dictionary<string, object?>
        {
            ["schema_version"] = "topology-counterfactual.v1",
            ["truth_release_digest"] = Release,
            ["topology_atlas_digest"] = Atlas,
            ["topology_atlas_evidence_digest"] = evidenceOverride ?? Evidence,
            ["algorithm_profile_digest"] = CounterfactualProfile,
            ["producer_commit"] = Producer,
            ["accepted"] = accepted,
            ["cycle_risk"] = cycleRisk,
            ["cycle_witnesses"] = cycleRisk ? new[] { "gid:node-b", "gid:node-a" } : [],
            ["edit_operation_count"] = operations,
            ["analysis"] = analysis
        };
        return CanonicalJson.Serialize(value);
    }

    private static byte[] EvidenceBytes(
        string release,
        string certified,
        string atlas,
        string profile,
        string producer)
    {
        var value = new Dictionary<string, object?>
        {
            ["schema_version"] = "topology-atlas-evidence.v1",
            ["truth_release_digest"] = release,
            ["certified_topology_digest"] = certified,
            ["topology_atlas_digest"] = atlas,
            ["algorithm_profile_digest"] = profile,
            ["producer_commit"] = producer,
            ["maximum_witnesses"] = 8,
            ["node_identities"] = new object[]
            {
                Identity("node-a"),
                Identity("node-b")
            },
            ["node_traits"] = new object[]
            {
                Traits("node-a", "foundation"),
                Traits("node-b", "frontier-adjacent")
            },
            ["cluster_interfaces"] = Array.Empty<object>(),
            ["affinity_witnesses"] = Array.Empty<object>()
        };
        return CanonicalJson.Serialize(value);
    }

    private static Dictionary<string, object?> Identity(string node) => new()
    {
        ["node_id"] = node,
        ["stable_node_id"] = "gid:" + node,
        ["identity_basis"] = "truth-gid",
        ["gid"] = "gid:" + node,
        ["source_path"] = node + ".lean",
        ["module_name"] = null
    };

    private static Dictionary<string, object?> Traits(
        string node,
        string role) => new()
    {
        ["node_id"] = node,
        ["stable_node_id"] = "gid:" + node,
        ["primary_role"] = role,
        ["structural_traits"] = new[] { role },
        ["evidence"] = new object[]
        {
            new Dictionary<string, object?>
            {
                ["trait"] = role,
                ["rule"] = "primary-role",
                ["integer_value"] = null,
                ["rational_value"] = null,
                ["witness_node_ids"] = Array.Empty<string>()
            }
        }
    };
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
            "trureturing-counterfactual-" + Guid.NewGuid().ToString("N"));
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
