local M = {}
local core = require("core")
M.spec = { consumes = { "state_registered" }, produces = { "proposal_batch_ready" }, stall_window = "45m" }
local seats = { "premise", "bridge", "reroot", "abstraction", "counterexample" }
function pipeline(event)
  local payload = event.payload or {}
  local p = core.paths(payload.repo_root)
  local state_json = file.read(core.artifact_path(p, payload.state_ref))
  local candidate_context = {}
  for _, ref in ipairs(payload.candidate_refs or {}) do
    table.insert(candidate_context, "CANDIDATE_REF=" .. ref .. "\n" .. file.read(core.artifact_path(p, ref)))
  end
  local work = p.work .. "/" .. payload.run_id .. "/proposals"
  core.ensure_dir(work)
  local inputs = {}
  for _, seat in ipairs(seats) do
    local output = work .. "/" .. seat .. ".json"
    local result = spawn_codex_sync({
      prompt = core.prompt(p, "propose", "SEAT=" .. seat .. "\nSTATE_REF=" .. payload.state_ref
        .. "\nSTATE=" .. state_json .. "\n" .. table.concat(candidate_context, "\n")),
      timeout = 2400,
    })
    if result.exit_code ~= 0 then error("propose seat " .. seat .. " failed") end
    core.write_agent_output(output, result.stdout)
    table.insert(inputs, output)
  end
  local args = { "proposal-set", "--root", p.store, "--state-ref", payload.state_ref }
  for _, input in ipairs(inputs) do table.insert(args, "--input"); table.insert(args, input) end
  local registered = core.run_cli(p, args, 300)
  raise("proposal_batch_ready", {
    repo_root = payload.repo_root, run_id = payload.run_id, state_ref = payload.state_ref,
    proposal_set_ref = registered.proposal_set_ref, proposal_refs = registered.proposal_refs,
  })
end
return M
