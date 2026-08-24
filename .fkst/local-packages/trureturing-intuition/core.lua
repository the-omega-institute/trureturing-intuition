local M = {}

function M.repo_root(observed_path)
  if type(observed_path) ~= "string" or observed_path == "" then
    return nil, "missing observed path"
  end
  local normalized = observed_path:gsub("\\", "/")
  local marker = "/inbox/"
  local at = normalized:find(marker, 1, true)
  if not at then return nil, "observed path is outside repository inbox" end
  return normalized:sub(1, at)
end

function M.paths(repo_root)
  local cli = repo_root .. "src/Trureturing.Intuition.Cli/bin/Release/net10.0/Trureturing.Intuition.Cli.dll"
  return {
    root = repo_root,
    cli = cli,
    store = repo_root .. "artifacts",
    work = repo_root .. "work",
    prompts = repo_root .. ".fkst/local-packages/trureturing-intuition/prompts/",
  }
end

function M.run_cli(paths, args, timeout)
  if not file.exists(paths.cli) then
    error("intuition CLI is not prebuilt: " .. paths.cli)
  end
  local argv = { "dotnet", paths.cli }
  for _, value in ipairs(args) do table.insert(argv, value) end
  local result = exec_argv({ argv = argv, timeout = timeout or 120 })
  if result.exit_code ~= 0 then
    error("intuition CLI exit=" .. tostring(result.exit_code) .. " stderr=" .. tostring(result.stderr))
  end
  local ok, decoded = pcall(json.decode, result.stdout)
  if not ok or type(decoded) ~= "table" then
    error("intuition CLI returned invalid JSON")
  end
  return decoded
end

function M.artifact_path(paths, ref)
  if type(ref) ~= "string" or not ref:match("^sha256:[0-9a-f]+$") or #ref ~= 71 then
    error("invalid artifact ref " .. tostring(ref))
  end
  local hex = ref:sub(8)
  return paths.store .. "/sha256/" .. hex:sub(1, 2) .. "/" .. hex .. ".json"
end

function M.ensure_dir(path)
  local result = exec_argv({ argv = { "mkdir", "-p", path }, timeout = 30 })
  if result.exit_code ~= 0 then error("mkdir failed: " .. tostring(result.stderr)) end
end

function M.write_agent_output(path, bytes)
  file.write(path, bytes)
end

function M.prompt(paths, name, context)
  return file.read(paths.prompts .. name .. ".md") .. "\n\nINPUT CONTEXT\n" .. context
end

return M
