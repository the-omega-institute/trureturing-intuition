using System.Globalization;

namespace Trureturing.Intuition.Core;

public static partial class ContractValidator
{
    private static readonly IReadOnlySet<string> StructureFormalizationOutcomeSet =
        new HashSet<string>(StringComparer.Ordinal)
        {
            StructureFormalizationOutcomes.Verified,
            StructureFormalizationOutcomes.Refuted,
            StructureFormalizationOutcomes.Inconclusive,
            StructureFormalizationOutcomes.InfrastructureFailure
        };

    public static void Validate(StructureFormalizationResult value)
    {
        ArgumentNullException.ThrowIfNull(value);
        RequireSchema(value.Schema, StructureFormalizationResultSchemas.Result);
        ArgumentNullException.ThrowIfNull(value.ResultContent);
        Validate(value.ResultContent);
        RequireArtifactRef(value.ResultId, nameof(value.ResultId));
        string expected = CanonicalJson.Sha256Reference(
            CanonicalJson.Serialize(value.ResultContent));
        if (!StringComparer.Ordinal.Equals(value.ResultId, expected))
        {
            throw new InvalidOperationException(
                "result_id does not address canonical result_content bytes.");
        }
    }

    public static void Validate(StructureFormalizationResultContent value)
    {
        ArgumentNullException.ThrowIfNull(value);
        RequireArtifactRef(value.CandidateRef, nameof(value.CandidateRef));
        RequireArtifactRef(value.CandidateId, nameof(value.CandidateId));
        RequireArtifactRef(
            value.FormalizationRequestRef,
            nameof(value.FormalizationRequestRef));
        RequireArtifactRef(
            value.TruthReleaseDigest,
            nameof(value.TruthReleaseDigest));
        RequireArtifactRef(
            value.TopologyAtlasDigest,
            nameof(value.TopologyAtlasDigest));
        if (!StructureFormalizationOutcomeSet.Contains(value.Outcome))
        {
            throw new InvalidOperationException(
                $"Unsupported formalization outcome '{value.Outcome}'.");
        }
        RequireSettlementText(value.Verifier, nameof(value.Verifier), 256);
        if (value.VerificationReceiptRef is not null)
        {
            RequireArtifactRef(
                value.VerificationReceiptRef,
                nameof(value.VerificationReceiptRef));
        }
        if (value.FormalArtifactRef is not null)
        {
            RequireArtifactRef(
                value.FormalArtifactRef,
                nameof(value.FormalArtifactRef));
        }
        if (value.StatementDigest is not null)
        {
            RequireArtifactRef(
                value.StatementDigest,
                nameof(value.StatementDigest));
        }
        RequireSortedUniqueRefs(
            value.DiagnosticArtifactRefs,
            nameof(value.DiagnosticArtifactRefs));
        if (value.Outcome is StructureFormalizationOutcomes.Verified
            or StructureFormalizationOutcomes.Refuted)
        {
            if (value.VerificationReceiptRef is null)
            {
                throw new InvalidOperationException(
                    "Verified or refuted formalization requires verification_receipt_ref.");
            }
        }
        if (value.Outcome == StructureFormalizationOutcomes.Verified &&
            value.FormalArtifactRef is null)
        {
            throw new InvalidOperationException(
                "Verified formalization requires formal_artifact_ref.");
        }
        if (value.Outcome == StructureFormalizationOutcomes.InfrastructureFailure &&
            (value.FormalArtifactRef is not null || value.StatementDigest is not null))
        {
            throw new InvalidOperationException(
                "Infrastructure failure cannot claim a formal artifact or statement digest.");
        }
        if (!StringComparer.Ordinal.Equals(
                value.Authority,
                "formalize-execution-evidence"))
        {
            throw new InvalidOperationException(
                "Formalization result authority must be formalize-execution-evidence.");
        }
        if (!DateTimeOffset.TryParse(
                value.CompletedAt,
                CultureInfo.InvariantCulture,
                DateTimeStyles.AssumeUniversal | DateTimeStyles.AdjustToUniversal,
                out _))
        {
            throw new InvalidOperationException(
                "completed_at must be an RFC 3339 timestamp.");
        }
    }

    public static void Validate(
        StructureFormalizationResultPublicationCoordinate value)
    {
        ArgumentNullException.ThrowIfNull(value);
        RequireSchema(
            value.Schema,
            StructureFormalizationResultSchemas.Publication);
        RequireArtifactRef(value.ResultDigest, nameof(value.ResultDigest));
        RequireArtifactRef(value.CandidateRef, nameof(value.CandidateRef));
        RequireArtifactRef(value.CandidateId, nameof(value.CandidateId));
        RequireArtifactRef(
            value.FormalizationRequestRef,
            nameof(value.FormalizationRequestRef));
        RequireArtifactRef(
            value.TruthReleaseDigest,
            nameof(value.TruthReleaseDigest));
        RequireArtifactRef(
            value.TopologyAtlasDigest,
            nameof(value.TopologyAtlasDigest));
        RequireSettlementText(value.Producer, nameof(value.Producer), 256);
        RequireGitId(value.ProducerCommit, nameof(value.ProducerCommit));
        if (value.ProducerCommit.Length != 40)
        {
            throw new InvalidOperationException(
                "producer_commit must use a 40-character Git object ID.");
        }
    }

    private static void RequireSettlementText(
        string? value,
        string name,
        int maximum)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            throw new InvalidOperationException($"{name} is empty.");
        }
        if (value.Length > maximum)
        {
            throw new InvalidOperationException(
                $"{name} exceeds {maximum} characters.");
        }
    }
}
