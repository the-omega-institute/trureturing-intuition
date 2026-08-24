local M = {}
M.spec = { consumes = { "intuition_release_ready", "intuition_calibration_ready" }, stall_window = "30s" }
function pipeline(event)
  local x = event.payload or {}
  if x.calibration_ref then
    log.info("intuition calibration ready run=" .. tostring(x.run_id) .. " ref=" .. tostring(x.calibration_ref))
  else
    log.info("intuition release ready run=" .. tostring(x.run_id) .. " ref=" .. tostring(x.intuition_release_ref))
  end
end
return M
