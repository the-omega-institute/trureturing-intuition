local M = {}
local core = require("core")
M.spec = { consumes = { "run_request_observed" }, produces = { "state_registered" }, stall_window = "5m" }
function pipeline(event)
  local path = event.payload and event.payload.path or nil
  local root, err = core.repo_root(path)
  if not root then error("intake-router: " .. tostring(err)) end
  local p = core.paths(root)
  local result = core.run_cli(p, { "ingest", "--root", p.store, "--input", path }, 300)
  raise("state_registered", {
    state_ref = result.state_ref,
    request_ref = result.request_ref,
    candidate_refs = result.candidate_refs,
    agent_mode = result.agent_mode,
    run_id = result.run_id,
    repo_root = root,
  })
end
return M
