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
        if (artifact is ResearchAttempt attempt)
        {
            var state = Get<IntuitionState>(attempt.StateRef);
            var allocation = Get<IntuitionAllocation>(attempt.AllocationRef);
            if (state.BaseWriteAllowed || state.SelectionMode == "shadow-pareto-bootstrap-v1" || allocation.Policy == "shadow-pareto-bootstrap-v1" || allocation.SelectedForExecution.Count == 0)
            {
                throw new InvalidOperationException("shadow-pareto-bootstrap-v1 forbids execution attempts.");
            }
        }
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
        return value;
    }

    public string PathFor(string reference)
    {
        ContractValidator.RequireArtifactRef(reference, nameof(reference));
        var hex = reference[7..];
        return Path.Combine(_root, "sha256", hex[..2], hex + ".json");
    }
}
