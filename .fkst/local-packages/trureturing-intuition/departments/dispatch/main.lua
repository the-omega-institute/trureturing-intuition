local M = {}
local core = require("core")
M.spec = { consumes = { "allocation_recorded" }, produces = { "shadow_cycle_complete" }, stall_window = "5m" }
function pipeline(event)
  local x = event.payload or {}
  local p = core.paths(x.repo_root)
  local allocation = json.decode(file.read(core.artifact_path(p, x.allocation_ref)))
  if type(allocation.selected_for_execution) ~= "table" or #allocation.selected_for_execution ~= 0 then
    error("dispatch: v1 allocation must be shadow-only")
  end
  raise("shadow_cycle_complete", x)
end
return M
