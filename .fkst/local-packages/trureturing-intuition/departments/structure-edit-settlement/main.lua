local M = {}
local core = require("core")

M.spec = {
  consumes = { "structure_edit_settlement_seen" },
  produces = { "intuition_structure_settlement_ready" },
  stall_window = "10m",
}

local function required_string(value, name)
  if type(value) ~= "string" or value == "" then
    error("structure-edit-settlement: missing " .. name)
  end
  return value
end

function pipeline(event)
  local envelope_path = event.payload and event.payload.path or nil
  local root, err = core.repo_root(envelope_path)
  if not root then
    error("structure-edit-settlement: " .. tostring(err))
  end
  local envelope = json.decode(file.read(envelope_path))
  if type(envelope) ~= "table"
      or envelope.schema ~= "intuition-structure-edit-settlement-envelope.v1" then
    error("structure-edit-settlement: invalid envelope schema")
  end
  local paths = core.paths(root)
  local cli = root ..
    "tools/Trureturing.Intuition.StructureEditSettlement/bin/Release/net10.0/" ..
    "Trureturing.Intuition.StructureEditSettlement.dll"
  if not file.exists(cli) then
    error("structure-edit-settlement: settlement tool is not prebuilt")
  end
  local result = exec_argv({
    argv = {
      "dotnet", cli,
      "--root", paths.store,
      "--valuation-ref", required_string(envelope.valuation_ref, "valuation_ref"),
      "--formalization-publication", required_string(
        envelope.formalization_publication_path,
        "formalization_publication_path"),
      "--formalization-result", required_string(
        envelope.formalization_result_path,
        "formalization_result_path"),
      "--delta-publication", required_string(
        envelope.delta_publication_path,
        "delta_publication_path"),
      "--atlas-delta", required_string(
        envelope.atlas_delta_path,
        "atlas_delta_path"),
    },
    timeout = 600,
  })
  if result.exit_code ~= 0 then
    error(
      "structure-edit-settlement: tool exit=" ..
      tostring(result.exit_code) .. " stderr=" .. tostring(result.stderr))
  end
  local ok, decoded = pcall(json.decode, result.stdout)
  if not ok or type(decoded) ~= "table" then
    error("structure-edit-settlement: tool returned invalid JSON")
  end
  raise("intuition_structure_settlement_ready", {
    formalization_result_ref = decoded.formalization_result_ref,
    atlas_delta_ref = decoded.atlas_delta_ref,
    settlement_ref = decoded.settlement_ref,
    receipt_ref = decoded.receipt_ref,
    settlement_id = decoded.settlement_id,
    candidate_ref = decoded.candidate_ref,
    from_truth_release_digest = decoded.from_truth_release_digest,
    to_truth_release_digest = decoded.to_truth_release_digest,
    settlement_status = decoded.settlement_status,
    calibration_class = decoded.calibration_class,
    counts = decoded.counts,
  })
end

return M
