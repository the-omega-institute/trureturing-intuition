local core = require("core")
local T = {}
function T.test_repo_root_from_inbox_path()
  local root, err = core.repo_root("/tmp/repo/inbox/run-requests/a.json")
  fkst.test.is_nil(err)
  fkst.test.eq(root, "/tmp/repo/")
end
function T.test_reject_path_outside_inbox()
  local root, err = core.repo_root("/tmp/repo/other/a.json")
  fkst.test.is_nil(root)
  fkst.test.is_true(type(err) == "string")
end
function T.test_artifact_path_is_content_addressed()
  local p = core.paths("/tmp/repo/")
  local ref = "sha256:" .. string.rep("a", 64)
  fkst.test.eq(core.artifact_path(p, ref), "/tmp/repo/artifacts/sha256/aa/" .. string.rep("a", 64) .. ".json")
end
return T
