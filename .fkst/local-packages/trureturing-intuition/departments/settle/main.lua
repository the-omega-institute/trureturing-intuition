local M = {}
local core = require("core")
M.spec = { consumes = { "settlement_observed" }, produces = { "settlement_registered" }, stall_window = "5m" }
function pipeline(event)
  local path = event.payload and event.payload.path or nil
  local root, err = core.repo_root(path)
  if not root then error("settle: " .. tostring(err)) end
  local p = core.paths(root)
  local registered = core.run_cli(p, { "settle", "--root", p.store, "--input", path }, 300)
  local attempt = json.decode(file.read(core.artifact_path(p, registered.attempt_ref)))
  local allocation = json.decode(file.read(core.artifact_path(p, attempt.allocation_ref)))
  local valuation_set = json.decode(file.read(core.artifact_path(p, allocation.valuation_set_ref)))
  local state = json.decode(file.read(core.artifact_path(p, attempt.state_ref)))
  raise("settlement_registered", {
    repo_root = root, run_id = state.run_id, state_ref = attempt.state_ref,
    proposal_set_ref = valuation_set.proposal_set_ref, critique_set_ref = valuation_set.critique_set_ref,
    valuation_set_ref = allocation.valuation_set_ref, allocation_ref = attempt.allocation_ref,
    attempt_ref = registered.attempt_ref, settlement_ref = registered.settlement_ref,
  })
end
return M
