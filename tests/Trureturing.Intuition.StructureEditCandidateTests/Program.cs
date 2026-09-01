using System.Text.Json;
using Trureturing.Intuition.Core;

var tests = new (string Name, Action Run)[]
{
    ("registers one exact episode-bound candidate idempotently", RegistersCandidate),
    ("rejects candidate kind outside episode algebra", RejectsDisallowedKind),
    ("rejects candidate ordinal beyond episode budget", RejectsOrdinalBeyondLimit),
    ("rejects graph patch endpoint outside stable evidence", RejectsUnknownStableEndpoint),
    ("rejects an add-node collision with stable evidence", RejectsAddedIdentityCollision),
    ("rejects tampered stored candidate bytes", RejectsTamperedCandidate),
    ("registration creates no formalization request", CreatesNoFormalizationRequest)
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

static void RegistersCandidate()
{
    using var temp = new TempDirectory();
    Fixture fixture = Fixture.Create(temp.Path);
    StructureEditCandidateContent content = fixture.Candidate();
    StructureEditCandidateRegistration first =
        StructureEditCandidateRegistrar.Register(fixture.Store, content);
    StructureEditCandidateRegistration replay =
        StructureEditCandidateRegistrar.Register(fixture.Store, content);

    Assert.Equal(first.CandidateRef, replay.CandidateRef);
    Assert.Equal(first.ReceiptRef, replay.ReceiptRef);
    Assert.Equal(StructureEditKinds.AddBridge, first.CandidateKind);
    Assert.Equal("advisory", first.Authority);
    StructureEditCandidate candidate =
        StructureEditCandidateRegistrar.ReadCandidate(
            fixture.Store,
            first.CandidateRef);
    StructureEditCandidateReceipt receipt =
        StructureEditCandidateRegistrar.ReadReceipt(
            fixture.Store,
            first.ReceiptRef);
    Assert.Equal(candidate.CandidateId, receipt.CandidateId);
    Assert.Equal(fixture.EpisodeRef, receipt.EpisodeRef);
    Assert.Equal(fixture.EvidenceReceiptRef,
        receipt.TopologyAtlasEvidenceInputReceiptRef);
}

static void RejectsDisallowedKind()
{
    using var temp = new TempDirectory();
    Fixture fixture = Fixture.Create(temp.Path);
    StructureEditCandidateContent content = fixture.Candidate() with
    {
        CandidateKind = StructureEditKinds.AddSubgoal,
        GraphPatch =
        [
            new StructureGraphPatchOperation(
                StructureGraphPatchOperations.AddNode,
                "proposed-subgoal",
                "candidate:subgoal",
                null,
                null,
                null,
                null,
                "open")
        ]
    };
    Assert.Throws(() =>
        StructureEditCandidateRegistrar.Register(fixture.Store, content));
}

static void RejectsOrdinalBeyondLimit()
{
    using var temp = new TempDirectory();
    Fixture fixture = Fixture.Create(temp.Path);
    Assert.Throws(() => StructureEditCandidateRegistrar.Register(
        fixture.Store,
        fixture.Candidate() with { CandidateOrdinal = 4 }));
}

static void RejectsUnknownStableEndpoint()
{
    using var temp = new TempDirectory();
    Fixture fixture = Fixture.Create(temp.Path);
    StructureEditCandidateContent content = fixture.Candidate() with
    {
        GraphPatch =
        [
            new StructureGraphPatchOperation(
                StructureGraphPatchOperations.AddEdge,
                null,
                null,
                "node-a",
                "missing-node",
                "gid:node-a",
                "gid:missing-node",
                null)
        ]
    };
    Assert.Throws(() =>
        StructureEditCandidateRegistrar.Register(fixture.Store, content));
}

static void RejectsAddedIdentityCollision()
{
    using var temp = new TempDirectory();
    Fixture fixture = Fixture.Create(temp.Path);
    StructureEditCandidateContent content = fixture.Candidate() with
    {
        CandidateKind = StructureEditKinds.AddAbstraction,
        GraphPatch =
        [
            new StructureGraphPatchOperation(
                StructureGraphPatchOperations.AddNode,
                "duplicate-a",
                "gid:node-a",
                null,
                null,
                null,
                null,
                "open")
        ]
    };
    Assert.Throws(() =>
        StructureEditCandidateRegistrar.Register(fixture.Store, content));
}

static void RejectsTamperedCandidate()
{
    using var temp = new TempDirectory();
    Fixture fixture = Fixture.Create(temp.Path);
    StructureEditCandidateRegistration result =
        StructureEditCandidateRegistrar.Register(
            fixture.Store,
            fixture.Candidate());
    string path = fixture.Store.PathFor(result.CandidateRef);
    byte[] bytes = File.ReadAllBytes(path);
    bytes[^2] = bytes[^2] == (byte)'0' ? (byte)'1' : (byte)'0';
    File.WriteAllBytes(path, bytes);
    Assert.Throws(() => StructureEditCandidateRegistrar.ReadCandidate(
        fixture.Store,
        result.CandidateRef));
}

static void CreatesNoFormalizationRequest()
{
    using var temp = new TempDirectory();
    Fixture fixture = Fixture.Create(temp.Path);
    _ = StructureEditCandidateRegistrar.Register(
        fixture.Store,
        fixture.Candidate());
    string storePath = Path.Combine(fixture.Root, "artifacts", "sha256");
    foreach (string path in Directory.EnumerateFiles(
        storePath,
        "*.json",
        SearchOption.AllDirectories))
    {
        using JsonDocument document = JsonDocument.Parse(File.ReadAllBytes(path));
        if (document.RootElement.TryGetProperty("schema", out JsonElement schema))
        {
            Assert.True(!StringComparer.Ordinal.Equals(
                schema.GetString(),
                "formalization-request.v1"));
        }
    }
}

sealed record Fixture(
    string Root,
    ArtifactStore Store,
    string EpisodeRef,
    string EvidenceReceiptRef,
    string AtlasReceiptRef,
    string Release,
    string Certified,
    string Atlas,
    string Evidence)
{
    private const string Producer =
        "cccccccccccccccccccccccccccccccccccccccc";

    public static Fixture Create(string root)
    {
        var store = new ArtifactStore(Path.Combine(root, "artifacts"));
        string release = Digest('1');
        string certified = Digest('2');
        string atlas = Digest('3');
        string atlasReceipt = Digest('4');
        string evidenceProfile = Digest('5');
        string sourceCommit = new string('6', 40);
        string sourceTree = new string('7', 40);
        byte[] evidenceBytes = EvidenceBytes(
            release,
            certified,
            atlas,
            evidenceProfile);
        string evidence = CanonicalJson.Sha256Reference(evidenceBytes);
        string evidencePath =
            TopologyAtlasEvidenceResearchInputRegistrar.EvidenceBlobPath(
                Path.Combine(root, "artifacts"),
                evidence);
        Directory.CreateDirectory(Path.GetDirectoryName(evidencePath)!);
        File.WriteAllBytes(evidencePath, evidenceBytes);

        var evidenceReceipt = new IntuitionTopologyAtlasEvidenceInputReceipt(
            TopologyAtlasEvidenceResearchInputSchemas.Receipt,
            Digest('8'),
            evidence,
            atlasReceipt,
            release,
            certified,
            atlas,
            evidence,
            evidenceProfile,
            sourceCommit,
            sourceTree,
            Producer);
        string evidenceReceiptRef = store.Put(evidenceReceipt);

        var episodeContent = new StructureEditEpisodeContent(
            Digest('9'),
            Digest('a'),
            atlasReceipt,
            release,
            certified,
            atlas,
            Digest('b'),
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
            "Could these two nodes require an explicit bridge?",
            "private-research",
            StructureEditEpisodeSchemas.NormalizationProfile,
            "2026-09-01T00:00:00Z");
        string episodeId = CanonicalJson.Sha256Reference(
            CanonicalJson.Serialize(episodeContent));
        var episode = new StructureEditEpisode(
            StructureEditEpisodeSchemas.Episode,
            episodeId,
            episodeContent);
        string episodeRef = store.Put(episode);
        return new Fixture(
            root,
            store,
            episodeRef,
            evidenceReceiptRef,
            atlasReceipt,
            release,
            certified,
            atlas,
            evidence);
    }

    public StructureEditCandidateContent Candidate() => new(
        EpisodeRef,
        Store.Get<StructureEditEpisode>(EpisodeRef).EpisodeId,
        Digest('9'),
        AtlasReceiptRef,
        EvidenceReceiptRef,
        Release,
        Certified,
        Atlas,
        Evidence,
        StructureEditKinds.AddBridge,
        1,
        "Add an explicit bridge between the selected concepts",
        "The selected concepts occupy a bounded episode and the missing relation can be tested as an explicit edge.",
        "The patch should shorten or create certified reachability between the selected stable identities.",
        "Reject the candidate when the edge creates a cycle or produces no structural gain under exact counterfactual analysis.",
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

    private static byte[] EvidenceBytes(
        string release,
        string certified,
        string atlas,
        string profile)
    {
        object[] identities =
        [
            Identity("node-a"),
            Identity("node-b")
        ];
        object[] traits =
        [
            Traits("node-a", "foundation"),
            Traits("node-b", "frontier-adjacent")
        ];
        var value = new Dictionary<string, object?>
        {
            ["schema_version"] = "topology-atlas-evidence.v1",
            ["truth_release_digest"] = release,
            ["certified_topology_digest"] = certified,
            ["topology_atlas_digest"] = atlas,
            ["algorithm_profile_digest"] = profile,
            ["producer_commit"] = Producer,
            ["maximum_witnesses"] = 8,
            ["node_identities"] = identities,
            ["node_traits"] = traits,
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

    private static string Digest(char value) =>
        "sha256:" + new string(value, 64);
}

sealed class TempDirectory : IDisposable
{
    public TempDirectory()
    {
        Path = System.IO.Path.Combine(
            System.IO.Path.GetTempPath(),
            "trureturing-intuition-candidate-" + Guid.NewGuid().ToString("N"));
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
