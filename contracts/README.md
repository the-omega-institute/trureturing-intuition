# Contract catalog

The normative machine records are the C# record types and validators in
`Trureturing.Intuition.Core`. Their schema tags are closed in `Schemas` and canonical artifacts are
content-addressed by `ArtifactStore`.

Main contracts:

- `truth-release-verification-receipt.v1`
- `target-interface.v1`
- `residual-witness.v1`
- `residual-universe.v1`
- `candidate-edit.v1`
- `intuition-state.v1`
- `intuition-proposal.v1`
- `intuition-critique.v1`
- `intuition-valuation.v1`
- `intuition-allocation.v1`
- `research-attempt.v1`
- `intuition-settlement.v1`
- `independent-settlement.v1`
- `formalization-request.v1`
- `intuition-ledger.v1`
- `intuition-release.v1`
- `temporal-replay-case.v1`
- `calibration-report.v1`

`intuition-intake-envelope.v1`, `intuition-run-request.v1`, and `intuition-state.v1` embed a bounded
`ConceptNeighborhood` grouping. `intuition-proposal.v1` carries its neighborhood/target binding,
two endpoint node IDs, conjectured bridge, and discovery ledger. `intuition-ledger.v1` repeats the
grouping and requires any populated candidate rows to cover it exactly.

The shared external `certified-topology.v1` port is consumed by
`CertifiedTopologyReader`; it is not redefined as an Intuition-owned contract. The reader exposes
an immutable Intuition read-model with arbitrary-precision structural integers and reduced exact
rationals. `TopologyReasoningAdvisor` projects only advisory load-bearing/frontier inputs for one
frozen `ConceptNeighborhood`; it does not modify proposals, valuations, allocation, or truth.
