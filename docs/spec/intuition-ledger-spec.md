# Intuition ledger specification v1

## Invariants

1. Every artifact is bound to an exact source truth-release digest.
2. Proposal is never truth and cannot write the base repository.
3. Every proposal has a closed action kind, explicit inputs/outputs, a representation map,
   assumptions, preserved invariants, falsifier and verification route.
4. Worth dimensions are `open` or `measured`. Open values remain incomparable.
5. No total score or best candidate exists without an explicitly versioned scalar policy.
6. Predictions and predicted cost are frozen before execution.
7. Proposer and settlement authority are distinct. Independent settlement authorities must match
   an accepted identity exactly; all unlisted, case-variant and whitespace-variant identities fail closed.
8. Proved, refuted and wall outcomes create durable settlement receipts. Proved and refuted
   independent outcomes require one or more digest-verified, present receipts; open independent
   outcomes may omit receipts.
9. Infrastructure failure is operational evidence and never a mathematical negative result.
10. Claims that a connection reduces research cost require replay or prospective controls.
11. Temporal replay forbids future artifact references, future theorem names and future dependency
    information in source features.
12. Shadow mode selects no attempt and cannot execute. A future executable policy must still
    require an owner-authorization artifact.
13. Independent settlement intake has no attempt reference and cannot be interpreted as work
    selected by the shadow allocator.
14. Refuted outcomes are retained in the content-addressed ledger with the same durability as
    proved outcomes.
15. One cycle freezes one coherent concept neighborhood around one target node. It contains 5-12
    unique related nodes, bounded by `candidate_limit`, classified as direct prerequisite, direct
    dependent or sibling lemma, and scoped to one declared module/domain.
16. The neighborhood member set and candidate edit universe cover each other exactly. Every member
    is a bridge whose endpoints are the target node and that member's related node.
17. Every proposal repeats its candidate, neighborhood and target IDs, its two endpoint node IDs,
    conjectured bridge and independent discovery ledger. A proposal batch is complete only when it
    has exactly one proposal per frozen neighborhood member.
18. The ledger repeats the state neighborhood grouping and each row's neighborhood/target binding,
    making the cluster traceable without treating the cluster as a Lean proof unit.

## Cycle granularity

The atomic proof unit remains one Lean node and the atomic judgment remains one
`intuition-proposal.v1`. The proposal department runs once per frozen neighborhood member and emits
the whole 5-12 proposal cluster before review, vector valuation, and shadow Pareto analysis. No
candidate in the cluster is selected for execution under `shadow-pareto-bootstrap-v1`.

## Closed action kinds

`premise_set`, `bridge`, `subgoal`, `abstraction`, `reroot`, `counterexample`,
`definition_package`, `evidence_acquisition`.

## Closed settlement outcomes

`proved`, `refuted`, `wall`, `duplicate`, `trivial`, `open`, `infrastructure_failure`.
