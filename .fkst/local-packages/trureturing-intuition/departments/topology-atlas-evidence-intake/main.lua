local M = {}
local core = require("core")

M.spec = {
  consumes = { "topology_atlas_evidence_release_ready" },
  produces = { "intuition_structure_evidence_ready" },
  stall_window = "5m",
}

local function required_string(value, name)
  if type(value) ~= "string" or value == "" then
    error("topology-atlas-evidence-intake: missing " .. name)
  end
  return value
end

function pipeline(event)
  local envelope_path = event.payload and event.payload.path or nil
  local root, err = core.repo_root(envelope_path)
  if not root then
    error("topology-atlas-evidence-intake: " .. tostring(err))
  end

  local envelope = json.decode(file.read(envelope_path))
  if type(envelope) ~= "table"
      or envelope.schema ~= "intuition-topology-atlas-evidence-input-envelope.v1" then
    error("topology-atlas-evidence-intake: invalid envelope schema")
  end

  local publication_path = required_string(
    envelope.publication_path,
    "publication_path")
  local evidence_path = required_string(
    envelope.evidence_path,
    "evidence_path")
  local paths = core.paths(root)
  core.ensure_dir(paths.work)

  local result = core.run_cli(paths, {
    "register-topology-atlas-evidence-input",
    "--root", paths.store,
    "--publication", publication_path,
    "--evidence", evidence_path,
    "--cursor", paths.work .. "/topology-atlas-evidence-input-cursor.v1.json",
  }, 300)

  raise("intuition_structure_evidence_ready", {
    publication_ref = result.publication_ref,
    evidence_ref = result.evidence_ref,
    receipt_ref = result.receipt_ref,
    topology_atlas_input_receipt_ref =
      result.topology_atlas_input_receipt_ref,
    truth_release_digest = result.truth_release_digest,
    certified_topology_digest = result.certified_topology_digest,
    topology_atlas_digest = result.topology_atlas_digest,
    topology_atlas_evidence_digest =
      result.topology_atlas_evidence_digest,
    evidence_algorithm_profile_digest =
      result.evidence_algorithm_profile_digest,
    stable_node_count = result.stable_node_count,
    trait_record_count = result.trait_record_count,
    cluster_interface_count = result.cluster_interface_count,
    affinity_witness_count = result.affinity_witness_count,
    replayed = result.replayed,
    dedup_key = "intuition-topology-atlas-evidence:v1:" .. result.receipt_ref,
  })
end

return M
