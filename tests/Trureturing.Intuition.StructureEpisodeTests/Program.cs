using System.Security.Cryptography;
using Trureturing.Intuition.Core;

var tests = new (string Name, Action Run)[]
{
    ("normalizes an edge-backed node comparison deterministically", NormalizesNodePair),
    ("normalizes a cluster peel episode", NormalizesClusterPeel),
    ("normalizes certified path inspection", NormalizesPathInspection),
    ("normalizes frontier research", NormalizesFrontier),
    ("rejects an observation receipt substitution", RejectsReceiptSubstitution),
    ("preserves privacy without creating a candidate", PreservesPrivacyAndAuthority),
    ("rejects tampered episode identity", RejectsTamperedEpisodeIdentity)
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

static void NormalizesNodePair()
{
    using var environment = EpisodeEnvironment.Create();
    (string observationRef, string receiptRef) = environment.RegisterObservation(
        environment.Observation());

    StructureEditEpisodeRegistration first =
        StructureEditEpisodeNormalizer.Normalize(
            environment.Store,
            observationRef,
            receiptRef);
    StructureEditEpisodeRegistration replay =
        StructureEditEpisodeNormalizer.Normalize(
            environment.Store,
            observationRef,
            receiptRef);

    Assert.Equal(first.EpisodeRef, replay.EpisodeRef);
    Assert.Equal(first.ReceiptRef, replay.ReceiptRef);
    Assert.Equal("node-pair", first.SelectionKind);
    Assert.Equal(6, first.CandidateLimit);
    Assert.Contains(StructureEditKinds.AddBridge, first.AllowedEditKinds);
    Assert.Contains(StructureEditKinds.AddAbstraction, first.AllowedEditKinds);
    Assert.Contains(StructureEditKinds.ChangeRepresentation, first.AllowedEditKinds);

    StructureEditEpisode episode =
        environment.Store.Get<StructureEditEpisode>(first.EpisodeRef);
    Assert.Equal(observationRef, episode.EpisodeContent.ObservationRef);
    Assert.Equal(receiptRef, episode.EpisodeContent.ObservationReceiptRef);
    Assert.Equal(
        StructureEditEpisodeSchemas.NormalizationProfile,
        episode.EpisodeContent.NormalizationProfile);
    Assert.Equal(
        first.EpisodeId,
        CanonicalJson.Sha256Reference(
            CanonicalJson.Serialize(episode.EpisodeContent)));
}

static void NormalizesClusterPeel()
{
    using var environment = EpisodeEnvironment.Create();
    string cluster = "cluster:sha256:" + new string('2', 64);
    HumanStructureObservation observation = environment.Observation(
        selection: new HumanStructureSelection(
            [],
            [cluster],
            [],
            null),
        gesture: new HumanStructureGesture(
            "cluster-peel",
            [],
            [],
            [cluster],
            []));
    (string observationRef, string receiptRef) =
        environment.RegisterObservation(observation);

    StructureEditEpisodeRegistration result =
        StructureEditEpisodeNormalizer.Normalize(
            environment.Store,
            observationRef,
            receiptRef);

    Assert.Equal("single-cluster", result.SelectionKind);
    Assert.Equal(3, result.CandidateLimit);
    Assert.Contains(StructureEditKinds.AddBridge, result.AllowedEditKinds);
    Assert.Contains(StructureEditKinds.AddSubgoal, result.AllowedEditKinds);
    Assert.Contains(StructureEditKinds.Reroot, result.AllowedEditKinds);
}

static void NormalizesPathInspection()
{
    using var environment = EpisodeEnvironment.Create();
    string pathRef = "sha256:" + new string('e', 64);
    HumanStructureObservation observation = environment.Observation(
        selection: new HumanStructureSelection(
            ["node-a", "node-b"],
            [],
            [new HumanStructureSelectedEdge("node-a", "node-b")],
            pathRef),
        gesture: new HumanStructureGesture(
            "path-inspection",
            ["node-a"],
            ["node-b"],
            [],
            []));
    (string observationRef, string receiptRef) =
        environment.RegisterObservation(observation);

    StructureEditEpisodeRegistration result =
        StructureEditEpisodeNormalizer.Normalize(
            environment.Store,
            observationRef,
            receiptRef);

    Assert.Equal("certified-path", result.SelectionKind);
    Assert.Contains(StructureEditKinds.AddCounterexample, result.AllowedEditKinds);
    Assert.Contains(StructureEditKinds.AddSubgoal, result.AllowedEditKinds);
    Assert.Contains(StructureEditKinds.Reroot, result.AllowedEditKinds);
}

static void NormalizesFrontier()
{
    using var environment = EpisodeEnvironment.Create();
    HumanStructureObservation observation = environment.Observation(
        selection: new HumanStructureSelection(
            ["node-c"],
            [],
            [],
            null),
        gesture: new HumanStructureGesture(
            "frontier-mark",
            ["node-c"],
            [],
            [],
            []));
    (string observationRef, string receiptRef) =
        environment.RegisterObservation(observation);

    StructureEditEpisodeRegistration result =
        StructureEditEpisodeNormalizer.Normalize(
            environment.Store,
            observationRef,
            receiptRef);

    Assert.Equal("frontier-region", result.SelectionKind);
    Assert.Contains(StructureEditKinds.AcquireEvidence, result.AllowedEditKinds);
    Assert.Contains(StructureEditKinds.RegisterOpenQuestion, result.AllowedEditKinds);
}

static void RejectsReceiptSubstitution()
{
    using var environment = EpisodeEnvironment.Create();
    (string observationRef, string receiptRef) = environment.RegisterObservation(
        environment.Observation());
    HumanStructureObservationReceipt receipt =
        environment.Store.Get<HumanStructureObservationReceipt>(receiptRef);
    string substituted = environment.Store.Put(receipt with
    {
        ObservationId = "sha256:" + new string('f', 64)
    });

    Assert.Throws(() => StructureEditEpisodeNormalizer.Normalize(
        environment.Store,
        observationRef,
        substituted));
}

static void PreservesPrivacyAndAuthority()
{
    using var environment = EpisodeEnvironment.Create();
    HumanStructureObservation observation = environment.Observation(
        privacyClass: "team-research");
    (string observationRef, string receiptRef) =
        environment.RegisterObservation(observation);

    StructureEditEpisodeRegistration result =
        StructureEditEpisodeNormalizer.Normalize(
            environment.Store,
            observationRef,
            receiptRef);
    Assert.Equal("team-research", result.PrivacyClass);
    StructureEditEpisode episode =
        environment.Store.Get<StructureEditEpisode>(result.EpisodeRef);
    Assert.Equal("team-research", episode.EpisodeContent.PrivacyClass);
    Assert.Throws(() => environment.Store.Get<CandidateEdit>(result.EpisodeRef));
    Assert.Throws(() => environment.Store.Get<ResearchAttempt>(result.EpisodeRef));
    Assert.Throws(() => environment.Store.Get<FormalizationRequest>(result.EpisodeRef));
}

static void RejectsTamperedEpisodeIdentity()
{
    using var environment = EpisodeEnvironment.Create();
    (string observationRef, string receiptRef) = environment.RegisterObservation(
        environment.Observation());
    StructureEditEpisodeRegistration result =
        StructureEditEpisodeNormalizer.Normalize(
            environment.Store,
            observationRef,
            receiptRef);
    StructureEditEpisode episode =
        environment.Store.Get<StructureEditEpisode>(result.EpisodeRef);

    Assert.Throws(() => ContractValidator.Validate(episode with
    {
        EpisodeId = "sha256:" + new string('f', 64)
    }));
}

sealed class EpisodeEnvironment : IDisposable
{
    private EpisodeEnvironment(
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

    public static EpisodeEnvironment Create()
    {
        string root = Path.Combine(
            Path.GetTempPath(),
            "trureturing-structure-episode-" + Guid.NewGuid().ToString("N"));
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
        return new EpisodeEnvironment(root, store, registration, atlasDigest);
    }

    public HumanStructureObservation Observation(
        HumanStructureSelection? selection = null,
        HumanStructureGesture? gesture = null,
        string privacyClass = "private-research")
    {
        selection ??= new HumanStructureSelection(
            ["node-a", "node-b"],
            [],
            [new HumanStructureSelectedEdge("node-a", "node-b")],
            null);
        gesture ??= new HumanStructureGesture(
            "compare",
            ["node-a"],
            ["node-b"],
            [],
            []);
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
            selection,
            gesture,
            "Explore the structural relation without presupposing a theorem.",
            privacyClass,
            true,
            "2026-08-31T12:00:00Z");
        return new HumanStructureObservation(
            HumanStructureObservationSchemas.Observation,
            CanonicalJson.Sha256Reference(CanonicalJson.Serialize(content)),
            content);
    }

    public (string ObservationRef, string ReceiptRef) RegisterObservation(
        HumanStructureObservation observation)
    {
        HumanStructureObservationRegistration result =
            HumanStructureObservationRegistrar.Register(Store, observation);
        return (result.ObservationRef, result.ReceiptRef);
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
    public static void Equal<T>(T expected, T actual)
    {
        if (!EqualityComparer<T>.Default.Equals(expected, actual))
        {
            throw new InvalidOperationException(
                $"Expected '{expected}', got '{actual}'.");
        }
    }

    public static void Contains<T>(T expected, IEnumerable<T> values)
    {
        if (!values.Contains(expected))
        {
            throw new InvalidOperationException(
                $"Expected collection to contain '{expected}'.");
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
