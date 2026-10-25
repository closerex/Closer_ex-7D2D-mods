-- LuaRuntimeProbe / errors/syntax_error.lua
--
-- DELIBERATELY MALFORMED.  This file must fail at COMPILE time and must never
-- execute.  It exists so the harness can prove that a syntax error produces a
-- readable message naming the chunk and the line.
--
-- The parse error below is an unterminated function parameter list plus an
-- unterminated string, which every Lua 5.1/5.2/5.3/5.4 parser rejects.

local function broken_forever(a, b
  return "this string is never closed
