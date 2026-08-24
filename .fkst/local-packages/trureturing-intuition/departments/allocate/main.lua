local M = {}
local core = require("core")
M.spec = { consumes = { "valuation_batch_ready" }, produces = { "allocation_recorded" }, stall_window = "5m" }
function pipeline(event)
  local x = event.payload or {}
  local p = core.paths(x.repo_root)
  local result = core.run_cli(p, { "allocate", "--root", p.store, "--state-ref", x.state_ref,
    "--valuation-set-ref", x.valuation_set_ref }, 300)
  raise("allocation_recorded", {
    repo_root = x.repo_root, run_id = x.run_id, state_ref = x.state_ref,
    proposal_set_ref = x.proposal_set_ref, critique_set_ref = x.critique_set_ref,
    valuation_set_ref = x.valuation_set_ref, allocation_ref = result.allocation_ref,
  })
end
return M
