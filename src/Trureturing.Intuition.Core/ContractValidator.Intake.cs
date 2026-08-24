using System.Security.Cryptography;
using System.Text;
using System.Text.Encodings.Web;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace Trureturing.Intuition.Core;

public static partial class ContractValidator
{
public static void Validate(TruthReleaseVerificationReceipt value)
    {
        RequireSchema(value.Schema, Schemas.TruthReceipt);
        RequireArtifactRef(value.ReleaseDigest, nameof(value.ReleaseDigest));
        RequireGitId(value.SourceCommit, nameof(value.SourceCommit));
        RequireGitId(value.SourceTree, nameof(value.SourceTree));
        if (value.SourceCommit.Length != value.SourceTree.Length) throw new InvalidOperationException("source_commit and source_tree use different hash algorithms.");
        RequireArtifactRef(value.TruthGraphRef, nameof(value.TruthGraphRef));
        RequireArtifactRef(value.TruthExportRef, nameof(value.TruthExportRef));
        RequireNonEmpty(value.VerifiedBy, nameof(value.VerifiedBy));
        if (value.VerifiedAtUnix < 0) throw new InvalidOperationException("verified_at_unix is negative.");
    }

    public static void Validate(IntuitionRunRequest value)
    {
        RequireSchema(value.Schema, Schemas.RunRequest);
        RequireIdentifier(value.RunId, nameof(value.RunId));
        RequireArtifactRef(value.TruthReleaseReceiptRef, nameof(value.TruthReleaseReceiptRef));
        RequireArtifactRef(value.TargetInterfaceRef, nameof(value.TargetInterfaceRef));
        RequireArtifactRef(value.ResidualUniverseRef, nameof(value.ResidualUniverseRef));
        RequireSortedUniqueRefs(value.CandidateUniverse, nameof(value.CandidateUniverse));
        RequireNonEmpty(value.HistoryCutoff, nameof(value.HistoryCutoff));
        ValidateBudget(value.Budget);
        RequireNonEmpty(value.VerificationProtocol, nameof(value.VerificationProtocol));
        RequireArtifactRef(value.ModelSnapshot, nameof(value.ModelSnapshot));
        if (value.SelectionMode != "shadow-pareto-bootstrap-v1") throw new InvalidOperationException("Only shadow-pareto-bootstrap-v1 is allowed in v1.");
    }

    public static void Validate(TargetInterface value)
    {
        RequireSchema(value.Schema, Schemas.TargetInterface);
        RequireIdentifier(value.TargetId, nameof(value.TargetId));
        RequireArtifactRef(value.CurrentReadoutRef, nameof(value.CurrentReadoutRef));
        RequireArtifactRef(value.TargetReadoutRef, nameof(value.TargetReadoutRef));
        RequireArtifactRef(value.WorldRef, nameof(value.WorldRef));
        if (value.AdequacyMode == AdequacyMode.ExactFormal)
        {
            RequireArtifactRef(value.FormalAdequacyReceiptRef, nameof(value.FormalAdequacyReceiptRef));
        }
        else if (value.FormalAdequacyReceiptRef is not null)
        {
            throw new InvalidOperationException("formal_adequacy_receipt_ref is reserved for exact_formal mode.");
        }
    }

    public static void Validate(ResidualWitness value)
    {
        RequireSchema(value.Schema, Schemas.ResidualWitness);
        RequireIdentifier(value.WitnessId, nameof(value.WitnessId));
        RequireArtifactRef(value.TargetInterfaceRef, nameof(value.TargetInterfaceRef));
        RequireArtifactRef(value.StateXRef, nameof(value.StateXRef));
        RequireArtifactRef(value.StateYRef, nameof(value.StateYRef));
        RequireArtifactRef(value.CurrentEqualReceiptRef, nameof(value.CurrentEqualReceiptRef));
        RequireArtifactRef(value.TargetDistinctReceiptRef, nameof(value.TargetDistinctReceiptRef));
        RequireNonEmpty(value.EvidenceStatus, nameof(value.EvidenceStatus));
    }

    public static void Validate(ResidualUniverse value)
    {
        RequireSchema(value.Schema, Schemas.ResidualUniverse);
        RequireIdentifier(value.UniverseId, nameof(value.UniverseId));
        RequireArtifactRef(value.TargetInterfaceRef, nameof(value.TargetInterfaceRef));
        RequireSortedUniqueRefs(value.WitnessRefs, nameof(value.WitnessRefs));
        if (value.Kind == ResidualUniverseKind.FormalComplete)
        {
            RequireArtifactRef(value.FormalCompletenessReceiptRef, nameof(value.FormalCompletenessReceiptRef));
        }
        else if (value.FormalCompletenessReceiptRef is not null)
        {
            throw new InvalidOperationException("A finite_observed universe cannot carry a formal completeness receipt.");
        }
    }

    public static void Validate(CandidateEdit value)
    {
        RequireSchema(value.Schema, Schemas.CandidateEdit);
        RequireIdentifier(value.CandidateId, nameof(value.CandidateId));
        RequireSortedUniqueRefs(value.Inputs, nameof(value.Inputs));
        RequireSortedUniqueRefs(value.Outputs, nameof(value.Outputs));
        RequireNonEmpty(value.RepresentationMap, nameof(value.RepresentationMap));
        RequireSortedUniqueStrings(value.AssumptionMap, nameof(value.AssumptionMap));
        RequireSortedUniqueStrings(value.PreservedInvariants, nameof(value.PreservedInvariants));
        RequireSortedUniqueRefs(value.ClaimedResidualCuts, nameof(value.ClaimedResidualCuts));
        RequireNonEmpty(value.Falsifier, nameof(value.Falsifier));
        RequireNonEmpty(value.VerificationRoute, nameof(value.VerificationRoute));
    }

    public static void Validate(IntuitionState value)
    {
        RequireSchema(value.Schema, Schemas.State);
        RequireIdentifier(value.RunId, nameof(value.RunId));
        RequireArtifactRef(value.TruthReleaseReceiptRef, nameof(value.TruthReleaseReceiptRef));
        RequireArtifactRef(value.ReleaseDigest, nameof(value.ReleaseDigest));
        RequireGitId(value.SourceCommit, nameof(value.SourceCommit));
        RequireGitId(value.SourceTree, nameof(value.SourceTree));
        RequireArtifactRef(value.TruthGraphRef, nameof(value.TruthGraphRef));
        RequireArtifactRef(value.TruthExportRef, nameof(value.TruthExportRef));
        RequireArtifactRef(value.TargetInterfaceRef, nameof(value.TargetInterfaceRef));
        RequireArtifactRef(value.ResidualUniverseRef, nameof(value.ResidualUniverseRef));
        RequireSortedUniqueRefs(value.CandidateUniverse, nameof(value.CandidateUniverse));
        RequireArtifactRef(value.CandidateUniverseDigest, nameof(value.CandidateUniverseDigest));
        ValidateBudget(value.Budget);
        RequireArtifactRef(value.ModelSnapshot, nameof(value.ModelSnapshot));
        if (value.SelectionMode != "shadow-pareto-bootstrap-v1") throw new InvalidOperationException("Unexpected selection mode.");
        if (value.ScalarizationAllowed) throw new InvalidOperationException("Scalarization is forbidden in v1.");
        if (value.BaseWriteAllowed) throw new InvalidOperationException("Base writes are forbidden.");
    }
}
