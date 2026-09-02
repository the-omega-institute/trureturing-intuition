using System.Security.Cryptography;
using System.Text;
using Trureturing.Intuition.Core;

var tests = new (string Name, Action Run)[]
{
    ("registers and replays one exact topology input", RegistersAndReplays),
    ("rejects topology digest mismatch", RejectsDigestMismatch),
    ("rejects same-release topology rebinding", RejectsSameReleaseRebinding),
    ("registers and replays one exact topology atlas input", RegistersAtlasAndReplays),
    ("rejects topology atlas digest mismatch", RejectsAtlasDigestMismatch),
    ("rejects same-release topology atlas rebinding", RejectsSameReleaseAtlasRebinding),
    ("rejects mixed topology atlas binding", RejectsMixedAtlasBinding),
    ("rejects unknown topology atlas fields", RejectsUnknownAtlasFields),
    ("registers one human research candidate idempotently", RegistersHumanCandidate),
    ("rejects a tampered human candidate id", RejectsTamperedCandidateId),
    ("rejects unordered human candidate nodes", RejectsUnorderedCandidateNodes)
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

static byte[] Fixture() => File.ReadAllBytes(Path.Combine(
    AppContext.BaseDirectory,
    "fixtures",
    "certified-topology.v1.json"));

static byte[] AtlasFixture() => File.ReadAllBytes(Path.Combine(
    AppContext.BaseDirectory,
    "fixtures",
    "topology-atlas.v1.json"));

static string Digest(ReadOnlySpan<byte> bytes) =>
    "sha256:" + Convert.ToHexStringLower(SHA256.HashData(bytes));

static TopologyPublicationCoordinate Publication(byte[] bytes) => new(
    TopologyResearchInputSchemas.Publication,
    "sha256:" + new string('5', 64),
    Digest(bytes),
    new string('1', 40),
    new string('2', 40),
    "sha256:" + new string('a', 64),
    new string('c', 40));

static TopologyAtlasPublicationCoordinate AtlasPublication(byte[] bytes) => new(
    TopologyAtlasResearchInputSchemas.Publication,
    "sha256:" + new string('5', 64),
    "sha256:" + new string('6', 64),
    Digest(bytes),
    new string('1', 40),
    new string('2', 40),
    "sha256:" + new string('a', 64),
    "sha256:" + new string('b', 64),
    new string('c', 40));

static HumanResearchCandidate HumanCandidate(
    IReadOnlyList<string>? nodes = null)
{
    var content = new HumanResearchCandidateContent(
        "sha256:" + new string('5', 64),
        "sha256:" + new string('6', 64),
        new string('1', 40),
        new string('2', 40),
        "trureturing-pages",
        "human:lexa",
        nodes ?? ["D5/S0/A", "D5/S0/B"],
        ["D5/S0/A->D5/S0/B"],
        "Could these nodes share a stronger invariant?",
        "sha256:" + new string('7', 64),
        "bridge",
        "There exists a structure-preserving bridge between the selected nodes.",
        "A typed counterexample in which every proposed invariant fails.",
        "2026-08-29T09:30:00Z");
    return new HumanResearchCandidate(
        HumanResearchCandidateSchemas.Candidate,
        CanonicalJson.Sha256Reference(CanonicalJson.Serialize(content)),
        content);
}

static void RegistersAndReplays()
{
    using var temp = new TempDirectory();
    byte[] topology = Fixture();
    string cursor = Path.Combine(temp.Path, "work", "topology-input-cursor.v1.json");

    TopologyResearchInputRegistration first =
        TopologyResearchInputRegistrar.Register(
            temp.Path,
            Publication(topology),
            topology,
            cursor);
    TopologyResearchInputRegistration replay =
        TopologyResearchInputRegistrar.Register(
            temp.Path,
            Publication(topology),
            topology,
            cursor);

    Assert.True(!first.Replayed);
    Assert.True(replay.Replayed);
    Assert.Equal(first.ReceiptRef, replay.ReceiptRef);
    Assert.True(File.Exists(
        TopologyResearchInputRegistrar.TopologyBlobPath(
            temp.Path,
            first.TopologyRef)));

    var store = new ArtifactStore(temp.Path);
    IntuitionTopologyInputReceipt receipt =
        store.Get<IntuitionTopologyInputReceipt>(first.ReceiptRef);
    Assert.Equal(first.TopologyRef, receipt.TopologyRef);
    Assert.Equal(
        Publication(topology).TruthReleaseDigest,
        receipt.TruthReleaseDigest);

    IntuitionTopologyInputCursor frozenCursor =
        CanonicalJson.DeserializeCanonical<IntuitionTopologyInputCursor>(
            File.ReadAllBytes(cursor));
    Assert.Equal(first.ReceiptRef, frozenCursor.ReceiptRef);
}

static void RejectsDigestMismatch()
{
    using var temp = new TempDirectory();
    byte[] topology = Fixture();
    TopologyPublicationCoordinate publication = Publication(topology) with
    {
        TopologyDigest = "sha256:" + new string('f', 64)
    };
    Assert.Throws(() => TopologyResearchInputRegistrar.Register(
        temp.Path,
        publication,
        topology,
        Path.Combine(temp.Path, "cursor.json")));
}

static void RejectsSameReleaseRebinding()
{
    using var temp = new TempDirectory();
    byte[] topology = Fixture();
    string cursor = Path.Combine(temp.Path, "cursor.json");
    _ = TopologyResearchInputRegistrar.Register(
        temp.Path,
        Publication(topology),
        topology,
        cursor);

    byte[] changed = Encoding.UTF8.GetBytes(
        Encoding.UTF8.GetString(topology).Replace(
            "\"descendant_cost\": 144",
            "\"descendant_cost\": 145",
            StringComparison.Ordinal));
    Assert.Throws(() => TopologyResearchInputRegistrar.Register(
        temp.Path,
        Publication(changed),
        changed,
        cursor));
}

static void RegistersAtlasAndReplays()
{
    using var temp = new TempDirectory();
    byte[] atlas = AtlasFixture();
    string cursor = Path.Combine(
        temp.Path,
        "work",
        "topology-atlas-input-cursor.v1.json");

    TopologyAtlasResearchInputRegistration first =
        TopologyAtlasResearchInputRegistrar.Register(
            temp.Path,
            AtlasPublication(atlas),
            atlas,
            cursor);
    TopologyAtlasResearchInputRegistration replay =
        TopologyAtlasResearchInputRegistrar.Register(
            temp.Path,
            AtlasPublication(atlas),
            atlas,
            cursor);

    Assert.True(!first.Replayed);
    Assert.True(replay.Replayed);
    Assert.Equal(first.ReceiptRef, replay.ReceiptRef);
    Assert.True(File.Exists(
        TopologyAtlasResearchInputRegistrar.AtlasBlobPath(
            temp.Path,
            first.AtlasRef)));

    var store = new ArtifactStore(temp.Path);
    IntuitionTopologyAtlasInputReceipt receipt =
        store.Get<IntuitionTopologyAtlasInputReceipt>(first.ReceiptRef);
    Assert.Equal(first.AtlasRef, receipt.AtlasRef);
    Assert.Equal(
        AtlasPublication(atlas).CertifiedTopologyDigest,
        receipt.CertifiedTopologyDigest);

    IntuitionTopologyAtlasInputCursor frozenCursor =
        CanonicalJson.DeserializeCanonical<IntuitionTopologyAtlasInputCursor>(
            File.ReadAllBytes(cursor));
    Assert.Equal(first.ReceiptRef, frozenCursor.ReceiptRef);

    TopologyAtlasReadModel model = TopologyAtlasReader.Read(
        atlas,
        new TopologyAtlasBinding(
            receipt.TruthReleaseDigest,
            receipt.CertifiedTopologyDigest,
            receipt.CertifiedAlgorithmProfileDigest,
            receipt.AtlasAlgorithmProfileDigest,
            receipt.ProducerCommit));
    Assert.Equal(3, model.Nodes.Count);
    Assert.Equal(4, model.Clusters.Count);
    Assert.Equal("bridge", model.GetNode("node-b").StructuralRole);
}

static void RejectsAtlasDigestMismatch()
{
    using var temp = new TempDirectory();
    byte[] atlas = AtlasFixture();
    TopologyAtlasPublicationCoordinate publication = AtlasPublication(atlas) with
    {
        TopologyAtlasDigest = "sha256:" + new string('f', 64)
    };
    Assert.Throws(() => TopologyAtlasResearchInputRegistrar.Register(
        temp.Path,
        publication,
        atlas,
        Path.Combine(temp.Path, "cursor.json")));
}

static void RejectsSameReleaseAtlasRebinding()
{
    using var temp = new TempDirectory();
    byte[] atlas = AtlasFixture();
    string cursor = Path.Combine(temp.Path, "cursor.json");
    _ = TopologyAtlasResearchInputRegistrar.Register(
        temp.Path,
        AtlasPublication(atlas),
        atlas,
        cursor);

    byte[] changed = Encoding.UTF8.GetBytes(
        Encoding.UTF8.GetString(atlas).Replace(
            "\"numerator\": 3, \"denominator\": 4",
            "\"numerator\": 2, \"denominator\": 3",
            StringComparison.Ordinal));
    Assert.Throws(() => TopologyAtlasResearchInputRegistrar.Register(
        temp.Path,
        AtlasPublication(changed),
        changed,
        cursor));
}

static void RejectsMixedAtlasBinding()
{
    using var temp = new TempDirectory();
    byte[] atlas = AtlasFixture();
    TopologyAtlasPublicationCoordinate publication = AtlasPublication(atlas) with
    {
        CertifiedTopologyDigest = "sha256:" + new string('d', 64)
    };
    Assert.Throws(() => TopologyAtlasResearchInputRegistrar.Register(
        temp.Path,
        publication,
        atlas,
        Path.Combine(temp.Path, "cursor.json")));
}

static void RejectsUnknownAtlasFields()
{
    using var temp = new TempDirectory();
    byte[] atlas = Encoding.UTF8.GetBytes(
        Encoding.UTF8.GetString(AtlasFixture()).Replace(
            "\"schema_version\": \"topology-atlas.v1\",",
            "\"schema_version\": \"topology-atlas.v1\", \"unknown\": true,",
            StringComparison.Ordinal));
    Assert.Throws(() => TopologyAtlasResearchInputRegistrar.Register(
        temp.Path,
        AtlasPublication(atlas),
        atlas,
        Path.Combine(temp.Path, "cursor.json")));
}

static void RegistersHumanCandidate()
{
    using var temp = new TempDirectory();
    var store = new ArtifactStore(temp.Path);
    HumanResearchCandidate candidate = HumanCandidate();
    HumanResearchCandidateRegistration first =
        HumanResearchCandidateRegistrar.Register(store, candidate);
    HumanResearchCandidateRegistration replay =
        HumanResearchCandidateRegistrar.Register(store, candidate);

    Assert.Equal(first.CandidateRef, replay.CandidateRef);
    Assert.Equal(first.ReceiptRef, replay.ReceiptRef);
    HumanResearchCandidateReceipt receipt =
        store.Get<HumanResearchCandidateReceipt>(first.ReceiptRef);
    Assert.Equal(candidate.CandidateId, receipt.CandidateId);
    Assert.Equal(candidate.CandidateContent.TopologyDigest, receipt.TopologyDigest);
}

static void RejectsTamperedCandidateId()
{
    using var temp = new TempDirectory();
    var store = new ArtifactStore(temp.Path);
    HumanResearchCandidate candidate = HumanCandidate() with
    {
        CandidateId = "sha256:" + new string('f', 64)
    };
    Assert.Throws(() => HumanResearchCandidateRegistrar.Register(store, candidate));
}

static void RejectsUnorderedCandidateNodes()
{
    using var temp = new TempDirectory();
    var store = new ArtifactStore(temp.Path);
    HumanResearchCandidate candidate = HumanCandidate(
        ["D5/S0/B", "D5/S0/A"]);
    Assert.Throws(() => HumanResearchCandidateRegistrar.Register(store, candidate));
}

sealed class TempDirectory : IDisposable
{
    public TempDirectory()
    {
        Path = System.IO.Path.Combine(
            System.IO.Path.GetTempPath(),
            "trureturing-intuition-input-" + Guid.NewGuid().ToString("N"));
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
