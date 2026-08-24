using System.Security.Cryptography;
using System.Text;
using System.Text.Encodings.Web;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace Trureturing.Intuition.Core;

public sealed class ArtifactStore
{
    private readonly string _root;
    public ArtifactStore(string root) => _root = Path.GetFullPath(root ?? throw new ArgumentNullException(nameof(root)));

    public string Put<T>(T artifact)
    {
        ContractValidator.Validate(artifact!);
        ValidateStoreBoundary(artifact);
        var bytes = CanonicalJson.Serialize(artifact);
        var hex = Convert.ToHexString(SHA256.HashData(bytes)).ToLowerInvariant();
        var reference = $"sha256:{hex}";
        var path = PathFor(reference);
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        if (File.Exists(path))
        {
            if (!File.ReadAllBytes(path).SequenceEqual(bytes))
            {
                throw new InvalidOperationException($"Content-address collision at {reference}.");
            }
            return reference;
        }
        var temporary = path + ".tmp-" + Guid.NewGuid().ToString("N");
        File.WriteAllBytes(temporary, bytes);
        try
        {
            File.Move(temporary, path, overwrite: false);
        }
        catch (IOException) when (File.Exists(path))
        {
            if (!File.ReadAllBytes(path).SequenceEqual(bytes))
            {
                throw;
            }
            File.Delete(temporary);
        }
        return reference;
    }

    public T Get<T>(string reference)
    {
        var path = PathFor(reference);
        var bytes = File.ReadAllBytes(path);
        var expected = reference[7..];
        var actual = Convert.ToHexString(SHA256.HashData(bytes)).ToLowerInvariant();
        if (!string.Equals(expected, actual, StringComparison.Ordinal))
        {
            throw new InvalidOperationException($"Artifact {reference} failed digest verification.");
        }
        var value = CanonicalJson.DeserializeCanonical<T>(bytes);
        ContractValidator.Validate(value!);
        ValidateStoreBoundary(value);
        return value;
    }

    public string PathFor(string reference)
    {
        ContractValidator.RequireArtifactRef(reference, nameof(reference));
        var hex = reference[7..];
        return Path.Combine(_root, "sha256", hex[..2], hex + ".json");
    }

    public IReadOnlyList<(string Ref, T Value)> FindBySchema<T>(string schema)
    {
        var results = new List<(string Ref, T Value)>();
        var storePath = Path.Combine(_root, "sha256");
        if (!Directory.Exists(storePath)) return results;

        foreach (var path in Directory.EnumerateFiles(storePath, "*.json", SearchOption.AllDirectories))
        {
            using var document = JsonDocument.Parse(File.ReadAllBytes(path));
            if (!document.RootElement.TryGetProperty("schema", out var schemaElement)
                || !string.Equals(schemaElement.GetString(), schema, StringComparison.Ordinal)) continue;

            var hex = Path.GetFileNameWithoutExtension(path);
            var reference = $"sha256:{hex}";
            if (!string.Equals(path, PathFor(reference), StringComparison.Ordinal))
            {
                throw new InvalidOperationException($"Artifact {reference} is stored at a non-canonical path.");
            }
            results.Add((reference, Get<T>(reference)));
        }
        return results.OrderBy(static item => item.Ref, StringComparer.Ordinal).ToArray();
    }

    private void ValidateStoreBoundary<T>(T artifact)
    {
        if (artifact is IndependentSettlement independentSettlement)
        {
            ContractValidator.Validate(independentSettlement, ArtifactExistsWithValidDigest);
        }

        if (artifact is not ResearchAttempt attempt) return;

        var state = Get<IntuitionState>(attempt.StateRef);
        var allocation = Get<IntuitionAllocation>(attempt.AllocationRef);
        if (state.BaseWriteAllowed)
        {
            throw new InvalidOperationException("Research attempts cannot persist with base_write enabled.");
        }
        if (state.SelectionMode == "shadow-pareto-bootstrap-v1" || allocation.Policy == "shadow-pareto-bootstrap-v1")
        {
            throw new InvalidOperationException("shadow-pareto-bootstrap-v1 forbids execution attempts.");
        }
        if (allocation.SelectedForExecution.Count == 0 || !allocation.SelectedForExecution.Contains(attempt.ValuationRef, StringComparer.Ordinal))
        {
            throw new InvalidOperationException("Attempt valuation was not selected for execution.");
        }
    }

    private bool ArtifactExistsWithValidDigest(string reference)
    {
        var path = PathFor(reference);
        if (!File.Exists(path)) return false;
        var bytes = File.ReadAllBytes(path);
        var actual = Convert.ToHexString(SHA256.HashData(bytes)).ToLowerInvariant();
        return string.Equals(reference[7..], actual, StringComparison.Ordinal);
    }
}
