internal static class TestCoordinates
{
    public static readonly string TruthReleaseDigest =
        "sha256:" + new string('5', 64);
    public static readonly string CertifiedTopologyDigest =
        "sha256:" + new string('6', 64);
    public static readonly string CertifiedProfileDigest =
        "sha256:" + new string('a', 64);
    public static readonly string AtlasProfileDigest =
        "sha256:" + new string('b', 64);
    public static readonly string EvidenceProfileDigest =
        "sha256:" + new string('d', 64);
    public static readonly string SourceCommit = new('1', 40);
    public static readonly string SourceTree = new('2', 40);
    public static readonly string ProducerCommit = new('c', 40);
    public static readonly string SourceCluster = Cluster('2');
    public static readonly string TargetCluster = Cluster('3');

    private static string Cluster(char value) =>
        "cluster:sha256:" + new string(value, 64);
}
