namespace Trureturing.Intuition.Core;

public static class StructureEditCandidateGenerator
{
    private static readonly string[] Priority =
    [
        StructureEditKinds.AddDefinitionPackage,
        StructureEditKinds.AddPremise,
        StructureEditKinds.AddSubgoal,
        StructureEditKinds.AddBridge,
        StructureEditKinds.AddAbstraction,
        StructureEditKinds.ChangeRepresentation,
        StructureEditKinds.AddCounterexample,
        StructureEditKinds.AcquireEvidence,
        StructureEditKinds.RegisterOpenQuestion,
        StructureEditKinds.Reroot
    ];

    public static StructureEditCandidateGeneration Generate(
        string artifactStoreRoot,
        string episodeRef,
        string episodeReceiptRef,
        string topologyAtlasEvidenceInputReceiptRef)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(artifactStoreRoot);
        ContractValidator.RequireArtifactRef(episodeRef, nameof(episodeRef));
        ContractValidator.RequireArtifactRef(
            episodeReceiptRef,
            nameof(episodeReceiptRef));
        ContractValidator.RequireArtifactRef(
            topologyAtlasEvidenceInputReceiptRef,
            nameof(topologyAtlasEvidenceInputReceiptRef));

        string root = Path.GetFullPath(artifactStoreRoot);
        var store = new ArtifactStore(root);
        StructureEditEpisode episode = store.Get<StructureEditEpisode>(episodeRef);
        StructureEditEpisodeReceipt episodeReceipt =
            store.Get<StructureEditEpisodeReceipt>(episodeReceiptRef);
        IntuitionTopologyAtlasEvidenceInputReceipt evidenceReceipt =
            store.Get<IntuitionTopologyAtlasEvidenceInputReceipt>(
                topologyAtlasEvidenceInputReceiptRef);
        ValidateEpisodeReceipt(
            episode,
            episodeRef,
            episodeReceipt,
            episodeReceiptRef);
        ValidateCoordinates(episode.EpisodeContent, evidenceReceipt);

        byte[] evidenceBytes = File.ReadAllBytes(
            TopologyAtlasEvidenceResearchInputRegistrar.EvidenceBlobPath(
                root,
                evidenceReceipt.EvidenceRef));
        var evidenceBinding = new TopologyAtlasEvidenceBinding(
            evidenceReceipt.TruthReleaseDigest,
            evidenceReceipt.CertifiedTopologyDigest,
            evidenceReceipt.TopologyAtlasDigest,
            evidenceReceipt.EvidenceAlgorithmProfileDigest,
            evidenceReceipt.ProducerCommit);
        TopologyAtlasEvidenceReadModel evidence =
            TopologyAtlasEvidenceReader.Read(
                evidenceBytes,
                evidenceBinding);

        StructureEditEpisodeContent source = episode.EpisodeContent;
        TopologyAtlasStableIdentityReadModel[] identities =
            source.SelectedNodeIds
                .Select(evidence.GetIdentityByNodeId)
                .OrderBy(identity => identity.NodeId, StringComparer.Ordinal)
                .ToArray();
        StructureEditCandidateEdge[] edges = source.SelectedEdges
            .Select(edge =>
            {
                TopologyAtlasStableIdentityReadModel dependency =
                    evidence.GetIdentityByNodeId(edge.DependencyId);
                TopologyAtlasStableIdentityReadModel dependent =
                    evidence.GetIdentityByNodeId(edge.DependentId);
                return new StructureEditCandidateEdge(
                    edge.DependencyId,
                    edge.DependentId,
                    dependency.StableNodeId,
                    dependent.StableNodeId);
            })
            .OrderBy(edge => edge.StableDependencyId, StringComparer.Ordinal)
            .ThenBy(edge => edge.StableDependentId, StringComparer.Ordinal)
            .ToArray();
        string[] editKinds = Priority
            .Where(kind => source.AllowedEditKinds.Contains(
                kind,
                StringComparer.Ordinal))
            .Take(source.CandidateLimit)
            .ToArray();
        if (editKinds.Length == 0)
        {
            throw new InvalidDataException(
                "Structure edit episode contains no supported edit kind.");
        }

        var candidates = new List<(string Ref, StructureEditCandidate Value)>();
        foreach (string editKind in editKinds)
        {
            CandidateLanguage language = LanguageFor(
                editKind,
                source,
                identities,
                edges);
            var content = new StructureEditCandidateContent(
                episodeRef,
                episode.EpisodeId,
                source.ObservationRef,
                topologyAtlasEvidenceInputReceiptRef,
                source.TruthReleaseDigest,
                source.TopologyAtlasDigest,
                evidenceReceipt.EvidenceDigest,
                editKind,
                identities.Select(identity => identity.NodeId)
                    .Order(StringComparer.Ordinal)
                    .ToArray(),
                identities.Select(identity => identity.StableNodeId)
                    .Order(StringComparer.Ordinal)
                    .ToArray(),
                source.SelectedClusterIds,
                edges,
                source.SelectedPathRef,
                language.SemanticQuestion,
                language.ProposedChange,
                language.FalsificationCondition,
                language.CounterfactualEligibility,
                language.SuggestedPatchShape,
                StructureEditCandidateSchemas.GenerationProfile,
                StructureEditCandidateSchemas.Authority);
            string candidateId = CanonicalJson.Sha256Reference(
                CanonicalJson.Serialize(content));
            var candidate = new StructureEditCandidate(
                StructureEditCandidateSchemas.Candidate,
                candidateId,
                content);
            ContractValidator.Validate(candidate);
            candidates.Add((store.Put(candidate), candidate));
        }

        string[] candidateRefs = candidates
            .Select(item => item.Ref)
            .Order(StringComparer.Ordinal)
            .ToArray();
        string[] candidateIds = candidates
            .Select(item => item.Value.CandidateId)
            .Order(StringComparer.Ordinal)
            .ToArray();
        var setContent = new StructureEditCandidateSetContent(
            episodeRef,
            episode.EpisodeId,
            topologyAtlasEvidenceInputReceiptRef,
            source.TruthReleaseDigest,
            source.TopologyAtlasDigest,
            evidenceReceipt.EvidenceDigest,
            candidateRefs,
            candidateIds,
            StructureEditCandidateSchemas.GenerationProfile,
            StructureEditCandidateSchemas.Authority);
        string setId = CanonicalJson.Sha256Reference(
            CanonicalJson.Serialize(setContent));
        var set = new StructureEditCandidateSet(
            StructureEditCandidateSchemas.CandidateSet,
            setId,
            setContent);
        ContractValidator.Validate(set);
        string setRef = store.Put(set);
        var receipt = new StructureEditCandidateSetReceipt(
            StructureEditCandidateSchemas.Receipt,
            setRef,
            setId,
            episodeRef,
            episode.EpisodeId,
            topologyAtlasEvidenceInputReceiptRef,
            source.TruthReleaseDigest,
            source.TopologyAtlasDigest,
            evidenceReceipt.EvidenceDigest,
            candidateRefs,
            StructureEditCandidateSchemas.GenerationProfile,
            StructureEditCandidateSchemas.Authority);
        ContractValidator.Validate(receipt);
        string receiptRef = store.Put(receipt);
        return new StructureEditCandidateGeneration(
            setRef,
            receiptRef,
            setId,
            candidateRefs,
            candidateIds,
            editKinds,
            source.TruthReleaseDigest,
            source.TopologyAtlasDigest,
            evidenceReceipt.EvidenceDigest);
    }

    private static void ValidateEpisodeReceipt(
        StructureEditEpisode episode,
        string episodeRef,
        StructureEditEpisodeReceipt receipt,
        string receiptRef)
    {
        if (!StringComparer.Ordinal.Equals(receipt.EpisodeRef, episodeRef) ||
            !StringComparer.Ordinal.Equals(receipt.EpisodeId, episode.EpisodeId) ||
            !StringComparer.Ordinal.Equals(
                receipt.ObservationRef,
                episode.EpisodeContent.ObservationRef) ||
            !StringComparer.Ordinal.Equals(
                receipt.TruthReleaseDigest,
                episode.EpisodeContent.TruthReleaseDigest) ||
            !StringComparer.Ordinal.Equals(
                receipt.TopologyAtlasDigest,
                episode.EpisodeContent.TopologyAtlasDigest))
        {
            throw new InvalidDataException(
                $"Structure edit episode receipt {receiptRef} does not bind the supplied episode.");
        }
    }

    private static void ValidateCoordinates(
        StructureEditEpisodeContent episode,
        IntuitionTopologyAtlasEvidenceInputReceipt evidence)
    {
        if (!StringComparer.Ordinal.Equals(
                episode.TopologyAtlasInputReceiptRef,
                evidence.TopologyAtlasInputReceiptRef) ||
            !StringComparer.Ordinal.Equals(
                episode.TruthReleaseDigest,
                evidence.TruthReleaseDigest) ||
            !StringComparer.Ordinal.Equals(
                episode.CertifiedTopologyDigest,
                evidence.CertifiedTopologyDigest) ||
            !StringComparer.Ordinal.Equals(
                episode.TopologyAtlasDigest,
                evidence.TopologyAtlasDigest))
        {
            throw new InvalidDataException(
                "Structure edit episode and Topology Atlas evidence use different release coordinates.");
        }
    }

    private static CandidateLanguage LanguageFor(
        string editKind,
        StructureEditEpisodeContent episode,
        IReadOnlyList<TopologyAtlasStableIdentityReadModel> identities,
        IReadOnlyList<StructureEditCandidateEdge> edges)
    {
        string anchors = AnchorDescription(
            identities,
            episode.SelectedClusterIds,
            edges,
            episode.SelectedPathRef);
        return editKind switch
        {
            StructureEditKinds.AcquireEvidence => new CandidateLanguage(
                $"Which missing observation would most strongly discriminate the structural interpretation of {anchors}?",
                $"Acquire one bounded evidence package that separates the leading explanations of {anchors}.",
                "The acquired evidence leaves every current explanation observationally equivalent or cannot be bound to the same release coordinates.",
                StructureEditCounterfactualEligibility.EvidenceAcquisition,
                StructureEditPatchShapes.None),
            StructureEditKinds.AddAbstraction => new CandidateLanguage(
                $"Does a reusable abstraction compress the certified relationships around {anchors}?",
                $"Introduce one explicit abstraction node whose premises and consequences account for the repeated structure around {anchors}.",
                "No single abstraction reduces duplicated premises or path structure without adding stronger unsupported assumptions.",
                StructureEditCounterfactualEligibility.GraphPatchRequired,
                StructureEditPatchShapes.AddNode),
            StructureEditKinds.AddBridge => new CandidateLanguage(
                $"Can a certified bridge connect the selected structural regions around {anchors}?",
                $"Propose one bridge relation with explicit source, target, premises, and a falsifier for {anchors}.",
                "Every admissible bridge either creates a dependency cycle, fails formal verification, or adds no certified reachability.",
                StructureEditCounterfactualEligibility.GraphPatchRequired,
                StructureEditPatchShapes.AddEdge),
            StructureEditKinds.AddCounterexample => new CandidateLanguage(
                $"What minimal counterexample would invalidate the apparent shared structure around {anchors}?",
                $"Register one typed counterexample target that attacks the strongest common invariant suggested by {anchors}.",
                "No bounded counterexample distinguishes the proposed invariant from the current certified structure.",
                StructureEditCounterfactualEligibility.GraphPatchRequired,
                StructureEditPatchShapes.AddNode),
            StructureEditKinds.AddDefinitionPackage => new CandidateLanguage(
                $"Which missing definition package would make the structure around {anchors} reusable and unambiguous?",
                $"Add one definition package with explicit carrier, operations, laws, and namespace boundary for {anchors}.",
                "The proposed package duplicates an existing definition or does not remove any ambiguous local construction.",
                StructureEditCounterfactualEligibility.GraphPatchRequired,
                StructureEditPatchShapes.AddNode),
            StructureEditKinds.AddPremise => new CandidateLanguage(
                $"Which explicit premise is currently implicit in the certified path around {anchors}?",
                $"Add one premise edge that exposes the missing dependency required by the selected construction around {anchors}.",
                "The premise is derivable from existing dependencies, creates a cycle, or is insufficient for the downstream claim.",
                StructureEditCounterfactualEligibility.GraphPatchRequired,
                StructureEditPatchShapes.AddEdge),
            StructureEditKinds.AddSubgoal => new CandidateLanguage(
                $"Which intermediate subgoal most effectively decomposes the open reasoning around {anchors}?",
                $"Introduce one intermediate subgoal with bounded prerequisites and a measurable downstream target for {anchors}.",
                "The subgoal cannot be stated without importing the original target or does not shorten any verification path.",
                StructureEditCounterfactualEligibility.GraphPatchRequired,
                StructureEditPatchShapes.AddNode),
            StructureEditKinds.ChangeRepresentation => new CandidateLanguage(
                $"Would a different representation expose a shorter certified path through {anchors}?",
                $"Translate the selected structure into one alternative representation and state the forward and reverse preservation obligations.",
                "The translation is not structure preserving, cannot be inverted on the required domain, or yields no path compression.",
                StructureEditCounterfactualEligibility.GraphPatchRequired,
                StructureEditPatchShapes.Mixed),
            StructureEditKinds.RegisterOpenQuestion => new CandidateLanguage(
                $"Which unresolved statement is made precise by the structural observation of {anchors}?",
                $"Register one release-bound open question with explicit scope, expected evidence, and closure condition for {anchors}.",
                "The question is already certified, cannot be falsified, or has no bounded settlement criterion.",
                StructureEditCounterfactualEligibility.QuestionRegistration,
                StructureEditPatchShapes.None),
            StructureEditKinds.Reroot => new CandidateLanguage(
                $"Can the reasoning around {anchors} be rerooted on a smaller or more reusable foundation?",
                $"Propose one rerooting that replaces the current dependency entrance with an explicit alternative foundation and preservation obligations.",
                "The rerooting loses a certified consequence, increases dependency cost without compensating gain, or creates a cycle.",
                StructureEditCounterfactualEligibility.GraphPatchRequired,
                StructureEditPatchShapes.Mixed),
            _ => throw new InvalidDataException(
                $"Unsupported structure edit kind '{editKind}'.")
        };
    }

    private static string AnchorDescription(
        IReadOnlyList<TopologyAtlasStableIdentityReadModel> identities,
        IReadOnlyList<string> clusterIds,
        IReadOnlyList<StructureEditCandidateEdge> edges,
        string? pathRef)
    {
        var parts = new List<string>();
        if (identities.Count > 0)
        {
            parts.Add($"stable nodes [{string.Join(", ", identities.Select(item => item.StableNodeId))}]");
        }
        if (clusterIds.Count > 0)
        {
            parts.Add($"release communities [{string.Join(", ", clusterIds)}]");
        }
        if (edges.Count > 0)
        {
            parts.Add($"{edges.Count} certified selected edge(s)");
        }
        if (pathRef is not null)
        {
            parts.Add($"certified path {pathRef}");
        }
        return parts.Count > 0
            ? string.Join(" and ", parts)
            : "the selected structure";
    }

    private sealed record CandidateLanguage(
        string SemanticQuestion,
        string ProposedChange,
        string FalsificationCondition,
        string CounterfactualEligibility,
        string SuggestedPatchShape);
}
