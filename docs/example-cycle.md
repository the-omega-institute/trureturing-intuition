# Local example research cycle

The checked-in example runs all three intuition mechanisms over four bridge conjectures and ten
frozen D5 carrier nodes. It is deterministic and uses the production CLI, Core contracts,
content-addressed store, intake router, and Pareto analyzer:

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

- intuition release: `sha256:7436bcc9e9134f0be2a31e499844ddb34f91959cfadad7a31a68e727e4817216`;
- intuition ledger: `sha256:31a4f6b0e5c3d5e675f1bfe453e434e3a168443c4bfceaa7d433a432f299409a`;
- shadow allocation: `sha256:968a25af3496479e0d37055f8d323cde964d06f5ddd0c7feb12235ffc9ee52a8`.

The allocation records an empty `selected_for_execution` array. No `research-attempt.v1` artifact
is generated. The two proved, one refuted, and one open outcomes enter through
`independent-settlement.v1`, which has no attempt reference. Only the proved outcomes produce
`formalization-request.v1` artifacts, and those artifacts require `mock_write_back: true` and
`push_allowed: false`.

All cycle outputs remain canonical JSON in the content-addressed store under `artifacts/`. The CLI
prints the candidate edit, allocation, independent settlement, formalization request, ledger, and
release references for downstream data consumers. Visualization belongs in `trureturing-pages`.
