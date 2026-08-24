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
                "proposal-set" => ProposalSet(store, Required(options, "state-ref"), Many(options, "input")),
                "critique-set" => CritiqueSet(store, Required(options, "state-ref"), Required(options, "proposal-set-ref"), Many(options, "input")),
                "valuation-set" => ValuationSet(store, Required(options, "state-ref"), Required(options, "proposal-set-ref"), Required(options, "critique-set-ref"), Many(options, "input")),
                "allocate" => Allocate(store, Required(options, "state-ref"), Required(options, "valuation-set-ref")),
                "coverage" => Coverage(store, Required(options, "universe-ref"), Required(options, "candidate-ref")),
                "attempt" => Attempt(store, Required(options, "state-ref"), Required(options, "proposal-ref"), Required(options, "valuation-ref"), Required(options, "allocation-ref"), Required(options, "authorization-ref"), Required(options, "attempt-id"), Required(options, "executor")),
                "settle" => Settle(store, Required(options, "input")),
                "build-release" => BuildRelease(store, options),
                "calibrate" => Calibrate(store, Required(options, "valuation-set-ref"), Many(options, "settlement-ref")),
                "verify" => Verify(store, Required(options, "kind"), Required(options, "ref")),
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
            "replay-case" => store.Put(CanonicalJson.DeserializeStrict<TemporalReplayCase>(bytes)),
            "replay-score" => store.Put(CanonicalJson.DeserializeStrict<ReplayScore>(bytes)),
            _ => throw new InvalidOperationException($"Unsupported store kind '{kind}'.")
        };
        WriteResult(new Dictionary<string, object?> { ["artifact_ref"] = reference });
        return 0;
    }

    private static int Ingest(ArtifactStore store, string input)
    {
        var envelope = CanonicalJson.DeserializeStrict<IntakeEnvelope>(File.ReadAllBytes(input));
        if (envelope.Schema != Schemas.IntakeEnvelope) throw new InvalidOperationException("Unexpected intake envelope schema.");
        var receipt = CanonicalJson.DeserializeStrict<TruthReleaseVerificationReceipt>(File.ReadAllBytes(envelope.TruthReleaseReceiptPath));
        var target = CanonicalJson.DeserializeStrict<TargetInterface>(File.ReadAllBytes(envelope.TargetInterfacePath));
        var universe = CanonicalJson.DeserializeStrict<ResidualUniverse>(File.ReadAllBytes(envelope.ResidualUniversePath));
        var receiptRef = store.Put(receipt);
        var targetRef = store.Put(target);
        var universeRef = store.Put(universe);
        var candidateEdits = envelope.CandidateEditPaths
            .Select(path => CanonicalJson.DeserializeStrict<CandidateEdit>(File.ReadAllBytes(path)))
            .ToArray();
        ContractValidator.ValidateCandidateEditSet(candidateEdits);
        var candidates = candidateEdits
            .Select(store.Put)
            .Order(StringComparer.Ordinal)
            .ToArray();
        var request = new IntuitionRunRequest(
            Schemas.RunRequest,
            envelope.RunId,
            receiptRef,
            targetRef,
            universeRef,
            candidates,
            envelope.HistoryCutoff,
            envelope.Budget,
            envelope.VerificationProtocol,
            envelope.ModelSnapshot,
            "shadow-pareto-bootstrap-v1");
        var requestRef = store.Put(request);
        var state = StateFactory.Create(request, receipt, receiptRef);
        var stateRef = store.Put(state);
        WriteResult(new Dictionary<string, object?>
        {
            ["state_ref"] = stateRef,
            ["request_ref"] = requestRef,
            ["candidate_refs"] = candidates,
            ["agent_mode"] = envelope.AgentMode,
            ["run_id"] = envelope.RunId
        });
        return 0;
    }
}
