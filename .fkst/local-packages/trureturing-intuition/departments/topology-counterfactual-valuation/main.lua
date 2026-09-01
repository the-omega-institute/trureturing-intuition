local M = {}
local core = require("core")

M.spec = {
  consumes = { "topology_counterfactual_seen" },
  produces = { "intuition_structure_valuation_ready" },
  stall_window = "5m",
}

local function required_string(value, name)
  if type(value) ~= "string" or value == "" then
    error("topology-counterfactual-valuation: missing " .. name)
  end
  return value
end

function pipeline(event)
  local envelope_path = event.payload and event.payload.path or nil
  local root, err = core.repo_root(envelope_path)
  if not root then
    error("topology-counterfactual-valuation: " .. tostring(err))
  end
  local envelope = json.decode(file.read(envelope_path))
  if type(envelope) ~= "table"
      or envelope.schema ~= "intuition-topology-counterfactual-input-envelope.v1" then
    error("topology-counterfactual-valuation: invalid envelope schema")
  end
  local publication = required_string(
    envelope.publication_path,
    "publication_path")
  local counterfactual = required_string(
    envelope.counterfactual_path,
    "counterfactual_path")
  local paths = core.paths(root)
  local cli = root ..
    "tools/Trureturing.Intuition.StructureCounterfactual/bin/Release/net10.0/" ..
    "Trureturing.Intuition.StructureCounterfactual.dll"
  if not file.exists(cli) then
    error("topology-counterfactual-valuation: valuation tool is not prebuilt")
  end
  local result = exec_argv({
    argv = {
      "dotnet", cli,
      "--root", paths.store,
      "--publication", publication,
      "--counterfactual", counterfactual,
    },
    timeout = 300,
  })
  if result.exit_code ~= 0 then
    error(
      "topology-counterfactual-valuation: tool exit=" ..
      tostring(result.exit_code) .. " stderr=" .. tostring(result.stderr))
  end
  local ok, decoded = pcall(json.decode, result.stdout)
  if not ok or type(decoded) ~= "table" then
    error("topology-counterfactual-valuation: tool returned invalid JSON")
  end
  raise("intuition_structure_valuation_ready", {
    counterfactual_ref = decoded.counterfactual_ref,
    valuation_ref = decoded.valuation_ref,
    receipt_ref = decoded.receipt_ref,
    valuation_id = decoded.valuation_id,
    candidate_ref = decoded.candidate_ref,
    classification = decoded.classification,
    accepted = decoded.accepted,
    cycle_risk = decoded.cycle_risk,
    benefit_vector = decoded.benefit_vector,
    risk_vector = decoded.risk_vector,
  })
end

return M
