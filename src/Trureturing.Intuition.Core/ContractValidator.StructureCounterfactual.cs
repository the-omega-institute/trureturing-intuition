namespace Trureturing.Intuition.Core;

public static partial class ContractValidator
{
    private static readonly IReadOnlySet<string> StructurePatchOperationKindSet =
        new HashSet<string>(StringComparer.Ordinal)
        {
            StructureGraphPatchOperationKinds.AddNode,
            StructureGraphPatchOperationKinds.AddEdge,
            StructureGraphPatchOperationKinds.RemoveEdge
        };

    public static void Validate(StructureEditGraphPatch value)
    {
        ArgumentNullException.ThrowIfNull(value);
        RequireSchema(value.Schema, StructureCounterfactualSchemas.Patch);
        ArgumentNullException.ThrowIfNull(value.PatchContent);
        Validate(value.PatchContent);
        RequireArtifactRef(value.PatchId, nameof(value.PatchId));
        string expected = CanonicalJson.Sha256Reference(
            CanonicalJson.Serialize(value.PatchContent));
        if (!StringComparer.Ordinal.Equals(value.PatchId, expected))
        {
            throw new InvalidOperationException(
                "patch_id does not address canonical patch_content bytes.");
        }
    }

    public static void Validate(StructureEditGraphPatchContent value)
    {
        ArgumentNullException.ThrowIfNull(value);
        RequireArtifactRef(value.CandidateRef, nameof(value.CandidateRef));
        RequireArtifactRef(value.CandidateId, nameof(value.CandidateId));
        RequireArtifactRef(
            value.TruthReleaseDigest,
            nameof(value.TruthReleaseDigest));
        RequireArtifactRef(
            value.TopologyAtlasDigest,
            nameof(value.TopologyAtlasDigest));
        RequireArtifactRef(
            value.TopologyAtlasEvidenceDigest,
            nameof(value.TopologyAtlasEvidenceDigest));
        if (value.Operations.Count is < 1 or > 64)
        {
            throw new InvalidOperationException(
                "operations must contain from 1 through 64 graph edits.");
        }
        string? previous = null;
        foreach (StructureEditGraphPatchOperation operation in value.Operations)
        {
            ArgumentNullException.ThrowIfNull(operation);
            RequireIdentifier(operation.OperationId, "operation_id");
            if (previous is not null &&
                StringComparer.Ordinal.Compare(previous, operation.OperationId) >= 0)
            {
                throw new InvalidOperationException(
                    "operations must be strictly ordinal-sorted by operation_id.");
            }
            previous = operation.OperationId;
            if (!StructurePatchOperationKindSet.Contains(operation.Kind))
            {
                throw new InvalidOperationException(
                    $"Unsupported graph patch operation '{operation.Kind}'.");
            }
            ValidatePatchOperation(operation);
        }
        RequireSortedUniqueRefs(value.AssumptionRefs, nameof(value.AssumptionRefs));
        RequireBoundedCounterfactualText(value.Rationale, nameof(value.Rationale), 8000);
        if (!value.ExplicitlySubmittedForCounterfactual)
        {
            throw new InvalidOperationException(
                "Graph patch must be explicitly submitted for counterfactual evaluation.");
        }
        if (!StringComparer.Ordinal.Equals(
                value.Authority,
                StructureCounterfactualSchemas.Authority))
        {
            throw new InvalidOperationException(
                "Graph patch authority must remain advisory.");
        }
    }

    public static void Validate(StructureEditGraphPatchReceipt value)
    {
        ArgumentNullException.ThrowIfNull(value);
        RequireSchema(value.Schema, StructureCounterfactualSchemas.PatchReceipt);
        RequireArtifactRef(value.PatchRef, nameof(value.PatchRef));
        RequireArtifactRef(value.PatchId, nameof(value.PatchId));
        RequireArtifactRef(value.CandidateRef, nameof(value.CandidateRef));
        RequireArtifactRef(value.CandidateId, nameof(value.CandidateId));
        RequireArtifactRef(
            value.TruthReleaseDigest,
            nameof(value.TruthReleaseDigest));
        RequireArtifactRef(
            value.TopologyAtlasDigest,
            nameof(value.TopologyAtlasDigest));
        RequireArtifactRef(
            value.TopologyAtlasEvidenceDigest,
            nameof(value.TopologyAtlasEvidenceDigest));
        if (value.OperationCount is < 1 or > 64)
        {
            throw new InvalidOperationException(
                "operation_count must be from 1 through 64.");
        }
        RequireAdvisory(value.Authority, nameof(value.Authority));
    }

    public static void Validate(TopologyCounterfactualProjection value)
    {
        ArgumentNullException.ThrowIfNull(value);
        RequireSortedUniqueStrings(
            value.AffectedStableNodeIds,
            nameof(value.AffectedStableNodeIds));
        RequireSortedUniqueCounterfactualClusters(
            value.TouchedClusterIds,
            nameof(value.TouchedClusterIds));
        foreach ((long metric, string name) in new[]
        {
            (value.ReachablePairGain, nameof(value.ReachablePairGain)),
            (value.ReachablePairLoss, nameof(value.ReachablePairLoss)),
            (value.ShortestPathImprovementCount, nameof(value.ShortestPathImprovementCount)),
            (value.TotalPathCompression, nameof(value.TotalPathCompression)),
            (value.NewCutBridgeCount, nameof(value.NewCutBridgeCount)),
            (value.RemovedCutBridgeCount, nameof(value.RemovedCutBridgeCount)),
            (value.NewInterfaceHypothesisCount, nameof(value.NewInterfaceHypothesisCount)),
            (value.RemovedInterfaceHypothesisCount, nameof(value.RemovedInterfaceHypothesisCount))
        })
        {
            if (metric < 0)
            {
                throw new InvalidOperationException($"{name} must be non-negative.");
            }
        }
        if (value.CycleRisk && value.Accepted)
        {
            throw new InvalidOperationException(
                "A cycle-risk counterfactual cannot be accepted.");
        }
        if (value.CycleRisk && value.AnalysisAvailable)
        {
            throw new InvalidOperationException(
                "A cycle-risk counterfactual cannot publish structural analysis.");
        }
        if (!value.AnalysisAvailable &&
            (value.AffectedStableNodeIds.Count > 0 ||
             value.TouchedClusterIds.Count > 0 ||
             value.ReachablePairGain > 0 ||
             value.ReachablePairLoss > 0 ||
             value.ShortestPathImprovementCount > 0 ||
             value.TotalPathCompression > 0 ||
             value.NewCutBridgeCount > 0 ||
             value.RemovedCutBridgeCount > 0 ||
             value.NewInterfaceHypothesisCount > 0 ||
             value.RemovedInterfaceHypothesisCount > 0))
        {
            throw new InvalidOperationException(
                "Unavailable analysis cannot carry structural metrics.");
        }
    }

    public static void Validate(TopologyCounterfactualPublication value)
    {
        ArgumentNullException.ThrowIfNull(value);
        RequireSchema(value.Schema, StructureCounterfactualSchemas.Publication);
        ArgumentNullException.ThrowIfNull(value.PublicationContent);
        Validate(value.PublicationContent);
        RequireArtifactRef(value.PublicationId, nameof(value.PublicationId));
        string expected = CanonicalJson.Sha256Reference(
            CanonicalJson.Serialize(value.PublicationContent));
        if (!StringComparer.Ordinal.Equals(value.PublicationId, expected))
        {
            throw new InvalidOperationException(
                "publication_id does not address canonical publication_content bytes.");
        }
    }

    public static void Validate(TopologyCounterfactualPublicationContent value)
    {
        ArgumentNullException.ThrowIfNull(value);
        RequireArtifactRef(value.PatchRef, nameof(value.PatchRef));
        RequireArtifactRef(value.PatchId, nameof(value.PatchId));
        RequireArtifactRef(value.CandidateRef, nameof(value.CandidateRef));
        RequireArtifactRef(value.CandidateId, nameof(value.CandidateId));
        RequireArtifactRef(
            value.TruthReleaseDigest,
            nameof(value.TruthReleaseDigest));
        RequireArtifactRef(
            value.TopologyAtlasDigest,
            nameof(value.TopologyAtlasDigest));
        RequireArtifactRef(
            value.TopologyAtlasEvidenceDigest,
            nameof(value.TopologyAtlasEvidenceDigest));
        RequireArtifactRef(
            value.TopologyCounterfactualResultDigest,
            nameof(value.TopologyCounterfactualResultDigest));
        RequireArtifactRef(
            value.TopologyCounterfactualProfileDigest,
            nameof(value.TopologyCounterfactualProfileDigest));
        RequireGitId(
            value.TopologyProducerCommit,
            nameof(value.TopologyProducerCommit));
        if (value.TopologyProducerCommit.Length != 40)
        {
            throw new InvalidOperationException(
                "topology_producer_commit must be 40 lowercase hexadecimal characters.");
        }
        Validate(value.Projection);
        if (!StringComparer.Ordinal.Equals(
                value.ProjectionProfile,
                StructureCounterfactualSchemas.ProjectionProfile))
        {
            throw new InvalidOperationException(
                "projection_profile is unsupported.");
        }
        RequireAdvisory(value.Authority, nameof(value.Authority));
    }

    public static void Validate(TopologyCounterfactualPublicationReceipt value)
    {
        ArgumentNullException.ThrowIfNull(value);
        RequireSchema(
            value.Schema,
            StructureCounterfactualSchemas.PublicationReceipt);
        RequireArtifactRef(value.PublicationRef, nameof(value.PublicationRef));
        RequireArtifactRef(value.PublicationId, nameof(value.PublicationId));
        RequireArtifactRef(value.ResultRef, nameof(value.ResultRef));
        RequireArtifactRef(value.PatchRef, nameof(value.PatchRef));
        RequireArtifactRef(value.PatchId, nameof(value.PatchId));
        RequireArtifactRef(value.CandidateRef, nameof(value.CandidateRef));
        RequireArtifactRef(value.CandidateId, nameof(value.CandidateId));
        RequireArtifactRef(
            value.TruthReleaseDigest,
            nameof(value.TruthReleaseDigest));
        RequireArtifactRef(
            value.TopologyAtlasDigest,
            nameof(value.TopologyAtlasDigest));
        RequireArtifactRef(
            value.TopologyAtlasEvidenceDigest,
            nameof(value.TopologyAtlasEvidenceDigest));
        RequireArtifactRef(
            value.TopologyCounterfactualProfileDigest,
            nameof(value.TopologyCounterfactualProfileDigest));
        RequireGitId(
            value.TopologyProducerCommit,
            nameof(value.TopologyProducerCommit));
        if (!StringComparer.Ordinal.Equals(
                value.ProjectionProfile,
                StructureCounterfactualSchemas.ProjectionProfile))
        {
            throw new InvalidOperationException(
                "receipt projection_profile is unsupported.");
        }
        RequireAdvisory(value.Authority, nameof(value.Authority));
    }

    public static void Validate(StructureCounterfactualWorthVector value)
    {
        ArgumentNullException.ThrowIfNull(value);
        foreach ((long metric, string name) in new[]
        {
            (value.ReachabilityGain, nameof(value.ReachabilityGain)),
            (value.ReachabilityLoss, nameof(value.ReachabilityLoss)),
            (value.PathCompressionGain, nameof(value.PathCompressionGain)),
            (value.InterfaceHypothesisGain, nameof(value.InterfaceHypothesisGain)),
            (value.InterfaceHypothesisLoss, nameof(value.InterfaceHypothesisLoss)),
            (value.CutBridgeReduction, nameof(value.CutBridgeReduction)),
            (value.CutBridgeCreation, nameof(value.CutBridgeCreation)),
            (value.AffectedScope, nameof(value.AffectedScope)),
            (value.PatchOperationCost, nameof(value.PatchOperationCost)),
            (value.CycleRiskPenalty, nameof(value.CycleRiskPenalty))
        })
        {
            if (metric < 0)
            {
                throw new InvalidOperationException($"{name} must be non-negative.");
            }
        }
        if (value.CycleRiskPenalty is not (0 or 1))
        {
            throw new InvalidOperationException(
                "cycle_risk_penalty must be zero or one.");
        }
    }

    public static void Validate(StructureCounterfactualValuation value)
    {
        ArgumentNullException.ThrowIfNull(value);
        RequireSchema(value.Schema, StructureCounterfactualSchemas.Valuation);
        ArgumentNullException.ThrowIfNull(value.ValuationContent);
        Validate(value.ValuationContent);
        RequireArtifactRef(value.ValuationId, nameof(value.ValuationId));
        string expected = CanonicalJson.Sha256Reference(
            CanonicalJson.Serialize(value.ValuationContent));
        if (!StringComparer.Ordinal.Equals(value.ValuationId, expected))
        {
            throw new InvalidOperationException(
                "valuation_id does not address canonical valuation_content bytes.");
        }
    }

    public static void Validate(StructureCounterfactualValuationContent value)
    {
        ArgumentNullException.ThrowIfNull(value);
        RequireArtifactRef(value.CandidateRef, nameof(value.CandidateRef));
        RequireArtifactRef(value.CandidateId, nameof(value.CandidateId));
        RequireArtifactRef(value.PatchRef, nameof(value.PatchRef));
        RequireArtifactRef(value.PatchId, nameof(value.PatchId));
        RequireArtifactRef(
            value.CounterfactualPublicationRef,
            nameof(value.CounterfactualPublicationRef));
        RequireArtifactRef(
            value.CounterfactualPublicationId,
            nameof(value.CounterfactualPublicationId));
        RequireArtifactRef(
            value.CounterfactualResultRef,
            nameof(value.CounterfactualResultRef));
        RequireArtifactRef(
            value.TruthReleaseDigest,
            nameof(value.TruthReleaseDigest));
        RequireArtifactRef(
            value.TopologyAtlasDigest,
            nameof(value.TopologyAtlasDigest));
        RequireArtifactRef(
            value.TopologyAtlasEvidenceDigest,
            nameof(value.TopologyAtlasEvidenceDigest));
        Validate(value.Projection);
        Validate(value.WorthVector);
        RequireNonEmpty(value.BlockingReason, nameof(value.BlockingReason));
        if (value.EligibleForFormalResearch &&
            !StringComparer.Ordinal.Equals(value.BlockingReason, "none"))
        {
            throw new InvalidOperationException(
                "eligible valuation must use blocking_reason 'none'.");
        }
        if (!value.EligibleForFormalResearch &&
            StringComparer.Ordinal.Equals(value.BlockingReason, "none"))
        {
            throw new InvalidOperationException(
                "blocked valuation must state a blocking_reason.");
        }
        if (!StringComparer.Ordinal.Equals(
                value.ValuationProfile,
                StructureCounterfactualSchemas.ValuationProfile))
        {
            throw new InvalidOperationException(
                "valuation_profile is unsupported.");
        }
        RequireAdvisory(value.Authority, nameof(value.Authority));
    }

    private static void ValidatePatchOperation(
        StructureEditGraphPatchOperation operation)
    {
        if (operation.Kind == StructureGraphPatchOperationKinds.AddNode)
        {
            RequireNonEmpty(operation.NodeId, "node_id");
            RequireNonEmpty(operation.StableNodeId, "stable_node_id");
            RequireNonEmpty(operation.SourcePath, "source_path");
            if (operation.ModuleName is not null)
            {
                RequireNonEmpty(operation.ModuleName, "module_name");
            }
            RequireAllNull(
                operation.DependencyId,
                operation.DependentId,
                operation.StableDependencyId,
                operation.StableDependentId);
            return;
        }
        RequireNonEmpty(operation.DependencyId, "dependency_id");
        RequireNonEmpty(operation.DependentId, "dependent_id");
        RequireNonEmpty(operation.StableDependencyId, "stable_dependency_id");
        RequireNonEmpty(operation.StableDependentId, "stable_dependent_id");
        RequireAllNull(
            operation.NodeId,
            operation.StableNodeId,
            operation.SourcePath,
            operation.ModuleName);
    }

    private static void RequireAllNull(params string?[] values)
    {
        if (values.Any(value => value is not null))
        {
            throw new InvalidOperationException(
                "Graph patch operation carries fields from a different operation kind.");
        }
    }

    private static void RequireSortedUniqueCounterfactualClusters(
        IReadOnlyList<string> values,
        string name)
    {
        RequireSortedUniqueStrings(values, name);
        foreach (string value in values)
        {
            if (value.Length != "cluster:sha256:".Length + 64 ||
                !value.StartsWith("cluster:sha256:", StringComparison.Ordinal) ||
                value["cluster:sha256:".Length..].Any(character =>
                    character is not (>= '0' and <= '9') and
                    not (>= 'a' and <= 'f')))
            {
                throw new InvalidOperationException(
                    $"{name} contains invalid cluster id '{value}'.");
            }
        }
    }

    private static void RequireBoundedCounterfactualText(
        string? value,
        string name,
        int maximum)
    {
        RequireNonEmpty(value, name);
        if (value!.Length > maximum)
        {
            throw new InvalidOperationException(
                $"{name} exceeds {maximum} characters.");
        }
    }

    private static void RequireAdvisory(string value, string name)
    {
        if (!StringComparer.Ordinal.Equals(
                value,
                StructureCounterfactualSchemas.Authority))
        {
            throw new InvalidOperationException(
                $"{name} must remain advisory.");
        }
    }
}
