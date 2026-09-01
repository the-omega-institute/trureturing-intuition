local M = {}
local core = require("core")

M.spec = {
  consumes = { "structure_edit_candidate_generation_requested" },
  produces = { "intuition_structure_candidates_ready" },
  stall_window = "5m",
}

local function required_string(value, name)
  if type(value) ~= "string" or value == "" then
    error("structure-edit-candidate-generator: missing " .. name)
  end
  return value
end

local function candidate_cli(paths)
  return paths.root
    .. "src/Trureturing.Intuition.StructureCandidateCli/bin/Release/net10.0/"
    .. "Trureturing.Intuition.StructureCandidateCli.dll"
end

function pipeline(event)
  local envelope_path = event.payload and event.payload.path or nil
  local root, err = core.repo_root(envelope_path)
  if not root then
    error("structure-edit-candidate-generator: " .. tostring(err))
  end

  local envelope = json.decode(file.read(envelope_path))
  if type(envelope) ~= "table"
      or envelope.schema ~= "intuition-structure-edit-candidate-request.v1" then
    error("structure-edit-candidate-generator: invalid envelope schema")
  end

  local paths = core.paths(root)
  local cli = candidate_cli(paths)
  if not file.exists(cli) then
    error("structure-edit-candidate-generator: candidate CLI is not prebuilt")
  end

  local result = exec_argv({
    argv = {
      "dotnet",
      cli,
      paths.store,
      required_string(envelope.episode_ref, "episode_ref"),
      required_string(envelope.episode_receipt_ref, "episode_receipt_ref"),
      required_string(
        envelope.topology_atlas_evidence_input_receipt_ref,
        "topology_atlas_evidence_input_receipt_ref"
      ),
    },
    timeout = 300,
  })
  if result.exit_code ~= 0 then
    error(
      "structure-edit-candidate-generator: CLI exit="
      .. tostring(result.exit_code)
      .. " stderr="
      .. tostring(result.stderr)
    )
  end
  local ok, decoded = pcall(json.decode, result.stdout)
  if not ok or type(decoded) ~= "table" then
    error("structure-edit-candidate-generator: CLI returned invalid JSON")
  end

  raise("intuition_structure_candidates_ready", {
    candidate_set_ref = decoded.candidate_set_ref,
    receipt_ref = decoded.receipt_ref,
    candidate_set_id = decoded.candidate_set_id,
    candidate_refs = decoded.candidate_refs,
    candidate_ids = decoded.candidate_ids,
    edit_kinds = decoded.edit_kinds,
    truth_release_digest = decoded.truth_release_digest,
    topology_atlas_digest = decoded.topology_atlas_digest,
    topology_atlas_evidence_digest = decoded.topology_atlas_evidence_digest,
  })
end

return M
