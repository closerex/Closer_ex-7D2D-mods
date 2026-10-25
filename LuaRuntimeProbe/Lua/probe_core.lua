-- LuaRuntimeProbe / probe_core.lua
--
-- The portable probe module.  This is the SINGLE script that every backend
-- executes, so that functional and performance results are comparable.
--
-- LANGUAGE TARGET: Lua 5.2 (lowest common denominator).  See probe_helpers.lua
-- for the list of constructs that must not be used.
--
-- Contract expected from the host (provided by ILuaProbeBackend):
--   * global `probe_helpers` : table  (result of loading Lua/probe_helpers.lua)
--   * global `probe`         : CLR bridge object with the methods used below
--   * this file's return value is stored by the host under a global name.
--
-- IMPORTANT: bridge members must be spelled EXACTLY as in C# AND invoked with
-- colon syntax (probe:Emit(...), not probe.Emit(...)).
--
-- Measured on the two integrated backends (see the capability_bridge_member_case
-- check, whose output is reproduced in docs/):
--   * MoonSharp resolves members case-insensitively AND accepts both
--     `probe.Emit(1)` and `probe:Emit(1)`.
--   * NLua matches member names exactly, lowercases nothing, and exposes CLR
--     instance methods as callable userdata that require the receiver as the
--     first argument -- `probe.Emit(1)` fails with "instance method 'Emit'
--     requires a non null target object" while `probe:Emit(1)` works.
-- Colon syntax with the exact C# spelling is therefore the only portable form.
--
-- Return value of this chunk: the module table.

local M = {}

local H = nil
if type(probe_helpers) == "table" then
  H = probe_helpers
end

if H == nil then
  -- A missing helper module is a *host wiring* failure, not a script failure;
  -- report it explicitly instead of producing confusing nil-index errors later.
  error("probe_core: global 'probe_helpers' was not loaded by the host before probe_core.lua")
end

M.helpers_loaded = true
M.schema_version = H.SCHEMA_VERSION
M.lua_version = _VERSION

local evaluate_calls = 0

----------------------------------------------------------------------
-- 1. Trivial scalar round trips (C# -> Lua -> C#)
----------------------------------------------------------------------

-- Module identity/version table.  Kept as a function (rather than letting the
-- host poke at module fields directly) so the host only needs one marshalling
-- convention: "call a function, read the returned table".
function M.info()
  return {
    helpers_loaded = M.helpers_loaded,
    schema_version = M.schema_version,
    lua_version = _VERSION,
    module_name = "probe_core",
  }
end

-- Minimal-body entry point used to isolate "C# builds the nested table" cost
-- from "Lua walks the nested table" cost.
function M.touch(ctx)
  if type(ctx) ~= "table" then
    error("touch: ctx must be a table, got " .. type(ctx))
  end
  return ctx.schema_version
end

function M.add(a, b)
  if type(a) ~= "number" or type(b) ~= "number" then
    error("add: expected two numbers, got " .. type(a) .. " and " .. type(b))
  end
  return a + b
end

function M.concat(a, b)
  if type(a) ~= "string" or type(b) ~= "string" then
    error("concat: expected two strings, got " .. type(a) .. " and " .. type(b))
  end
  return a .. b
end

function M.negate(v)
  if type(v) ~= "boolean" then
    error("negate: expected a boolean, got " .. type(v))
  end
  return not v
end

----------------------------------------------------------------------
-- 2. Value semantics: number / bool / string / nil / enum-like number
----------------------------------------------------------------------

function M.scalars()
  return {
    an_int      = 42,
    a_float     = 2.5,
    a_negative  = -7,
    a_bool_t    = true,
    a_bool_f    = false,
    a_string    = "hello",
    a_nil       = nil,          -- absent key: must stay distinguishable from false/0
    enum_none   = H.REACTION.NONE,
    enum_flee   = H.REACTION.FLEE,
  }
end

-- nil vs 0 vs false must not collapse into one another.
function M.nil_vs_zero()
  local t = {}
  t.zero = 0
  t.empty_string = ""
  t.false_value = false
  return t
end

----------------------------------------------------------------------
-- 3. Lua -> C# callbacks (boundary crossing)
----------------------------------------------------------------------

-- Calls back into C# `n` times and returns the sum, so the host can verify
-- that every callback actually arrived (not just that the loop ran).
function M.callback_probe(n)
  if type(n) ~= "number" or n < 0 then
    error("callback_probe: expected a non-negative number")
  end
  local sum = 0
  local i = 1
  while i <= n do
    probe:Emit(i)
    sum = sum + i
    i = i + 1
  end
  return sum
end

----------------------------------------------------------------------
-- 4. Structured / nested event evaluation
----------------------------------------------------------------------

-- Deterministic deep traversal used as the benchmark checksum.  Every nested
-- value participates, so a backend that silently drops nested data produces a
-- different number instead of a "fast" result.
function M.sum_ids(ctx)
  local total = 0
  local events = ctx.events
  if type(events) ~= "table" then
    error("sum_ids: ctx.events missing")
  end
  local i = 1
  while events[i] ~= nil do
    local ev = events[i]
    total = total + ev.id
    local tags = ev.tags
    if type(tags) == "table" then
      local j = 1
      while tags[j] ~= nil do
        if type(tags[j]) == "string" then
          total = total + string.len(tags[j])
        end
        j = j + 1
      end
      local t = tags.threat
      if type(t) == "number" then
        total = total + math.floor(t * 100)
      end
    end
    local actor = ev.actor
    if type(actor) == "table" then
      if type(actor.name) == "string" then
        total = total + string.len(actor.name)
      end
      if actor.hostile == true then
        total = total + 1000
      end
      if type(actor.level) == "number" then
        total = total + actor.level
      end
    end
    i = i + 1
  end
  return total
end

-- Evaluates the complete dataset and returns a reaction NAME, or nil when no
-- rule matches.  This is the shape VoicePackFramework would consume.
function M.evaluate(ctx)
  if type(ctx) ~= "table" then
    error("evaluate: ctx must be a table, got " .. type(ctx))
  end
  if type(ctx.events) ~= "table" then
    error("evaluate: ctx.events is not a table (got " .. type(ctx.events) .. ")")
  end

  evaluate_calls = evaluate_calls + 1

  local summary, err = H.summarize(ctx)
  if summary == nil then
    error("evaluate: " .. tostring(err))
  end

  -- Rule order is intentional and part of the compared dataset.
  if summary.max_threat >= H.THREAT_FLEE then
    return H.reaction_name(H.REACTION.FLEE)
  end
  if summary.any_reloading then
    return H.reaction_name(H.REACTION.RELOAD)
  end
  if summary.enemies > 0 and summary.hostile_actors > 0 then
    return H.reaction_name(H.REACTION.ENGAGE)
  end
  if summary.enemies > 0 then
    return H.reaction_name(H.REACTION.INVESTIGATE)
  end
  if summary.event_count == 0 then
    return nil
  end
  return nil
end

function M.call_count()
  return evaluate_calls
end

----------------------------------------------------------------------
-- 5. Read-only game context pulled from the host (no writes)
----------------------------------------------------------------------

local PLAYER_FIELDS = {
  "available", "reason", "name", "display_name", "health", "max_health",
  "stamina", "game_stage", "is_dead", "is_admin", "pos_x", "pos_y", "pos_z",
  "world_guid", "world_day", "world_hour", "captured_on_frame",
}

function M.player_snapshot()
  local t = {}
  local i = 1
  while PLAYER_FIELDS[i] ~= nil do
    local key = PLAYER_FIELDS[i]
    local v = probe:PlayerField(key)
    if v ~= nil then
      t[key] = v
    end
    i = i + 1
  end
  t.has_world = probe:HasWorld()
  return t
end

----------------------------------------------------------------------
-- 6. Capability probes (measured, never assumed)
----------------------------------------------------------------------

-- Describes the observed CLR-enum interop behaviour for this backend.  Never
-- throws: a failure to round-trip an enum is a finding, not an error.
function M.enum_probe()
  local info = {}
  local ok, r = pcall(function() return probe:SampleReaction() end)
  if not ok then
    info.sample_ok = false
    info.sample_error = tostring(r)
    return info
  end
  info.sample_ok = true
  info.lua_type = type(r)
  info.text = tostring(r)

  local ok2, back = pcall(function() return probe:EchoReaction(r) end)
  info.roundtrip_ok = ok2
  if ok2 then
    info.roundtrip_type = type(back)
    info.roundtrip_text = tostring(back)
  else
    info.roundtrip_error = tostring(back)
  end
  return info
end

-- Measures how the binding layer resolves CLR member names.  MoonSharp is
-- case-insensitive; NLua is case-sensitive.  A portable Lua API must therefore
-- use the exact C# spelling, and this probe is what proves it per backend.
function M.bridge_probe()
  local function try(fn)
    local ok, v = pcall(fn)
    local text = tostring(v)
    if string.len(text) > 90 then
      text = string.sub(text, 1, 90) .. "..."
    end
    if ok then
      return "ok:" .. text
    end
    return "err:" .. text
  end

  local f = function()
    return {
      probe = probe,
      emit_exact = probe.Emit,
      emit_lower = probe.emit,
      player_field_exact = probe.PlayerField,
      player_field_lower = probe.player_field,
      sample_exact = probe.SampleReaction,
      sample_lower = probe.sample_reaction,
    }
  end

  local ok, res = pcall(f)
  if not ok then
    return { ok = false, error = tostring(res) }
  end

  return {
    ok = true,
    probe_type = type(res.probe),
    emit_exact = type(res.emit_exact),
    emit_lower = type(res.emit_lower),
    player_field_exact = type(res.player_field_exact),
    player_field_lower = type(res.player_field_lower),
    sample_exact = type(res.sample_exact),
    sample_lower = type(res.sample_lower),
    version_dot = try(function() return probe.Version() end),
    version_colon = try(function() return probe:Version() end),
    emit_dot = try(function() probe.Emit(1) return "called" end),
    emit_colon = try(function() probe:Emit(1) return "called" end),
    player_field_dot = try(function() return probe.PlayerField("health") end),
    player_field_colon = try(function() return probe:PlayerField("health") end),
  }
end

-- Reports which standard-library surfaces this runtime actually provides.
--
-- NOTE: `//`, `math.type`, `utf8` and `string.pack` all exist in Lua 5.3, so they
-- only establish "5.3 or newer". They are therefore complemented by probes for
-- constructs that are genuinely NEW in 5.4 (to-be-closed variables, coroutine.close,
-- the warn function), because otherwise a 5.3 runtime would be mislabelled 5.4.
function M.language_features()
  local f = {}
  f.version = _VERSION
  f.has_math_type = (type(math) == "table" and math.type ~= nil)
  f.has_utf8 = (type(utf8) == "table")
  f.has_table_pack = (type(table) == "table" and table.pack ~= nil)
  f.has_table_unpack = (type(table) == "table" and table.unpack ~= nil)
  f.has_bit32 = (type(bit32) == "table")
  f.has_goto = false
  f.has_string_pack = (type(string) == "table" and string.pack ~= nil)
  f.has_coroutine = (type(coroutine) == "table")
  f.has_os_time = (type(os) == "table" and os.time ~= nil)
  f.has_load = (type(load) == "function")
  f.has_loadstring = (type(loadstring) == "function")
  f.has_setfenv = (type(setfenv) == "function")
  f.has_package = (type(package) == "table")
  f.math_maxinteger = tostring(rawget(math, "maxinteger"))
  -- Genuine 5.4 discriminators:
  f.has_coroutine_close = (type(coroutine) == "table" and coroutine.close ~= nil)
  f.has_warn = (type(warn) == "function")
  return f
end

-- `local x <close> = ...` is 5.4-only syntax.  Compiled, never executed.
-- Returns 0 when the construct parses (5.4+) and non-zero otherwise.
function M.close_attrib_probe()
  local chunk = "local t <close> = setmetatable({}, { __close = function() end })\nreturn 1\n"
  local fn, err = load(chunk, "=close_probe")
  if fn == nil and type(loadstring) == "function" then
    fn, err = loadstring(chunk, "close_probe")
  end
  return {
    compiled = (fn ~= nil),
    error_text = (err ~= nil) and tostring(err) or nil,
  }
end

-- `goto` is syntactically rejected on 5.1 and accepted on 5.2+.  It is probed
-- by compiling, not executing, so the result is a compile-capability finding.
function M.goto_compile_probe()
  local chunk = "local x = 0\ndo goto skip end\n::skip::\nreturn x + 1\n"
  local fn, err = load(chunk, "=goto_probe")
  if fn == nil and type(loadstring) == "function" then
    fn, err = loadstring(chunk, "goto_probe")
  end
  return { compiled = (fn ~= nil), error_text = (err ~= nil) and tostring(err) or nil }
end

-- `//` is a Lua 5.3+ operator.  On a 5.2 parser this must fail at COMPILE time.
function M.intdiv_compile_probe()
  local chunk = "return 7 // 2\n"
  local fn, err = load(chunk, "=intdiv_probe")
  if fn == nil and type(loadstring) == "function" then
    fn, err = loadstring(chunk, "intdiv_probe")
  end
  local ok, value = false, nil
  if fn ~= nil then
    ok, value = pcall(fn)
  end
  return {
    compiled = (fn ~= nil),
    executed = ok,
    value = (ok and value ~= nil) and tostring(value) or nil,
    error_text = (err ~= nil) and tostring(err) or nil,
  }
end

----------------------------------------------------------------------
-- 7. Deliberate failure entry point (invoked at most once per test run)
----------------------------------------------------------------------

function M.error_probe()
  error("INTENTIONAL_RUNTIME_ERROR: probe_core.error_probe was invoked on purpose")
end

return M
