using System.Numerics;

namespace Trureturing.Intuition.Core;

public static partial class ContractValidator
{
    private static readonly IReadOnlySet<string> StructureOperationSettlementOutcomes =
        new HashSet<string>(StringComparer.Ordinal)
        {
            "realized",
            "not-realized",
            "already-present",
            "contradicted"
        };

    private static readonly IReadOnlySet<string> StructureSettlementStatusSet =
        new HashSet<string>(StringComparer.Ordinal)
        {
            StructureEditSettlementStatuses.VerifiedAndRealized,
            StructureEditSettlementStatuses.VerifiedNotYetRealized,
            StructureEditSettlementStatuses.Refuted,
            StructureEditSettlementStatuses.Unresolved,
            StructureEditSettlementStatuses.InfrastructureFailure,
            StructureEditSettlementStatuses.CounterfactualRejected
        };

    private static readonly IReadOnlySet<string> StructureCalibrationClasses =
        new HashSet<string>(StringComparer.Ordinal)
        {
            "confirmed-structural-transfer",
            "formal-proof-awaiting-release",
            "candidate-redundant",
            "counterfactual-overpredicted",
            "formal-refutation",
            "unresolved-evidence",
            "infrastructure-only",
            "rejected-before-formalization"
        };

    public static void Validate(TopologyAtlasDeltaPublicationCoordinate value)
    {
        ArgumentNullException.ThrowIfNull(value);
        RequireSchema(value.Schema, StructureEditSettlementSchemas.DeltaPublication);
        RequireArtifactRef(value.DeltaDigest, nameof(value.DeltaDigest));
        RequireArtifactRef(
            value.FromTruthReleaseDigest,
            nameof(value.FromTruthReleaseDigest));
        RequireArtifactRef(
            value.ToTruthReleaseDigest,
            nameof(value.ToTruthReleaseDigest));
        RequireArtifactRef(
            value.FromTopologyAtlasDigest,
            nameof(value.FromTopologyAtlasDigest));
        RequireArtifactRef(
            value.ToTopologyAtlasDigest,
            nameof(value.ToTopologyAtlasDigest));
        RequireArtifactRef(
            value.FromEvidenceDigest,
            nameof(value.FromEvidenceDigest));
        RequireArtifactRef(
            value.ToEvidenceDigest,
            nameof(value.ToEvidenceDigest));
        RequireArtifactRef(
            value.AlgorithmProfileDigest,
            nameof(value.AlgorithmProfileDigest));
        RequireGitId(value.ProducerCommit, nameof(value.ProducerCommit));
        if (value.ProducerCommit.Length != 40)
        {
            throw new InvalidOperationException(
                "producer_commit must use a 40-character Git object ID.");
        }
        if (StringComparer.Ordinal.Equals(
                value.FromTruthReleaseDigest,
                value.ToTruthReleaseDigest))
        {
            throw new InvalidOperationException(
                "Topology Atlas delta must cross two truth releases.");
        }
    }

    public static void Validate(StructureEditSettlement value)
    {
        ArgumentNullException.ThrowIfNull(value);
        RequireSchema(value.Schema, StructureEditSettlementSchemas.Settlement);
        ArgumentNullException.ThrowIfNull(value.SettlementContent);
        Validate(value.SettlementContent);
        RequireArtifactRef(value.SettlementId, nameof(value.SettlementId));
        string expected = CanonicalJson.Sha256Reference(
            CanonicalJson.Serialize(value.SettlementContent));
        if (!StringComparer.Ordinal.Equals(value.SettlementId, expected))
        {
            throw new InvalidOperationException(
                "settlement_id does not address canonical settlement_content bytes.");
        }
    }

    public static void Validate(StructureEditSettlementContent value)
    {
        ArgumentNullException.ThrowIfNull(value);
        foreach ((string name, string reference) in new[]
        {
            (nameof(value.CandidateRef), value.CandidateRef),
            (nameof(value.CandidateId), value.CandidateId),
            (nameof(value.EpisodeRef), value.EpisodeRef),
            (nameof(value.EpisodeId), value.EpisodeId),
            (nameof(value.CounterfactualValuationRef), value.CounterfactualValuationRef),
            (nameof(value.CounterfactualValuationId), value.CounterfactualValuationId),
            (nameof(value.CounterfactualRef), value.CounterfactualRef),
            (nameof(value.FormalizationResultRef), value.FormalizationResultRef),
            (nameof(value.FormalizationResultId), value.FormalizationResultId),
            (nameof(value.FormalizationRequestRef), value.FormalizationRequestRef),
            (nameof(value.AtlasDeltaRef), value.AtlasDeltaRef),
            (nameof(value.AtlasDeltaDigest), value.AtlasDeltaDigest),
            (nameof(value.FromTruthReleaseDigest), value.FromTruthReleaseDigest),
            (nameof(value.ToTruthReleaseDigest), value.ToTruthReleaseDigest),
            (nameof(value.FromTopologyAtlasDigest), value.FromTopologyAtlasDigest),
            (nameof(value.ToTopologyAtlasDigest), value.ToTopologyAtlasDigest),
            (nameof(value.FromEvidenceDigest), value.FromEvidenceDigest),
            (nameof(value.ToEvidenceDigest), value.ToEvidenceDigest)
        })
        {
            RequireArtifactRef(reference, name);
        }
        if (!StringComparer.Ordinal.Equals(
                value.AtlasDeltaRef,
                value.AtlasDeltaDigest))
        {
            throw new InvalidOperationException(
                "atlas_delta_ref must equal atlas_delta_digest.");
        }
        if (StringComparer.Ordinal.Equals(
                value.FromTruthReleaseDigest,
                value.ToTruthReleaseDigest))
        {
            throw new InvalidOperationException(
                "Settlement requires a later truth release.");
        }
        if (!StructureFormalizationOutcomeSet.Contains(
                value.FormalizationOutcome))
        {
            throw new InvalidOperationException(
                $"Unsupported formalization outcome '{value.FormalizationOutcome}'.");
        }
        if (!CounterfactualClassifications.Contains(
                value.CounterfactualClassification))
        {
            throw new InvalidOperationException(
                $"Unsupported counterfactual classification '{value.CounterfactualClassification}'.");
        }
        ValidateOperationSettlements(value.OperationSettlements);
        ArgumentNullException.ThrowIfNull(value.Counts);
        ValidateSettlementCounts(value.OperationSettlements, value.Counts);
        ArgumentNullException.ThrowIfNull(value.PredictedBenefitVector);
        ArgumentNullException.ThrowIfNull(value.PredictedRiskVector);
        ValidateNonNegativeBenefit(value.PredictedBenefitVector);
        ValidateNonNegativeRisk(value.PredictedRiskVector);
        ArgumentNullException.ThrowIfNull(value.RealizedDeltaSummary);
        ValidateRealizedDeltaSummary(value.RealizedDeltaSummary);
        if (!StructureSettlementStatusSet.Contains(value.SettlementStatus))
        {
            throw new InvalidOperationException(
                $"Unsupported settlement_status '{value.SettlementStatus}'.");
        }
        string expectedStatus = ClassifyStructureSettlement(
            value.FormalizationOutcome,
            value.CounterfactualClassification,
            value.Counts);
        if (!StringComparer.Ordinal.Equals(
                value.SettlementStatus,
                expectedStatus))
        {
            throw new InvalidOperationException(
                "settlement_status disagrees with formalization and delta evidence.");
        }
        if (!StructureCalibrationClasses.Contains(value.CalibrationClass))
        {
            throw new InvalidOperationException(
                $"Unsupported calibration_class '{value.CalibrationClass}'.");
        }
        string expectedCalibration = ClassifyStructureCalibration(
            value.SettlementStatus,
            value.CounterfactualClassification,
            value.Counts,
            value.PredictedBenefitVector);
        if (!StringComparer.Ordinal.Equals(
                value.CalibrationClass,
                expectedCalibration))
        {
            throw new InvalidOperationException(
                "calibration_class disagrees with the exact settlement evidence.");
        }
        if (!StringComparer.Ordinal.Equals(
                value.Authority,
                "independent-structure-settlement"))
        {
            throw new InvalidOperationException(
                "Structure edit settlements must preserve independent evidence authority.");
        }
    }

    public static void Validate(StructureEditSettlementReceipt value)
    {
        ArgumentNullException.ThrowIfNull(value);
        RequireSchema(value.Schema, StructureEditSettlementSchemas.Receipt);
        foreach ((string name, string reference) in new[]
        {
            (nameof(value.SettlementRef), value.SettlementRef),
            (nameof(value.SettlementId), value.SettlementId),
            (nameof(value.CandidateRef), value.CandidateRef),
            (nameof(value.CandidateId), value.CandidateId),
            (nameof(value.CounterfactualValuationRef), value.CounterfactualValuationRef),
            (nameof(value.FormalizationResultRef), value.FormalizationResultRef),
            (nameof(value.AtlasDeltaRef), value.AtlasDeltaRef),
            (nameof(value.FromTruthReleaseDigest), value.FromTruthReleaseDigest),
            (nameof(value.ToTruthReleaseDigest), value.ToTruthReleaseDigest)
        })
        {
            RequireArtifactRef(reference, name);
        }
        if (!StructureSettlementStatusSet.Contains(value.SettlementStatus))
        {
            throw new InvalidOperationException(
                $"Unsupported receipt settlement_status '{value.SettlementStatus}'.");
        }
        if (!StructureCalibrationClasses.Contains(value.CalibrationClass))
        {
            throw new InvalidOperationException(
                $"Unsupported receipt calibration_class '{value.CalibrationClass}'.");
        }
        if (!StringComparer.Ordinal.Equals(
                value.Authority,
                "independent-structure-settlement"))
        {
            throw new InvalidOperationException(
                "Settlement receipt must preserve independent evidence authority.");
        }
    }

    internal static string ClassifyStructureSettlement(
        string formalizationOutcome,
        string counterfactualClassification,
        StructureEditSettlementCounts counts)
    {
        if (counterfactualClassification is "rejected-cycle" or "rejected-topology")
        {
            return StructureEditSettlementStatuses.CounterfactualRejected;
        }
        return formalizationOutcome switch
        {
            StructureFormalizationOutcomes.InfrastructureFailure =>
                StructureEditSettlementStatuses.InfrastructureFailure,
            StructureFormalizationOutcomes.Inconclusive =>
                StructureEditSettlementStatuses.Unresolved,
            StructureFormalizationOutcomes.Refuted =>
                StructureEditSettlementStatuses.Refuted,
            StructureFormalizationOutcomes.Verified
                when counts.OperationCount > 0 &&
                     counts.RealizedCount == counts.OperationCount =>
                StructureEditSettlementStatuses.VerifiedAndRealized,
            StructureFormalizationOutcomes.Verified =>
                StructureEditSettlementStatuses.VerifiedNotYetRealized,
            _ => throw new InvalidOperationException(
                $"Unsupported formalization outcome '{formalizationOutcome}'.")
        };
    }

    internal static string ClassifyStructureCalibration(
        string settlementStatus,
        string counterfactualClassification,
        StructureEditSettlementCounts counts,
        StructureCounterfactualBenefitVector predictedBenefit)
    {
        return settlementStatus switch
        {
            StructureEditSettlementStatuses.VerifiedAndRealized =>
                "confirmed-structural-transfer",
            StructureEditSettlementStatuses.VerifiedNotYetRealized
                when counts.AlreadyPresentCount > 0 &&
                     counts.NotRealizedCount == 0 &&
                     counts.ContradictedCount == 0 =>
                "candidate-redundant",
            StructureEditSettlementStatuses.VerifiedNotYetRealized =>
                "formal-proof-awaiting-release",
            StructureEditSettlementStatuses.Refuted
                when HasPredictedBenefit(predictedBenefit) &&
                     counterfactualClassification is
                         "structural-upside" or "mixed-structural-risk" =>
                "counterfactual-overpredicted",
            StructureEditSettlementStatuses.Refuted =>
                "formal-refutation",
            StructureEditSettlementStatuses.Unresolved =>
                "unresolved-evidence",
            StructureEditSettlementStatuses.InfrastructureFailure =>
                "infrastructure-only",
            StructureEditSettlementStatuses.CounterfactualRejected =>
                "rejected-before-formalization",
            _ => throw new InvalidOperationException(
                $"Unsupported settlement status '{settlementStatus}'.")
        };
    }

    private static bool HasPredictedBenefit(
        StructureCounterfactualBenefitVector value) =>
        value.ReachabilityGain > 0 ||
        value.PathCompression > 0 ||
        value.RemovedCutBridges > 0 ||
        value.NewInterfaces > 0 ||
        value.ShortestPathChanges > 0;

    private static void ValidateOperationSettlements(
        IReadOnlyList<StructureEditOperationSettlement> values)
    {
        ArgumentNullException.ThrowIfNull(values);
        if (values.Count is < 1 or > 32)
        {
            throw new InvalidOperationException(
                "operation_settlements must contain from 1 through 32 entries.");
        }
        for (int index = 0; index < values.Count; index++)
        {
            StructureEditOperationSettlement value = values[index];
            ArgumentNullException.ThrowIfNull(value);
            if (value.OperationOrdinal != index + 1)
            {
                throw new InvalidOperationException(
                    "operation_settlements must use contiguous one-based ordinals.");
            }
            if (!StructureGraphOperations.Contains(value.Operation))
            {
                throw new InvalidOperationException(
                    $"Unsupported settled operation '{value.Operation}'.");
            }
            RequireSettlementIdentity(
                value.StableSubjectId,
                "operation_settlements.stable_subject_id");
            if (value.Operation == StructureGraphPatchOperations.AddNode)
            {
                if (value.StableObjectId is not null ||
                    !StringComparer.Ordinal.Equals(
                        value.ExpectedDeltaRelation,
                        "added"))
                {
                    throw new InvalidOperationException(
                        "add-node settlement has invalid subject or expected relation.");
                }
            }
            else
            {
                RequireSettlementIdentity(
                    value.StableObjectId,
                    "operation_settlements.stable_object_id");
                string expected = value.Operation ==
                    StructureGraphPatchOperations.AddEdge
                        ? "added"
                        : "removed";
                if (!StringComparer.Ordinal.Equals(
                        value.ExpectedDeltaRelation,
                        expected))
                {
                    throw new InvalidOperationException(
                        $"{value.Operation} settlement has invalid expected relation.");
                }
            }
            if (value.ObservedDeltaRelation is not null &&
                value.ObservedDeltaRelation is not
                    ("added" or "retained" or "retired" or "removed"))
            {
                throw new InvalidOperationException(
                    $"Unsupported observed delta relation '{value.ObservedDeltaRelation}'.");
            }
            if (!StructureOperationSettlementOutcomes.Contains(value.Outcome))
            {
                throw new InvalidOperationException(
                    $"Unsupported operation settlement outcome '{value.Outcome}'.");
            }
        }
    }

    private static void ValidateSettlementCounts(
        IReadOnlyList<StructureEditOperationSettlement> values,
        StructureEditSettlementCounts counts)
    {
        foreach (BigInteger value in new[]
        {
            counts.OperationCount,
            counts.RealizedCount,
            counts.NotRealizedCount,
            counts.AlreadyPresentCount,
            counts.ContradictedCount
        })
        {
            if (value < 0)
            {
                throw new InvalidOperationException(
                    "Settlement counts must be non-negative.");
            }
        }
        if (counts.OperationCount != values.Count ||
            counts.RealizedCount != values.Count(value => value.Outcome == "realized") ||
            counts.NotRealizedCount != values.Count(value => value.Outcome == "not-realized") ||
            counts.AlreadyPresentCount != values.Count(value => value.Outcome == "already-present") ||
            counts.ContradictedCount != values.Count(value => value.Outcome == "contradicted") ||
            counts.OperationCount != counts.RealizedCount +
                counts.NotRealizedCount +
                counts.AlreadyPresentCount +
                counts.ContradictedCount)
        {
            throw new InvalidOperationException(
                "Settlement counts disagree with operation_settlements.");
        }
    }

    private static void ValidateRealizedDeltaSummary(
        StructureEditRealizedDeltaSummary value)
    {
        foreach (BigInteger metric in new[]
        {
            value.NodesAdded,
            value.NodesRetired,
            value.EdgesAdded,
            value.EdgesRemoved,
            value.ClusterSplits,
            value.ClusterMerges,
            value.ClusterReorganizations,
            value.FrontierEntered,
            value.FrontierLeft
        })
        {
            if (metric < 0)
            {
                throw new InvalidOperationException(
                    "Realized Atlas delta summary must be non-negative.");
            }
        }
    }

    private static void RequireSettlementIdentity(
        string? value,
        string name)
    {
        if (string.IsNullOrWhiteSpace(value) || value.Length > 512)
        {
            throw new InvalidOperationException(
                $"{name} must contain from 1 through 512 characters.");
        }
    }
}
