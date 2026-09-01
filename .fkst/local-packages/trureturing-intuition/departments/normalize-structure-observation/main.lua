local M = {}
local core = require("core")

M.spec = {
  consumes = { "intuition_human_observation_ready" },
  produces = { "intuition_structure_edit_episode_ready" },
  stall_window = "5m",
}

local function required(value, name)
  if type(value) ~= "string" or value == "" then
    error("normalize-structure-observation: missing " .. name)
  end
  return value
end

function pipeline(event)
  local payload = event.payload or {}
  local root, err = core.repo_root(required(payload.repo_root, "repo_root"))
  if not root then
    error("normalize-structure-observation: " .. tostring(err))
  end
  local paths = core.paths(root)
  local result = core.run_cli(paths, {
    "normalize-structure-edit-episode",
    "--root", paths.store,
    "--observation-ref", required(payload.observation_ref, "observation_ref"),
    "--observation-receipt-ref", required(payload.receipt_ref, "receipt_ref"),
  }, 300)

  raise("intuition_structure_edit_episode_ready", {
    repo_root = root,
    episode_ref = result.episode_ref,
    receipt_ref = result.receipt_ref,
    episode_id = result.episode_id,
    selection_kind = result.selection_kind,
    allowed_edit_kinds = result.allowed_edit_kinds,
    candidate_limit = result.candidate_limit,
    privacy_class = result.privacy_class,
    dedup_key = "intuition-structure-episode:v1:" .. result.episode_ref,
  })
end

return M
