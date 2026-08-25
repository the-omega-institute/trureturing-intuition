# Local example research cycle

The checked-in example runs all three intuition mechanisms over one bounded neighborhood of eight
bridge conjectures around the frozen target
`D5/S0/Carrier/TraceConjugation.trace_conj`. The members include its direct prerequisite and
dependents plus structurally adjacent sibling lemmas in the D5 carrier domain. It is deterministic
and uses the production CLI, Core contracts, content-addressed store, intake router, and Pareto
analyzer:

```bash
dotnet build Trureturing.Intuition.slnx -c Release
dotnet run --project src/Trureturing.Intuition.Cli -c Release --no-build -- \
  example-cycle --root artifacts
```

The local adapter is deliberately named `LocalDevMockTruthAdapter`. Its subset is bound to
`the-omega-institute/trureturing` `dev` commit
`453e725795fda1d57bf01756cee8611f2c966d15` and tree
`c21635d1dc8533602b81ffde03b414b1d4503d24`. It emits a structurally valid
`truth-release-verification-receipt.v1`, but it does not run or impersonate the real
`Trureturing.Truth` verifier. The mock release binding and generated artifacts carry that caveat
explicitly.

The example's stable root artifacts are:

- intuition release: `sha256:bbe42313fbcb56c85a61994a08ca281d23720b943afef067f2c68b363de66ddc`;
- intuition state with neighborhood grouping: `sha256:247bcf30792b88d83516583bbb44efccc71c7ee49834064b6648b5b850b05eff`;
- intuition ledger with the same grouping: `sha256:688ff05b222ee924877bda707ea6ff174c25ae36b1d1bd443a859fd0fab4dda7`;
- example typed proposal: `sha256:52b446e7f2bd20f98aedc93992087f5f90692a7be922f70933133ce2b23ae28b`;
- shadow allocation over all eight WorthVectors: `sha256:31e1feb2957c4b341024e8e35b5509db0b46fb1e180f54966bca2b9469049f8a`.

The allocation records an empty `selected_for_execution` array. No `research-attempt.v1` artifact
is generated. The three proved, two refuted, and three open outcomes enter through
`independent-settlement.v1`, which has no attempt reference. Only the proved outcomes produce
`formalization-request.v1` artifacts, and those artifacts require `mock_write_back: true` and
`push_allowed: false`.

Every candidate has its own `WorthVector`; the shadow Pareto front is computed over the complete
eight-proposal cluster. All cycle outputs remain canonical JSON in the content-addressed store under
`artifacts/`. The CLI prints the neighborhood ID and target alongside candidate edit, allocation,
independent settlement, formalization request, ledger, and release references for downstream data
consumers. Visualization belongs in `trureturing-pages`.
