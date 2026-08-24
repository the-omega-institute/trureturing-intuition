using System.Net;
using System.Text;
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

    private static int ExampleCycle(ArtifactStore store, string site)
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
                "local-dev-independent-settlement-fixture",
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

        RenderExampleSite(site, store, adapter, allocation, ledger, ledgerRef, releaseRef);
        WriteResult(new Dictionary<string, object?>
        {
            ["allocation_ref"] = allocationRef,
            ["formalization_request_refs"] = formalizationRequestRefs,
            ["independent_settlement_refs"] = independentSettlementRefs,
            ["intuition_release_ref"] = releaseRef,
            ["ledger_ref"] = ledgerRef,
            ["receipt_ref"] = adapter.ReceiptRef,
            ["selected_for_execution"] = allocation.SelectedForExecution,
            ["site_path"] = Path.GetFullPath(site),
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

    private static void RenderExampleSite(
        string site,
        ArtifactStore store,
        LocalDevMockTruthAdapterResult adapter,
        IntuitionAllocation allocation,
        IntuitionLedger ledger,
        string ledgerRef,
        string releaseRef)
    {
        var fullSite = Path.GetFullPath(site);
        Directory.CreateDirectory(fullSite);
        Directory.CreateDirectory(Path.Combine(fullSite, "data"));
        File.Copy(store.PathFor(ledgerRef), Path.Combine(fullSite, "data", "intuition-ledger.v1.json"), overwrite: true);
        File.Copy(store.PathFor(releaseRef), Path.Combine(fullSite, "data", "intuition-release.v1.json"), overwrite: true);

        var rows = new StringBuilder();
        foreach (var entry in ledger.Candidates)
        {
            var outcome = entry.Outcome.ToString().ToUpperInvariant();
            var frontier = entry.OnParetoFront ? "PARETO FRONT" : "DOMINATED";
            var writeBack = entry.FormalizationRequestRef is null
                ? "No write-back request"
                : $"Mock formalization request {ShortRef(entry.FormalizationRequestRef)} written back as an artifact";
            rows.Append($$"""
              <article class="candidate">
                <div class="candidate-head">
                  <div><span class="candidate-id">{{H(entry.CandidateId)}}</span><h2>{{H(entry.ConjecturedBridge)}}</h2></div>
                  <span class="outcome {{outcome.ToLowerInvariant()}}">{{outcome}}</span>
                </div>
                <div class="endpoints"><code>{{H(entry.EndpointNodeIds[0])}}</code><span aria-hidden="true">&#8596;</span><code>{{H(entry.EndpointNodeIds[1])}}</code></div>
                <div class="worth" aria-label="Worth vector">
                  {{Metric("Novelty", entry.Worth.Novelty)}}{{Metric("Dependency readiness", entry.Worth.Readiness)}}{{Metric("Structural realization", entry.Worth.Realization)}}{{Metric("Receipt potential", entry.Worth.ReceiptPotential)}}
                </div>
                <div class="candidate-foot"><span class="front {{(entry.OnParetoFront ? "on" : "off")}}">{{frontier}}</span><span>{{H(writeBack)}}</span></div>
              </article>
            """);
        }

        var html = $$"""
        <!doctype html>
        <html lang="en">
        <head>
          <meta charset="utf-8">
          <meta name="viewport" content="width=device-width,initial-scale=1">
          <meta name="description" content="Advisory intuition ledger example over frozen TrueTurning nodes">
          <title>Intuition Ledger / D5 Carrier Example</title>
          <style>
            :root{color-scheme:dark;--bg:#101417;--panel:#171d21;--line:#344047;--text:#f3f6f7;--muted:#a9b4ba;--cyan:#69d2e7;--green:#46c981;--red:#ef6a6a;--amber:#e8b44e;--paper:#e8edf0}*{box-sizing:border-box}body{margin:0;background:var(--bg);color:var(--text);font-family:Inter,ui-sans-serif,system-ui,-apple-system,BlinkMacSystemFont,"Segoe UI",sans-serif;letter-spacing:0}.shell{width:min(1180px,calc(100% - 32px));margin:0 auto}.topbar{border-bottom:1px solid var(--line);background:#0c1012}.topbar .shell{min-height:64px;display:flex;align-items:center;justify-content:space-between;gap:16px}.brand{font-weight:750}.brand span{color:var(--cyan)}.source{color:var(--muted);font:12px ui-monospace,SFMono-Regular,Consolas,monospace}.intro{padding:40px 0 28px;border-bottom:1px solid var(--line)}h1{font-size:clamp(30px,5vw,54px);line-height:1.03;margin:0 0 14px;max-width:780px;letter-spacing:0}.lede{max-width:820px;color:var(--muted);font-size:17px;line-height:1.6;margin:0}.advisory{margin-top:24px;border-left:4px solid var(--amber);background:#211d14;padding:15px 17px;color:#f4d99a;font-size:14px;line-height:1.5}.stats{display:grid;grid-template-columns:repeat(5,minmax(0,1fr));border-bottom:1px solid var(--line)}.stat{padding:24px 18px;border-right:1px solid var(--line)}.stat:last-child{border-right:0}.stat strong{display:block;font-size:30px}.stat span{color:var(--muted);font-size:12px;text-transform:uppercase}.policy{display:grid;grid-template-columns:1fr 1fr;gap:24px;padding:28px 0;border-bottom:1px solid var(--line)}.policy h2{font-size:14px;text-transform:uppercase;color:var(--cyan);margin:0 0 9px}.policy p{margin:0;color:var(--muted);line-height:1.55}.policy code{color:var(--paper)}.ledger{padding:30px 0 48px}.ledger-title{display:flex;justify-content:space-between;align-items:end;gap:20px;margin-bottom:18px}.ledger-title h2{font-size:21px;margin:0}.ledger-title a{color:var(--cyan);font-size:13px}.candidate{background:var(--panel);border:1px solid var(--line);border-radius:6px;margin:0 0 14px;padding:20px}.candidate-head{display:flex;justify-content:space-between;align-items:flex-start;gap:18px}.candidate-id{font:11px ui-monospace,SFMono-Regular,Consolas,monospace;color:var(--cyan);text-transform:uppercase}.candidate h2{font-size:18px;line-height:1.4;margin:6px 0 0;max-width:850px}.outcome{flex:none;padding:6px 9px;border:1px solid currentColor;border-radius:4px;font-size:11px;font-weight:800}.proved{color:var(--green);background:#10261b}.refuted{color:var(--red);background:#291515}.open{color:var(--amber);background:#292210}.endpoints{display:grid;grid-template-columns:minmax(0,1fr) 24px minmax(0,1fr);gap:8px;align-items:center;margin:18px 0}.endpoints code{display:block;padding:10px;background:#0d1214;border:1px solid #283238;overflow-wrap:anywhere;color:#d5dee2;font-size:12px}.endpoints span{text-align:center;color:var(--muted)}.worth{display:grid;grid-template-columns:repeat(4,minmax(0,1fr));border:1px solid #2d383e}.metric{padding:12px;border-right:1px solid #2d383e}.metric:last-child{border-right:0}.metric span{display:block;color:var(--muted);font-size:11px;margin-bottom:5px}.metric strong{font:18px ui-monospace,SFMono-Regular,Consolas,monospace}.metric .unknown{color:var(--amber);font-size:14px}.candidate-foot{display:flex;justify-content:space-between;gap:20px;align-items:center;color:var(--muted);font-size:12px;margin-top:16px}.front{font-weight:800}.front.on{color:var(--cyan)}.front.off{color:#899399}footer{padding:28px 0 42px;border-top:1px solid var(--line);color:var(--muted);font-size:12px;line-height:1.7}footer code{color:var(--paper)}@media(max-width:760px){.source{display:none}.intro{padding-top:28px}.stats{grid-template-columns:repeat(2,1fr)}.stat{border-bottom:1px solid var(--line)}.policy{grid-template-columns:1fr}.candidate-head,.candidate-foot{align-items:flex-start;flex-direction:column}.endpoints{grid-template-columns:1fr}.endpoints span{transform:rotate(90deg)}.worth{grid-template-columns:repeat(2,1fr)}.metric:nth-child(2){border-right:0}.metric:nth-child(-n+2){border-bottom:1px solid #2d383e}.ledger-title{align-items:flex-start;flex-direction:column} }
          </style>
        </head>
        <body>
          <header class="topbar"><div class="shell"><div class="brand">TRURETURING <span>/ INTUITION</span></div><div class="source">D5 CARRIER / FROZEN {{H(LocalDevMockTruthAdapter.SourceCommit[..8])}}</div></div></header>
          <main>
            <section class="intro"><div class="shell"><h1>Intuition ledger: one complete research cycle</h1><p class="lede">Four typed candidate bridges move from a frozen receipt through vector valuation and shadow Pareto allocation, then receive independent mock settlements. The ledger preserves positive, negative, and unresolved findings with equal fidelity.</p><div class="advisory"><strong>CONJECTURE, NOT CERTIFIED TRUTH.</strong> {{H(ledger.Advisory)}} The local adapter and every settlement in this example are explicitly mocked.</div></div></section>
            <section class="shell stats" aria-label="Calibration summary"><div class="stat"><strong>{{ledger.Calibration.TotalCount}}</strong><span>candidates</span></div><div class="stat"><strong>{{ledger.Calibration.ProvedCount}}</strong><span>proved</span></div><div class="stat"><strong>{{ledger.Calibration.RefutedCount}}</strong><span>refuted</span></div><div class="stat"><strong>{{ledger.Calibration.OpenCount}}</strong><span>open</span></div><div class="stat"><strong>{{ledger.Calibration.HitRate:P0}}</strong><span>hit rate / decided</span></div></section>
            <section class="shell policy"><div><h2>Allocation policy</h2><p><code>{{H(allocation.Policy)}}</code> computes a non-dominated set and selected <strong>{{allocation.SelectedForExecution.Count}}</strong> candidates for execution. No attempt artifact exists.</p></div><div><h2>Independent intake</h2><p>Settlements arrive independently of allocation and execution. PROVED bridges emit mock <code>formalization-request.v1</code> write-back payloads; no base repository is changed.</p></div></section>
            <section class="shell ledger"><div class="ledger-title"><h2>Candidate bridges</h2><a href="data/intuition-ledger.v1.json">Open content-addressed ledger payload</a></div>{{rows}}</section>
          </main>
          <footer><div class="shell">Mock receipt source binding: <code>{{H(LocalDevMockTruthAdapter.SourceCommit)}}</code> / tree <code>{{H(LocalDevMockTruthAdapter.SourceTree)}}</code><br>Ledger <code>{{H(ledgerRef)}}</code> / intuition release <code>{{H(releaseRef)}}</code></div></footer>
        </body>
        </html>
        """;
        File.WriteAllText(Path.Combine(fullSite, "index.html"), html, new UTF8Encoding(encoderShouldEmitUTF8Identifier: false));

        static string H(string value) => WebUtility.HtmlEncode(value);
        static string ShortRef(string value) => value[..19] + "...";
        static string Metric(string label, MetricEvidence metric)
        {
            var value = metric.Status == MetricStatus.Open ? "<strong class=\"unknown\">OPEN</strong>" : $"<strong>{metric.Value!.Value:0.00}</strong>";
            return $"<div class=\"metric\"><span>{H(label)}</span>{value}</div>";
        }
    }
}
