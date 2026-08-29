using System.Text;
using System.Text.Json;
using Trureturing.Intuition.Core;

internal static partial class Cli
{
public static Task<int> RunAsync(string[] args)
    {
        try
        {
            if (args.Length == 0) return Task.FromResult(Fail("missing command"));
            var command = args[0];
            var options = Parse(args[1..]);
            var store = new ArtifactStore(Required(options, "root"));
            return Task.FromResult(command switch
            {
                "store" => Store(store, Required(options, "kind"), Required(options, "input")),
                "ingest" => Ingest(store, Required(options, "input")),
                "register-topology-input" => RegisterTopologyInput(
                    store,
                    Required(options, "publication"),
                    Required(options, "topology"),
                    Required(options, "cursor")),
                "proposal-set" => ProposalSet(store, Required(options, "state-ref"), Many(options, "input")),
                "critique-set" => CritiqueSet(store, Required(options, "state-ref"), Required(options, "proposal-set-ref"), Many(options, "input")),
                "valuation-set" => ValuationSet(store, Required(options, "state-ref"), Required(options, "proposal-set-ref"), Required(options, "critique-set-ref"), Many(options, "input")),
                "allocate" => Allocate(store, Required(options, "state-ref"), Required(options, "valuation-set-ref")),
                "coverage" => Coverage(store, Required(options, "universe-ref"), Required(options, "candidate-ref")),
                "topology-context" => TopologyContext(
                    store,
                    Required(options, "state-ref"),
                    Required(options, "topology"),
                    Required(options, "algorithm-profile-digest"),
                    Required(options, "topology-producer-commit")),
                "attempt" => Attempt(store, Required(options, "state-ref"), Required(options, "proposal-ref"), Required(options, "valuation-ref"), Required(options, "allocation-ref"), Required(options, "authorization-ref"), Required(options, "attempt-id"), Required(options, "executor")),
                "settle" => Settle(store, Required(options, "input")),
                "independent-settle" => IndependentSettle(store, Required(options, "input")),
                "formalization-request" => RegisterFormalizationRequest(store, Required(options, "input")),
                "build-release" => BuildRelease(store, options),
                "calibrate" => Calibrate(store, Required(options, "valuation-set-ref"), Many(options, "settlement-ref")),
                "calibrate-independent" => CalibrateIndependent(store, Required(options, "independent-settlement-ref")),
                "verify" => Verify(store, Required(options, "kind"), Required(options, "ref")),
                "example-cycle" => ExampleCycle(store),
                _ => Fail($"unknown command '{command}'")
            });
        }
        catch (Exception exception)
        {
            Console.Error.WriteLine(exception.Message);
            return Task.FromResult(2);
        }
    }

    private static int Store(ArtifactStore store, string kind, string input)
    {
        var bytes = File.ReadAllBytes(input);
        var reference = kind switch
        {
            "truth-receipt" => store.Put(CanonicalJson.DeserializeStrict<TruthReleaseVerificationReceipt>(bytes)),
            "run-request" => store.Put(CanonicalJson.DeserializeStrict<IntuitionRunRequest>(bytes)),
            "target-interface" => store.Put(CanonicalJson.DeserializeStrict<TargetInterface>(bytes)),
            "residual-witness" => store.Put(CanonicalJson.DeserializeStrict<ResidualWitness>(bytes)),
            "residual-universe" => store.Put(CanonicalJson.DeserializeStrict<ResidualUniverse>(bytes)),
            "candidate-edit" => store.Put(CanonicalJson.DeserializeStrict<CandidateEdit>(bytes)),
            "proposal" => store.Put(CanonicalJson.DeserializeStrict<IntuitionProposal>(bytes)),
            "critique" => store.Put(CanonicalJson.DeserializeStrict<IntuitionCritique>(bytes)),
            "valuation" => store.Put(CanonicalJson.DeserializeStrict<IntuitionValuation>(bytes)),
            "authorization" => store.Put(CanonicalJson.DeserializeStrict<OwnerAuthorization>(bytes)),
            "settlement" => store.Put(CanonicalJson.DeserializeStrict<IntuitionSettlement>(bytes)),
            "independent-settlement" => store.Put(CanonicalJson.DeserializeStrict<IndependentSettlement>(bytes)),
            "formalization-request" => store.Put(CanonicalJson.DeserializeStrict<FormalizationRequest>(bytes)),
            "ledger" => store.Put(CanonicalJson.DeserializeStrict<IntuitionLedger>(bytes)),
            "replay-case" => store.Put(CanonicalJson.DeserializeStrict<TemporalReplayCase>(bytes)),
            "replay-score" => store.Put(CanonicalJson.DeserializeStrict<ReplayScore>(bytes)),
            "topology-publication" => store.Put(CanonicalJson.DeserializeStrict<TopologyPublicationCoordinate>(bytes)),
            "topology-input-receipt" => store.Put(CanonicalJson.DeserializeStrict<IntuitionTopologyInputReceipt>(bytes)),
            "topology-input-cursor" => store.Put(CanonicalJson.DeserializeStrict<IntuitionTopologyInputCursor>(bytes)),
            _ => throw new InvalidOperationException($"Unsupported store kind '{kind}'.")
        };
        WriteResult(new Dictionary<string, object?> { ["artifact_ref"] = reference });
        return 0;
    }

    private static int Ingest(ArtifactStore store, string input)
    {
        var envelope = CanonicalJson.DeserializeStrict<IntakeEnvelope>(File.ReadAllBytes(input));
        var receipt = CanonicalJson.DeserializeStrict<TruthReleaseVerificationReceipt>(File.ReadAllBytes(envelope.TruthReleaseReceiptPath));
        var target = CanonicalJson.DeserializeStrict<TargetInterface>(File.ReadAllBytes(envelope.TargetInterfacePath));
        var universe = CanonicalJson.DeserializeStrict<ResidualUniverse>(File.ReadAllBytes(envelope.ResidualUniversePath));
        var candidateEdits = envelope.CandidateEditPaths
            .Select(path => CanonicalJson.DeserializeStrict<CandidateEdit>(File.ReadAllBytes(path)))
            .ToArray();
        var result = IntakeRouter.Freeze(store, envelope, receipt, target, universe, candidateEdits);
        WriteResult(new Dictionary<string, object?>
        {
            ["state_ref"] = result.StateRef,
            ["request_ref"] = result.RequestRef,
            ["candidate_refs"] = result.CandidateRefs,
            ["agent_mode"] = result.AgentMode,
            ["run_id"] = result.RunId
        });
        return 0;
    }
}
