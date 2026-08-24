local M = {}
M.spec = { consumes = { "intuition_release_ready" }, stall_window = "30s" }
function pipeline(event)
  local x = event.payload or {}
  log.info("intuition release ready run=" .. tostring(x.run_id) .. " ref=" .. tostring(x.intuition_release_ref))
end
return M
