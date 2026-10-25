-- LuaRuntimeProbe / probe_require_probe.lua
--
-- Loaded through `require` during the capability probe.  It exists purely to
-- demonstrate that module resolution + caching works, and it carries a counter
-- so the harness can prove the module is evaluated once, not once per call.

local M = {}

M.module_name = "probe_require_probe"
M.evaluations = 1

function M.marker()
  return "require-ok"
end

return M
