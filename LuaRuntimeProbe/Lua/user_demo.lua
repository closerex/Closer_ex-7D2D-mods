-- LuaRuntimeProbe / user_demo.lua
--
-- Demonstrates `luaprobe run <backend> Lua/user_demo.lua [args...]`.
--
-- A script executed through `luaprobe run` must return a table containing a
-- function named `main`; the console command calls it with the remaining
-- command arguments as strings and prints the return value.
--
-- This script only READS game state through the bridge.  There is no code path
-- that writes to the player, inventory, world or save.

local M = {}

function M.main(...)
  local args = { ... }

  probe:Log("user_demo invoked with " .. tostring(#args) .. " argument(s)")

  local has_world = probe:HasWorld()
  local health = probe:PlayerField("health")
  local name = probe:PlayerField("name")
  local reason = probe:PlayerField("reason")

  return string.format(
    "user_demo backend_version=%s args=%d has_world=%s health=%s name=%s reason=%s",
    probe:Version(),
    #args,
    tostring(has_world),
    tostring(health),
    tostring(name),
    tostring(reason))
end

return M
