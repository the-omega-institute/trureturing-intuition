using System.Security.Cryptography;
using System.Text;
using Trureturing.Intuition.Core;

namespace Trureturing.Intuition.CounterfactualTests;

internal static class Program
{
    private static readonly string Release = DigestText('1');
    private static readonly string Atlas = DigestText('2');
    private static readonly string Evidence = DigestText('3');
    private static readonly string Episode = DigestText('4');
    private static readonly string EpisodeId = DigestText('5');
    private static readonly string Observation = DigestText('6');
    private static readonly string EvidenceReceipt = DigestText('7');
    private static readonly string CounterfactualProfile = DigestText('8');
    private static readonly string Producer = new('9', 40);
    private static readonly string Cluster = "cluster:sha256:" + new string('a', 64);

    public static int Main()
    {
        var tests = new (string Name, Action Run)[]
        {
            ("registers patches and results deterministically", RegistersDeterministically),
            ("derives a non scalar worth vector", DerivesWorthVector),
            ("blocks cycle-risk counterfactuals", BlocksCycleRisk),
            ("rejects stable endpoint mismatches", RejectsEndpointMismatch),
            ("rejects removal outside selected certified edges", RejectsUnselectedRemoval),
            ("requires explicit counterfactual submission", RequiresExplicitSubmission),
            ("rejects candidates outside graph-patch eligibility", RejectsIneligibleCandidate),
            ("rejects exact result digest mismatch", RejectsResultDigestMismatch),
            ("rejects result and projection status mismatch", RejectsStatusMismatch),
            ("stores no scalar score or execution authority", StoresNoScalarOrExecutionAuthority)
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

    private static void RegistersDeterministically()
    {
        using var temp = new TempDirectory();
        var store = new ArtifactStore(temp.Path);
        string candidateRef = StoreCandidate(store);
        StructureEditGraphPatch patch = AddEdgePatch(store, candidateRef);
        StructureEditGraphPatchRegistration firstPatch =
            StructureCounterfactualRegistrar.RegisterPatch(
                temp.Path,
                candidateRef,
                patch);
        StructureEditGraphPatchRegistration replayPatch =
            StructureCounterfactualRegistrar.RegisterPatch(
                temp.Path,
                candidateRef,
                patch);
        Check.Equal(firstPatch.PatchRef, replayPatch.PatchRef);
        Check.Equal(firstPatch.ReceiptRef, replayPatch.ReceiptRef);

        byte[] resultBytes = AcceptedResult(patch, store.Get<StructureEditCandidate>(candidateRef));
        TopologyCounterfactualPublication publication = AcceptedPublication(
            firstPatch.PatchRef,
            patch,
            store.Get<StructureEditCandidate>(candidateRef),
            resultBytes);
        StructureCounterfactualRegistration first =
            StructureCounterfactualRegistrar.RegisterCounterfactual(
                temp.Path,
                firstPatch.PatchRef,
                publication,
                resultBytes);
        StructureCounterfactualRegistration replay =
            StructureCounterfactualRegistrar.RegisterCounterfactual(
                temp.Path,
                firstPatch.PatchRef,
                publication,
                resultBytes);
        Check.Equal(first.CounterfactualPublicationRef,
            replay.CounterfactualPublicationRef);
        Check.Equal(first.CounterfactualPublicationReceiptRef,
            replay.CounterfactualPublicationReceiptRef);
        Check.Equal(first.ValuationRef, replay.ValuationRef);
        Check.True(File.Exists(
            StructureCounterfactualRegistrar.ResultBlobPath(
                temp.Path,
                first.CounterfactualResultRef)));
    }

    private static void DerivesWorthVector()
    {
        using var temp = new TempDirectory();
        var store = new ArtifactStore(temp.Path);
        string candidateRef = StoreCandidate(store);
        StructureEditGraphPatch patch = AddEdgePatch(store, candidateRef);
        StructureEditGraphPatchRegistration patchResult =
            StructureCounterfactualRegistrar.RegisterPatch(
                temp.Path,
                candidateRef,
                patch);
        StructureEditCandidate candidate = store.Get<StructureEditCandidate>(candidateRef);
        byte[] resultBytes = AcceptedResult(patch, candidate);
        TopologyCounterfactualPublication publication = AcceptedPublication(
            patchResult.PatchRef,
            patch,
            candidate,
            resultBytes);
        StructureCounterfactualRegistration registration =
            StructureCounterfactualRegistrar.RegisterCounterfactual(
                temp.Path,
                patchResult.PatchRef,
                publication,
                resultBytes);
        StructureCounterfactualValuation valuation =
            store.Get<StructureCounterfactualValuation>(registration.ValuationRef);
        StructureCounterfactualWorthVector vector =
            valuation.ValuationContent.WorthVector;
        Check.Equal(4L, vector.ReachabilityGain);
        Check.Equal(1L, vector.ReachabilityLoss);
        Check.Equal(3L, vector.PathCompressionGain);
        Check.Equal(2L, vector.InterfaceHypothesisGain);
        Check.Equal(1L, vector.InterfaceHypothesisLoss);
        Check.Equal(1L, vector.CutBridgeReduction);
        Check.Equal(2L, vector.CutBridgeCreation);
        Check.Equal(3L, vector.AffectedScope);
        Check.Equal(1L, vector.PatchOperationCost);
        Check.Equal(0L, vector.CycleRiskPenalty);
        Check.True(vector.FormalVerificationOpen);
        Check.True(valuation.ValuationContent.EligibleForFormalResearch);
        Check.Equal("none", valuation.ValuationContent.BlockingReason);
    }

    private static void BlocksCycleRisk()
    {
        using var temp = new TempDirectory();
        var store = new ArtifactStore(temp.Path);
        string candidateRef = StoreCandidate(store);
        StructureEditGraphPatch patch = AddEdgePatch(store, candidateRef);
        StructureEditGraphPatchRegistration patchResult =
            StructureCounterfactualRegistrar.RegisterPatch(
                temp.Path,
                candidateRef,
                patch);
        StructureEditCandidate candidate = store.Get<StructureEditCandidate>(candidateRef);
        byte[] resultBytes = CounterfactualResult(
            patch,
            candidate,
            accepted: false,
            cycleRisk: true,
            analysis: null);
        var projection = new TopologyCounterfactualProjection(
            Accepted: false,
            CycleRisk: true,
            AnalysisAvailable: false,
            AffectedStableNodeIds: [],
            ReachablePairGain: 0,
            ReachablePairLoss: 0,
            ShortestPathImprovementCount: 0,
            TotalPathCompression: 0,
            NewCutBridgeCount: 0,
            RemovedCutBridgeCount: 0,
            TouchedClusterIds: [],
            NewInterfaceHypothesisCount: 0,
            RemovedInterfaceHypothesisCount: 0);
        TopologyCounterfactualPublication publication = Publication(
            patchResult.PatchRef,
            patch,
            candidate,
            resultBytes,
            projection);
        StructureCounterfactualRegistration registration =
            StructureCounterfactualRegistrar.RegisterCounterfactual(
                temp.Path,
                patchResult.PatchRef,
                publication,
                resultBytes);
        StructureCounterfactualValuation valuation =
            store.Get<StructureCounterfactualValuation>(registration.ValuationRef);
        Check.True(!registration.EligibleForFormalResearch);
        Check.Equal("cycle-risk", registration.BlockingReason);
        Check.Equal(1L,
            valuation.ValuationContent.WorthVector.CycleRiskPenalty);
    }

    private static void RejectsEndpointMismatch()
    {
        using var temp = new TempDirectory();
        var store = new ArtifactStore(temp.Path);
        string candidateRef = StoreCandidate(store);
        StructureEditGraphPatch patch = AddEdgePatch(store, candidateRef);
        StructureEditGraphPatchOperation operation = patch.PatchContent.Operations[0]
            with { StableDependentId = "gid:wrong" };
        StructureEditGraphPatch changed = Rehash(patch with
        {
            PatchContent = patch.PatchContent with
            {
                Operations = [operation]
            }
        });
        Check.Throws(() => StructureCounterfactualRegistrar.RegisterPatch(
            temp.Path,
            candidateRef,
            changed));
    }

    private static void RejectsUnselectedRemoval()
    {
        using var temp = new TempDirectory();
        var store = new ArtifactStore(temp.Path);
        string candidateRef = StoreCandidate(
            store,
            patchShape: StructureEditPatchShapes.RemoveEdge,
            selectedEdges:
            [
                new StructureEditCandidateEdge(
                    "A", "B", "gid:A", "gid:B")
            ]);
        StructureEditCandidate candidate = store.Get<StructureEditCandidate>(candidateRef);
        var content = new StructureEditGraphPatchContent(
            candidateRef,
            candidate.CandidateId,
            Release,
            Atlas,
            Evidence,
            [
                new StructureEditGraphPatchOperation(
                    "01-remove-edge",
                    StructureGraphPatchOperationKinds.RemoveEdge,
                    null, null, null, null,
                    "B", "A", "gid:B", "gid:A")
            ],
            [],
            "Test an explicit certified-edge removal.",
            true,
            StructureCounterfactualSchemas.Authority);
        StructureEditGraphPatch patch = Patch(content);
        Check.Throws(() => StructureCounterfactualRegistrar.RegisterPatch(
            temp.Path,
            candidateRef,
            patch));
    }

    private static void RequiresExplicitSubmission()
    {
        using var temp = new TempDirectory();
        var store = new ArtifactStore(temp.Path);
        string candidateRef = StoreCandidate(store);
        StructureEditGraphPatch patch = AddEdgePatch(store, candidateRef);
        StructureEditGraphPatch changed = Rehash(patch with
        {
            PatchContent = patch.PatchContent with
            {
                ExplicitlySubmittedForCounterfactual = false
            }
        });
        Check.Throws(() => StructureCounterfactualRegistrar.RegisterPatch(
            temp.Path,
            candidateRef,
            changed));
    }

    private static void RejectsIneligibleCandidate()
    {
        using var temp = new TempDirectory();
        var store = new ArtifactStore(temp.Path);
        string candidateRef = StoreCandidate(
            store,
            eligibility: StructureEditCounterfactualEligibility.QuestionRegistration,
            patchShape: StructureEditPatchShapes.None);
        StructureEditGraphPatch patch = AddEdgePatch(store, candidateRef);
        Check.Throws(() => StructureCounterfactualRegistrar.RegisterPatch(
            temp.Path,
            candidateRef,
            patch));
    }

    private static void RejectsResultDigestMismatch()
    {
        using var temp = new TempDirectory();
        var store = new ArtifactStore(temp.Path);
        string candidateRef = StoreCandidate(store);
        StructureEditGraphPatch patch = AddEdgePatch(store, candidateRef);
        StructureEditGraphPatchRegistration patchResult =
            StructureCounterfactualRegistrar.RegisterPatch(
                temp.Path,
                candidateRef,
                patch);
        StructureEditCandidate candidate = store.Get<StructureEditCandidate>(candidateRef);
        byte[] resultBytes = AcceptedResult(patch, candidate);
        TopologyCounterfactualPublication publication = AcceptedPublication(
            patchResult.PatchRef,
            patch,
            candidate,
            resultBytes);
        byte[] changed = resultBytes.Concat([(byte)' ']).ToArray();
        Check.Throws(() => StructureCounterfactualRegistrar.RegisterCounterfactual(
            temp.Path,
            patchResult.PatchRef,
            publication,
            changed));
    }

    private static void RejectsStatusMismatch()
    {
        using var temp = new TempDirectory();
        var store = new ArtifactStore(temp.Path);
        string candidateRef = StoreCandidate(store);
        StructureEditGraphPatch patch = AddEdgePatch(store, candidateRef);
        StructureEditGraphPatchRegistration patchResult =
            StructureCounterfactualRegistrar.RegisterPatch(
                temp.Path,
                candidateRef,
                patch);
        StructureEditCandidate candidate = store.Get<StructureEditCandidate>(candidateRef);
        byte[] resultBytes = CounterfactualResult(
            patch,
            candidate,
            accepted: false,
            cycleRisk: false,
            analysis: new { reachable_pair_gain = 4 });
        TopologyCounterfactualPublication publication = AcceptedPublication(
            patchResult.PatchRef,
            patch,
            candidate,
            resultBytes);
        Check.Throws(() => StructureCounterfactualRegistrar.RegisterCounterfactual(
            temp.Path,
            patchResult.PatchRef,
            publication,
            resultBytes));
    }

    private static void StoresNoScalarOrExecutionAuthority()
    {
        using var temp = new TempDirectory();
        var store = new ArtifactStore(temp.Path);
        string candidateRef = StoreCandidate(store);
        StructureEditGraphPatch patch = AddEdgePatch(store, candidateRef);
        StructureEditGraphPatchRegistration patchResult =
            StructureCounterfactualRegistrar.RegisterPatch(
                temp.Path,
                candidateRef,
                patch);
        StructureEditCandidate candidate = store.Get<StructureEditCandidate>(candidateRef);
        byte[] resultBytes = AcceptedResult(patch, candidate);
        StructureCounterfactualRegistration result =
            StructureCounterfactualRegistrar.RegisterCounterfactual(
                temp.Path,
                patchResult.PatchRef,
                AcceptedPublication(
                    patchResult.PatchRef,
                    patch,
                    candidate,
                    resultBytes),
                resultBytes);
        string json = Encoding.UTF8.GetString(File.ReadAllBytes(
            store.PathFor(result.ValuationRef)));
        Check.True(!json.Contains("scalar", StringComparison.OrdinalIgnoreCase));
        Check.True(!json.Contains("score", StringComparison.OrdinalIgnoreCase));
        Check.True(!json.Contains("selected_for_execution", StringComparison.Ordinal));
        Check.True(!json.Contains("base_write", StringComparison.Ordinal));
        Check.True(json.Contains("formal_verification_open", StringComparison.Ordinal));
    }

    private static string StoreCandidate(
        ArtifactStore store,
        string eligibility = StructureEditCounterfactualEligibility.GraphPatchRequired,
        string patchShape = StructureEditPatchShapes.AddEdge,
        IReadOnlyList<StructureEditCandidateEdge>? selectedEdges = null)
    {
        var content = new StructureEditCandidateContent(
            Episode,
            EpisodeId,
            Observation,
            EvidenceReceipt,
            Release,
            Atlas,
            Evidence,
            StructureEditKinds.AddBridge,
            [
                new StructureEditCandidateNodeIdentity(
                    "A", "gid:A", "truth-gid", "gid:A", "A.lean", "A"),
                new StructureEditCandidateNodeIdentity(
                    "B", "gid:B", "truth-gid", "gid:B", "B.lean", "B")
            ],
            [],
            selectedEdges ?? [],
            null,
            "Can the selected structures support a certified bridge?",
            "Propose one explicit bridge edge with bounded endpoints.",
            "The edge creates a cycle or does not improve certified structure.",
            eligibility,
            patchShape,
            StructureEditCandidateSchemas.GenerationProfile,
            StructureEditCandidateSchemas.Authority);
        string id = CanonicalJson.Sha256Reference(CanonicalJson.Serialize(content));
        return store.Put(new StructureEditCandidate(
            StructureEditCandidateSchemas.Candidate,
            id,
            content));
    }

    private static StructureEditGraphPatch AddEdgePatch(
        ArtifactStore store,
        string candidateRef)
    {
        StructureEditCandidate candidate = store.Get<StructureEditCandidate>(candidateRef);
        var content = new StructureEditGraphPatchContent(
            candidateRef,
            candidate.CandidateId,
            Release,
            Atlas,
            Evidence,
            [
                new StructureEditGraphPatchOperation(
                    "01-add-edge",
                    StructureGraphPatchOperationKinds.AddEdge,
                    null, null, null, null,
                    "A", "B", "gid:A", "gid:B")
            ],
            [],
            "Evaluate one explicit candidate bridge without changing truth.",
            true,
            StructureCounterfactualSchemas.Authority);
        return Patch(content);
    }

    private static StructureEditGraphPatch Patch(
        StructureEditGraphPatchContent content) =>
        new(
            StructureCounterfactualSchemas.Patch,
            CanonicalJson.Sha256Reference(CanonicalJson.Serialize(content)),
            content);

    private static StructureEditGraphPatch Rehash(
        StructureEditGraphPatch patch) =>
        patch with
        {
            PatchId = CanonicalJson.Sha256Reference(
                CanonicalJson.Serialize(patch.PatchContent))
        };

    private static byte[] AcceptedResult(
        StructureEditGraphPatch patch,
        StructureEditCandidate candidate) =>
        CounterfactualResult(
            patch,
            candidate,
            accepted: true,
            cycleRisk: false,
            analysis: new
            {
                reachable_pair_gain = 4,
                path_compression = 3
            });

    private static byte[] CounterfactualResult(
        StructureEditGraphPatch patch,
        StructureEditCandidate candidate,
        bool accepted,
        bool cycleRisk,
        object? analysis) =>
        CanonicalJson.Serialize(new
        {
            schema_version = "topology-counterfactual.v1",
            truth_release_digest = Release,
            topology_atlas_digest = Atlas,
            patch_id = patch.PatchId,
            candidate_id = candidate.CandidateId,
            accepted,
            cycle_risk = cycleRisk,
            analysis
        });

    private static TopologyCounterfactualPublication AcceptedPublication(
        string patchRef,
        StructureEditGraphPatch patch,
        StructureEditCandidate candidate,
        byte[] resultBytes) =>
        Publication(
            patchRef,
            patch,
            candidate,
            resultBytes,
            new TopologyCounterfactualProjection(
                Accepted: true,
                CycleRisk: false,
                AnalysisAvailable: true,
                AffectedStableNodeIds: ["gid:A", "gid:B"],
                ReachablePairGain: 4,
                ReachablePairLoss: 1,
                ShortestPathImprovementCount: 2,
                TotalPathCompression: 3,
                NewCutBridgeCount: 2,
                RemovedCutBridgeCount: 1,
                TouchedClusterIds: [Cluster],
                NewInterfaceHypothesisCount: 2,
                RemovedInterfaceHypothesisCount: 1));

    private static TopologyCounterfactualPublication Publication(
        string patchRef,
        StructureEditGraphPatch patch,
        StructureEditCandidate candidate,
        byte[] resultBytes,
        TopologyCounterfactualProjection projection)
    {
        var content = new TopologyCounterfactualPublicationContent(
            patchRef,
            patch.PatchId,
            patch.PatchContent.CandidateRef,
            candidate.CandidateId,
            Release,
            Atlas,
            Evidence,
            Digest(resultBytes),
            CounterfactualProfile,
            Producer,
            projection,
            StructureCounterfactualSchemas.ProjectionProfile,
            StructureCounterfactualSchemas.Authority);
        return new TopologyCounterfactualPublication(
            StructureCounterfactualSchemas.Publication,
            CanonicalJson.Sha256Reference(CanonicalJson.Serialize(content)),
            content);
    }

    private static string Digest(ReadOnlySpan<byte> bytes) =>
        "sha256:" + Convert.ToHexStringLower(SHA256.HashData(bytes));

    private static string DigestText(char value) =>
        "sha256:" + new string(value, 64);

    private sealed class TempDirectory : IDisposable
    {
        public TempDirectory()
        {
            Path = System.IO.Path.Combine(
                System.IO.Path.GetTempPath(),
                "trureturing-intuition-counterfactual-" +
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
