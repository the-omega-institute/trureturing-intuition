local M = {}
local core = require("core")
M.spec = {
  consumes = { "independent_settlement_observed" },
  produces = { "independent_settlement_registered" },
  stall_window = "5m",
}
function pipeline(event)
  local path = event.payload and event.payload.path or nil
  local root, err = core.repo_root(path)
  if not root then error("settle-independent: " .. tostring(err)) end
  local p = core.paths(root)
  local registered = core.run_cli(p, { "independent-settle", "--root", p.store, "--input", path }, 300)
  local settlement = json.decode(file.read(core.artifact_path(p, registered.independent_settlement_ref)))
  local state = json.decode(file.read(core.artifact_path(p, settlement.state_ref)))
  raise("independent_settlement_registered", {
    repo_root = root,
    run_id = state.run_id,
    state_ref = settlement.state_ref,
    proposal_ref = settlement.proposal_ref,
    independent_settlement_ref = registered.independent_settlement_ref,
  })
end
return M
