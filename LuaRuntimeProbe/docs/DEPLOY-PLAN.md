# Deploy plan — loading the current build into 7 Days to Die

Written for the operator. The implementer has not executed any of it: the game was running
throughout and every build in this round was written to a staging folder (with the deploy
target asserted in the build output, see §6).

## 1. What is being deployed, and why a restart is required

| file | where it goes | bytes | sha256 |
| --- | --- | --- | --- |
| `LuaRuntimeProbe.dll` | mod root | 128 000 | `739EA02E55386B420E750EC7C203A27568C61BC9446784C3FBA2D4B5DE82B17C` |
| `LuaRuntimeProbe.pdb` | mod root | 39 000 | `2E2F6E24E6E85764E0F91F0807CDB549B18F371067EAA2D186227E8DED98BBF8` |
| `XLua.Mini.dll` | mod root | 164 864 | `05193745B84EE893D1058990F78F87FC5B9158B00EA293724047BD99F996927E` |
| `MoonSharp.Interpreter.dll` | mod root | 366 592 | `1DB76110F21698639F55D28E21BDDB536C0C497CEB741DEE49FEDCCA9BCD1588` |
| `NLua.dll` | mod root | 92 160 | `476FFF1274AA8A85D18DF8CA1CFC1F420ED4654DCF9ABA0957B93B978A3592F6` |
| `KeraLua.dll` | mod root | 32 256 | `2681F797597E50C55D60C2179982F63998308551B6DFE6981244DE2CE9E5495A` |
| `native\lua54.dll` | `native\` | 523 264 | `6FC318FC57BCEC525DAC1D4A8B20D962AA072CEBC38E14185DA83C1ED39C20B4` |
| `native\xlua.dll` | `native\` | 570 368 | `F027A8505DE79BC3890BDA7A381F5B72FD81F6B3C1DFAA75B8ED8185BF623245` |

Source of truth: `staging\mod-with-xlua\` plus its `STAGING-MANIFEST.txt`.

**A full process restart is required.** Mod assemblies and native libraries are loaded once
at start-up. The game is at the main menu, which is a convenient moment, but the *process*
must end.

### What is on disk right now (measured, not assumed)

The live mod folder currently contains **the previous generation of the same xLua payload**:
`LuaRuntimeProbe.dll` = 127 488 bytes, sha256 `4D73134A8EA91F19F454DAB35DFD67F9EE86423CEE5BF3F6A9356870DAE20DA5`,
written 02:42:30, together with `XLua.Mini.dll` and `native\xlua.dll`. It is therefore an
**update**, not a first-time install of the xLua route — but it predates the round-3 kernel
fixes (cancel boundaries, Tick accounting, double-record guard). See §6.

## 2. Steps

1. **Fully exit** 7 Days to Die.
2. Copy the six managed files over the symlink target `<game>\Mods\LuaRuntimeProbe\`.
3. Ensure **both** native libraries are in `<game>\Mods\LuaRuntimeProbe\native\`:
   `lua54.dll` (NLua/KeraLua) and `xlua.dll` (xLua).
4. Do **not** touch `Lua\acceptance_start_cue.lua` or `docs\acceptance-20261002-*`
   (operator-owned).
5. Start the game.

> **Never put either native DLL in the mod root.** `Mod.LoadAssemblies()` calls
> `Assembly.LoadFrom()` on every `*.dll` in the root. The reliable symptom of a native file
> there is the game's own load failure — `[MODS] Failed loading DLL ...` /
> `BadImageFormatException` — and the *whole mod* stops loading. The mod's start-up
> `LOADABILITY HAZARD` self-check only helps if the mod got far enough to run its own
> initialisation; do not treat the absence of that log line as proof the layout is clean.

## 3. Acceptance sequence after the restart

| # | command | expected |
| --- | --- | --- |
| 1 | `luaprobe test moonsharp` | `pass=43 fail=0 info=1`. This is also what loads the native libraries. |
| 2 | `luaprobe status` | now `nativeLoaded=` lists **both** `lua54.dll` and `xlua.dll` (it shows `<none>` before any backend has initialised, because loading happens in `Initialize`, not in `status`); the `xlua` row is a real backend (`available : True`), not the stub |
| 3 | `luaprobe test all --caps` | each of `moonsharp`, `nlua`, `xlua`: `pass=43 fail=0 info=9` |
| 4 | `luaprobe ctx` (main menu) | `available = false` with a `world_unavailable: …` reason |
| 5 | enter a world, then `luaprobe test all` | each backend `pass=43 fail=0 info=1`. **A 42 here means the structural-check fix did not take effect — investigate, do not accept it as the baseline.** |
| 6 | `luaprobe ctx` (in world) | `available = true`, non-empty `name`, numeric `health`/`max_health` matching `sdtd_players` |
| 7 | `luaprobe bench start all 100000 8000` | returns immediately: `bench job started (cross-frame)` |
| 8 | `luaprobe bench status` (repeat) | progress with `activeMs`, `wallMs`, `idleMs`; the game stays responsive. Setup (`setup=`) is reported per backend and happens on the frame that starts it |
| 9 | after it finishes, `luaprobe bench status` | three tables. **Target**, to be confirmed on this machine: `flag census: rows=10/10 measured=10` with `ok=Y` on every row. If a row is `T`, that is a *budget* outcome, not a failure — read `performed` and the per-op figure; the flag census makes it explicit |
| 10 | `luaprobe bench start all 100 60`, then `bench status` | a deliberately small budget; expect `T` (truncated) and/or `S` (skipped) rows with their own reason lines. Whether a specific row truncates depends on the machine, so treat "some row shows T or S" as the assertion, not a fixed row list |
| 11 | start a bench, then `luaprobe test nlua` | refused with a reason while the queue drives |
| 12 | start a bench, then wait one frame, then `luaprobe bench cancel` | the in-flight row is completed and flagged `C` (a partial but verified measurement); queued backends are named in the queue notes. Cancelling **before the first frame** produces no `C` row at all — the queue is simply cleared |
| 13 | `luaprobe reset`, then `luaprobe run nlua Lua/user_demo.lua` | works after a reset |

## 4. Rollback

Rebuild **without** `-p:WithXLua=true` (a default payload is staged in
`staging\mod-default\`) and redeploy it, then restart.

Be precise about what that achieves: it is a **functional downgrade to the xLua stub**, not
a removal. The default build still registers the `xlua` backend and reports it as
`NOT COMPILED IN`, and it does **not** delete `XLua.Mini.dll` or `native\xlua.dll` from
disk. If the extra files must actually be removed, that is an explicit, operator-approved
cleanup step — this plan does not do it.

## 5. Build commands used for staging (repeatable)

```powershell
# xLua's own General runtime (managed + native) -> LuaRuntimeProbe\staging\xlua\
pwsh -File LuaRuntimeProbe\tools\Build-XLuaGeneral.ps1 -Smoke

# xLua-capable mod payload -> staging only
dotnet build LuaRuntimeProbe\LuaRuntimeProbe.csproj -c Release -p:WithXLua=true `
    -p:ModDeployDir=<repo>\LuaRuntimeProbe\staging\mod-with-xlua

# default (no xLua) mod payload -> staging only
dotnet build LuaRuntimeProbe\LuaRuntimeProbe.csproj -c Release `
    -p:ModDeployDir=<repo>\LuaRuntimeProbe\staging\mod-default
```

**The path must NOT end with a separator** — see §6. Always read the build's own
`deployed to <directory>` line back and confirm it names the staging folder.

## 6. Corrected deviation record

An earlier version of this section said the live folder held a default build from 02:25:48
and had not been written since. That was wrong, and a reviewer caught it. The measured
truth: the live folder holds the 02:42:30 xLua-generation build described in §1.

**Root cause.** The staging builds were invoked as
`-p:ModDeployDir=E:\…\staging\mod-with-xlua\` — with a **trailing backslash**. PowerShell
passes a native argument containing spaces wrapped in quotes, and the C runtime reads the
trailing `\"` as an escaped quote, which corrupts that argument; MSBuild never saw the
override, so those builds deployed to the project's default `ModDeployDir` — the live mod
folder. That happened for both the default and the xLua builds, which is why the live folder
went 120 320 (02:41:39, default) → 127 488 (02:42:30, xLua).

**What it did and did not affect.** The running game session kept executing the assembly it
had loaded at start-up, so no in-game result was corrupted. On-disk state was affected, and
the earlier claim that staging was in force was unverified at the time — the build prints its
real destination and that line was not being read back.

**Fix applied.** All subsequent builds pass the path **without** a trailing separator and the
command asserts that the build's `deployed to …` line names the intended staging folder; the
live folder has been re-measured afterwards and is unchanged since 02:42:30.

**What the operator still owns.** Whether to replace the live 02:42:30 build with the staged
one, and when. The implementer has not deployed, reverted or authorised anything in the live
folder.
