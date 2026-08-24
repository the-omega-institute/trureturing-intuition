# Intuition ledger specification v1

## Invariants

1. Every artifact is bound to an exact source truth-release digest.
2. Proposal is never truth and cannot write the base repository.
3. Every proposal has a closed action kind, explicit inputs/outputs, a representation map,
   assumptions, preserved invariants, falsifier and verification route.
4. Worth dimensions are `open` or `measured`. Open values remain incomparable.
5. No total score or best candidate exists without an explicitly versioned scalar policy.
6. Predictions and predicted cost are frozen before execution.
7. Proposer and settlement authority are distinct. An `agent` cannot be settlement authority.
8. Proved, refuted and wall outcomes all create durable settlement receipts.
9. Infrastructure failure is operational evidence and never a mathematical negative result.
10. Claims that a connection reduces research cost require replay or prospective controls.
11. Temporal replay forbids future artifact references, future theorem names and future dependency
    information in source features.
12. Shadow mode selects no attempt and cannot execute. A future executable policy must still
    require an owner-authorization artifact.

## Closed action kinds

`premise_set`, `bridge`, `subgoal`, `abstraction`, `reroot`, `counterexample`,
`definition_package`, `evidence_acquisition`.

## Closed settlement outcomes

`proved`, `refuted`, `wall`, `duplicate`, `trivial`, `open`, `infrastructure_failure`.
