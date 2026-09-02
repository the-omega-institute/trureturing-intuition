local M = {}
local core = require("core")

M.spec = {
  consumes = { "human_structure_observation_seen" },
  produces = { "intuition_human_observation_ready" },
  stall_window = "5m",
}

function pipeline(event)
  local input_path = event.payload and event.payload.path or nil
  local root, err = core.repo_root(input_path)
  if not root then
    error("human-observation-intake: " .. tostring(err))
  end

  local paths = core.paths(root)
  local result = core.run_cli(paths, {
    "register-human-structure-observation",
    "--root", paths.store,
    "--input", input_path,
  }, 300)

  raise("intuition_human_observation_ready", {
    observation_ref = result.observation_ref,
    receipt_ref = result.receipt_ref,
    truth_release_digest = result.truth_release_digest,
    topology_atlas_digest = result.topology_atlas_digest,
    privacy_class = result.privacy_class,
    dedup_key = "intuition-human-observation:v1:" .. result.observation_ref,
  })
end

return M
