local M = {}
local core = require("core")
M.spec = { consumes = { "proposal_batch_ready" }, produces = { "critique_batch_ready" }, stall_window = "45m" }
local lenses = { "duplicate", "type-and-assumption", "falsifier", "structural-value", "cost-and-verifiability" }
function pipeline(event)
  local x = event.payload or {}
  local p = core.paths(x.repo_root)
  local state_json = file.read(core.artifact_path(p, x.state_ref))
  local proposal_context = {}
  for _, ref in ipairs(x.proposal_refs or {}) do
    table.insert(proposal_context, "PROPOSAL_REF=" .. ref .. "\n" .. file.read(core.artifact_path(p, ref)))
  end
  local work = p.work .. "/" .. x.run_id .. "/critiques"
  core.ensure_dir(work)
  local inputs = {}
  for _, lens in ipairs(lenses) do
    local result = spawn_codex_sync({
      prompt = core.prompt(p, "review", "LENS=" .. lens .. "\nSTATE=" .. state_json
        .. "\n" .. table.concat(proposal_context, "\n")),
      timeout = 2400,
    })
    if result.exit_code ~= 0 then error("review lens " .. lens .. " failed") end
    local decoded = json.decode(result.stdout)
    if type(decoded) ~= "table" or type(decoded.critiques) ~= "table" then
      error("review lens must return {critiques:[{json:string}]} ")
    end
    for index, critique in ipairs(decoded.critiques) do
      local output = work .. "/" .. lens .. "-" .. tostring(index) .. ".json"
      file.write(output, critique.json)
      table.insert(inputs, output)
    end
  end
  local args = { "critique-set", "--root", p.store, "--state-ref", x.state_ref, "--proposal-set-ref", x.proposal_set_ref }
  for _, input in ipairs(inputs) do table.insert(args, "--input"); table.insert(args, input) end
  local registered = core.run_cli(p, args, 300)
  raise("critique_batch_ready", {
    repo_root = x.repo_root, run_id = x.run_id, state_ref = x.state_ref,
    proposal_set_ref = x.proposal_set_ref, critique_set_ref = registered.critique_set_ref,
    neighborhood_id = x.neighborhood_id, target_node_id = x.target_node_id,
  })
end
return M
