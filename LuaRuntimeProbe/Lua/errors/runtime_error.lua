-- LuaRuntimeProbe / errors/runtime_error.lua
--
-- DELIBERATELY BROKEN AT RUNTIME.
--
-- Loaded exactly once per `luaprobe test` run so that the harness can prove
-- that a Lua runtime error surfaces as a readable message.  It is never
-- executed inside a loop and its output is never logged per-iteration, so it
-- cannot spam the game log.

error("INTENTIONAL_RUNTIME_ERROR_FILE: Lua/errors/runtime_error.lua is expected to fail when executed")
