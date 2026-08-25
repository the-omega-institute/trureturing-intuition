using Trureturing.Intuition.Core;

internal static partial class Cli
{
    private const long ExampleRecordedAtUnix = LocalDevMockTruthAdapter.FrozenAtUnix + 3600;
    private const string ExampleAdvisory = "Advisory research ledger: every bridge is a conjecture paired with a mock independent outcome, not certified truth.";

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
        string? RequestedLemma);

    private static int ExampleCycle(ArtifactStore store)
    {
        var adapter = LocalDevMockTruthAdapter.Produce(store);
        var nodeRefs = adapter.NodeRefs;
        var target = new TargetInterface(
            Schemas.TargetInterface,
            "d5-carrier-bridge-example",
            nodeRefs["D5/S0/Carrier/TraceConjugation.trace_conj"],
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
            "local-dev-mock");
        var intake = IntakeRouter.Freeze(store, envelope, adapter.Receipt, target, universe, candidates.Select(candidate => candidate.Edit).ToArray());
        var candidateRefs = candidates.ToDictionary(candidate => candidate.Edit.CandidateId, candidate => store.Put(candidate.Edit), StringComparer.Ordinal);

        var proposalRefs = new SortedDictionary<string, string>(StringComparer.Ordinal);
        foreach (var candidate in candidates)
        {
            var evidence = new[] { nodeRefs[candidate.LeftNodeId], nodeRefs[candidate.RightNodeId] }.Order(StringComparer.Ordinal).ToArray();
            var proposal = new IntuitionProposal(
                Schemas.Proposal,
                "proposal-" + candidate.Edit.CandidateId,
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
                candidateRefs[candidate.Edit.CandidateId],
                proposalRefs[candidate.Edit.CandidateId],
                valuationRef,
                new[] { candidate.LeftNodeId, candidate.RightNodeId }.Order(StringComparer.Ordinal).ToArray(),
                new[] { nodeRefs[candidate.LeftNodeId], nodeRefs[candidate.RightNodeId] }.Order(StringComparer.Ordinal).ToArray(),
                candidate.Conjecture,
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
                "algebraic-trace-conjugation",
                "D5/S0/Carrier/AlgebraicModel.golden_algebraic_model_spec",
                "D5/S0/Carrier/TraceConjugation.trace_conj",
                "The quotient model's coordinate formulas should expose trace invariance under its conjugation map as a reusable lemma.",
                ResearchOutcome.Proved,
                "Mock independent formalization located the same coordinate identity, trace(conj(a,b)) = 2a+b = trace(a,b).",
                new WorthVector(Measured(.82), Measured(.91), Measured(.88), Measured(.94)),
                Cost(2, 1, 18, 8),
                new OutcomeDistribution(.62, .08, .05, .05, .02, .15, .03),
                "Expose trace_conj_from_golden_algebraic_model as a reusable lemma backed by the settled coordinate identity."),
            Candidate(
                "natural-to-integral-power-norm",
                "D5/S0/Carrier/NormPowers.norm_pow",
                "D5/S0/Carrier/Powers/IntegerPowerNorm.norm_phiUnit_zpow",
                "Natural-power norm multiplicativity and the unit inverse law should combine into a uniform integral-power norm bridge for phiUnit.",
                ResearchOutcome.Proved,
                "Mock independent formalization split the integer exponent by sign and reused norm_pow plus the unit inverse law.",
                new WorthVector(Measured(.73), Measured(.84), Measured(.79), Measured(.86)),
                Cost(1, 2, 14, 5),
                new OutcomeDistribution(.58, .1, .04, .08, .04, .13, .03),
                "Package norm_phiUnit_zpow_from_norm_pow as a dependency-explicit bridge lemma."),
            Candidate(
                "critical-band-midline-factorization",
                "D5/S0/Carrier/Powers/GoldenCriticalBandScaling.golden_critical_band_scaling",
                "D5/S0/Carrier/Powers/GoldenMidlineFactorization.golden_midline_factorization",
                "The critical-band midpoint containment should follow structurally from the golden midline reciprocal factorization alone.",
                ResearchOutcome.Refuted,
                "Mock independent formalization found no implication: factorization is an algebraic rewrite, while band containment needs separate order and positivity hypotheses.",
                new WorthVector(Measured(.45), Measured(.36), Measured(.31), Measured(.42)),
                Cost(2, 2, 22, 7),
                new OutcomeDistribution(.25, .32, .08, .04, .03, .25, .03),
                null),
            Candidate(
                "zsqrtd-image-principal-ideal",
                "D5/S0/Carrier/PrincipalIdeal.golden_int_is_pid",
                "D5/S0/Carrier/ZsqrtdImage.mem_range_toZsqrtd_iff",
                "The parity characterization of the doubled-coordinate image may transport principal-ideal generators into an explicit Zsqrtd 5 normal form.",
                ResearchOutcome.Open,
                "Mock independent review left the bridge open: the image criterion is concrete, but generator transport and ideal compatibility were not formalized.",
                new WorthVector(OpenMetric(), Measured(.39), OpenMetric(), Measured(.33)),
                Cost(4, 4, 40, 15),
                new OutcomeDistribution(.22, .12, .12, .04, .02, .44, .04),
                null)
        ];

        ExampleCandidate Candidate(
            string id,
            string left,
            string right,
            string conjecture,
            ResearchOutcome outcome,
            string finding,
            WorthVector worth,
            VerificationBudget cost,
            OutcomeDistribution distribution,
            string? requestedLemma)
        {
            var endpointRefs = new[] { nodeRefs[left], nodeRefs[right] };
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
            return new ExampleCandidate(edit, left, right, conjecture, outcome, finding, worth, cost, distribution, requestedLemma);
        }
    }

}
