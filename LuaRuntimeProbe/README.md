# LuaRuntimeProbe

Standalone experimental mod for **7 Days to Die** that evaluates candidate
embeddable Lua runtimes *inside the real game process* and reports machine-checkable
evidence through the game console.

**This is not a voice framework.** No game state is written: the mod only reads a
few local-player/world fields and never touches the player, inventory, world or save.

---

## 1. What it answers

| Question | How it is answered |
| --- | --- |
| Which Lua runtimes can actually be loaded by 7DTD's Mono runtime? | `luaprobe test` runs the same suite on every backend |
| What Lua *language* level does each one really give you? | feature probes (`//`, `math.type`, `utf8`, `bit32`, `_VERSION`), not version strings |
| Can C# call Lua, and Lua call back into C#? | same suite, both directions, with counters |
| Can structured/nested data cross the boundary intact? | `sum_ids` checksum verified against an independent C# implementation |
| How do number / bool / string / nil / enum semantics behave? | explicit scalar matrix incl. nil-vs-0-vs-false |
| How fast is each route, honestly? | bounded, chunked benchmark with a C# baseline and verified checksums |
| What does the failing route (xLua) actually cost? | reproducible build/load experiments under `tools/`, recorded in `docs/` |

---

## 2. Layout

```
LuaRuntimeProbe/
  LuaRuntimeProbe.csproj          the mod (SDK-style, net48, built alone)
  ModInfo.xml                     mod metadata (v1 format, same as the other mods here)
  Init.cs                         IModApi entry point
  Scripts/
    Backends/                     the runtime abstraction + MoonSharp / NLua / xLua
    Contracts/                    DTOs, normalised Lua values, the probe bridge
    Game/                         game-coupled host: mod paths + read-only player capture
    Harness/                      the functional suite, the benchmark, the report sink
    Console/                      the `luaprobe` console command
  Lua/
    probe_helpers.lua             helper module (module-reuse test)
    probe_core.lua                the single portable probe module every backend runs
    probe_require_probe.lua       target of the `require` capability probe
    user_demo.lua                 sample script for `luaprobe run`
    errors/runtime_error.lua      deliberately fails at runtime
    errors/syntax_error.lua       deliberately fails at compile time
  native/lua54.dll                side-loaded native Lua 5.4 for the NLua backend
  tools/                          reproducible xLua assessment experiments
  docs/                           raw evidence logs + the xLua assessment
LuaRuntimeProbe.OfflineProbe/     console harness that runs the SAME suite without the game
```

---

## 3. Backends and versions

| id | embedding library | Lua language level (measured) | runtime | native dependency |
| --- | --- | --- | --- | --- |
| `moonsharp` | MoonSharp **2.0.0** (nuget.org, `lib/net40-client`) | **5.2** — no `//`, no `math.type`, no `utf8`, `bit32` present, `goto` compiles | pure managed | none |
| `nlua` | NLua **1.7.9** (net46) + KeraLua **1.4.10** (net46) | **5.4** — `_VERSION="Lua 5.4"`, integer subtype, `//`, `utf8`, `string.pack` | native P/Invoke | `native/lua54.dll` (win-x64, from the KeraLua package) |
| `xlua` | Tencent/xLua, tag `v2.1.16_ndkr21b_with_win` | **5.4** — `_VERSION="Lua 5.4"`, `<close>` attribute, `coroutine.close`, `warn` | native P/Invoke + managed reflection layer | `native/xlua.dll` (win-x64, from the matching release bundle) |

`xlua` is compiled in only when the project is built with `-p:WithXLua=true`; without
that property it is a documented stub that says so in `luaprobe status`. The managed
half is xLua's **own** General configuration (`XLUA_GENERAL`, no UnityEngine
reference), built from the tagged sources by `tools/Build-XLuaGeneral.ps1` — no
upstream source is modified. See `docs/xlua-assessment.md`.

The embedding version and the Lua *language* version are reported separately on
purpose: MoonSharp 2.0.0 reports `_VERSION = "MoonSharp 2.0.0.0"`, so the language
level is derived from feature probes (including 5.4-only ones), never from the
version string.

### Interop contract every backend must satisfy

Measured, not assumed — and it is the same for both native backends:

| | MoonSharp | NLua | xLua |
| --- | --- | --- | --- |
| member name resolution | case-insensitive | **exact** | **exact** |
| `probe.Emit(1)` (dot) | works | fails | fails |
| `probe:Emit(1)` (colon) | works | works | works |
| `type(probe.Emit)` | `function` | `userdata` (callable) | `function` |

`Lua/probe_core.lua` therefore uses the exact C# spelling with **colon** syntax
(`probe:Emit(...)`), which is the only portable form. The `capability_bridge_member_case`
line in `luaprobe test --caps` reports this per backend rather than asserting it.

---

## 4. Build

```powershell
# from the repository root
dotnet build LuaRuntimeProbe\LuaRuntimeProbe.csproj -c Release
```

The build deploys the loadable payload into the project folder itself (which is
the mod folder, symlinked from `<game>\Mods\LuaRuntimeProbe`):

```
LuaRuntimeProbe.dll            mod assembly
LuaRuntimeProbe.pdb
MoonSharp.Interpreter.dll      MoonSharp 2.0.0
NLua.dll                       NLua 1.7.9
KeraLua.dll                    KeraLua 1.4.10
native\lua54.dll               native Lua 5.4 (570 KB)
```

> `KeraLua`'s NuGet targets would copy `lua54.dll` next to the DLLs. That is
> disabled (`ShouldIncludeNativeLua=False`) because 7DTD's `Mod.LoadAssemblies()`
> calls `Assembly.LoadFrom()` on **every** `*.dll` in the mod root — a native DLL
> there throws `BadImageFormatException` and the whole mod fails to load. The
> native libraries are staged under `native\` and side-loaded explicitly.

### Building the xLua-capable payload (optional, staged)

```powershell
# 1. build xLua's own General runtime and stage it
pwsh -File LuaRuntimeProbe\tools\Build-XLuaGeneral.ps1

# 2. build the mod with xLua compiled in, INTO A STAGING FOLDER
#    (never over the deployed payload without coordinating the restart)
dotnet build LuaRuntimeProbe\LuaRuntimeProbe.csproj -c Release -p:WithXLua=true `
    -p:ModDeployDir=<repo>\LuaRuntimeProbe\staging\mod-with-xlua\
```

The staging folder gets a `STAGING-MANIFEST.txt` with the sha256 of every file,
the current on-disk live payload's hash, the deploy steps and the rollback. The
default build (no `-p:WithXLua=true`) has no xLua reference at all.

### Deployment

`<game>\Mods\LuaRuntimeProbe` is a symbolic link to `..\ModsRepo\LuaRuntimeProbe`
(matching how the other mods in this repository are deployed). It already exists;
if it is ever lost, recreate it with:

```powershell
New-Item -ItemType SymbolicLink -Path '<game>\Mods\LuaRuntimeProbe' -Value '..\ModsRepo\LuaRuntimeProbe'
```

**A game restart is required** to load a new mod. Mods are loaded at startup by
`ModManager`; `luaprobe` will not appear in a running session that started before
the mod was deployed. Restarting the game is a user action — nothing in this
repository restarts it.

---

## 5. Acceptance commands

All commands are ordinary console commands, so they can be driven by the 7DTD MCP
(`sdtd_console`) without opening the F1 window or simulating input.

| # | Command | Expected result |
| --- | --- | --- |
| 1 | `luaprobe help` | usage block; lists `moonsharp,nlua,xlua` |
| 2 | `luaprobe status` | mod/lua/native paths; environment (Unity, CLR, Mono, `Constants` version fields); each backend with available/embed version/declared vs measured Lua level/runtime/native dependency/state |
| 3 | `luaprobe list` | one line per backend: `id  available  embedVersion  lua=…  runtimeKind` |
| 4 | `luaprobe ctx` | read-only player/world snapshot; with no world loaded it prints `available = false` plus an explicit `world_unavailable: …` reason, never zeroes |
| 5 | `luaprobe test all --caps` | for `moonsharp` and `nlua`: **`pass=43 fail=0 info=9`**; for `xlua`: one informational "not integrated" line |
| 6 | `luaprobe test nlua` | same 43 checks for one backend only |
| 7 | `luaprobe bench start all 100000 8000` | starts a **cross-frame** job and returns immediately; a hidden driver advances the shared kernel by ~4 ms of measured work per frame |
| 7b | `luaprobe bench status` | queue state + per-backend tables; repeat until every backend has finished |
| 7c | `luaprobe bench cancel` | stops the queue (the chunk in flight finishes first) |
| 7d | `luaprobe bench step 50` | advances the job manually by 50 ms of measured work; for deterministic driving or when no driver is active |
| 7e | `luaprobe bench sync all 100000 8000` | explicit opt-in to the blocking driver (blocks the main thread for the whole run) |
| 8 | `luaprobe run nlua Lua/user_demo.lua alpha beta` | `user_module.main returned "user_demo backend_version=LuaRuntimeProbe/1.0.0 args=2 has_world=… health=… name=… reason=…"` |
| 9 | `luaprobe run moonsharp Lua/user_demo.lua` | same shape on the managed backend |
| 10 | `luaprobe reset` | `all backends shut down; next command will re-initialise them` |

Benchmark flag column: `-` measured, `T` truncated at a work budget, `C` cancelled
(partial row, still verified against the iterations it did), `S` skipped because the
run's work budget was already spent, `A` aborted. Each report prints a **flag census**
(`rows=N/expected ...`) and a row-count mismatch is a loud warning, so the table can no
longer silently disagree with what was measured. A row with `ok=N` prints a
`NOT VERIFIED:` line naming the reason; `S` prints `SKIPPED (not a failure)` because
declining to measure a metric is a declared budget outcome, not a defect.

`T` is genuinely reachable — verified offline with `--bench 1200`
(`docs/offline-budget-truncation.txt`), where NLua reports 3 truncated rows and xLua
2 truncated plus 1 skipped, each still `ok=Y` because the checksum is evaluated against
the iterations that actually ran. An earlier revision of the kernel checked only a
per-Advance slice, so no metric could ever be truncated and the `T` flag was
unreachable; that is fixed and the fix is what that log records.

Each result also reports `setup=` (backend initialise + module load + bridge install).
Setup runs on the frame that starts a backend and is **not** bounded by the per-frame
work budget, so it is reported instead of hidden. `idle` is genuinely time waiting
between frames; `overhead` is the setup/verify/GC time that happens inside advances.

### Assertion counts by mode — and which build produced which number

`--caps` only **adds** 8 informational capability lines; it never changes the number of
assertions. The **pass count depends on which build is loaded**, because one assertion was
unreachable in the world-available branch until it was fixed:

| build | world | command | pass | fail | info |
| --- | --- | --- | --- | --- | --- |
| current (post-fix) | no world | `luaprobe test <backend>` | **43** | 0 | 1 |
| current (post-fix) | world loaded | `luaprobe test <backend>` | **43** | 0 | 1 |
| current (post-fix) | either | `luaprobe test <backend> --caps` | **43** | 0 | 9 |
| the build the operator had loaded (pre-fix) | world loaded | `luaprobe test all` | 42 | 0 | 1 |
| the build the operator had loaded (pre-fix) | main menu | `luaprobe test all --caps` | 43 | 0 | 9 |

The 42 is explained rather than excused: `readonly_bridge_surface_shape` used to be the
last statement of `RunReadOnlyChecks`, after the world-available branch returned early, so
it disappeared **exactly when a world was loaded**. The operator's own unmodified log
(`docs/acceptance-20261002-inworld.log`) is missing that one check name and nothing else;
43 − 1 = 42 with `fail=0` is the whole difference. Measured modes and the check-name diff:
`docs/offline-assertion-modes.txt`.

**Tripwire.** If the staged build reports **42** in world without `--caps`, the fix did not
take effect — treat that as a regression signal to investigate, not as the expected
number. The fixed build reports 43 with the structural check present in both world states.

Every command also writes its output to the game log (`Log.Out`), so results can be
recovered from `Logs\` afterwards.

### What "pass=43 fail=0" means, and what it does not

`pass=43 fail=0` means the same 43 assertions that pass in the offline harness
(`docs/offline-verification.txt`) also pass in the game. It is **not** a claim that
the mod or the game is correct. The in-game values a human still has to read and
judge are: the reported versions, the `capability_*` informational lines, and the
benchmark numbers. In particular `readonly_player_context` only proves the snapshot
mechanism works (with a world it must report `has_world=true` plus a non-empty name
and numeric health; without one it must report an explicit `world_unavailable:`
reason and *not* a `context_error:`).

### Anti-cheat note

7DTD refuses to load code mods when the client anti-cheat is enabled unless the mod
declares itself compatible. `luaprobe` is an ordinary code mod; if `luaprobe status`
reports nothing at all after a restart, check that EAC is off. The mod also performs a
loadability self-check at startup and logs an error for any non-managed `*.dll` found
in the mod root, because such a file makes `Mod.LoadAssemblies()` abort the whole mod.

### Freshness of the read-only context

Backends (and their bridges) live for the whole game session, so `luaprobe` drops any
cached player snapshot at the start of every command. Without that, a later command
could report the player/world values captured in a previous world.

---

## 6. What is tested, what is not, what is unsupported

### Tested (offline, `LuaRuntimeProbe.OfflineProbe`, .NET Framework 4.8)

`dotnet build LuaRuntimeProbe.OfflineProbe\LuaRuntimeProbe.OfflineProbe.csproj -c Release`
then run `LuaRuntimeProbe.OfflineProbe\bin\Release\net48\LuaRuntimeProbe.OfflineProbe.exe [--bench [ms]] [--budget ms] [--filter id]`.
Exit code 0 means every assertion passed. Raw output: `docs/offline-verification.txt`.

* 43 assertions per backend on MoonSharp 2.0.0 and NLua 1.7.9: module loading,
  C#→Lua calls, Lua→C# callbacks (with delivered-call counters), nested dataset
  traversal (checksum cross-checked against an independent C# implementation),
  reaction-name-or-nil evaluation over four datasets, nil/bool/number/string/enum
  semantics, deliberate runtime error, deliberate syntax error, module state reuse,
  the `luaprobe run` code path (`Lua/user_demo.lua` loads and its `main` is callable),
  clean re-init and reload after shutdown, and two structural checks on the bridge
  surface (no public setter / no public mutable field / no mutating method name).
* `native\lua54.dll` side-loading by `LoadLibraryW` on the real Windows x64 loader.
* The identical backend + suite + benchmark sources that the mod ships
  (`LuaRuntimeProbe.OfflineProbe.csproj` links them; there is no second copy).

### Tested (in the game) — by the operator, 2026-10-02

Recorded by the operator in `docs/acceptance-20261002-summary.json` and the two raw logs
beside it (operator-owned files; not rewritten here):

| run | command | result |
| --- | --- | --- |
| main menu | `luaprobe test all --caps` | `moonsharp` 43/0/9, `nlua` 43/0/9, `xlua` "full suite not integrated" (that build predates the xLua integration) |
| in world (save `__mcp_work_luaprobe_20261002`) | `luaprobe test all` | `moonsharp` 42/0/1, `nlua` 42/0/1; player health/max 100/100 and name cross-checked against `sdtd_players` and a direct CUE read |
| in world | `luaprobe reset` then `luaprobe run ... Lua/user_demo.lua` | both backends pass after reset |
| in world | native disconnect | both backends report `has_world=false`, health/name nil (no stale snapshot) |
| CUE smoke (Unity-flavoured xLua build) | CUE MCP | `XLUA_GAME_SMOKE:Lua 5.4\|2\|7\|missed` |

**The 42 vs 43 difference is explained and fixed.** In that in-world run one assertion was
missing: `readonly_bridge_surface_shape` sat *after* an early `return` in the
world-available branch of `RunReadOnlyChecks`, so it silently disappeared whenever a world
was loaded — precisely the case that matters. It is now a separate step invoked
unconditionally from `Run`, and `LUAPROBE_FAKE_WORLD=1` makes the offline harness cover
the world-available branch too (`docs/offline-world-available.txt`), where the count is 43
with the structural check present. The operator's original log is left untouched.

So: the **43-assertion** suite is what the current build runs in every mode; the in-game
figures above are from the build the operator had loaded at the time.

### Not tested / known gaps

* **Unity Mono vs .NET Framework.** The offline harness runs on .NET Framework 4.8;
  the game runs Unity Mono. Results can differ (the xLua failure in
  `docs/xlua-assessment.md` is exactly such a difference).
* **`managed_delta` in the benchmark is a live heap delta, not an allocation
  measurement.** It can be negative because a GC may run during the measured
  region. `GC.GetTotalAllocatedBytes` is unavailable on .NET Framework 4.8 and on
  this Mono profile, so the `allocated` column reads `n/a` rather than `0`.
  Gen0/1/2 collection counts deltas are the reliable allocation-pressure proxy.
* **No instruction-count watchdog for user scripts.** `luaprobe run` executes a
  mod-supplied Lua file on the main thread with no step limit; a runaway script
  will freeze the game. Use short scripts. (The built-in suite and benchmark are
  bounded; user scripts are not.)
* **`require` in MoonSharp goes through a custom `IScriptLoader`** rooted at
  `<mod>\Lua`; `package.path`/`package.cpath` and native C modules are not
  exercised. NLua's `require` uses the real Lua 5.4 package system with
  `package.path` extended by the mod's `Lua` folder.
* **xLua interop is not exercised at all** — see below.
* **Coroutines, `debug` library, `os`/`io` sandboxing, and long-running GC
  behaviour are not covered.** `collectgarbage`, `os.exit` and `io` are reachable
  from scripts because the runtimes are opened with their standard libraries.
* **Multiplayer / remote senders are untested.** The command declares
  `AllowedInMainMenu = true` and `DefaultPermissionLevel = 0`; `ConsoleCmdAbstract`
  defaults `IsExecuteOnClient` to false. Those two properties describe menu
  visibility and the permission level, and they do **not** by themselves prove that
  no remote sender can reach the command — that was not tested here.

### Unsupported by design

* Random-world generation, save modification and any write to player/inventory/world
  state are out of scope and deliberately absent.
* xLua is compiled in only with `-p:WithXLua=true`; the default build contains a stub
  that reports itself as such rather than silently omitting the route.

---

## 7. How to read the benchmark (what the numbers do and do not support)

* **Every row is an end-to-end adapter cost**, not bare Lua-VM throughput: it includes
  argument marshalling, function resolution (handles are cached, so no per-iteration
  global lookup), the Lua call itself, and return conversion.
* **Rows are comparable between backends; differences are not "engine speed".**
  The three routes build the context table differently — MoonSharp allocates a managed
  `Table` directly, NLua goes through `NewTable(name)` + `GetTable(name)` + a global
  write to release the scratch name, xLua goes through `LuaEnv.NewTable()` — so the
  `lua_dto_*` rows compare **adapter implementations**, not the Lua engines. That is
  stated in each row's own `note:` in the report. It is still a legitimate input to
  "which runtime is cheaper to integrate".
* **The `csharp_baseline.*` rows are minimal-work references, not upper bounds.**
  `csharp_baseline.add` returns a constant; a Lua row additionally allocates an
  argument array and converts a return value. Subtracting one from the other does
  **not** yield "pure Lua cost".
* **`managed_delta` is a live heap delta, not an allocation count.** It can be
  negative because a GC may run inside the measured region. `GC.Collect()` +
  `WaitForPendingFinalizers()` is run *before* each row to give a clean baseline
  (which also drains NLua's finalizer-released registry references).
  The `allocated` column is `n/a` because `GC.GetTotalAllocatedBytes` does not exist on
  .NET Framework 4.8 — it is never reported as `0`.
  Gen0/1/2 collection-count deltas are the usable allocation-pressure proxy, and even
  they only describe *managed* activity: native Lua's own allocator is invisible here.
* **`active_ms` is measured work; `wall_ms` also contains the frames spent waiting.**
  Per-op figures are derived from active work, so a cross-frame run is not penalised
  for waiting, and the run's `idle_ms` is reported separately. Warm-up is charged to
  the budget but excluded from per-op figures.
* **The budget is in measured work milliseconds, not wall time**, and it is
  cooperative: a step in flight is never interrupted, so the worst case overruns by
  one chunk. Warm-up is inside the budget. When the whole budget is spent the
  remaining metrics are reported as `S` (skipped) instead of being measured with a
  token amount of work — an earlier version of this harness did the latter, and an
  earlier recorded run really did contain `T` rows, so "every row was `-`" is a
  property of a specific run, not a guarantee of the tool. The flag census printed
  under each table is what makes that checkable.
* **`T` rows are not comparable by total time.** Compare per-op figures and note the
  performed count; `active_ms` is normalized into `us/op`, the raw total is not.
* **The callback metric is chunked** (see `BenchConfig.CallbackChunk`), because a
  single Lua call cannot be interrupted. Each chunk restarts the emitted id sequence,
  so the expected checksum is the sum of per-chunk triangles, and the bridge's
  `emitCount`/`emitSum` counters must match it — a script cannot pass by computing the
  answer itself.
* **These numbers come from .NET Framework 4.8, not Unity Mono**, and the offline
  harness prints `gameRunning=true/false` because timings taken while 7 Days to Die is
  running on the same machine are contended. They are exploratory samples of this
  adapter on this machine; the in-game run is what counts, and neither run establishes
  tail latency or real frame-budget behaviour.

## 8. Verification commands used while building this

```powershell
# mod
dotnet build LuaRuntimeProbe\LuaRuntimeProbe.csproj -c Release

# offline harness (same suite, no game)
dotnet build LuaRuntimeProbe.OfflineProbe\LuaRuntimeProbe.OfflineProbe.csproj -c Release
LuaRuntimeProbe.OfflineProbe\bin\Release\net48\LuaRuntimeProbe.OfflineProbe.exe --bench 2500

# xLua assessment (bounded; downloads into %TEMP%\xlua-probe)
pwsh -File LuaRuntimeProbe\tools\Attempt-XLuaStandaloneBuild.ps1
pwsh -File LuaRuntimeProbe\tools\Attempt-XLuaRuntimeSmoke.ps1 -SourceRef v2.1.16_ndkr21b_with_win
```
