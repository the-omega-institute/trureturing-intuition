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

foreach (var authorityBoundary in new[] { "Trureturing.Intuition.Core", "Trureturing.Intuition.Cli" })
{
    foreach (var path in Directory.EnumerateFiles(Path.Combine(root, "src", authorityBoundary), "*.cs", SearchOption.TopDirectoryOnly))
    {
        var text = File.ReadAllText(path);
        foreach (var token in new[] { "namespace StrataLint", "using StrataLint", "using Trureturing.Truth", "FrozenLedger", "TruthGraphJsonReader", "TruthExportJsonReader" })
        {
            if (text.Contains(token, StringComparison.Ordinal)) failures.Add($"{Path.GetRelativePath(root, path)} duplicates upstream authority: {token}");
        }
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

static bool IsSourceFile(string path) => !path.Split(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar).Any(segment => segment is "bin" or "obj");
