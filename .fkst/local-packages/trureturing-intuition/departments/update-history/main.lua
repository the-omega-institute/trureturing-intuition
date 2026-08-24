local M = {}
local core = require("core")
M.spec = { consumes = { "shadow_cycle_complete", "settlement_registered" }, produces = { "intuition_release_ready" }, stall_window = "5m" }
function pipeline(event)
  local x = event.payload or {}
  if not x.state_ref or not x.proposal_set_ref or not x.critique_set_ref or not x.valuation_set_ref or not x.allocation_ref then
    log.info("update-history: settlement lacks complete cycle coordinates; retained by settlement artifact")
    return
  end
  local p = core.paths(x.repo_root)
  local args = { "build-release", "--root", p.store, "--state-ref", x.state_ref,
    "--proposal-set-ref", x.proposal_set_ref, "--critique-set-ref", x.critique_set_ref,
    "--valuation-set-ref", x.valuation_set_ref, "--allocation-ref", x.allocation_ref }
  if x.attempt_ref then table.insert(args, "--attempt-ref"); table.insert(args, x.attempt_ref) end
  if x.settlement_ref then table.insert(args, "--settlement-ref"); table.insert(args, x.settlement_ref) end
  local result = core.run_cli(p, args, 300)
  raise("intuition_release_ready", { repo_root = x.repo_root, run_id = x.run_id, intuition_release_ref = result.intuition_release_ref })
end
return M
