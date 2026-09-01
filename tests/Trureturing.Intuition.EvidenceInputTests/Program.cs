using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using Trureturing.Intuition.Core;
using static TestCoordinates;

var tests = new (string Name, Action Run)[]
{
    ("registers and replays exact topology atlas evidence", RegistersAndReplays),
    ("rejects evidence digest mismatch", RejectsEvidenceDigestMismatch),
    ("rejects atlas receipt substitution", RejectsAtlasReceiptSubstitution),
    ("rejects mixed atlas binding", RejectsMixedAtlasBinding),
    ("rejects duplicate stable identity", RejectsDuplicateStableIdentity),
    ("rejects tampered interface edge identity", RejectsTamperedInterfaceIdentity),
    ("rejects invalid affinity witness", RejectsInvalidAffinityWitness),
    ("rejects floating numeric lexemes", RejectsFloatingNumericLexeme),
    ("rejects same-release evidence rebinding", RejectsSameReleaseRebinding),
    ("pins the exact evidence schema", PinsEvidenceSchema),
    ("registration creates no research candidate", CreatesNoCandidateArtifacts)
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

static byte[] AtlasFixture() => File.ReadAllBytes(Path.Combine(
    AppContext.BaseDirectory,
    "fixtures",
    "topology-atlas.v1.json"));

static string Digest(ReadOnlySpan<byte> bytes) =>
    "sha256:" + Convert.ToHexStringLower(SHA256.HashData(bytes));

static TopologyAtlasPublicationCoordinate AtlasPublication(byte[] atlas) => new(
    TopologyAtlasResearchInputSchemas.Publication,
    TruthReleaseDigest,
    CertifiedTopologyDigest,
    Digest(atlas),
    SourceCommit,
    SourceTree,
    CertifiedProfileDigest,
    AtlasProfileDigest,
    ProducerCommit);

static TopologyAtlasResearchInputRegistration RegisterAtlas(
    TempDirectory temp,
    byte[] atlas)
{
    return TopologyAtlasResearchInputRegistrar.Register(
        temp.Path,
        AtlasPublication(atlas),
        atlas,
        Path.Combine(
            temp.Path,
            "work",
            "topology-atlas-input-cursor.v1.json"));
}

static TopologyAtlasEvidencePublicationCoordinate EvidencePublication(
    byte[] atlas,
    string atlasReceiptRef,
    byte[] evidence) => new(
        TopologyAtlasEvidenceResearchInputSchemas.Publication,
        atlasReceiptRef,
        TruthReleaseDigest,
        CertifiedTopologyDigest,
        Digest(atlas),
        Digest(evidence),
        SourceCommit,
        SourceTree,
        CertifiedProfileDigest,
        AtlasProfileDigest,
        EvidenceProfileDigest,
        ProducerCommit);

static string EvidenceCursor(TempDirectory temp) => Path.Combine(
    temp.Path,
    "work",
    "topology-atlas-evidence-input-cursor.v1.json");

static byte[] EvidenceBytes(
    byte[] atlas,
    Action<JsonObject>? mutate = null)
{
    string atlasDigest = Digest(atlas);
    object Rational(int numerator, int denominator) => new
    {
        Numerator = numerator,
        Denominator = denominator
    };
    object TraitEvidence(
        string trait,
        string rule,
        int? integerValue,
        object? rationalValue,
        string[] witnesses) => new
    {
        Trait = trait,
        Rule = rule,
        IntegerValue = integerValue,
        RationalValue = rationalValue,
        WitnessNodeIds = witnesses
    };

    var value = new
    {
        SchemaVersion = "topology-atlas-evidence.v1",
        TruthReleaseDigest,
        CertifiedTopologyDigest,
        TopologyAtlasDigest = atlasDigest,
        AlgorithmProfileDigest = EvidenceProfileDigest,
        ProducerCommit,
        WitnessLimit = 3,
        NodeIdentities = new object[]
        {
            new
            {
                NodeId = "node-a",
                StableNodeId = "gid:a",
                IdentityBasis = "truth-gid",
                Gid = (string?)"gid:a",
                SourcePath = "node-a",
                ModuleName = (string?)"D.NodeA"
            },
            new
            {
                NodeId = "node-b",
                StableNodeId = "gid:b",
                IdentityBasis = "truth-gid",
                Gid = (string?)"gid:b",
                SourcePath = "node-b",
                ModuleName = (string?)"D.NodeB"
            },
            new
            {
                NodeId = "node-c",
                StableNodeId = "node-c",
                IdentityBasis = "node-id-fallback",
                Gid = (string?)null,
                SourcePath = "node-c",
                ModuleName = (string?)null
            }
        },
        NodeTraits = new object[]
        {
            new
            {
                NodeId = "node-a",
                StableNodeId = "gid:a",
                PrimaryRole = "foundation",
                StructuralTraits = new[] { "foundation" },
                Evidence = new object[]
                {
                    TraitEvidence(
                        "foundation",
                        "in-degree-zero",
                        0,
                        null,
                        [])
                }
            },
            new
            {
                NodeId = "node-b",
                StableNodeId = "gid:b",
                PrimaryRole = "bridge",
                StructuralTraits = new[] { "bridge", "interface" },
                Evidence = new object[]
                {
                    TraitEvidence(
                        "bridge",
                        "underlying-undirected-articulation",
                        null,
                        null,
                        []),
                    TraitEvidence(
                        "interface",
                        "leaf-cluster-boundary-score-positive",
                        null,
                        Rational(1, 2),
                        [])
                }
            },
            new
            {
                NodeId = "node-c",
                StableNodeId = "node-c",
                PrimaryRole = "frontier-adjacent",
                StructuralTraits = new[]
                {
                    "frontier-adjacent",
                    "specialized-leaf"
                },
                Evidence = new object[]
                {
                    TraitEvidence(
                        "frontier-adjacent",
                        "node-state-open",
                        null,
                        null,
                        []),
                    TraitEvidence(
                        "specialized-leaf",
                        "out-degree-zero",
                        0,
                        null,
                        [])
                }
            }
        },
        ClusterInterfaces = new object[]
        {
            new
            {
                InterfaceId = InterfaceId(
                    atlasDigest,
                    SourceCluster,
                    TargetCluster),
                SourceClusterId = SourceCluster,
                TargetClusterId = TargetCluster,
                CertifiedEdges = new object[]
                {
                    new
                    {
                        EdgeId = EdgeId("node-b", "node-c"),
                        DependencyId = "node-b",
                        DependentId = "node-c",
                        IsCutBridge = true,
                        EdgeBetweenness = Rational(2, 1),
                        DependencySpan = 1
                    }
                },
                SourceBoundaryNodeIds = new[] { "node-b" },
                TargetBoundaryNodeIds = new[] { "node-c" },
                CutBridgeEdgeIds = new[] { EdgeId("node-b", "node-c") },
                TotalEdgeBetweenness = Rational(2, 1),
                DependencySpanMin = 1,
                DependencySpanMax = 1
            }
        },
        AffinityWitnesses = new object[]
        {
            new
            {
                SourceNodeId = "node-a",
                NeighborNodeId = "node-b",
                Rank = 1,
                SharedPrerequisiteWitnessIds = Array.Empty<string>(),
                SharedDependentWitnessIds = new[] { "node-c" },
                DeepestCommonPrerequisiteIds = Array.Empty<string>()
            },
            new
            {
                SourceNodeId = "node-b",
                NeighborNodeId = "node-a",
                Rank = 1,
                SharedPrerequisiteWitnessIds = Array.Empty<string>(),
                SharedDependentWitnessIds = new[] { "node-c" },
                DeepestCommonPrerequisiteIds = Array.Empty<string>()
            }
        }
    };

    var options = new JsonSerializerOptions
    {
        PropertyNamingPolicy = JsonNamingPolicy.SnakeCaseLower,
        WriteIndented = true
    };
    JsonObject root = JsonSerializer.SerializeToNode(value, options)!.AsObject();
    mutate?.Invoke(root);
    return Encoding.UTF8.GetBytes(root.ToJsonString(options) + "\n");
}

static TopologyAtlasEvidenceReadModel ReadEvidence(
    byte[] atlas,
    byte[] evidence)
{
    TopologyAtlasReadModel atlasModel = TopologyAtlasReader.Read(
        atlas,
        new TopologyAtlasBinding(
            TruthReleaseDigest,
            CertifiedTopologyDigest,
            CertifiedProfileDigest,
            AtlasProfileDigest,
            ProducerCommit));
    return TopologyAtlasEvidenceReader.Read(
        evidence,
        new TopologyAtlasEvidenceBinding(
            TruthReleaseDigest,
            CertifiedTopologyDigest,
            Digest(atlas),
            EvidenceProfileDigest,
            ProducerCommit),
        atlasModel);
}

static void RegistersAndReplays()
{
    using var temp = new TempDirectory();
    byte[] atlas = AtlasFixture();
    TopologyAtlasResearchInputRegistration atlasRegistration =
        RegisterAtlas(temp, atlas);
    byte[] evidence = EvidenceBytes(atlas);
    TopologyAtlasEvidencePublicationCoordinate publication =
        EvidencePublication(
            atlas,
            atlasRegistration.ReceiptRef,
            evidence);

    TopologyAtlasEvidenceResearchInputRegistration first =
        TopologyAtlasEvidenceResearchInputRegistrar.Register(
            temp.Path,
            publication,
            evidence,
            EvidenceCursor(temp));
    TopologyAtlasEvidenceResearchInputRegistration replay =
        TopologyAtlasEvidenceResearchInputRegistrar.Register(
            temp.Path,
            publication,
            evidence,
            EvidenceCursor(temp));

    Assert.True(!first.Replayed);
    Assert.True(replay.Replayed);
    Assert.Equal(first.ReceiptRef, replay.ReceiptRef);
    Assert.Equal(3, first.StableNodeCount);
    Assert.Equal(3, first.TraitRecordCount);
    Assert.Equal(1, first.ClusterInterfaceCount);
    Assert.Equal(2, first.AffinityWitnessCount);
    Assert.True(File.Exists(
        TopologyAtlasEvidenceResearchInputRegistrar.EvidenceBlobPath(
            temp.Path,
            first.EvidenceRef)));

    var store = new ArtifactStore(temp.Path);
    IntuitionTopologyAtlasEvidenceInputReceipt receipt =
        store.Get<IntuitionTopologyAtlasEvidenceInputReceipt>(
            first.ReceiptRef);
    Assert.Equal(first.EvidenceRef, receipt.EvidenceRef);
    Assert.Equal(
        atlasRegistration.ReceiptRef,
        receipt.TopologyAtlasInputReceiptRef);

    IntuitionTopologyAtlasEvidenceInputCursor cursor =
        CanonicalJson.DeserializeCanonical<
            IntuitionTopologyAtlasEvidenceInputCursor>(
                File.ReadAllBytes(EvidenceCursor(temp)));
    Assert.Equal(first.ReceiptRef, cursor.ReceiptRef);

    TopologyAtlasEvidenceReadModel model = ReadEvidence(atlas, evidence);
    Assert.Equal("gid:a", model.StableNodeId("node-a"));
    Assert.Equal("node-c", model.GetStableIdentity("node-c").NodeId);
    Assert.True(
        model.GetTraits("node-b").StructuralTraits.Contains(
            "interface",
            StringComparer.Ordinal));
    Assert.True(
        model.FindInterface(SourceCluster, TargetCluster) is not null);
    Assert.Equal(
        "node-c",
        model.FindAffinityWitness("node-a", "node-b", 1)!
            .SharedDependentWitnessIds.Single());
}

static void RejectsEvidenceDigestMismatch()
{
    using var temp = new TempDirectory();
    byte[] atlas = AtlasFixture();
    TopologyAtlasResearchInputRegistration atlasRegistration =
        RegisterAtlas(temp, atlas);
    byte[] evidence = EvidenceBytes(atlas);
    TopologyAtlasEvidencePublicationCoordinate publication =
        EvidencePublication(atlas, atlasRegistration.ReceiptRef, evidence) with
        {
            TopologyAtlasEvidenceDigest =
                "sha256:" + new string('f', 64)
        };
    Assert.Throws(() =>
        TopologyAtlasEvidenceResearchInputRegistrar.Register(
            temp.Path,
            publication,
            evidence,
            EvidenceCursor(temp)));
}

static void RejectsAtlasReceiptSubstitution()
{
    using var temp = new TempDirectory();
    byte[] atlas = AtlasFixture();
    _ = RegisterAtlas(temp, atlas);
    byte[] evidence = EvidenceBytes(atlas);
    TopologyAtlasEvidencePublicationCoordinate publication =
        EvidencePublication(
            atlas,
            "sha256:" + new string('f', 64),
            evidence);
    Assert.Throws(() =>
        TopologyAtlasEvidenceResearchInputRegistrar.Register(
            temp.Path,
            publication,
            evidence,
            EvidenceCursor(temp)));
}

static void RejectsMixedAtlasBinding()
{
    using var temp = new TempDirectory();
    byte[] atlas = AtlasFixture();
    TopologyAtlasResearchInputRegistration atlasRegistration =
        RegisterAtlas(temp, atlas);
    byte[] evidence = EvidenceBytes(atlas);
    TopologyAtlasEvidencePublicationCoordinate publication =
        EvidencePublication(atlas, atlasRegistration.ReceiptRef, evidence) with
        {
            CertifiedTopologyDigest = "sha256:" + new string('7', 64)
        };
    Assert.Throws(() =>
        TopologyAtlasEvidenceResearchInputRegistrar.Register(
            temp.Path,
            publication,
            evidence,
            EvidenceCursor(temp)));
}

static void RejectsDuplicateStableIdentity()
{
    using var temp = new TempDirectory();
    byte[] atlas = AtlasFixture();
    TopologyAtlasResearchInputRegistration atlasRegistration =
        RegisterAtlas(temp, atlas);
    byte[] evidence = EvidenceBytes(atlas, root =>
    {
        JsonObject identity = root["node_identities"]![1]!.AsObject();
        identity["stable_node_id"] = "gid:a";
        identity["gid"] = "gid:a";
    });
    Assert.Throws(() =>
        TopologyAtlasEvidenceResearchInputRegistrar.Register(
            temp.Path,
            EvidencePublication(atlas, atlasRegistration.ReceiptRef, evidence),
            evidence,
            EvidenceCursor(temp)));
}

static void RejectsTamperedInterfaceIdentity()
{
    using var temp = new TempDirectory();
    byte[] atlas = AtlasFixture();
    TopologyAtlasResearchInputRegistration atlasRegistration =
        RegisterAtlas(temp, atlas);
    byte[] evidence = EvidenceBytes(atlas, root =>
    {
        root["cluster_interfaces"]![0]!["certified_edges"]![0]!["edge_id"] =
            "edge:sha256:" + new string('f', 64);
    });
    Assert.Throws(() =>
        TopologyAtlasEvidenceResearchInputRegistrar.Register(
            temp.Path,
            EvidencePublication(atlas, atlasRegistration.ReceiptRef, evidence),
            evidence,
            EvidenceCursor(temp)));
}

static void RejectsInvalidAffinityWitness()
{
    using var temp = new TempDirectory();
    byte[] atlas = AtlasFixture();
    TopologyAtlasResearchInputRegistration atlasRegistration =
        RegisterAtlas(temp, atlas);
    byte[] evidence = EvidenceBytes(atlas, root =>
    {
        root["affinity_witnesses"]![0]!["shared_dependent_witness_ids"] =
            new JsonArray(JsonValue.Create("node-b"));
    });
    Assert.Throws(() =>
        TopologyAtlasEvidenceResearchInputRegistrar.Register(
            temp.Path,
            EvidencePublication(atlas, atlasRegistration.ReceiptRef, evidence),
            evidence,
            EvidenceCursor(temp)));
}

static void RejectsFloatingNumericLexeme()
{
    using var temp = new TempDirectory();
    byte[] atlas = AtlasFixture();
    TopologyAtlasResearchInputRegistration atlasRegistration =
        RegisterAtlas(temp, atlas);
    byte[] evidence = EvidenceBytes(atlas);
    evidence = Encoding.UTF8.GetBytes(
        Encoding.UTF8.GetString(evidence).Replace(
            "\"witness_limit\": 3",
            "\"witness_limit\": 3.0",
            StringComparison.Ordinal));
    Assert.Throws(() =>
        TopologyAtlasEvidenceResearchInputRegistrar.Register(
            temp.Path,
            EvidencePublication(atlas, atlasRegistration.ReceiptRef, evidence),
            evidence,
            EvidenceCursor(temp)));
}

static void RejectsSameReleaseRebinding()
{
    using var temp = new TempDirectory();
    byte[] atlas = AtlasFixture();
    TopologyAtlasResearchInputRegistration atlasRegistration =
        RegisterAtlas(temp, atlas);
    byte[] evidence = EvidenceBytes(atlas);
    _ = TopologyAtlasEvidenceResearchInputRegistrar.Register(
        temp.Path,
        EvidencePublication(atlas, atlasRegistration.ReceiptRef, evidence),
        evidence,
        EvidenceCursor(temp));

    byte[] changed = EvidenceBytes(atlas, root =>
    {
        root["node_identities"]![0]!["module_name"] = "D.NodeA2";
    });
    Assert.Throws(() =>
        TopologyAtlasEvidenceResearchInputRegistrar.Register(
            temp.Path,
            EvidencePublication(atlas, atlasRegistration.ReceiptRef, changed),
            changed,
            EvidenceCursor(temp)));
}

static void PinsEvidenceSchema()
{
    string schemaPath = Path.Combine(
        AppContext.BaseDirectory,
        "contracts",
        "topology-atlas-evidence.v1.schema.json");
    string digestPath = Path.Combine(
        AppContext.BaseDirectory,
        "contracts",
        "topology-atlas-evidence.v1.schema.sha256");
    string expected = File.ReadAllText(digestPath)
        .Split(' ', StringSplitOptions.RemoveEmptyEntries)[0];
    string actual = Convert.ToHexStringLower(
        SHA256.HashData(File.ReadAllBytes(schemaPath)));
    Assert.Equal(expected, actual);
}

static void CreatesNoCandidateArtifacts()
{
    using var temp = new TempDirectory();
    byte[] atlas = AtlasFixture();
    TopologyAtlasResearchInputRegistration atlasRegistration =
        RegisterAtlas(temp, atlas);
    byte[] evidence = EvidenceBytes(atlas);
    _ = TopologyAtlasEvidenceResearchInputRegistrar.Register(
        temp.Path,
        EvidencePublication(atlas, atlasRegistration.ReceiptRef, evidence),
        evidence,
        EvidenceCursor(temp));

    string artifacts = string.Join(
        "\n",
        Directory.EnumerateFiles(
                Path.Combine(temp.Path, "sha256"),
                "*.json",
                SearchOption.AllDirectories)
            .Select(File.ReadAllText));
    Assert.DoesNotContain("candidate-edit", artifacts);
    Assert.DoesNotContain("intuition-proposal", artifacts);
    Assert.DoesNotContain("formalization-request", artifacts);
    Assert.DoesNotContain("research-attempt", artifacts);
}

static string EdgeId(string dependencyId, string dependentId) =>
    "edge:sha256:" + HashText(
        "topology-certified-edge.v1\n" +
        dependencyId + "\n" +
        dependentId + "\n");

static string InterfaceId(
    string atlasDigest,
    string sourceClusterId,
    string targetClusterId) =>
    "interface:sha256:" + HashText(
        "topology-cluster-interface.v1\n" +
        atlasDigest + "\n" +
        sourceClusterId + "\n" +
        targetClusterId + "\n");

static string HashText(string value) => Convert.ToHexStringLower(
    SHA256.HashData(Encoding.UTF8.GetBytes(value)));

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

    public static void DoesNotContain(string term, string value)
    {
        if (value.Contains(term, StringComparison.Ordinal))
        {
            throw new InvalidOperationException(
                $"Unexpected term '{term}' was present.");
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
