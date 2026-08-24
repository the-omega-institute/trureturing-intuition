var root = FindRoot();
var failures = new List<string>();
var productionRoots = new[]
{
    Path.Combine(root, "src"),
    Path.Combine(root, ".fkst", "local-packages", "trureturing-intuition")
};
var forbiddenComposition = new[]
{
    "/Users/", "\\Users\\", "deployment-set", "fkst.lock", "substrate-ref",
    "machine-profile", "engine_revision", "target_identity", "github_write_enabled",
    "cadence_enabled", "lock_ref"
};
foreach (var productionRoot in productionRoots)
{
    foreach (var path in Directory.EnumerateFiles(productionRoot, "*", SearchOption.AllDirectories).Where(IsSourceFile).Where(IsTextFile))
    {
        var text = File.ReadAllText(path);
        foreach (var token in forbiddenComposition)
        {
            if (text.Contains(token, StringComparison.OrdinalIgnoreCase))
            {
                failures.Add($"{Path.GetRelativePath(root, path)}: forbidden composition token {token}");
            }
        }
    }
}

var luaRoot = Path.Combine(root, ".fkst", "local-packages", "trureturing-intuition");
var luaForbidden = new[]
{
    "Trureturing.Truth", "StrataLint", "FrozenLedger", "TruthGraphJsonReader",
    "TruthExportJsonReader", "git push", "gh pr", "api.github.com"
};
foreach (var path in Directory.EnumerateFiles(luaRoot, "*.lua", SearchOption.AllDirectories))
{
    var text = File.ReadAllText(path);
    foreach (var token in luaForbidden)
    {
        if (text.Contains(token, StringComparison.OrdinalIgnoreCase))
        {
            failures.Add($"{Path.GetRelativePath(root, path)}: Lua owns upstream/business authority token {token}");
        }
    }
}

var manifest = File.ReadAllText(Path.Combine(luaRoot, "fkst.toml"));
foreach (var token in new[] { "deployment", "provider", "target_identity", "engine_revision", "github_write_enabled", "cadence_enabled" })
{
    if (manifest.Contains(token, StringComparison.OrdinalIgnoreCase)) failures.Add($"fkst.toml contains composition token {token}");
}

var authorityRoots = new[] { "Trureturing.Intuition.Core", "Trureturing.Intuition.Cli" }
    .Select(project => Path.Combine(root, "src", project));
failures.AddRange(FindAuthorityDuplicationFailures(authorityRoots, root));

using (var fixture = new TempDirectory())
{
    var nestedCli = Path.Combine(fixture.Path, "Trureturing.Intuition.Cli", "Commands", "NestedAuthority.cs");
    Directory.CreateDirectory(Path.GetDirectoryName(nestedCli)!);
    File.WriteAllText(nestedCli, "using " + "Trureturing.Truth;\n");
    if (FindAuthorityDuplicationFailures(new[] { Path.Combine(fixture.Path, "Trureturing.Intuition.Cli") }, fixture.Path).Count != 1)
    {
        failures.Add("authority boundary scanner did not detect a forbidden token in a nested CLI source file");
    }
}

if (failures.Count != 0)
{
    foreach (var failure in failures) Console.Error.WriteLine(failure);
    return 1;
}
Console.WriteLine("architecture boundary: pass");
return 0;

static string FindRoot()
{
    var directory = new DirectoryInfo(AppContext.BaseDirectory);
    while (directory is not null)
    {
        if (File.Exists(Path.Combine(directory.FullName, "Trureturing.Intuition.slnx"))) return directory.FullName;
        directory = directory.Parent;
    }
    throw new InvalidOperationException("Repository root not found.");
}

static bool IsTextFile(string path)
{
    var extension = Path.GetExtension(path);
    return extension is ".cs" or ".csproj" or ".md" or ".json" or ".toml" or ".lua" or ".yml" or ".yaml" or ".props" or ".slnx";
}

static List<string> FindAuthorityDuplicationFailures(IEnumerable<string> projectRoots, string displayRoot)
{
    var failures = new List<string>();
    var forbidden = new[] { "namespace StrataLint", "using StrataLint", "using Trureturing.Truth", "FrozenLedger", "TruthGraphJsonReader", "TruthExportJsonReader" };
    foreach (var projectRoot in projectRoots)
    {
        foreach (var path in Directory.EnumerateFiles(projectRoot, "*.cs", SearchOption.AllDirectories).Where(IsSourceFile))
        {
            var text = File.ReadAllText(path);
            foreach (var token in forbidden)
            {
                if (text.Contains(token, StringComparison.Ordinal)) failures.Add($"{Path.GetRelativePath(displayRoot, path)} duplicates upstream authority: {token}");
            }
        }
    }
    return failures;
}

static bool IsSourceFile(string path) => !path
    .Split(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar)
    .Any(segment => segment.Equals("bin", StringComparison.OrdinalIgnoreCase)
        || segment.Equals("obj", StringComparison.OrdinalIgnoreCase)
        || segment.Equals("generated", StringComparison.OrdinalIgnoreCase));

sealed class TempDirectory : IDisposable
{
    public string Path { get; } = System.IO.Path.Combine(System.IO.Path.GetTempPath(), "intuition-architecture-test-" + Guid.NewGuid().ToString("N"));
    public TempDirectory() => Directory.CreateDirectory(Path);
    public void Dispose() => Directory.Delete(Path, recursive: true);
}
