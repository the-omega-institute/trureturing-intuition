using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using Trureturing.Intuition.Core;

var tests = new (string Name, Action Run)[]
{
    ("registers and replays exact atlas evidence", RegistersAndReplays),
    ("rejects evidence digest mismatch", RejectsEvidenceDigestMismatch),
    ("rejects atlas binding mismatch", RejectsAtlasBindingMismatch),
    ("rejects evidence node outside atlas", RejectsUnknownEvidenceNode),
    ("rejects duplicate stable identities", RejectsDuplicateStableIdentity),
    ("rejects floating numeric lexemes", RejectsFloatingLexeme),
    ("rejects same release evidence rebinding", RejectsSameReleaseRebinding)
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

static void RegistersAndReplays()
{
    using var temp = new TempDirectory();
    Fixture fixture = Fixture.Create(temp.Path);
    TopologyAtlasEvidenceResearchInputRegistration first = Register(fixture);
    TopologyAtlasEvidenceResearchInputRegistration replay = Register(fixture);

    Assert.True(!first.Replayed);
    Assert.True(replay.Replayed);
    Assert.Equal(first.ReceiptRef, replay.ReceiptRef);
    Assert.Equal(fixture.NodeIds.Count, first.StableIdentityCount);
    Assert.True(File.Exists(
        TopologyAtlasEvidenceResearchInputRegistrar.EvidenceBlobPath(
            fixture.StoreRoot,
            first.EvidenceRef)));

    var store = new ArtifactStore(fixture.StoreRoot);
    IntuitionTopologyAtlasEvidenceInputReceipt receipt =
        store.Get<IntuitionTopologyAtlasEvidenceInputReceipt>(first.ReceiptRef);
    Assert.Equal(first.EvidenceRef, receipt.EvidenceRef);
    Assert.Equal(fixture.AtlasDigest, receipt.TopologyAtlasDigest);
}

static void RejectsEvidenceDigestMismatch()
{
    using var temp = new TempDirectory();
    Fixture fixture = Fixture.Create(temp.Path);
    TopologyAtlasEvidencePublicationCoordinate hostile = fixture.Publication with
    {
        EvidenceDigest = Digest(Encoding.UTF8.GetBytes("different"))
    };
    Assert.Throws(() => TopologyAtlasEvidenceResearchInputRegistrar.Register(
        fixture.StoreRoot,
        hostile,
        fixture.EvidenceBytes,
        fixture.AtlasBytes,
        fixture.CursorPath));
}

static void RejectsAtlasBindingMismatch()
{
    using var temp = new TempDirectory();
    Fixture fixture = Fixture.Create(temp.Path);
    JsonObject root = JsonNode.Parse(fixture.EvidenceBytes)!.AsObject();
    root["topology_atlas_digest"] = Digest(Encoding.UTF8.GetBytes("other-atlas"));
    byte[] changed = Encoding.UTF8.GetBytes(root.ToJsonString());
    TopologyAtlasEvidencePublicationCoordinate hostile = fixture.Publication with
    {
        EvidenceDigest = Digest(changed)
    };
    Assert.Throws(() => TopologyAtlasEvidenceResearchInputRegistrar.Register(
        fixture.StoreRoot,
        hostile,
        changed,
        fixture.AtlasBytes,
        fixture.CursorPath));
}

static void RejectsUnknownEvidenceNode()
{
    using var temp = new TempDirectory();
    Fixture fixture = Fixture.Create(temp.Path);
    JsonObject root = JsonNode.Parse(fixture.EvidenceBytes)!.AsObject();
    JsonObject identity = root["node_identities"]![0]!.AsObject();
    JsonObject traits = root["node_traits"]![0]!.AsObject();
    identity["node_id"] = "unknown-node";
    identity["stable_node_id"] = "gid:unknown-node";
    identity["gid"] = "gid:unknown-node";
    traits["node_id"] = "unknown-node";
    traits["stable_node_id"] = "gid:unknown-node";
    byte[] changed = Encoding.UTF8.GetBytes(root.ToJsonString());
    TopologyAtlasEvidencePublicationCoordinate hostile = fixture.Publication with
    {
        EvidenceDigest = Digest(changed)
    };
    Assert.Throws(() => TopologyAtlasEvidenceResearchInputRegistrar.Register(
        fixture.StoreRoot,
        hostile,
        changed,
        fixture.AtlasBytes,
        fixture.CursorPath));
}

static void RejectsDuplicateStableIdentity()
{
    using var temp = new TempDirectory();
    Fixture fixture = Fixture.Create(temp.Path);
    JsonObject root = JsonNode.Parse(fixture.EvidenceBytes)!.AsObject();
    JsonArray identities = root["node_identities"]!.AsArray();
    JsonArray traits = root["node_traits"]!.AsArray();
    if (identities.Count < 2)
    {
        throw new InvalidOperationException("Atlas fixture requires at least two nodes.");
    }
    string duplicate = identities[0]!["stable_node_id"]!.GetValue<string>();
    identities[1]!["stable_node_id"] = duplicate;
    identities[1]!["gid"] = duplicate;
    traits[1]!["stable_node_id"] = duplicate;
    byte[] changed = Encoding.UTF8.GetBytes(root.ToJsonString());
    TopologyAtlasEvidencePublicationCoordinate hostile = fixture.Publication with
    {
        EvidenceDigest = Digest(changed)
    };
    Assert.Throws(() => TopologyAtlasEvidenceResearchInputRegistrar.Register(
        fixture.StoreRoot,
        hostile,
        changed,
        fixture.AtlasBytes,
        fixture.CursorPath));
}

static void RejectsFloatingLexeme()
{
    using var temp = new TempDirectory();
    Fixture fixture = Fixture.Create(temp.Path);
    string text = Encoding.UTF8.GetString(fixture.EvidenceBytes)
        .Replace("\"maximum_witnesses\":8", "\"maximum_witnesses\":8.0", StringComparison.Ordinal);
    byte[] changed = Encoding.UTF8.GetBytes(text);
    TopologyAtlasEvidencePublicationCoordinate hostile = fixture.Publication with
    {
        EvidenceDigest = Digest(changed)
    };
    Assert.Throws(() => TopologyAtlasEvidenceResearchInputRegistrar.Register(
        fixture.StoreRoot,
        hostile,
        changed,
        fixture.AtlasBytes,
        fixture.CursorPath));
}

static void RejectsSameReleaseRebinding()
{
    using var temp = new TempDirectory();
    Fixture fixture = Fixture.Create(temp.Path);
    _ = Register(fixture);
    JsonObject root = JsonNode.Parse(fixture.EvidenceBytes)!.AsObject();
    root["maximum_witnesses"] = 9;
    byte[] changed = Encoding.UTF8.GetBytes(root.ToJsonString());
    TopologyAtlasEvidencePublicationCoordinate hostile = fixture.Publication with
    {
        EvidenceDigest = Digest(changed)
    };
    Assert.Throws(() => TopologyAtlasEvidenceResearchInputRegistrar.Register(
        fixture.StoreRoot,
        hostile,
        changed,
        fixture.AtlasBytes,
        fixture.CursorPath));
}

static TopologyAtlasEvidenceResearchInputRegistration Register(Fixture fixture) =>
    TopologyAtlasEvidenceResearchInputRegistrar.Register(
        fixture.StoreRoot,
        fixture.Publication,
        fixture.EvidenceBytes,
        fixture.AtlasBytes,
        fixture.CursorPath);

static string Digest(ReadOnlySpan<byte> bytes) =>
    "sha256:" + Convert.ToHexStringLower(SHA256.HashData(bytes));

sealed record Fixture(
    string StoreRoot,
    string CursorPath,
    byte[] AtlasBytes,
    string AtlasDigest,
    IReadOnlyList<string> NodeIds,
    byte[] EvidenceBytes,
    TopologyAtlasEvidencePublicationCoordinate Publication)
{
    public static Fixture Create(string root)
    {
        string fixturePath = Path.Combine(
            AppContext.BaseDirectory,
            "fixtures",
            "topology-atlas.v1.json");
        byte[] atlasBytes = File.ReadAllBytes(fixturePath);
        string atlasDigest = Digest(atlasBytes);
        using JsonDocument atlasDocument = JsonDocument.Parse(atlasBytes);
        JsonElement atlas = atlasDocument.RootElement;
        string release = atlas.GetProperty("truth_release_digest").GetString()!;
        string certified = atlas.GetProperty("certified_topology_digest").GetString()!;
        string certifiedProfile = atlas.GetProperty(
            "certified_algorithm_profile_digest").GetString()!;
        string atlasProfile = atlas.GetProperty("algorithm_profile_digest").GetString()!;
        string producer = atlas.GetProperty("producer_commit").GetString()!;
        string sourceCommit = new string('1', 40);
        string sourceTree = new string('2', 40);
        string storeRoot = Path.Combine(root, "artifacts");
        var store = new ArtifactStore(storeRoot);

        var atlasReceiptValue = new Dictionary<string, object?>
        {
            ["schema"] = "intuition-topology-atlas-input-receipt.v1",
            ["truth_release_digest"] = release,
            ["certified_topology_digest"] = certified,
            ["topology_atlas_digest"] = atlasDigest,
            ["certified_algorithm_profile_digest"] = certifiedProfile,
            ["atlas_algorithm_profile_digest"] = atlasProfile,
            ["source_commit"] = sourceCommit,
            ["source_tree"] = sourceTree,
            ["producer_commit"] = producer
        };
        byte[] atlasReceiptBytes = CanonicalJson.Serialize(atlasReceiptValue);
        string atlasReceiptRef = Digest(atlasReceiptBytes);
        string atlasReceiptPath = store.PathFor(atlasReceiptRef);
        Directory.CreateDirectory(Path.GetDirectoryName(atlasReceiptPath)!);
        File.WriteAllBytes(atlasReceiptPath, atlasReceiptBytes);

        string[] nodeIds = atlas.GetProperty("node_structure")
            .EnumerateArray()
            .Select(value => value.GetProperty("node_id").GetString()!)
            .Order(StringComparer.Ordinal)
            .ToArray();
        var roleByNode = atlas.GetProperty("node_structure")
            .EnumerateArray()
            .ToDictionary(
                value => value.GetProperty("node_id").GetString()!,
                value => value.GetProperty("structural_role").GetString()!,
                StringComparer.Ordinal);
        string evidenceProfile = "sha256:" + new string('e', 64);
        var identities = nodeIds.Select(node => new Dictionary<string, object?>
        {
            ["node_id"] = node,
            ["stable_node_id"] = "gid:" + node,
            ["identity_basis"] = "truth-gid",
            ["gid"] = "gid:" + node,
            ["source_path"] = node,
            ["module_name"] = null
        }).ToArray();
        var traits = nodeIds.Select(node =>
        {
            string role = roleByNode[node];
            return new Dictionary<string, object?>
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
        }).ToArray();
        var evidenceValue = new Dictionary<string, object?>
        {
            ["schema_version"] = "topology-atlas-evidence.v1",
            ["truth_release_digest"] = release,
            ["certified_topology_digest"] = certified,
            ["topology_atlas_digest"] = atlasDigest,
            ["algorithm_profile_digest"] = evidenceProfile,
            ["producer_commit"] = producer,
            ["maximum_witnesses"] = 8,
            ["node_identities"] = identities,
            ["node_traits"] = traits,
            ["cluster_interfaces"] = Array.Empty<object>(),
            ["affinity_witnesses"] = Array.Empty<object>()
        };
        byte[] evidenceBytes = CanonicalJson.Serialize(evidenceValue);
        string evidenceDigest = Digest(evidenceBytes);
        var publication = new TopologyAtlasEvidencePublicationCoordinate(
            TopologyAtlasEvidenceResearchInputSchemas.Publication,
            release,
            certified,
            atlasDigest,
            evidenceDigest,
            evidenceProfile,
            atlasReceiptRef,
            sourceCommit,
            sourceTree,
            producer);
        return new Fixture(
            storeRoot,
            Path.Combine(root, "work", "topology-atlas-evidence-cursor.v1.json"),
            atlasBytes,
            atlasDigest,
            nodeIds,
            evidenceBytes,
            publication);
    }
}

sealed class TempDirectory : IDisposable
{
    public TempDirectory()
    {
        Path = System.IO.Path.Combine(
            System.IO.Path.GetTempPath(),
            "trureturing-intuition-evidence-" + Guid.NewGuid().ToString("N"));
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
