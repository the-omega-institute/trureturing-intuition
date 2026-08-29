local M = {}
local core = require("core")

M.spec = {
  consumes = { "human_intuition_candidate_seen" },
  produces = { "intuition_human_candidate_ready" },
  stall_window = "5m",
}

function pipeline(event)
  local input_path = event.payload and event.payload.path or nil
  local root, err = core.repo_root(input_path)
  if not root then
    error("human-candidate-intake: " .. tostring(err))
  end

  local paths = core.paths(root)
  local result = core.run_cli(paths, {
    "register-human-candidate",
    "--root", paths.store,
    "--input", input_path,
  }, 300)

  raise("intuition_human_candidate_ready", {
    candidate_ref = result.candidate_ref,
    receipt_ref = result.receipt_ref,
    truth_release_digest = result.truth_release_digest,
    topology_digest = result.topology_digest,
    dedup_key = "intuition-human-candidate:v1:" .. result.candidate_ref,
  })
end

return M
