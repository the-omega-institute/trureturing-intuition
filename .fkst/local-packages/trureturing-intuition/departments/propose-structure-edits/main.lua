local M = {}
local core = require("core")

M.spec = {
  consumes = { "intuition_structure_edit_episode_ready" },
  produces = { "intuition_structure_candidates_ready" },
  stall_window = "45m",
}

local function required(value, name)
  if type(value) ~= "string" or value == "" then
    error("propose-structure-edits: missing " .. name)
  end
  return value
end

local function utc_timestamp()
  local result = exec_argv({
    argv = { "date", "-u", "+%Y-%m-%dT%H:%M:%S.0000000Z" },
    timeout = 30,
  })
  if result.exit_code ~= 0 then
    error("propose-structure-edits: cannot establish UTC generation time")
  end
  return tostring(result.stdout):gsub("%s+$", "")
end

local function model_snapshot(result)
  if type(result.model_snapshot) == "string" and result.model_snapshot ~= "" then
    return result.model_snapshot
  end
  if type(result.model) == "string" and result.model ~= "" then
    return result.model
  end
  return "fkst-spawn-codex-sync"
end

function pipeline(event)
  local payload = event.payload or {}
  local root, err = core.repo_root(required(payload.repo_root, "repo_root"))
  if not root then
    error("propose-structure-edits: " .. tostring(err))
  end
  local paths = core.paths(root)
  local evidence_cursor_path =
    paths.work .. "/topology-atlas-evidence-input-cursor.v1.json"
  if not file.exists(evidence_cursor_path) then
    error("propose-structure-edits: topology atlas evidence cursor is unavailable")
  end
  local evidence_cursor = json.decode(file.read(evidence_cursor_path))
  if type(evidence_cursor) ~= "table"
      or evidence_cursor.schema ~= "intuition-topology-atlas-evidence-input-cursor.v1" then
    error("propose-structure-edits: invalid topology atlas evidence cursor")
  end
  local evidence_receipt_ref = required(
    evidence_cursor.receipt_ref,
    "evidence cursor receipt_ref")

  local context = core.run_cli(paths, {
    "prepare-structure-edit-candidate-context",
    "--root", paths.store,
    "--episode-ref", required(payload.episode_ref, "episode_ref"),
    "--episode-receipt-ref", required(payload.receipt_ref, "receipt_ref"),
    "--evidence-receipt-ref", evidence_receipt_ref,
  }, 300)

  local result = spawn_codex_sync({
    prompt = core.prompt(
      paths,
      "propose-structure-edits",
      "STRUCTURE_EDIT_CONTEXT=" .. json.encode(context)),
    timeout = 2400,
  })
  if result.exit_code ~= 0 then
    error("propose-structure-edits: candidate generation failed")
  end
  local ok, agent_output = pcall(json.decode, result.stdout)
  if not ok or type(agent_output) ~= "table"
      or agent_output.schema ~= "structure-edit-candidate-agent-output.v1"
      or type(agent_output.candidates) ~= "table" then
    error("propose-structure-edits: agent returned an invalid candidate object")
  end

  local draft_set = {
    schema = "structure-edit-candidate-draft-set.v1",
    episode_ref = payload.episode_ref,
    episode_receipt_ref = payload.receipt_ref,
    topology_atlas_evidence_input_receipt_ref = evidence_receipt_ref,
    generated_by = "fkst:propose-structure-edits",
    model_snapshot = model_snapshot(result),
    candidates = agent_output.candidates,
    generated_at = utc_timestamp(),
  }
  local episode_key = payload.episode_ref:gsub("^sha256:", "")
  local work = paths.work .. "/structure-edit-candidates/" .. episode_key
  core.ensure_dir(work)
  local draft_path = work .. "/candidate-draft-set.v1.json"
  core.write_agent_output(draft_path, json.encode(draft_set))

  local registered = core.run_cli(paths, {
    "register-structure-edit-candidate-set",
    "--root", paths.store,
    "--input", draft_path,
  }, 300)

  raise("intuition_structure_candidates_ready", {
    repo_root = root,
    episode_ref = payload.episode_ref,
    episode_receipt_ref = payload.receipt_ref,
    topology_atlas_evidence_input_receipt_ref = evidence_receipt_ref,
    candidate_set_ref = registered.candidate_set_ref,
    receipt_ref = registered.receipt_ref,
    candidate_set_id = registered.candidate_set_id,
    candidate_ids = registered.candidate_ids,
    candidate_count = registered.candidate_count,
    truth_release_digest = registered.truth_release_digest,
    topology_atlas_evidence_digest =
      registered.topology_atlas_evidence_digest,
    dedup_key =
      "intuition-structure-candidates:v1:" .. registered.candidate_set_ref,
  })
end

return M
