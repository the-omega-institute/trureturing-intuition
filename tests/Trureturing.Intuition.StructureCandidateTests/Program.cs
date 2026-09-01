using System.Security.Cryptography;
using Trureturing.Intuition.Core;

namespace Trureturing.Intuition.StructureCandidateTests;

internal static class Program
{
    private static readonly string Release = DigestText('1');
    private static readonly string Certified = DigestText('2');
    private static readonly string Atlas = DigestText('3');
    private static readonly string EvidenceProfile = DigestText('4');
    private static readonly string AtlasReceipt = DigestText('5');
    private static readonly string Observation = DigestText('6');
    private static readonly string ObservationReceipt = DigestText('7');
    private static readonly string PagesConformation = DigestText('8');
    private static readonly string SourceCommit = new('9', 40);
    private static readonly string SourceTree = new('a', 40);
    private static readonly string Producer = new('b', 40);

    public static int Main()
    {
        var tests = new (string Name, Action Run)[]
        {
            ("generates deterministic stable candidates", GeneratesDeterministically),
            ("preserves release to stable identity mapping", PreservesIdentityMapping),
            ("separates graph patches from question registration", SeparatesEligibility),
            ("respects candidate priority and budget", RespectsPriorityAndBudget),
            ("rejects mixed release evidence", RejectsMixedReleaseEvidence),
            ("rejects selected nodes outside evidence closure", RejectsUnknownSelectedNode),
            ("rejects a mismatched episode receipt", RejectsMismatchedEpisodeReceipt),
            ("keeps generated artifacts advisory", KeepsArtifactsAdvisory)
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
    }

    private static void GeneratesDeterministically()
    {
        using var temp = new TempDirectory();
        Fixture fixture = CreateFixture(temp.Path);
        StructureEditCandidateGeneration first = Generate(temp.Path, fixture);
        StructureEditCandidateGeneration replay = Generate(temp.Path, fixture);
        Check.Equal(first.CandidateSetRef, replay.CandidateSetRef);
        Check.Equal(first.ReceiptRef, replay.ReceiptRef);
        Check.SequenceEqual(first.CandidateRefs, replay.CandidateRefs);
        Check.SequenceEqual(first.CandidateIds, replay.CandidateIds);
        Check.Equal(2, first.CandidateRefs.Count);
        Check.Equal(Release, first.TruthReleaseDigest);
        Check.Equal(Atlas, first.TopologyAtlasDigest);
        Check.Equal(fixture.EvidenceRef, first.TopologyAtlasEvidenceDigest);
    }

    private static void PreservesIdentityMapping()
    {
        using var temp = new TempDirectory();
        Fixture fixture = CreateFixture(temp.Path);
        StructureEditCandidateGeneration result = Generate(temp.Path, fixture);
        var store = new ArtifactStore(temp.Path);
        StructureEditCandidate candidate = result.CandidateRefs
            .Select(store.Get<StructureEditCandidate>)
            .Single(value => value.CandidateContent.EditKind == StructureEditKinds.AddBridge);
        Check.Equal(2, candidate.CandidateContent.NodeIdentities.Count);
        StructureEditCandidateNodeIdentity a = candidate.CandidateContent.NodeIdentities[0];
        StructureEditCandidateNodeIdentity b = candidate.CandidateContent.NodeIdentities[1];
        Check.Equal("A", a.ReleaseNodeId);
        Check.Equal("gid:A", a.StableNodeId);
        Check.Equal("truth-gid", a.IdentityBasis);
        Check.Equal("B", b.ReleaseNodeId);
        Check.Equal("B", b.StableNodeId);
        Check.Equal("node-id-fallback", b.IdentityBasis);
        Check.Equal(null, b.Gid);
        StructureEditCandidateEdge edge = Check.Single(
            candidate.CandidateContent.SelectedEdges);
        Check.Equal("A", edge.ReleaseDependencyId);
        Check.Equal("B", edge.ReleaseDependentId);
        Check.Equal("gid:A", edge.StableDependencyId);
        Check.Equal("B", edge.StableDependentId);
    }

    private static void SeparatesEligibility()
    {
        using var temp = new TempDirectory();
        Fixture fixture = CreateFixture(temp.Path);
        StructureEditCandidateGeneration result = Generate(temp.Path, fixture);
        var store = new ArtifactStore(temp.Path);
        Dictionary<string, StructureEditCandidate> candidates = result.CandidateRefs
            .Select(store.Get<StructureEditCandidate>)
            .ToDictionary(value => value.CandidateContent.EditKind, StringComparer.Ordinal);
        StructureEditCandidate bridge = candidates[StructureEditKinds.AddBridge];
        Check.Equal(
            StructureEditCounterfactualEligibility.GraphPatchRequired,
            bridge.CandidateContent.CounterfactualEligibility);
        Check.Equal(
            StructureEditPatchShapes.AddEdge,
            bridge.CandidateContent.SuggestedPatchShape);
        StructureEditCandidate question =
            candidates[StructureEditKinds.RegisterOpenQuestion];
        Check.Equal(
            StructureEditCounterfactualEligibility.QuestionRegistration,
            question.CandidateContent.CounterfactualEligibility);
        Check.Equal(
            StructureEditPatchShapes.None,
            question.CandidateContent.SuggestedPatchShape);
    }

    private static void RespectsPriorityAndBudget()
    {
        using var temp = new TempDirectory();
        string[] allowed =
        [
            StructureEditKinds.AddBridge,
            StructureEditKinds.AddPremise,
            StructureEditKinds.RegisterOpenQuestion
        ];
        Array.Sort(allowed, StringComparer.Ordinal);
        Fixture fixture = CreateFixture(
            temp.Path,
            allowedEditKinds: allowed,
            candidateLimit: 2);
        StructureEditCandidateGeneration result = Generate(temp.Path, fixture);
        Check.SequenceEqual(
            [StructureEditKinds.AddPremise, StructureEditKinds.AddBridge],
            result.EditKinds);
        Check.Equal(2, result.CandidateRefs.Count);
    }

    private static void RejectsMixedReleaseEvidence()
    {
        using var temp = new TempDirectory();
        Fixture fixture = CreateFixture(temp.Path);
        var store = new ArtifactStore(temp.Path);
        IntuitionTopologyAtlasEvidenceInputReceipt original =
            store.Get<IntuitionTopologyAtlasEvidenceInputReceipt>(
                fixture.EvidenceReceiptRef);
        string mixed = store.Put(original with
        {
            TruthReleaseDigest = DigestText('e')
        });
        Check.Throws(() => StructureEditCandidateGenerator.Generate(
            temp.Path,
            fixture.EpisodeRef,
            fixture.EpisodeReceiptRef,
            mixed));
    }

    private static void RejectsUnknownSelectedNode()
    {
        using var temp = new TempDirectory();
        Fixture fixture = CreateFixture(
            temp.Path,
            selectedNodeIds: ["A", "Z"],
            selectedEdges: []);
        Check.Throws(() => Generate(temp.Path, fixture));
    }

    private static void RejectsMismatchedEpisodeReceipt()
    {
        using var temp = new TempDirectory();
        Fixture fixture = CreateFixture(temp.Path);
        var store = new ArtifactStore(temp.Path);
        StructureEditEpisodeReceipt receipt =
            store.Get<StructureEditEpisodeReceipt>(fixture.EpisodeReceiptRef);
        string mismatched = store.Put(receipt with
        {
            EpisodeId = DigestText('f')
        });
        Check.Throws(() => StructureEditCandidateGenerator.Generate(
            temp.Path,
            fixture.EpisodeRef,
            mismatched,
            fixture.EvidenceReceiptRef));
    }

    private static void KeepsArtifactsAdvisory()
    {
        using var temp = new TempDirectory();
        Fixture fixture = CreateFixture(temp.Path);
        StructureEditCandidateGeneration result = Generate(temp.Path, fixture);
        var store = new ArtifactStore(temp.Path);
        StructureEditCandidateSet set =
            store.Get<StructureEditCandidateSet>(result.CandidateSetRef);
        Check.Equal(
            StructureEditCandidateSchemas.Authority,
            set.CandidateSetContent.Authority);
        foreach (string candidateRef in result.CandidateRefs)
        {
            StructureEditCandidate candidate =
                store.Get<StructureEditCandidate>(candidateRef);
            Check.Equal(
                StructureEditCandidateSchemas.Authority,
                candidate.CandidateContent.Authority);
            string json = System.Text.Encoding.UTF8.GetString(
                CanonicalJson.Serialize(candidate));
            Check.True(!json.Contains("formalization_request", StringComparison.Ordinal));
            Check.True(!json.Contains("selected_for_execution", StringComparison.Ordinal));
            Check.True(!json.Contains("base_write", StringComparison.Ordinal));
        }
    }

    private static StructureEditCandidateGeneration Generate(
        string root,
        Fixture fixture) =>
        StructureEditCandidateGenerator.Generate(
            root,
            fixture.EpisodeRef,
            fixture.EpisodeReceiptRef,
            fixture.EvidenceReceiptRef);

    private static Fixture CreateFixture(
        string root,
        IReadOnlyList<string>? allowedEditKinds = null,
        int candidateLimit = 2,
        IReadOnlyList<string>? selectedNodeIds = null,
        IReadOnlyList<HumanStructureSelectedEdge>? selectedEdges = null)
    {
        var store = new ArtifactStore(root);
        byte[] evidenceBytes = EvidenceBytes();
        string evidenceRef = Digest(evidenceBytes);
        string evidencePath =
            TopologyAtlasEvidenceResearchInputRegistrar.EvidenceBlobPath(
                root,
                evidenceRef);
        Directory.CreateDirectory(Path.GetDirectoryName(evidencePath)!);
        File.WriteAllBytes(evidencePath, evidenceBytes);

        var evidenceReceipt = new IntuitionTopologyAtlasEvidenceInputReceipt(
            TopologyAtlasEvidenceResearchInputSchemas.Receipt,
            DigestText('c'),
            evidenceRef,
            AtlasReceipt,
            Release,
            Certified,
            Atlas,
            evidenceRef,
            EvidenceProfile,
            SourceCommit,
            SourceTree,
            Producer);
        string evidenceReceiptRef = store.Put(evidenceReceipt);

        string[] nodes = (selectedNodeIds ?? ["A", "B"])
            .Order(StringComparer.Ordinal)
            .ToArray();
        HumanStructureSelectedEdge[] edges = (selectedEdges ??
            [new HumanStructureSelectedEdge("A", "B")])
            .OrderBy(edge => edge.DependencyId, StringComparer.Ordinal)
            .ThenBy(edge => edge.DependentId, StringComparer.Ordinal)
            .ToArray();
        string[] edits = (allowedEditKinds ??
            [
                StructureEditKinds.AddBridge,
                StructureEditKinds.RegisterOpenQuestion
            ])
            .Order(StringComparer.Ordinal)
            .ToArray();
        var content = new StructureEditEpisodeContent(
            Observation,
            ObservationReceipt,
            AtlasReceipt,
            Release,
            Certified,
            Atlas,
            PagesConformation,
            nodes.Length == 2 ? "node-pair" : "node-set",
            nodes,
            [],
            edges,
            null,
            "compare",
            edits,
            candidateLimit,
            "Investigate whether the selected structures admit a reusable bridge.",
            "private-research",
            StructureEditEpisodeSchemas.NormalizationProfile,
            "2026-09-01T03:00:00Z");
        string episodeId = CanonicalJson.Sha256Reference(
            CanonicalJson.Serialize(content));
        var episode = new StructureEditEpisode(
            StructureEditEpisodeSchemas.Episode,
            episodeId,
            content);
        string episodeRef = store.Put(episode);
        var receipt = new StructureEditEpisodeReceipt(
            StructureEditEpisodeSchemas.Receipt,
            episodeRef,
            episodeId,
            Observation,
            ObservationReceipt,
            Release,
            Atlas,
            "private-research",
            StructureEditEpisodeSchemas.NormalizationProfile);
        string episodeReceiptRef = store.Put(receipt);
        return new Fixture(
            episodeRef,
            episodeReceiptRef,
            evidenceReceiptRef,
            evidenceRef);
    }

    private static byte[] EvidenceBytes() => CanonicalJson.Serialize(new
    {
        schema_version = "topology-atlas-evidence.v1",
        truth_release_digest = Release,
        certified_topology_digest = Certified,
        topology_atlas_digest = Atlas,
        algorithm_profile_digest = EvidenceProfile,
        producer_commit = Producer,
        maximum_witnesses_per_relation = 8,
        node_identities = new object[]
        {
            new
            {
                node_id = "A",
                stable_node_id = "gid:A",
                identity_basis = "truth-gid",
                gid = "gid:A",
                source_path = "A.lean",
                module_name = "A"
            },
            new
            {
                node_id = "B",
                stable_node_id = "B",
                identity_basis = "node-id-fallback",
                gid = (string?)null,
                source_path = "B.lean",
                module_name = "B"
            }
        },
        node_traits = new object[]
        {
            new
            {
                node_id = "A",
                stable_node_id = "gid:A",
                primary_role = "foundation",
                structural_traits = new[] { "foundation" },
                evidence = new object[]
                {
                    new
                    {
                        trait = "foundation",
                        rule = "source-depth-zero",
                        integer_value = (int?)0,
                        rational_value = (object?)null,
                        witness_node_ids = Array.Empty<string>()
                    }
                }
            },
            new
            {
                node_id = "B",
                stable_node_id = "B",
                primary_role = "bridge",
                structural_traits = new[] { "bridge", "hub" },
                evidence = new object[]
                {
                    new
                    {
                        trait = "bridge",
                        rule = "articulation-cut",
                        integer_value = (int?)null,
                        rational_value = new { numerator = 1, denominator = 2 },
                        witness_node_ids = new[] { "A" }
                    },
                    new
                    {
                        trait = "hub",
                        rule = "degree-threshold",
                        integer_value = (int?)3,
                        rational_value = (object?)null,
                        witness_node_ids = Array.Empty<string>()
                    }
                }
            }
        },
        cluster_interfaces = Array.Empty<object>(),
        affinity_witnesses = Array.Empty<object>()
    });

    private static string Digest(ReadOnlySpan<byte> bytes) =>
        "sha256:" + Convert.ToHexStringLower(SHA256.HashData(bytes));

    private static string DigestText(char value) =>
        "sha256:" + new string(value, 64);

    private sealed record Fixture(
        string EpisodeRef,
        string EpisodeReceiptRef,
        string EvidenceReceiptRef,
        string EvidenceRef);

    private sealed class TempDirectory : IDisposable
    {
        public TempDirectory()
        {
            Path = System.IO.Path.Combine(
                System.IO.Path.GetTempPath(),
                "trureturing-intuition-structure-candidates-" +
                Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(Path);
        }

        public string Path { get; }

        public void Dispose()
        {
            if (Directory.Exists(Path))
            {
                Directory.Delete(Path, recursive: true);
            }
        }
    }

    private static class Check
    {
        public static void True(bool value)
        {
            if (!value)
            {
                throw new InvalidOperationException("Expected true.");
            }
        }

        public static void Equal<T>(T expected, T actual)
        {
            if (!EqualityComparer<T>.Default.Equals(expected, actual))
            {
                throw new InvalidOperationException(
                    $"Expected '{expected}', got '{actual}'.");
            }
        }

        public static void SequenceEqual<T>(
            IReadOnlyList<T> expected,
            IReadOnlyList<T> actual)
        {
            if (!expected.SequenceEqual(actual))
            {
                throw new InvalidOperationException(
                    $"Expected [{string.Join(", ", expected)}], got [{string.Join(", ", actual)}].");
            }
        }

        public static T Single<T>(IReadOnlyList<T> values)
        {
            if (values.Count != 1)
            {
                throw new InvalidOperationException(
                    $"Expected one value, got {values.Count}.");
            }
            return values[0];
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
}
