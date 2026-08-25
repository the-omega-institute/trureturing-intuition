# trureturing-intuition

`trureturing-intuition` is the read-only structural-intuition organ for the TrueTurning ecosystem.
It consumes a verification receipt issued by the upstream `Trureturing.Truth` boundary, freezes a
research state, proposes typed graph edits, reviews and values them, allocates only in shadow mode,
records independently settled outcomes, and publishes content-addressed data artifacts for
downstream consumers. Visualization and Pages deployment live in `trureturing-pages`.

The organ does **not** parse the truth graph, replay the Frozen Ledger, validate the proof DAG, write
base truth, or own deployment/engine composition. Those authorities remain upstream or in the
configuration repository.

## Research contract

The operational definition is:

```text
intuition = an amortized policy over past independently settled research trajectories
proposal  = a typed, falsifiable candidate edit to a frozen knowledge state
value     = a vector of evidence-bound predictions, never an implicit scalar
learning  = calibration between frozen predictions and independent settlement
```

The default policy is deliberately non-authoritative:

```text
selection_mode = shadow-pareto-bootstrap-v1
scalarization  = forbidden
base_write     = forbidden
```

## Pipeline

```text
verified truth-release receipt
  -> frozen intuition state
  -> proposal seats
  -> adversarial review seats
  -> vector valuation
  -> shadow Pareto allocation
  -> intuition-release.v1
```

The bootstrap policy cannot create attempts or settlements, even with an owner authorization.
The attempt and settlement ledger contracts remain available for legacy records and a future,
separately versioned executable policy.

Independently observed research outcomes use a separate `independent-settlement.v1` intake. They
do not contain an attempt reference and do not represent work selected or executed by the shadow
allocator.

The responsibilities are expressed as FKST departments. An agent is a short-lived FKST Person
spawned by a department; it is not a long-lived authority and it never stores business truth.

## Build and test

```bash
dotnet build Trureturing.Intuition.slnx -c Release
dotnet run --project tests/Trureturing.Intuition.Tests -c Release
dotnet run --project tests/Trureturing.Intuition.ArchitectureTests -c Release
```

## Complete local example

Run the deterministic generate, settle, write-back, and ledger example with:

```bash
dotnet run --project src/Trureturing.Intuition.Cli -c Release -- \
  example-cycle --root artifacts
```

This command uses the explicitly non-authoritative local dev mock receipt adapter. It never invokes
the attempt path, selects nothing for execution, and never pushes a formalization request to the
base truth repository. It writes candidate edits, WorthVectors, allocation, independent
settlements, formalization requests, the intuition ledger, and the intuition release to the
content-addressed store under `artifacts/`. See [the example cycle notes](docs/example-cycle.md).

FKST package test and conformance are run against the exact substrate commit recorded in
`.github/workflows/ci.yml`.
