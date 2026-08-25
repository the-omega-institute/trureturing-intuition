using Trureturing.Intuition.Core;

internal static partial class Cli
{
    private const long ExampleRecordedAtUnix = LocalDevMockTruthAdapter.FrozenAtUnix + 3600;
    private const string ExampleAdvisory = "Advisory research ledger: every bridge is a conjecture paired with a mock independent outcome, not certified truth.";
    private const string ExampleTargetNodeId = "D5/S0/Carrier/TraceConjugation.trace_conj";
    private const string ExampleNeighborhoodId = "d5-trace-conjugation-neighborhood-v1";

    private sealed record ExampleCandidate(
        CandidateEdit Edit,
        string LeftNodeId,
        string RightNodeId,
        string Conjecture,
        ResearchOutcome Outcome,
        string SettlementFinding,
        WorthVector Worth,
        VerificationBudget PredictedCost,
        OutcomeDistribution PredictedOutcomes,
        string? RequestedLemma,
        ConceptRelation Relation);

    private static int ExampleCycle(ArtifactStore store)
    {
        var adapter = LocalDevMockTruthAdapter.Produce(store);
        var nodeRefs = adapter.NodeRefs;
        var target = new TargetInterface(
            Schemas.TargetInterface,
            "d5-trace-conjugation-neighborhood",
            nodeRefs[ExampleTargetNodeId],
            nodeRefs["D5/S0/Carrier/AlgebraicModel.golden_algebraic_model_spec"],
            adapter.ReleaseBindingRef,
            AdequacyMode.FiniteEnumerated,
            null);
        var targetRef = store.Put(target);
        var universe = new ResidualUniverse(
            Schemas.ResidualUniverse,
            "d5-carrier-bridge-example",
            targetRef,
            ResidualUniverseKind.FiniteObserved,
            Array.Empty<string>(),
            null);
        var candidates = BuildExampleCandidates(nodeRefs, adapter.ReleaseBindingRef);
        var neighborhood = new ConceptNeighborhood(
            ExampleNeighborhoodId,
            ExampleTargetNodeId,
            nodeRefs[ExampleTargetNodeId],
            "D5/S0/Carrier",
            "golden-integer-algebra",
            CandidateLimit: 12,
            candidates.Select(candidate => new ConceptNeighborhoodMember(
                candidate.Edit.CandidateId,
                candidate.RightNodeId,
                nodeRefs[candidate.RightNodeId],
                candidate.Relation)).OrderBy(static member => member.CandidateId, StringComparer.Ordinal).ToArray());
        var envelope = new IntakeEnvelope(
            Schemas.IntakeEnvelope,
            "local-dev-d5-bridge-cycle-v1",
            "local-dev-mock://truth-receipt",
            "local-dev-mock://target-interface",
            "local-dev-mock://residual-universe",
            candidates.Select(candidate => "local-dev-mock://candidate/" + candidate.Edit.CandidateId).ToArray(),
            LocalDevMockTruthAdapter.SourceCommit,
            new VerificationBudget(0, 0, 0, 0, 0, 0, 0),
            "independent-local-dev-mock-settlement-v1",
            adapter.ReleaseBindingRef,
            "local-dev-mock",
            neighborhood);
        var intake = IntakeRouter.Freeze(store, envelope, adapter.Receipt, target, universe, candidates.Select(candidate => candidate.Edit).ToArray());
        var candidateRefs = candidates.ToDictionary(candidate => candidate.Edit.CandidateId, candidate => store.Put(candidate.Edit), StringComparer.Ordinal);

        var proposalRefs = new SortedDictionary<string, string>(StringComparer.Ordinal);
        foreach (var candidate in candidates)
        {
            var evidence = new[] { nodeRefs[candidate.LeftNodeId], nodeRefs[candidate.RightNodeId] }.Order(StringComparer.Ordinal).ToArray();
            var proposal = new IntuitionProposal(
                Schemas.Proposal,
                "proposal-" + candidate.Edit.CandidateId,
                candidate.Edit.CandidateId,
                neighborhood.NeighborhoodId,
                neighborhood.TargetNodeId,
                new[] { candidate.LeftNodeId, candidate.RightNodeId }.Order(StringComparer.Ordinal).ToArray(),
                candidate.Conjecture,
                intake.StateRef,
                candidateRefs[candidate.Edit.CandidateId],
                "bridge",
                evidence,
                new DiscoveryLedger(CatalogStatus.Unsearched, SemanticStatus.Unknown, CertificationStatus.Unattempted),
                adapter.ReleaseBindingRef,
                candidate.Edit.Falsifier,
                LocalDevMockTruthAdapter.FrozenAtUnix);
            proposalRefs.Add(candidate.Edit.CandidateId, store.Put(proposal));
        }
        var proposalSetRef = store.Put(new IntuitionProposalSet(
            Schemas.ProposalSet,
            intake.StateRef,
            proposalRefs.Values.Order(StringComparer.Ordinal).ToArray()));

        var critiqueRefs = candidates.Select(candidate => store.Put(new IntuitionCritique(
            Schemas.Critique,
            proposalRefs[candidate.Edit.CandidateId],
            "example-boundary-review",
            "comment",
            new[] { "Bridge remains conjectural until independent settlement intake." },
            new[] { candidateRefs[candidate.Edit.CandidateId] },
            "local-dev-review-fixture"))).Order(StringComparer.Ordinal).ToArray();
        var critiqueSetRef = store.Put(new IntuitionCritiqueSet(
            Schemas.CritiqueSet,
            intake.StateRef,
            proposalSetRef,
            critiqueRefs));

        var valuationRefs = new SortedDictionary<string, string>(StringComparer.Ordinal);
        foreach (var candidate in candidates)
        {
            var valuation = new IntuitionValuation(
                Schemas.Valuation,
                proposalRefs[candidate.Edit.CandidateId],
                candidate.Worth,
                candidate.PredictedCost,
                candidate.PredictedOutcomes,
                0,
                0,
                candidate.Worth.Novelty.Status == MetricStatus.Open ? 0.72 : 0.28,
                new[] { adapter.ReleaseBindingRef },
                "local-dev-value-fixture",
                LocalDevMockTruthAdapter.FrozenAtUnix);
            valuationRefs.Add(candidate.Edit.CandidateId, store.Put(valuation));
        }
        var valuationSetRef = store.Put(new IntuitionValuationSet(
            Schemas.ValuationSet,
            intake.StateRef,
            proposalSetRef,
            critiqueSetRef,
            valuationRefs.Values.Order(StringComparer.Ordinal).ToArray()));
        var pareto = ParetoAnalyzer.Analyze(valuationRefs.Select(pair => (pair.Value, store.Get<IntuitionValuation>(pair.Value))).ToArray());
        var allocation = new IntuitionAllocation(
            Schemas.Allocation,
            intake.StateRef,
            valuationSetRef,
            "shadow-pareto-bootstrap-v1",
            pareto.ParetoFront,
            pareto.Dominated,
            pareto.Incomparable,
            Array.Empty<string>(),
            ExampleRecordedAtUnix);
        var allocationRef = store.Put(allocation);

        var settlementRefs = new SortedDictionary<string, string>(StringComparer.Ordinal);
        var formalizationRefs = new SortedDictionary<string, string>(StringComparer.Ordinal);
        foreach (var candidate in candidates)
        {
            var proposalRef = proposalRefs[candidate.Edit.CandidateId];
            var evidenceRef = store.Put(new LocalDevMockSettlementEvidence(
                Schemas.LocalDevSettlementEvidence,
                proposalRef,
                candidate.Outcome,
                candidate.SettlementFinding,
                MockEvidence: true));
            var settlement = new IndependentSettlement(
                Schemas.IndependentSettlement,
                intake.StateRef,
                proposalRef,
                candidate.Outcome,
                SettlementAuthorities.LocalDevMockIndependentVerifier,
                new[] { evidenceRef },
                "independent-local-dev-mock-v1",
                candidate.SettlementFinding,
                ExampleRecordedAtUnix);
            var settlementRef = store.Put(settlement);
            settlementRefs.Add(candidate.Edit.CandidateId, settlementRef);

            if (candidate.Outcome != ResearchOutcome.Proved) continue;
            var endpointRefs = new[] { nodeRefs[candidate.LeftNodeId], nodeRefs[candidate.RightNodeId] }.Order(StringComparer.Ordinal).ToArray();
            var request = new FormalizationRequest(
                Schemas.FormalizationRequest,
                "formalize-" + candidate.Edit.CandidateId,
                intake.StateRef,
                proposalRef,
                candidateRefs[candidate.Edit.CandidateId],
                settlementRef,
                LocalDevMockTruthAdapter.SourceRepository,
                LocalDevMockTruthAdapter.SourceBranch,
                candidate.RequestedLemma!,
                endpointRefs,
                new[] { evidenceRef },
                MockWriteBack: true,
                PushAllowed: false,
                ExampleRecordedAtUnix);
            formalizationRefs.Add(candidate.Edit.CandidateId, store.Put(request));
        }

        var proved = candidates.Count(candidate => candidate.Outcome == ResearchOutcome.Proved);
        var refuted = candidates.Count(candidate => candidate.Outcome == ResearchOutcome.Refuted);
        var open = candidates.Count(candidate => candidate.Outcome == ResearchOutcome.Open);
        var evaluated = proved + refuted;
        var calibration = new CalibrationSummary(proved, refuted, open, candidates.Length, evaluated, evaluated == 0 ? 0 : (double)proved / evaluated);
        var ledgerEntries = candidates.OrderBy(candidate => candidate.Edit.CandidateId, StringComparer.Ordinal).Select(candidate =>
        {
            var valuationRef = valuationRefs[candidate.Edit.CandidateId];
            return new IntuitionLedgerEntry(
                candidate.Edit.CandidateId,
                neighborhood.NeighborhoodId,
                neighborhood.TargetNodeId,
                candidateRefs[candidate.Edit.CandidateId],
                proposalRefs[candidate.Edit.CandidateId],
                valuationRef,
                new[] { candidate.LeftNodeId, candidate.RightNodeId }.Order(StringComparer.Ordinal).ToArray(),
                new[] { nodeRefs[candidate.LeftNodeId], nodeRefs[candidate.RightNodeId] }.Order(StringComparer.Ordinal).ToArray(),
                candidate.Conjecture,
                store.Get<IntuitionProposal>(proposalRefs[candidate.Edit.CandidateId]).Discovery,
                candidate.Worth,
                pareto.ParetoFront.Contains(valuationRef, StringComparer.Ordinal),
                pareto.Dominated.Contains(valuationRef, StringComparer.Ordinal),
                pareto.Incomparable.Contains(valuationRef, StringComparer.Ordinal),
                settlementRefs[candidate.Edit.CandidateId],
                candidate.Outcome,
                formalizationRefs.GetValueOrDefault(candidate.Edit.CandidateId));
        }).ToArray();
        var ledger = new IntuitionLedger(
            Schemas.Ledger,
            intake.StateRef,
            allocationRef,
            neighborhood,
            ledgerEntries,
            calibration,
            ExampleAdvisory,
            ExampleRecordedAtUnix);
        var ledgerRef = store.Put(ledger);

        var independentSettlementRefs = settlementRefs.Values.Order(StringComparer.Ordinal).ToArray();
        var formalizationRequestRefs = formalizationRefs.Values.Order(StringComparer.Ordinal).ToArray();
        ValidateReleaseGraph(
            store,
            intake.StateRef,
            store.Get<IntuitionState>(intake.StateRef),
            proposalSetRef,
            critiqueSetRef,
            valuationSetRef,
            allocationRef,
            Array.Empty<string>(),
            Array.Empty<string>(),
            independentSettlementRefs,
            formalizationRequestRefs,
            ledgerRef);
        var release = new IntuitionRelease(
            Schemas.Release,
            intake.StateRef,
            proposalSetRef,
            critiqueSetRef,
            valuationSetRef,
            allocationRef,
            Array.Empty<string>(),
            Array.Empty<string>(),
            independentSettlementRefs,
            formalizationRequestRefs,
            ledgerRef,
            adapter.Receipt.ReleaseDigest,
            ExampleRecordedAtUnix);
        var releaseRef = store.Put(release);

        WriteResult(new Dictionary<string, object?>
        {
            ["allocation_ref"] = allocationRef,
            ["candidate_edit_refs"] = candidateRefs.Values.Order(StringComparer.Ordinal).ToArray(),
            ["formalization_request_refs"] = formalizationRequestRefs,
            ["independent_settlement_refs"] = independentSettlementRefs,
            ["intuition_release_ref"] = releaseRef,
            ["ledger_ref"] = ledgerRef,
            ["neighborhood_id"] = neighborhood.NeighborhoodId,
            ["neighborhood_target_node_id"] = neighborhood.TargetNodeId,
            ["receipt_ref"] = adapter.ReceiptRef,
            ["selected_for_execution"] = allocation.SelectedForExecution,
            ["state_ref"] = intake.StateRef
        });
        return 0;
    }

    private static ExampleCandidate[] BuildExampleCandidates(IReadOnlyDictionary<string, string> nodeRefs, string evidenceRef)
    {
        MetricEvidence Measured(double value) => new(MetricStatus.Measured, value, evidenceRef);
        static MetricEvidence OpenMetric() => new(MetricStatus.Open, null, null);
        static VerificationBudget Cost(long calls, long lemmas, double wall, double review) => new(calls, 0, lemmas, 0, 0, wall, review);

        return
        [
            Candidate(
                "algebraic-model-trace",
                "D5/S0/Carrier/AlgebraicModel.golden_algebraic_model_spec",
                "The quotient model's coordinate formulas should expose trace invariance under its conjugation map as a reusable lemma.",
                ResearchOutcome.Proved,
                "Mock independent formalization located the same coordinate identity, trace(conj(a,b)) = 2a+b = trace(a,b).",
                new WorthVector(Measured(.92), Measured(.94), Measured(.91), Measured(.96)),
                Cost(2, 1, 18, 8),
                new OutcomeDistribution(.62, .08, .05, .05, .02, .15, .03),
                "Expose trace_conj_from_golden_algebraic_model as a reusable lemma backed by the settled coordinate identity.",
                ConceptRelation.DirectPrerequisite),
            Candidate(
                "discriminant-trace-coordinate",
                "D5/S0/Carrier/GoldenDiscriminant.golden_discriminant_spec",
                "The discriminant-five identity may yield a trace-coordinate characterization for conjugate golden roots.",
                ResearchOutcome.Open,
                "Mock independent review found the shared quadratic coordinates plausible but did not close the coercion and root-order obligations.",
                new WorthVector(OpenMetric(), Measured(.58), Measured(.47), Measured(.61)),
                Cost(3, 2, 28, 10),
                new OutcomeDistribution(.31, .13, .09, .04, .03, .36, .04),
                null,
                ConceptRelation.SiblingLemma),
            Candidate(
                "euclidean-trace-remainder",
                "D5/S0/Carrier/Euclidean.golden_division",
                "A canonical Euclidean remainder should be selectable using only a trace-normalized representative.",
                ResearchOutcome.Refuted,
                "Mock independent formalization produced equal-trace elements requiring different norm-decreasing quotients, refuting trace-only selection.",
                new WorthVector(Measured(.42), Measured(.44), Measured(.36), Measured(.51)),
                Cost(3, 2, 30, 9),
                new OutcomeDistribution(.21, .39, .08, .03, .03, .22, .04),
                null,
                ConceptRelation.DirectDependent),
            Candidate(
                "integer-power-trace-unit",
                "D5/S0/Carrier/Powers/IntegerPowerNorm.norm_phiUnit_zpow",
                "Integral powers of phiUnit may admit a trace recurrence compatible with the settled norm control.",
                ResearchOutcome.Open,
                "Mock independent review identified the expected recurrence but left negative-exponent normalization open.",
                new WorthVector(Measured(.69), Measured(.52), OpenMetric(), Measured(.57)),
                Cost(4, 3, 36, 12),
                new OutcomeDistribution(.33, .11, .1, .04, .02, .36, .04),
                null,
                ConceptRelation.SiblingLemma),
            Candidate(
                "midline-trace-scaling",
                "D5/S0/Carrier/Powers/GoldenMidlineFactorization.golden_midline_factorization",
                "Trace invariance alone should determine the reciprocal-square midline factorization under golden scaling.",
                ResearchOutcome.Refuted,
                "Mock independent formalization found trace invariance insufficient: the factorization additionally depends on multiplicative inverse identities.",
                new WorthVector(Measured(.31), Measured(.29), Measured(.27), Measured(.34)),
                Cost(3, 2, 31, 10),
                new OutcomeDistribution(.18, .46, .07, .03, .03, .19, .04),
                null,
                ConceptRelation.SiblingLemma),
            Candidate(
                "norm-power-trace-recurrence",
                "D5/S0/Carrier/NormPowers.norm_pow",
                "Power multiplicativity of norm and conjugation-invariant trace should package a two-term recurrence for natural powers.",
                ResearchOutcome.Proved,
                "Mock independent formalization derived the recurrence from the quadratic characteristic identity and norm_pow.",
                new WorthVector(Measured(.81), Measured(.86), Measured(.83), Measured(.89)),
                Cost(2, 2, 20, 7),
                new OutcomeDistribution(.59, .09, .05, .05, .03, .16, .03),
                "Package trace_pow_recurrence_from_norm_pow with explicit frozen dependencies.",
                ConceptRelation.SiblingLemma),
            Candidate(
                "principal-ideal-trace-generator",
                "D5/S0/Carrier/PrincipalIdeal.golden_int_is_pid",
                "Principal ideal generators may be normalized up to units by a trace-minimal representative.",
                ResearchOutcome.Open,
                "Mock independent review could not establish existence of a trace minimum across the full unit orbit.",
                new WorthVector(OpenMetric(), Measured(.41), Measured(.38), OpenMetric()),
                Cost(5, 4, 48, 16),
                new OutcomeDistribution(.2, .15, .13, .04, .02, .42, .04),
                null,
                ConceptRelation.DirectDependent),
            Candidate(
                "zsqrtd-image-trace-parity",
                "D5/S0/Carrier/ZsqrtdImage.mem_range_toZsqrtd_iff",
                "The doubled-coordinate image criterion should make golden trace parity explicit in the Zsqrtd representation.",
                ResearchOutcome.Proved,
                "Mock independent formalization reduced trace parity to the existing doubled-coordinate membership congruence.",
                new WorthVector(Measured(.76), Measured(.79), Measured(.74), Measured(.84)),
                Cost(2, 1, 17, 6),
                new OutcomeDistribution(.55, .1, .05, .06, .03, .18, .03),
                "Expose trace_parity_iff_mem_range_toZsqrtd as a representation bridge.",
                ConceptRelation.SiblingLemma)
        ];

        ExampleCandidate Candidate(
            string id,
            string related,
            string conjecture,
            ResearchOutcome outcome,
            string finding,
            WorthVector worth,
            VerificationBudget cost,
            OutcomeDistribution distribution,
            string? requestedLemma,
            ConceptRelation relation)
        {
            var endpointRefs = new[] { nodeRefs[ExampleTargetNodeId], nodeRefs[related] };
            var edit = new CandidateEdit(
                Schemas.CandidateEdit,
                id,
                CandidateKind.Bridge,
                new[] { endpointRefs[0] },
                new[] { endpointRefs[1] },
                conjecture,
                new[] { "Endpoints retain their frozen source statements." },
                new[] { "No base write", "No certification claim", "Shadow allocation selects no execution" }.Order(StringComparer.Ordinal).ToArray(),
                Array.Empty<string>(),
                "Independent formalization fails to derive the requested bridge from the two frozen endpoint statements and explicit assumptions.",
                "Independent settlement intake; no research-attempt path");
            return new ExampleCandidate(edit, ExampleTargetNodeId, related, conjecture, outcome, finding, worth, cost, distribution, requestedLemma, relation);
        }
    }

}
