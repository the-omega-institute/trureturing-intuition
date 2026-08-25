local M = {}
local core = require("core")
M.spec = { consumes = { "state_registered" }, produces = { "proposal_batch_ready" }, stall_window = "45m" }
function pipeline(event)
  local payload = event.payload or {}
  local p = core.paths(payload.repo_root)
  local state_json = file.read(core.artifact_path(p, payload.state_ref))
  local state = json.decode(state_json)
  local candidate_refs = payload.candidate_refs or {}
  if type(state.neighborhood) ~= "table" or type(state.neighborhood.members) ~= "table" then
    error("propose: frozen state is missing concept neighborhood grouping")
  end
  if #candidate_refs < 5 or #candidate_refs > 12 or #candidate_refs ~= #state.neighborhood.members then
    error("propose: cycle must cover one complete bounded neighborhood of 5-12 candidates")
  end
  local work = p.work .. "/" .. payload.run_id .. "/proposals"
  core.ensure_dir(work)
  local inputs = {}
  for index, candidate_ref in ipairs(candidate_refs) do
    local output = work .. "/candidate-" .. tostring(index) .. ".json"
    local result = spawn_codex_sync({
      prompt = core.prompt(p, "propose", "SEAT=bridge-neighborhood-member\nSTATE_REF=" .. payload.state_ref
        .. "\nSTATE=" .. state_json .. "\nCANDIDATE_REF=" .. candidate_ref
        .. "\nCANDIDATE=" .. file.read(core.artifact_path(p, candidate_ref))),
      timeout = 2400,
    })
    if result.exit_code ~= 0 then error("propose neighborhood candidate " .. tostring(index) .. " failed") end
    core.write_agent_output(output, result.stdout)
    table.insert(inputs, output)
  end
  local args = { "proposal-set", "--root", p.store, "--state-ref", payload.state_ref }
  for _, input in ipairs(inputs) do table.insert(args, "--input"); table.insert(args, input) end
  local registered = core.run_cli(p, args, 300)
  raise("proposal_batch_ready", {
    repo_root = payload.repo_root, run_id = payload.run_id, state_ref = payload.state_ref,
    proposal_set_ref = registered.proposal_set_ref, proposal_refs = registered.proposal_refs,
    neighborhood_id = state.neighborhood.neighborhood_id,
    target_node_id = state.neighborhood.target_node_id,
  })
end
return M
