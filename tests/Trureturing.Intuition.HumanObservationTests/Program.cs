using System.Security.Cryptography;
using Trureturing.Intuition.Core;

var tests = new (string Name, Action Run)[]
{
    ("registers and replays an explicit human structure observation", RegistersAndReplays),
    ("rejects observations that were not explicitly saved", RejectsImplicitObservation),
    ("rejects an unknown selected atlas node", RejectsUnknownNode),
    ("rejects an unknown selected atlas cluster", RejectsUnknownCluster),
    ("rejects an unknown selected certified edge", RejectsUnknownEdge),
    ("rejects mixed atlas and source bindings", RejectsMixedBinding),
    ("rejects gesture endpoints outside the saved selection", RejectsGestureOutsideSelection),
    ("rejects empty selections", RejectsEmptySelection),
    ("rejects compare gestures without two sides", RejectsIncompleteComparison),
    ("keeps observation identity distinct from candidate identity", KeepsObservationDistinct)
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
    using var environment = ObservationEnvironment.Create();
    HumanStructureObservation observation = environment.Observation();

    HumanStructureObservationRegistration first =
        HumanStructureObservationRegistrar.Register(
            environment.Store,
            observation);
    HumanStructureObservationRegistration replay =
        HumanStructureObservationRegistrar.Register(
            environment.Store,
            observation);

    Assert.Equal(first.ObservationRef, replay.ObservationRef);
    Assert.Equal(first.ReceiptRef, replay.ReceiptRef);
    Assert.Equal("private-research", first.PrivacyClass);

    HumanStructureObservationReceipt receipt =
        environment.Store.Get<HumanStructureObservationReceipt>(
            first.ReceiptRef);
    Assert.Equal(observation.ObservationId, receipt.ObservationId);
    Assert.Equal(
        environment.AtlasRegistration.ReceiptRef,
        receipt.TopologyAtlasInputReceiptRef);
    Assert.Equal(environment.AtlasDigest, receipt.TopologyAtlasDigest);
}

static void RejectsImplicitObservation()
{
    using var environment = ObservationEnvironment.Create();
    HumanStructureObservation observation = environment.Observation(
        transform: content => content with { ExplicitlySaved = false });
    Assert.Throws(() => HumanStructureObservationRegistrar.Register(
        environment.Store,
        observation));
}

static void RejectsUnknownNode()
{
    using var environment = ObservationEnvironment.Create();
    HumanStructureObservation observation = environment.Observation(
        transform: content => content with
        {
            Selection = content.Selection with
            {
                SelectedNodeIds = ["missing-node", "node-b"]
            },
            Gesture = content.Gesture with
            {
                SourceNodeIds = ["missing-node"]
            }
        });
    Assert.Throws(() => HumanStructureObservationRegistrar.Register(
        environment.Store,
        observation));
}

static void RejectsUnknownCluster()
{
    using var environment = ObservationEnvironment.Create();
    string missing = "cluster:sha256:" + new string('9', 64);
    HumanStructureObservation observation = environment.Observation(
        transform: content => content with
        {
            Selection = content.Selection with
            {
                SelectedClusterIds = [missing]
            }
        });
    Assert.Throws(() => HumanStructureObservationRegistrar.Register(
        environment.Store,
        observation));
}

static void RejectsUnknownEdge()
{
    using var environment = ObservationEnvironment.Create();
    HumanStructureObservation observation = environment.Observation(
        transform: content => content with
        {
            Selection = content.Selection with
            {
                SelectedEdges =
                [
                    new HumanStructureSelectedEdge("node-a", "node-c")
                ]
            }
        });
    Assert.Throws(() => HumanStructureObservationRegistrar.Register(
        environment.Store,
        observation));
}

static void RejectsMixedBinding()
{
    using var environment = ObservationEnvironment.Create();
    HumanStructureObservation observation = environment.Observation(
        transform: content => content with
        {
            TopologyAtlasDigest = "sha256:" + new string('f', 64)
        });
    Assert.Throws(() => HumanStructureObservationRegistrar.Register(
        environment.Store,
        observation));

    HumanStructureObservation sourceMismatch = environment.Observation(
        transform: content => content with
        {
            SourceCommit = new string('3', 40)
        });
    Assert.Throws(() => HumanStructureObservationRegistrar.Register(
        environment.Store,
        sourceMismatch));
}

static void RejectsGestureOutsideSelection()
{
    using var environment = ObservationEnvironment.Create();
    HumanStructureObservation observation = environment.Observation(
        transform: content => content with
        {
            Gesture = content.Gesture with
            {
                TargetNodeIds = ["node-c"]
            }
        });
    Assert.Throws(() => HumanStructureObservationRegistrar.Register(
        environment.Store,
        observation));
}

static void RejectsEmptySelection()
{
    using var environment = ObservationEnvironment.Create();
    HumanStructureObservation observation = environment.Observation(
        transform: content => content with
        {
            Selection = new HumanStructureSelection([], [], [], null),
            Gesture = new HumanStructureGesture(
                "selection",
                [],
                [],
                [],
                [])
        });
    Assert.Throws(() => HumanStructureObservationRegistrar.Register(
        environment.Store,
        observation));
}

static void RejectsIncompleteComparison()
{
    using var environment = ObservationEnvironment.Create();
    HumanStructureObservation observation = environment.Observation(
        transform: content => content with
        {
            Gesture = content.Gesture with
            {
                TargetNodeIds = []
            }
        });
    Assert.Throws(() => HumanStructureObservationRegistrar.Register(
        environment.Store,
        observation));
}

static void KeepsObservationDistinct()
{
    using var environment = ObservationEnvironment.Create();
    HumanStructureObservation observation = environment.Observation();
    HumanStructureObservationRegistration registration =
        HumanStructureObservationRegistrar.Register(
            environment.Store,
            observation);

    Assert.True(registration.ObservationRef.StartsWith(
        "sha256:",
        StringComparison.Ordinal));
    Assert.Equal(
        observation.ObservationId,
        CanonicalJson.Sha256Reference(
            CanonicalJson.Serialize(observation.ObservationContent)));
    Assert.Throws(() => environment.Store.Get<HumanResearchCandidate>(
        registration.ObservationRef));
}

sealed class ObservationEnvironment : IDisposable
{
    private ObservationEnvironment(
        string root,
        ArtifactStore store,
        TopologyAtlasResearchInputRegistration atlasRegistration,
        string atlasDigest)
    {
        Root = root;
        Store = store;
        AtlasRegistration = atlasRegistration;
        AtlasDigest = atlasDigest;
    }

    public string Root { get; }
    public ArtifactStore Store { get; }
    public TopologyAtlasResearchInputRegistration AtlasRegistration { get; }
    public string AtlasDigest { get; }

    public static ObservationEnvironment Create()
    {
        string root = Path.Combine(
            Path.GetTempPath(),
            "trureturing-human-observation-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(root);
        var store = new ArtifactStore(root);
        byte[] atlas = File.ReadAllBytes(Path.Combine(
            AppContext.BaseDirectory,
            "fixtures",
            "topology-atlas.v1.json"));
        string atlasDigest = Digest(atlas);
        var publication = new TopologyAtlasPublicationCoordinate(
            TopologyAtlasResearchInputSchemas.Publication,
            "sha256:" + new string('5', 64),
            "sha256:" + new string('6', 64),
            atlasDigest,
            new string('1', 40),
            new string('2', 40),
            "sha256:" + new string('a', 64),
            "sha256:" + new string('b', 64),
            new string('c', 40));
        TopologyAtlasResearchInputRegistration registration =
            TopologyAtlasResearchInputRegistrar.Register(
                root,
                publication,
                atlas,
                Path.Combine(root, "work", "topology-atlas-cursor.json"));
        return new ObservationEnvironment(
            root,
            store,
            registration,
            atlasDigest);
    }

    public HumanStructureObservation Observation(
        Func<HumanStructureObservationContent,
            HumanStructureObservationContent>? transform = null)
    {
        string community =
            "cluster:sha256:" + new string('2', 64);
        var content = new HumanStructureObservationContent(
            AtlasRegistration.ReceiptRef,
            "sha256:" + new string('5', 64),
            "sha256:" + new string('6', 64),
            AtlasDigest,
            "sha256:" + new string('d', 64),
            null,
            new string('1', 40),
            new string('2', 40),
            "trureturing-pages",
            "human:lexa",
            new HumanStructureSelection(
                ["node-a", "node-b"],
                [community],
                [new HumanStructureSelectedEdge("node-a", "node-b")],
                null),
            new HumanStructureGesture(
                "compare",
                ["node-a"],
                ["node-b"],
                [],
                []),
            "These concepts appear to share a stronger invariant.",
            "private-research",
            true,
            "2026-08-31T12:00:00Z");
        if (transform is not null)
        {
            content = transform(content);
        }
        return new HumanStructureObservation(
            HumanStructureObservationSchemas.Observation,
            CanonicalJson.Sha256Reference(CanonicalJson.Serialize(content)),
            content);
    }

    public void Dispose()
    {
        if (Directory.Exists(Root))
        {
            Directory.Delete(Root, recursive: true);
        }
    }

    private static string Digest(ReadOnlySpan<byte> bytes) =>
        "sha256:" + Convert.ToHexStringLower(SHA256.HashData(bytes));
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
