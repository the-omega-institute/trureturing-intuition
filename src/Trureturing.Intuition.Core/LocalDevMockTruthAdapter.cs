namespace Trureturing.Intuition.Core;

public sealed record LocalDevMockTruthAdapterResult(
    TruthReleaseVerificationReceipt Receipt,
    string ReceiptRef,
    string ReleaseBindingRef,
    IReadOnlyDictionary<string, string> NodeRefs);

public static class LocalDevMockTruthAdapter
{
    public const string Identity = "local-dev-mock-truth-adapter-v1";
    public const string SourceRepository = "the-omega-institute/trureturing";
    public const string SourceBranch = "dev";
    public const string SourceCommit = "453e725795fda1d57bf01756cee8611f2c966d15";
    public const string SourceTree = "c21635d1dc8533602b81ffde03b414b1d4503d24";
    public const long FrozenAtUnix = 1787616000;
    public const string Caveat = "LOCAL DEV MOCK ONLY: this structurally valid receipt was not issued by the real Trureturing.Truth verifier and certifies no truth.";

    private static readonly (string Id, string Path, string Summary)[] Nodes =
    [
        ("D5/S0/Carrier/AlgebraicModel.golden_algebraic_model_spec", "Blueprint/D5/S0/Carrier/AlgebraicModel.md", "Quadratic quotient model with explicit conjugation, trace, and norm."),
        ("D5/S0/Carrier/Euclidean.golden_division", "Blueprint/D5/S0/Carrier/Norm.md", "Golden integers admit norm-Euclidean division."),
        ("D5/S0/Carrier/GoldenDiscriminant.golden_discriminant_spec", "Blueprint/D5/S0/Carrier/GoldenDiscriminant.md", "The golden polynomial has discriminant five and phi satisfies it."),
        ("D5/S0/Carrier/NormPowers.norm_pow", "Blueprint/D5/S0/Carrier/Norm.md", "The golden norm is power-multiplicative for natural powers."),
        ("D5/S0/Carrier/Powers/GoldenCriticalBandScaling.golden_critical_band_scaling", "Blueprint/D5/S0/Carrier/Powers/GoldenCriticalBandScaling.md", "The scaled golden band contains its critical midpoint."),
        ("D5/S0/Carrier/Powers/GoldenMidlineFactorization.golden_midline_factorization", "Blueprint/D5/S0/Carrier/Powers/GoldenMidlineFactorization.md", "The golden midline marker factors as one half times reciprocal phi squared."),
        ("D5/S0/Carrier/Powers/IntegerPowerNorm.norm_phiUnit_zpow", "Blueprint/D5/S0/Carrier/Powers/IntegerPowerNorm.md", "The norm of an integral power of the golden unit is controlled."),
        ("D5/S0/Carrier/PrincipalIdeal.golden_int_is_pid", "Blueprint/D5/S0/Carrier/Norm.md", "The golden integer ring is a principal ideal domain."),
        ("D5/S0/Carrier/TraceConjugation.trace_conj", "Blueprint/D5/S0/Carrier/TraceConjugation.md", "Golden trace is invariant under Galois conjugation."),
        ("D5/S0/Carrier/ZsqrtdImage.mem_range_toZsqrtd_iff", "Blueprint/D5/S0/Carrier/ZsqrtdImage.md", "Exact membership criterion for the doubled-coordinate Zsqrtd image.")
    ];

    public static LocalDevMockTruthAdapterResult Produce(ArtifactStore store)
    {
        var nodeRefs = new SortedDictionary<string, string>(StringComparer.Ordinal);
        foreach (var node in Nodes)
        {
            var artifact = new LocalDevFrozenTruthNode(
                Schemas.LocalDevFrozenNode,
                node.Id,
                SourceCommit,
                SourceTree,
                node.Path,
                node.Summary);
            nodeRefs.Add(node.Id, store.Put(artifact));
        }

        var sortedRefs = nodeRefs.Values.Order(StringComparer.Ordinal).ToArray();
        var graphRef = store.Put(new LocalDevMockTruthSubset(
            Schemas.LocalDevTruthSubset,
            LocalDevTruthSubsetKind.Graph,
            Identity,
            SourceRepository,
            SourceBranch,
            SourceCommit,
            SourceTree,
            sortedRefs,
            Caveat));
        var exportRef = store.Put(new LocalDevMockTruthSubset(
            Schemas.LocalDevTruthSubset,
            LocalDevTruthSubsetKind.Export,
            Identity,
            SourceRepository,
            SourceBranch,
            SourceCommit,
            SourceTree,
            sortedRefs,
            Caveat));
        var releaseRef = store.Put(new LocalDevMockTruthRelease(
            Schemas.LocalDevTruthRelease,
            Identity,
            SourceCommit,
            SourceTree,
            graphRef,
            exportRef,
            Caveat));

        // The schema fixes verified_by to the upstream identity. The surrounding adapter artifact
        // and caveat make explicit that this local fixture performs no upstream verification.
        var receipt = new TruthReleaseVerificationReceipt(
            Schemas.TruthReceipt,
            releaseRef,
            SourceCommit,
            SourceTree,
            graphRef,
            exportRef,
            Schemas.TruthVerifierIdentity,
            FrozenAtUnix);
        var receiptRef = store.Put(receipt);
        return new LocalDevMockTruthAdapterResult(receipt, receiptRef, releaseRef, nodeRefs);
    }
}
