local M = {}
local core = require("core")

M.spec = {
  consumes = { "structure_edit_candidate_seen" },
  produces = { "intuition_structure_candidate_ready" },
  stall_window = "5m",
}

function pipeline(event)
  local input_path = event.payload and event.payload.path or nil
  local root, err = core.repo_root(input_path)
  if not root then
    error("structure-edit-candidate-intake: " .. tostring(err))
  end
  local paths = core.paths(root)
  local cli = root ..
    "tools/Trureturing.Intuition.StructureEditCandidate/bin/Release/net10.0/" ..
    "Trureturing.Intuition.StructureEditCandidate.dll"
  if not file.exists(cli) then
    error("structure-edit-candidate-intake: registrar tool is not prebuilt")
  end
  local result = exec_argv({
    argv = {
      "dotnet", cli,
      "--root", paths.store,
      "--input", input_path,
    },
    timeout = 300,
  })
  if result.exit_code ~= 0 then
    error(
      "structure-edit-candidate-intake: registrar exit=" ..
      tostring(result.exit_code) .. " stderr=" .. tostring(result.stderr))
  end
  local ok, decoded = pcall(json.decode, result.stdout)
  if not ok or type(decoded) ~= "table" then
    error("structure-edit-candidate-intake: registrar returned invalid JSON")
  end
  raise("intuition_structure_candidate_ready", {
    candidate_ref = decoded.candidate_ref,
    receipt_ref = decoded.receipt_ref,
    candidate_id = decoded.candidate_id,
    episode_ref = decoded.episode_ref,
    candidate_kind = decoded.candidate_kind,
    candidate_ordinal = decoded.candidate_ordinal,
    graph_patch_operation_count = decoded.graph_patch_operation_count,
    authority = decoded.authority,
  })
end

return M
