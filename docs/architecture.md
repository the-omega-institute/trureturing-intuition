# Architecture

## Authority split

| Layer | Owns |
|---|---|
| `Trureturing.Truth` upstream | release integrity, truth graph wire, strict frozen proof DAG |
| this repository | research-state freezing, typed proposals, review, vector valuation, shadow allocation, attempt-bound legacy records, and independent settlement intake |
| FKST substrate | event delivery, retry, locking, Lua runtime and process boundary |
| repository-local Lua | event routing and invocation of this repository's CLI/agents |
| independent verifier | proof, refutation, duplicate, wall or other settlement authority |

The repository accepts `truth-release-verification-receipt.v1`. It does not accept a raw truth
bundle as authority. The receipt binds the release digest, source commit/tree, truth graph artifact
and strict truth-export artifact. The adapter that produces the receipt is outside this repository
and must use the upstream verifier.

The checked-in local example is a deliberately named mock exception for development only. Its
adapter freezes a small source-bound node subset but performs no truth verification and carries an
explicit non-authoritative caveat.

## Durable facts

All durable business artifacts are canonical JSON addressed by `sha256:<64 lowercase hex>` and
stored below `artifacts/sha256/<prefix>/<digest>.json`. FKST queues, Lua tables, logs, process
handles, marks and caches are not business truth.

## Three independent discovery ledgers

A candidate tracks three independent states:

1. catalog status: unsearched, duplicate, escaped;
2. semantic status: unknown, residual-witnessed, finite-observed-cover, formally-refining;
3. certification status: unattempted, proved, refuted, wall, duplicate, trivial, open, infrastructure-failure.

Novelty does not imply semantic growth. Semantic growth does not imply truth.

## Residual interpretation

For a current readout `C` and target `T`, a residual witness records a pair collapsed by `C` but
separated by `T`. A candidate may claim cuts only against a frozen residual universe. Covering all
observed witnesses is called `finite-observed-cover`. `formal-cover` additionally requires an
independent formal receipt and a universe declared `formal-complete`.

## Independent settlement intake

`intuition-settlement.v1` remains bound to `research-attempt.v1` and is forbidden in a
`shadow-pareto-bootstrap-v1` release. `independent-settlement.v1` is a distinct intake record: it
binds a frozen state and proposal directly, has an independent authority, and has no attempt or
allocation field. This lets historical or externally produced mock outcomes calibrate intuition
without converting shadow allocation into execution.

Independent settlement authority is an exact, case-sensitive allow-list. Proved and refuted
outcomes require at least one receipt whose content exists at, and matches, its `sha256:` store
address. Open outcomes may omit receipts; any receipt they do name is verified the same way.
