-- LuaRuntimeProbe / probe_helpers.lua
--
-- Helper module used by probe_core.lua.
--
-- This file is loaded by an explicit chunk load performed by each backend
-- (see ILuaProbeBackend.LoadScriptFile) instead of going through `require`,
-- so that the SAME file works on MoonSharp (Lua 5.2 language level) and on
-- KeraLua / native Lua 5.4 without depending on backend-specific
-- package.path / package.loaded semantics.  `require` support is measured
-- separately as a capability probe, not assumed.
--
-- LANGUAGE TARGET: Lua 5.2 (lowest common denominator).  Do NOT use:
--   * integer division `//`            (Lua 5.3+)
--   * bitwise operators & | ~ << >>    (Lua 5.3+)
--   * `goto` labels                    (Lua 5.2+ only, absent in 5.1)
--   * math.type / utf8 library         (Lua 5.3+)
--   * integer-only literals / `1LL`

local H = {}

H.SCHEMA_VERSION = 2

-- Enum-like constants are exchanged across the language boundary as plain
-- numbers, because Lua 5.2 has no integer subtype and no shared CLR enum type
-- exists in a portable Lua script.  Real CLR-enum marshalling is measured by
-- the separate `enum_probe` capability check.
H.REACTION = {
  NONE        = 0,
  FLEE        = 1,
  ENGAGE      = 2,
  RELOAD      = 3,
  INVESTIGATE = 4,
}

H.REACTION_NAME = {
  [0] = "None",
  [1] = "Flee",
  [2] = "Engage",
  [3] = "Reload",
  [4] = "Investigate",
}

-- Threat thresholds used by the shared evaluation dataset.
H.THREAT_FLEE = 0.75

function H.reaction_name(id)
  if type(id) ~= "number" then
    return nil
  end
  return H.REACTION_NAME[id]
end

function H.clamp(v, lo, hi)
  if v < lo then return lo end
  if v > hi then return hi end
  return v
end

-- Returns nil for "not a usable number", which is deliberately different from
-- returning 0: the probe suite asserts that nil and 0 stay distinguishable.
function H.to_number_or_nil(v)
  if type(v) == "number" then
    return v
  end
  return nil
end

-- Iterates an array part of a table without relying on the `#` operator,
-- whose result is unspecified for tables that also carry hash keys.
function H.array_len(t)
  local n = 0
  while t[n + 1] ~= nil do
    n = n + 1
  end
  return n
end

-- Deep, read-only traversal of the nested event payload handed over by C#.
-- Returns a summary table; never mutates its input.
function H.summarize(ctx)
  local enemies = 0
  local max_threat = 0
  local any_reloading = false
  local event_count = 0
  local hostile_actors = 0

  if type(ctx) ~= "table" then
    return nil, "ctx is not a table (got " .. type(ctx) .. ")"
  end

  local events = ctx.events
  if type(events) ~= "table" then
    return nil, "ctx.events is not a table (got " .. type(events) .. ")"
  end

  local i = 1
  while events[i] ~= nil do
    local ev = events[i]
    event_count = event_count + 1

    local tags = ev.tags
    if type(tags) == "table" then
      local j = 1
      while tags[j] ~= nil do
        if tags[j] == "enemy" then
          enemies = enemies + 1
        end
        if tags[j] == "reloading" then
          any_reloading = true
        end
        j = j + 1
      end
      local t = H.to_number_or_nil(tags.threat)
      if t ~= nil and t > max_threat then
        max_threat = t
      end
    end

    local actor = ev.actor
    if type(actor) == "table" and actor.hostile == true then
      hostile_actors = hostile_actors + 1
    end

    i = i + 1
  end

  return {
    event_count = event_count,
    enemies = enemies,
    hostile_actors = hostile_actors,
    max_threat = max_threat,
    any_reloading = any_reloading,
  }
end

return H
