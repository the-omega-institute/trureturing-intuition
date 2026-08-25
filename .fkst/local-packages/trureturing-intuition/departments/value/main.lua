local M = {}
local core = require("core")
M.spec = { consumes = { "critique_batch_ready" }, produces = { "valuation_batch_ready" }, stall_window = "45m" }
function pipeline(event)
  local x = event.payload or {}
  local p = core.paths(x.repo_root)
  local context = "STATE=" .. file.read(core.artifact_path(p, x.state_ref))
    .. "\nPROPOSALS=" .. file.read(core.artifact_path(p, x.proposal_set_ref))
    .. "\nCRITIQUES=" .. file.read(core.artifact_path(p, x.critique_set_ref))
  local result = spawn_codex_sync({ prompt = core.prompt(p, "value", context), timeout = 2400 })
  if result.exit_code ~= 0 then error("value seat failed") end
  local work = p.work .. "/" .. x.run_id .. "/valuations"
  core.ensure_dir(work)
  local decoded = json.decode(result.stdout)
  if type(decoded) ~= "table" or type(decoded.valuations) ~= "table" then error("value seat must return {valuations:[...]}") end
  local inputs = {}
  for index, valuation in ipairs(decoded.valuations) do
    local path = work .. "/" .. tostring(index) .. ".json"
    file.write(path, valuation.json)
    table.insert(inputs, path)
  end
  local args = { "valuation-set", "--root", p.store, "--state-ref", x.state_ref,
    "--proposal-set-ref", x.proposal_set_ref, "--critique-set-ref", x.critique_set_ref }
  for _, input in ipairs(inputs) do table.insert(args, "--input"); table.insert(args, input) end
  local registered = core.run_cli(p, args, 300)
  raise("valuation_batch_ready", {
    repo_root = x.repo_root, run_id = x.run_id, state_ref = x.state_ref,
    proposal_set_ref = x.proposal_set_ref, critique_set_ref = x.critique_set_ref,
    valuation_set_ref = registered.valuation_set_ref,
    neighborhood_id = x.neighborhood_id, target_node_id = x.target_node_id,
  })
end
return M
