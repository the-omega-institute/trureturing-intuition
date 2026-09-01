using System.Security.Cryptography;
using System.Text;
using Trureturing.Intuition.Core;

namespace Trureturing.Intuition.AtlasEvidenceInputTests;

internal static class Program
{
    private static readonly string Release = "sha256:" + new string('1', 64);
    private static readonly string Certified = "sha256:" + new string('2', 64);
    private static readonly string Atlas = "sha256:" + new string('3', 64);
    private static readonly string EvidenceProfile = "sha256:" + new string('4', 64);
    private static readonly string SourceCommit = new string('5', 40);
    private static readonly string SourceTree = new string('6', 40);
    private static readonly string Producer = new string('7', 40);
    private static readonly string ClusterA = "cluster:sha256:" + new string('a', 64);
    private static readonly string ClusterB = "cluster:sha256:" + new string('b', 64);

    public static int Main()
    {
        var tests = new (string Name, Action Run)[]
        {
            ("registers and replays exact atlas evidence", RegistersAndReplays),
            ("indexes stable identity and structural traits", ReadsStableIdentityAndTraits),
            ("rejects evidence digest mismatch", RejectsDigestMismatch),
            ("rejects atlas receipt coordinate mismatch", RejectsAtlasReceiptMismatch),
            ("rejects duplicate stable identities", RejectsDuplicateStableIdentity),
            ("rejects unknown root properties", RejectsUnknownRootProperty),
            ("rejects floating point lexemes", RejectsFloatingPointLexeme),
            ("rejects witness nodes outside identity closure", RejectsUnknownWitness),
            ("rejects same-release evidence rebinding", RejectsSameReleaseRebinding),
            ("persists an immutable evidence receipt", PersistsReceipt)
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

    private static void RegistersAndReplays()
    {
        using var temp = new TempDirectory();
        string atlasReceiptRef = WriteAtlasReceipt(temp.Path);
        byte[] evidence = EvidenceBytes();
        string cursor = Path.Combine(temp.Path, "work", "atlas-evidence-cursor.json");
        TopologyAtlasEvidencePublicationCoordinate publication = Publication(
            atlasReceiptRef,
            evidence);
        TopologyAtlasEvidenceResearchInputRegistration first =
            TopologyAtlasEvidenceResearchInputRegistrar.Register(
                temp.Path,
                publication,
                evidence,
                cursor);
        TopologyAtlasEvidenceResearchInputRegistration replay =
            TopologyAtlasEvidenceResearchInputRegistrar.Register(
                temp.Path,
                publication,
                evidence,
                cursor);
        Check.True(!first.Replayed);
        Check.True(replay.Replayed);
        Check.Equal(first.ReceiptRef, replay.ReceiptRef);
        Check.Equal(3, first.StableIdentityCount);
        Check.Equal(1, first.ClusterInterfaceCount);
        Check.Equal(1, first.AffinityWitnessCount);
        Check.True(File.Exists(
            TopologyAtlasEvidenceResearchInputRegistrar.EvidenceBlobPath(
                temp.Path,
                first.EvidenceRef)));
    }

    private static void ReadsStableIdentityAndTraits()
    {
        TopologyAtlasEvidenceReadModel model = TopologyAtlasEvidenceReader.Read(
            EvidenceBytes(),
            Binding());
        TopologyAtlasStableIdentityReadModel identity =
            model.GetIdentityByStableId("gid:B");
        Check.Equal("B", identity.NodeId);
        Check.Equal("truth-gid", identity.IdentityBasis);
        TopologyAtlasNodeTraitsReadModel traits = model.GetTraits("B");
        Check.Equal("bridge", traits.PrimaryRole);
        Check.True(traits.StructuralTraits.Contains("hub", StringComparer.Ordinal));
        Check.Equal(1, model.ClusterInterfaces.Count);
        Check.Equal(1, model.AffinityWitnesses.Count);
    }

    private static void RejectsDigestMismatch()
    {
        using var temp = new TempDirectory();
        string receipt = WriteAtlasReceipt(temp.Path);
        byte[] evidence = EvidenceBytes();
        TopologyAtlasEvidencePublicationCoordinate publication = Publication(
            receipt,
            evidence) with
        {
            EvidenceDigest = "sha256:" + new string('f', 64)
        };
        Check.Throws(() => Register(temp.Path, publication, evidence));
    }

    private static void RejectsAtlasReceiptMismatch()
    {
        using var temp = new TempDirectory();
        string receipt = WriteAtlasReceipt(temp.Path);
        byte[] evidence = EvidenceBytes();
        TopologyAtlasEvidencePublicationCoordinate publication = Publication(
            receipt,
            evidence) with
        {
            TopologyAtlasDigest = "sha256:" + new string('e', 64)
        };
        Check.Throws(() => Register(temp.Path, publication, evidence));
    }

    private static void RejectsDuplicateStableIdentity()
    {
        byte[] evidence = Replace(
            EvidenceBytes(),
            "\"stable_node_id\":\"gid:B\"",
            "\"stable_node_id\":\"gid:A\"");
        Check.Throws(() => TopologyAtlasEvidenceReader.Read(evidence, Binding()));
    }

    private static void RejectsUnknownRootProperty()
    {
        byte[] evidence = Replace(
            EvidenceBytes(),
            "{\"affinity_witnesses\"",
            "{\"unknown\":true,\"affinity_witnesses\"");
        Check.Throws(() => TopologyAtlasEvidenceReader.Read(evidence, Binding()));
    }

    private static void RejectsFloatingPointLexeme()
    {
        byte[] evidence = Replace(
            EvidenceBytes(),
            "\"maximum_witnesses_per_relation\":8",
            "\"maximum_witnesses_per_relation\":8.0");
        Check.Throws(() => TopologyAtlasEvidenceReader.Read(evidence, Binding()));
    }

    private static void RejectsUnknownWitness()
    {
        byte[] evidence = Replace(
            EvidenceBytes(),
            "\"shared_prerequisite_node_ids\":[\"A\"]",
            "\"shared_prerequisite_node_ids\":[\"Z\"]");
        Check.Throws(() => TopologyAtlasEvidenceReader.Read(evidence, Binding()));
    }

    private static void RejectsSameReleaseRebinding()
    {
        using var temp = new TempDirectory();
        string receipt = WriteAtlasReceipt(temp.Path);
        byte[] evidence = EvidenceBytes();
        string cursor = Path.Combine(temp.Path, "cursor.json");
        _ = TopologyAtlasEvidenceResearchInputRegistrar.Register(
            temp.Path,
            Publication(receipt, evidence),
            evidence,
            cursor);
        byte[] changed = Replace(
            evidence,
            "source-depth-zero",
            "source-depth-origin");
        Check.Throws(() =>
            TopologyAtlasEvidenceResearchInputRegistrar.Register(
                temp.Path,
                Publication(receipt, changed),
                changed,
                cursor));
    }

    private static void PersistsReceipt()
    {
        using var temp = new TempDirectory();
        string atlasReceipt = WriteAtlasReceipt(temp.Path);
        byte[] evidence = EvidenceBytes();
        TopologyAtlasEvidenceResearchInputRegistration registration =
            Register(temp.Path, Publication(atlasReceipt, evidence), evidence);
        var store = new ArtifactStore(temp.Path);
        IntuitionTopologyAtlasEvidenceInputReceipt receipt =
            store.Get<IntuitionTopologyAtlasEvidenceInputReceipt>(
                registration.ReceiptRef);
        Check.Equal(registration.EvidenceRef, receipt.EvidenceRef);
        Check.Equal(atlasReceipt, receipt.TopologyAtlasInputReceiptRef);
        Check.Equal(Atlas, receipt.TopologyAtlasDigest);
        Check.Equal(EvidenceProfile, receipt.EvidenceAlgorithmProfileDigest);
    }

    private static TopologyAtlasEvidenceResearchInputRegistration Register(
        string root,
        TopologyAtlasEvidencePublicationCoordinate publication,
        byte[] evidence) =>
        TopologyAtlasEvidenceResearchInputRegistrar.Register(
            root,
            publication,
            evidence,
            Path.Combine(root, "cursor.json"));

    private static TopologyAtlasEvidenceBinding Binding() => new(
        Release,
        Certified,
        Atlas,
        EvidenceProfile,
        Producer);

    private static TopologyAtlasEvidencePublicationCoordinate Publication(
        string atlasReceiptRef,
        byte[] evidence) => new(
            TopologyAtlasEvidenceResearchInputSchemas.Publication,
            atlasReceiptRef,
            Release,
            Certified,
            Atlas,
            Digest(evidence),
            EvidenceProfile,
            SourceCommit,
            SourceTree,
            Producer);

    private static string WriteAtlasReceipt(string root)
    {
        byte[] bytes = CanonicalJson.Serialize(new AtlasReceiptFixture(
            "intuition-topology-atlas-input-receipt.v1",
            Release,
            Certified,
            Atlas,
            SourceCommit,
            SourceTree,
            Producer));
        string reference = Digest(bytes);
        var store = new ArtifactStore(root);
        string path = store.PathFor(reference);
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        File.WriteAllBytes(path, bytes);
        return reference;
    }

    private static byte[] EvidenceBytes()
    {
        string json = $$"""
{"affinity_witnesses":[{"deepest_common_prerequisite_node_ids":["A"],"neighbor_node_id":"C","rank":1,"shared_dependent_node_ids":[],"shared_prerequisite_node_ids":["A"],"source_node_id":"B"}],"algorithm_profile_digest":"{{EvidenceProfile}}","certified_topology_digest":"{{Certified}}","cluster_interfaces":[{"certified_edge_witnesses":[{"dependency_id":"B","dependent_id":"C"}],"source_boundary_node_ids":["B"],"source_cluster_id":"{{ClusterA}}","target_boundary_node_ids":["C"],"target_cluster_id":"{{ClusterB}}"}],"maximum_witnesses_per_relation":8,"node_identities":[{"gid":"gid:A","identity_basis":"truth-gid","module_name":"A","node_id":"A","source_path":"A.lean","stable_node_id":"gid:A"},{"gid":"gid:B","identity_basis":"truth-gid","module_name":"B","node_id":"B","source_path":"B.lean","stable_node_id":"gid:B"},{"gid":"gid:C","identity_basis":"truth-gid","module_name":"C","node_id":"C","source_path":"C.lean","stable_node_id":"gid:C"}],"node_traits":[{"evidence":[{"integer_value":0,"rational_value":null,"rule":"source-depth-zero","trait":"foundation","witness_node_ids":[]}],"node_id":"A","primary_role":"foundation","stable_node_id":"gid:A","structural_traits":["foundation"]},{"evidence":[{"integer_value":null,"rational_value":{"denominator":2,"numerator":1},"rule":"articulation-cut","trait":"bridge","witness_node_ids":["A","C"]},{"integer_value":3,"rational_value":null,"rule":"degree-threshold","trait":"hub","witness_node_ids":[]}],"node_id":"B","primary_role":"bridge","stable_node_id":"gid:B","structural_traits":["bridge","hub"]},{"evidence":[{"integer_value":0,"rational_value":null,"rule":"open-successor","trait":"frontier-adjacent","witness_node_ids":[]}],"node_id":"C","primary_role":"frontier-adjacent","stable_node_id":"gid:C","structural_traits":["frontier-adjacent"]}],"producer_commit":"{{Producer}}","schema_version":"topology-atlas-evidence.v1","topology_atlas_digest":"{{Atlas}}","truth_release_digest":"{{Release}}"}
""";
        return Encoding.UTF8.GetBytes(json);
    }

    private static byte[] Replace(byte[] bytes, string oldValue, string newValue) =>
        Encoding.UTF8.GetBytes(
            Encoding.UTF8.GetString(bytes).Replace(
                oldValue,
                newValue,
                StringComparison.Ordinal));

    private static string Digest(ReadOnlySpan<byte> bytes) =>
        "sha256:" + Convert.ToHexStringLower(SHA256.HashData(bytes));

    private sealed record AtlasReceiptFixture(
        string Schema,
        string TruthReleaseDigest,
        string CertifiedTopologyDigest,
        string TopologyAtlasDigest,
        string SourceCommit,
        string SourceTree,
        string ProducerCommit);

    private sealed class TempDirectory : IDisposable
    {
        public TempDirectory()
        {
            Path = System.IO.Path.Combine(
                System.IO.Path.GetTempPath(),
                "trureturing-intuition-atlas-evidence-" + Guid.NewGuid().ToString("N"));
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
