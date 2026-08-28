using System.Security.Cryptography;
using System.Text;
using Trureturing.Intuition.Core;

var tests = new (string Name, Action Run)[]
{
    ("registers and replays one exact topology input", RegistersAndReplays),
    ("rejects topology digest mismatch", RejectsDigestMismatch),
    ("rejects same-release topology rebinding", RejectsSameReleaseRebinding)
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
