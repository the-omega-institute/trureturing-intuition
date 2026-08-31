local M = {}
local core = require("core")

M.spec = {
  consumes = { "topology_atlas_release_ready" },
  produces = { "intuition_structure_input_ready" },
  stall_window = "5m",
}

local function required_string(value, name)
  if type(value) ~= "string" or value == "" then
    error("topology-atlas-intake: missing " .. name)
  end
  return value
end

function pipeline(event)
  local envelope_path = event.payload and event.payload.path or nil
  local root, err = core.repo_root(envelope_path)
  if not root then
    error("topology-atlas-intake: " .. tostring(err))
  end

  local envelope = json.decode(file.read(envelope_path))
  if type(envelope) ~= "table"
      or envelope.schema ~= "intuition-topology-atlas-input-envelope.v1" then
    error("topology-atlas-intake: invalid envelope schema")
  end

  local publication_path = required_string(
    envelope.publication_path,
    "publication_path")
  local atlas_path = required_string(envelope.atlas_path, "atlas_path")
  local paths = core.paths(root)
  core.ensure_dir(paths.work)

  local result = core.run_cli(paths, {
    "register-topology-atlas-input",
    "--root", paths.store,
    "--publication", publication_path,
    "--atlas", atlas_path,
    "--cursor", paths.work .. "/topology-atlas-input-cursor.v1.json",
  }, 300)

  raise("intuition_structure_input_ready", {
    publication_ref = result.publication_ref,
    atlas_ref = result.atlas_ref,
    receipt_ref = result.receipt_ref,
    truth_release_digest = result.truth_release_digest,
    certified_topology_digest = result.certified_topology_digest,
    topology_atlas_digest = result.topology_atlas_digest,
    atlas_algorithm_profile_digest = result.atlas_algorithm_profile_digest,
    replayed = result.replayed,
    dedup_key = "intuition-topology-atlas:v1:" .. result.receipt_ref,
  })
end

return M
