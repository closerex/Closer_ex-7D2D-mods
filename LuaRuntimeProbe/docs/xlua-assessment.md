# xLua route — assessment and integration record

**Status: INTEGRATED** as a third backend of the same `ILuaProbeBackend` contract, in
the build produced with `-p:WithXLua=true`. It runs the identical `Lua/probe_core.lua`,
the identical 43 assertions and the identical benchmark kernel as MoonSharp and
NLua. This file records how it was made to work, what was measured, and the exact
boundary between the evidence gathered offline and the evidence gathered in game.

Nothing upstream was modified. The managed half is xLua's **own** non-Unity
configuration.

---

## 1. The finding that unblocked it: the upstream General (通用版本) build

The earlier conclusion in this repository was "not integrated, because `LuaEnv..ctor`
throws `ArgumentException: Can not find System.MonoType` outside Mono". That was
correct about the **Unity-flavoured** build and wrong about the route as a whole.

xLua ships a supported C#-host configuration:

* `General/vs2013/XLua.Mini.csproj` — `AssemblyName=XLua.Mini`, **`DefineConstants=…;XLUA_GENERAL`**, references **only `System` and `System.Core`** (no UnityEngine), and compiles the same 23 sources the Unity build uses.
* `General/README.md` states the scope in the vendor's own words: the General version "provides Lua scripting support in a C# environment"; compared with the Unity version it removes only the `print` redirect to the console and the Unity-specific script loader, and **"everything else is preserved"**. It also says `XLua.Mini.dll` uses reflection for Lua↔C# interop.
* The exact upstream compile-time branches are below. Note that they run in **both
  directions** — `XLUA_GENERAL` also *enables* things the Unity build does not use — and
  that two of them touch code the probe actually exercises (runtime wrapper emission and
  string decoding), so this configuration is a real behavioural difference, not only a
  subtraction of Unity features. Verified by reading the tagged sources.

| file:line | guard | what it does |
| --- | --- | --- |
| `LuaEnv.cs:95` | `#if !XLUA_GENERAL` | excludes registering `print` through `xlua_setglobal` (the Unity console redirect) |
| `LuaEnv.cs:108` | `#if !XLUA_GENERAL` | excludes the `LoadFromResource` / `LoadFromStreamingAssetsPath` searchers |
| `LuaEnv.cs:164` | `#if !XLUA_GENERAL` | **excludes `ObjectTranslator.Alias(typeof(Type), "System.MonoType")`** — the `System.MonoType` blocker |
| `LuaEnv.cs:325-336` | `#if !XLUA_GENERAL` | excludes the `UnityEngine.Object` validity scanner (`object_valid_checker`, `max_check_per_tick`) |
| `LuaEnv.cs:353-355` | `#if !XLUA_GENERAL` | excludes the `translator.objects.Check(...)` call inside `Tick()`; **`Tick()` still drains the deferred-release queue** either way |
| `ObjectTranslator.cs:170` | `#if !XLUA_GENERAL` | excludes a Unity-only branch |
| `ObjectTranslator.cs:357-362` | `#if !XLUA_GENERAL` | excludes the generic-delegate restriction (non-void value-type return, or >4 parameters); the General build does **not** apply that limitation |
| `StaticLuaCallbacks.cs:601, :683`, `GenAttributes.cs:130` | `#if !XLUA_GENERAL` | further Unity-only branches |
| `CodeEmit.cs:9`, `LuaBase.cs:29`, `ObjectTranslator.cs:153, :263, :292, :334, :586, :953`, `ObjectTranslatorPool.cs:40`, `Utils.cs:116, :123, :344, :359, :374` | `#if UNITY_EDITOR \|\| XLUA_GENERAL` | **enables** the Reflection.Emit wrapper generation and related reflection utilities that are otherwise editor-only — this is the path the probe's DTO pushes and callbacks go through |
| `LuaDLL.cs:215-217` | `#if XLUA_GENERAL \|\| WSA` | **adds** `GCHandle.Alloc(function)` to root delegates whose function pointers native code holds |
| `LuaDLL.cs:253-257` | `#if XLUA_GENERAL \|\| WSA` | **changes** Lua string decoding to `Marshal.Copy` + `Encoding.UTF8.GetString` instead of `Marshal.PtrToStringAnsi` |

So the correct statement is: *the General build removes the Unity-specific pieces and
switches on the runtime wrapper generator plus explicit UTF-8 decoding.* The shared
`probe_core.lua` algorithm and the DTO shape are unaffected, but **the xLua performance
numbers below describe the General adapter**, and the in-game payload must therefore be
the General build too (which is what the staging payload is) before those numbers are
compared with the other two backends in game.

## 2. Reproduce it

```powershell
pwsh -File LuaRuntimeProbe\tools\Build-XLuaGeneral.ps1 -Smoke      # build + offline smoke
dotnet build LuaRuntimeProbe.OfflineProbe\LuaRuntimeProbe.OfflineProbe.csproj -c Release -p:WithXLua=true
LuaRuntimeProbe.OfflineProbe\bin\Release\net48\LuaRuntimeProbe.OfflineProbe.exe --bench 8000
```

Staged artifacts (`staging\xlua\`, with `build-manifest.txt`):

| artifact | bytes | sha256 |
| --- | --- | --- |
| `XLua.Mini.dll` | 164 864 | `05193745B84EE893D1058990F78F87FC5B9158B00EA293724047BD99F996927E` |
| `native\xlua.dll` (win-x64) | 570 368 | `F027A8505DE79BC3890BDA7A381F5B72FD81F6B3C1DFAA75B8ED8185BF623245` |

Source revision for both halves: tag **`v2.1.16_ndkr21b_with_win`**.

## 3. Offline smoke result (General build, .NET Framework 4.8)

```
[general-smoke] LuaEnv constructed (no System.MonoType alias needed)
[general-smoke] version/arith: Lua 5.4 | 2 | 3 | integer
[general-smoke] nested table : 42
[general-smoke] bridge       : userdata | function | nil | dot=false/…NullReferenceException… Emit4(IntPtr L)
                               | colon=true/nil | version=true/general-smoke/1.0
[general-smoke] emitSum      : 2 (2 means only the colon call crossed the boundary)
[general-smoke] RESULT: General build works on .NET Framework
```

`_VERSION` = **Lua 5.4**, `7 // 2` = 3, `math.type(7)` = `integer`.

## 4. Integration hazards found, and how the integration avoids them

**Hazard 1 — case-insensitive filename collision (reproduced, fix verified).**
With the managed assembly named `XLua.dll` beside the native `xlua.dll`,
`new XLua.LuaEnv()` failed with
`EntryPointNotFoundException: cannot find entry point 'xlua_get_lib_version' in DLL 'xlua'`
while `GetProcAddress` on the same preloaded native module resolved that exact symbol.
Cause: Windows file lookup is case-insensitive, so the CLR's `LoadLibrary("xlua")` found
the *managed* assembly in the application directory; a pure-IL image loads as a module,
has no export table, and P/Invoke reports "entry point not found". Renaming the managed
assembly to `XLuaManaged.dll` removed the failure, which is how the cause was proven.
Raw log: `docs/xlua-runtime-smoke-hazard1.log`.
The integration uses the assembly name **`XLua.Mini`** for exactly this reason.

**Hazard 2 — Mono-only reflection assumption.** Removed by the General build (§1); it
is inherent to the Unity-flavoured build, where `LuaEnv..ctor` unconditionally aliases
`typeof(Type)` to `"System.MonoType"` and `ObjectTranslator.Alias` throws when that type
does not exist.

**Hazard 3 — native DLL in the mod root.** `Mod.LoadAssemblies()` loads every `*.dll`
in the mod root; a native library there aborts the whole mod. Both native libraries ship
in `native\` and are preloaded by full path (`LoadLibraryExW` with
`LOAD_WITH_ALTERED_SEARCH_PATH`, no process-wide DLL-directory change). The mod performs
a startup loadability self-check for this.

**Hazard 4 — a single global native handle.** `NativeLibraryLoader` originally cached
one handle, so with two native runtimes the second preload became a no-op and xLua
failed with `DllNotFoundException: xlua` although the file existed. The loader is now
keyed per library file name.

## 5. Interop contract (measured)

`probe.Emit(1)` (dot) fails; `probe:Emit(1)` (colon) works; `probe.emit` is `nil`
(exact C# spelling required). This is the **same contract NLua requires**, which is why
the shared `Lua/probe_core.lua` runs unchanged on xLua. `type(probe.Emit)` is
`function` on xLua (NLua reports callable `userdata`).

## 6. Measured results — three-way (offline, .NET Framework 4.8, contended machine)

These are the exact figures from `docs/offline-verification.txt` (the run whose flag
census is `rows=10/10 measured=10 truncated=0 skipped=0 aborted=0 unverified=0` for all
three backends). The harness reported `gameRunning=true` for that run, so they are
exploratory samples of this adapter on this machine, not clean-room numbers.

| metric | MoonSharp | NLua | xLua |
| --- | --- | --- | --- |
| `csharp_baseline.add` (`us/op`) | 0.002 | 0.002 | 0.002 |
| `lua_hot_call.add` (`us/op`) | 1.157 | **0.539** | 0.841 |
| `lua_dto_push_touch` (`us/op`) | **5.561** | 35.658 | 35.263 |
| `lua_dto_sum_ids` (`us/op`) | **20.716** | 38.926 | 46.229 |
| `lua_dto_evaluate` (`us/op`) | **21.217** | 40.422 | 46.752 |
| `lua_to_csharp.boundary` (`us/op`) | 1.151 | 0.857 | **0.198** |
| `lua_compile_core` (`us/op`) | 1570 | **271.7** | 406.2 |
| `lua_cold_init` (ms per init) | **3.558** | 4.633 | 63.691 |

Read these as **adapter** costs. xLua's cold start is ~14-18× the cost of the other two.
The cold-init row measures the whole sequence (fresh `LuaEnv`, both module files, one
`add(1,1)` through the bridge, then `Dispose`), and xLua's `LuaEnv` constructor and
`Dispose` both perform runtime code emission plus forced collections, so **the total is
attributable to that end-to-end setup, not proven to be caused by any single one of those
steps**. Its Lua→C# callback path is the cheapest of the three (`0.198 us/op`, ~4.3×
faster than NLua). No engine ranking is drawn from the DTO rows: the three adapters build
tables differently, and each row states that in its own `note:` in the report.

## 7. Cost summary

| item | cost / observation |
| --- | --- |
| NuGet package | none exists (still true) |
| Managed build | 23 sources via the vendor's General configuration, ~2 s with `dotnet`, **no UnityEngine reference** |
| Unity Editor / code generation | **not required**; wrappers are emitted at runtime by reflection |
| Native library | taken prebuilt from the matching release bundle; **cannot be built here** (no `cl`/`cmake`/`gcc`/`make`/`ninja`) |
| Extra shipped bytes | 164 864 (managed) + 570 368 (native) |
| Version coupling | managed source and native library must match revisions; `GetProcAddress` on the documented exports is the cheap pre-check |
| Integration hazards | 4 found; all four are reproduced, diagnosed and avoided by the integration |

## 8. Evidence boundary (deliberate, not blurred)

* **Offline** evidence for xLua comes from the **General build** above.
* **In game**, the operator's smoke run (`docs/acceptance-20261002-xlua-smoke.json`,
  `XLUA_GAME_SMOKE:Lua 5.4|2|7|missed`) used a **Unity-flavoured** managed build with the
  **identical native `xlua.dll`** (same sha256).
* The **staged payload** proposed for the next deploy uses the **General** build, so the
  offline and the next in-game run share one managed configuration. That is a change
  from the configuration the operator's smoke used, and it is the reason the staged
  payload must be re-smoked in game before anything is concluded from it.
* A restart is required to load any of this; the deployment moment is the operator's
  decision (`staging\mod-with-xlua\STAGING-MANIFEST.txt`).

## 9. Recommendation

With the General build, xLua is no longer the "expensive, unverifiable" route: it is a
normal third backend whose managed half needs no editor and whose native half is a
file copy. Its distinguishing costs are a heavier cold start and a runtime code-emission
dependency; its distinguishing benefit is the cheapest Lua→C# callback path measured
here. The decision between the three should still be made from the **in-game** numbers
produced by `luaprobe bench start all`, not from this table.
