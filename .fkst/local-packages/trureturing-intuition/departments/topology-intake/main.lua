local M = {}
local core = require("core")

M.spec = {
  consumes = { "topology_release_ready" },
  produces = { "intuition_research_input_ready" },
  stall_window = "5m",
}

local function required_string(value, name)
  if type(value) ~= "string" or value == "" then
    error("topology-intake: missing " .. name)
  end
  return value
end

function pipeline(event)
  local envelope_path = event.payload and event.payload.path or nil
  local root, err = core.repo_root(envelope_path)
  if not root then error("topology-intake: " .. tostring(err)) end

  local envelope = json.decode(file.read(envelope_path))
  if type(envelope) ~= "table"
      or envelope.schema ~= "intuition-topology-input-envelope.v1" then
    error("topology-intake: invalid envelope schema")
  end

  local publication_path = required_string(
    envelope.publication_path,
    "publication_path")
  local topology_path = required_string(envelope.topology_path, "topology_path")
  local paths = core.paths(root)
  core.ensure_dir(paths.work)

  local result = core.run_cli(paths, {
    "register-topology-input",
    "--root", paths.store,
    "--publication", publication_path,
    "--topology", topology_path,
    "--cursor", paths.work .. "/topology-input-cursor.v1.json",
  }, 300)

  raise("intuition_research_input_ready", {
    publication_ref = result.publication_ref,
    topology_ref = result.topology_ref,
    receipt_ref = result.receipt_ref,
    truth_release_digest = result.truth_release_digest,
    replayed = result.replayed,
  })
end

return M
