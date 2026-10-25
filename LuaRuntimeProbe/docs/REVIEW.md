# Inline code review record — LuaRuntimeProbe

* **Reviewer route:** `openai-codex` / **`gpt-6.1-sol`** (GPT-6.1 Sol)
* **Harness:** DSH `workflow` tool, workflow name `luaprobe-inline-review`, phase `review`, one `agent()` call with an explicit provider/model override — **no separate review conversation was created**.
* **Model evidence:** the `subagent_live_models` listing at review time showed
  `openai-codex/gpt-6.1-sol — GPT-6.1 Sol` among the 12 allowed routes.
* **Reviewer's own verification:** it read the sources, rebuilt both projects (0 warnings, 0 errors), decompiled the game's registration/execution paths and the NLua release path, and re-ran the offline suite (43 pass / 0 fail / 8 info at that moment). It did **not** start the game, did not modify any file, and did not re-run the download experiments.
* **Verdict:** `accept_with_fixes`.
* **Disposition:** all 6 "important" findings and 5 of the 7 "minor" findings were fixed; the 2 remaining "minor" findings are documentation/scope corrections, both applied. The 3 "info" findings resulted in narrowed claims and one real improvement (bounded callback chunking).

---

## Reviewer summary (verbatim)

> 裁决：有条件接受，须修正上下文断言及基准测量问题后再将结果用于运行时选型。本人已读取实现、文档和原始日志，并反编译游戏注册/执行路径及 NLua 引用释放路径；实际构建两个项目，均为 0 警告、0 错误；实际离线运行得到 MoonSharp/NLua 各 pass=43 fail=0 info=8，退出码 0。这些通过数不消除下列假阳性。影响评估：仅运行获准构建和已审查的只读离线功能套件；未修改源码、未启动或操作游戏、未重跑下载实验及性能测试。

### Not reviewed (verbatim)

> 1. 按用户限制，未启动、重启或操作游戏；未验证实际 Unity Mono 装载/JIT、命令注册时序、真实玩家字段及跨世界快照行为。
> 2. 未进行远程权限、多人、dedicated server 或 AntiCheat 开启状态下的运行测试；仅审查相关游戏代码路径。
> 3. 未做初始化故障注入、异常/挂起/零样本基准实验；相关缺陷为本人读码确认的逻辑问题，运行后果明确标为推断。
> 4. 未重跑性能基准；表格数字来自已保存作者日志。未验证长期 native Lua 堆增长、最终器清理时延、真实游戏 GC/帧尾延迟或 T 行行为。
> 5. 未执行 xLua 下载、构建或 smoke 脚本；未独立复现文件名冲突、枚举完整原生导出、核验发布包哈希、当前 NuGet/发布版本和本机 C/C++ 工具链可用性。
> 6. 未验证任意用户 Lua 脚本的沙箱、标准库进程退出/文件副作用或 CLR 反射逃逸；当前实现与文档不能视作这些能力的安全保证。
> 7. 未审查或修改仓库中的无关脏工作；构建成功只证明当前依赖环境下的两个项目，未验证全新机器恢复依赖或未来残留 DLL 情形。

---

## Findings and dispositions

| id | severity | area | Reviewer's claim (condensed) | Disposition | Fix |
| --- | --- | --- | --- | --- | --- |
| R01 | info | loadability | Static loadability holds, but that is not "verified inside Unity Mono"; AntiCheat precondition; nothing detects a stray native DLL left in the mod root. | **Accepted** | Added `GameProbeHost.VerifyModRootIsLoadable()`: at startup every `*.dll` in the mod root is probed with `AssemblyName.GetAssemblyName`, and a non-managed file is logged as `LOADABILITY HAZARD`. AntiCheat note added to `README.md`. |
| R02 | important | test_parity | `readonly_player_context` can never take the "world available" branch: it tested `available` for a Lua *boolean*, but the bridge returns *strings*; the unavailable branch passed unconditionally without checking the reason. | **Accepted — real bug** | `RunReadOnlyChecks` now branches on the real boolean `probe:HasWorld()`, validates `name`/`health`/`max_health`/`captured_on_frame` as the string types the bridge actually produces, requires `available == "true"` in that branch, and in the unavailable branch requires a `world_unavailable:`/`player_unavailable:` reason while explicitly failing on `context_error:`. |
| R03 | important | lifecycle | Cached player snapshot can go stale across `run` invocations and across worlds, because the bridge (and its cache) outlive a command; the init-time visibility probe pre-fills the cache. | **Accepted** | New `ILuaProbeBackend.InvalidatePlayerSnapshot()`; `LuaBackendRegistry.InvalidatePlayerSnapshots()` is called at the start of every console command, and `ProbeSuite.Run` / `ProbeBench.Run` each invalidate before capturing. |
| R04 | important | performance | `ok=Y` is unreliable: a zero-iteration row satisfies `0 == coefficient*0`; an aborted loop can report `Performed=0`; the callback row never checked delivered counts. | **Accepted** | `BenchRow` gained `Skipped`/`Aborted`/`IsVerifiable`; `Add()` refuses to verify any row that did not complete work; the callback metric is verified three ways (Lua return value **and** `EmitCount` **and** `EmitSum`); aborts are surfaced as `A` with a `NOT VERIFIED:` reason. |
| R05 | important | performance | `lua_cold_init` printed `0/0/0` GC and `managed=0`, which were never measured. | **Accepted** | `RunColdInit` now captures real GC collection counts, `GcProbe` allocation deltas and the managed heap delta around the timed region. |
| R06 | important | performance | "Strictly bounded" was overstated: warm-up was outside the budget, an exhausted budget still granted one chunk per metric, and `RunCallbackBoundary` ignored the budget entirely and could be inflated from the command line. | **Accepted** | Warm-up is now inside the budget and aborts on overrun; an exhausted budget marks the remaining rows `S` (skipped) instead of measuring them; the callback metric is chunked (`BenchConfig.CallbackChunk`, default 2000) with budget checks between chunks and a hard ceiling of 200 000 total crossings; README now describes the budget as a **cooperative soft budget** with a one-chunk worst-case overrun. |
| R07 | important | performance | The table compares the current adapter paths, not bare engines; the NLua DTO path pays for `NewTable`+`GetTable`+global write per table; `lua_hot_call` claimed "cached function" while NLua re-resolved the dotted path per call. | **Accepted** | Both backends now cache resolved function handles (invalidated on `Shutdown`/module reload), so the row's claim is true — this also changed the measured numbers (NLua hot call 1.47 µs → ~0.55–0.65 µs), which is the fix working. The NLua table-construction round-trip is now stated in the metric's own `note:` in the report, and README §7 forbids ranking engines from the DTO rows. |
| R08 | minor | performance | The C# baselines are not "upper bounds" and the difference does not isolate Lua runtime cost. | **Accepted** | Notes renamed to "minimal-work reference … NOT an upper bound"; the fairness comment in the class header and README §7 rewritten; dead claim about "isolating the Lua runtime cost" removed. |
| R09 | important | test_parity | Language-level inference reported "5.4-compatible" from `//` + `math.type` + `utf8`, all of which exist in 5.3. | **Accepted — real bug** | `language_features` now also probes genuine 5.4 signals (`coroutine.close`, `warn`) plus a new `close_attrib_probe` that compiles `local x <close> = …`; `InferLanguageLevel` reports **5.4 only when a 5.4-only feature is present**, otherwise "5.3 or newer, but no 5.4-only feature detected". Verified: MoonSharp now reports `5.2 (no // operator, bit32 present)`, NLua reports `5.4 (5.4-only feature present: <close> attribute)`. |
| R10 | minor | test_parity | `readonly_bridge_surface` is a limited name-based check, not a read-only proof; `ProbeBridge` exposes a mutable list and `Reset()`, so the wording was too broad. | **Accepted** | Renamed to `readonly_bridge_surface_shape`, extended with a public-mutable-field scan, and the detail now states explicitly that it is a structural check of the declaration only, is not a sandbox, and that "read-only" refers to the reviewed game access path. |
| R11 | minor | lifecycle | After a failed NLua init, `reset` could not re-arm the backend, contradicting its own message. | **Accepted** | NLua now distinguishes a **permanent** failure (native library missing → `IsAvailable == false`) from a **retryable** one (`_initError` only); `ClearRetryableFailure()` is called by `reset`/`ShutdownAll`, so the next command genuinely retries. |
| R12 | minor | lifecycle | `SetDllDirectoryW` permanently changed the whole process's DLL search policy. | **Accepted** | Replaced with `LoadLibraryExW(..., LOAD_WITH_ALTERED_SEARCH_PATH)`, which scopes dependency resolution to the loaded module's own directory and makes no process-wide change. |
| R13 | minor | loadability | `GetModuleFileNameW` was declared without `CharSet.Unicode`, so the resolved-path diagnostic contained NUL bytes and was truncated. | **Accepted — real bug** | Declaration fixed to `CharSet.Unicode` with an explicit truncation/error path; the loader also now reports the flag used. |
| R14 | minor | docs | README said 41 passes (actual 43), documented `--bench 2500` which the harness ignored, and claimed "local-only" without support. | **Accepted** | Counts corrected to 43 + info=9; the offline harness now accepts `--bench [ms]` as well as `--budget ms`, matching the documented command; the "local-only" claim is replaced with an explicit statement of what `AllowedInMainMenu`/`DefaultPermissionLevel`/`IsExecuteOnClient` do and do not prove. |
| R15 | info | test_parity | Shared sources and the main assertions are genuine, but the checksum's coverage was described too broadly. | **Accepted** | The checksum check's detail now enumerates exactly what it covers (event id, tag lengths, `floor(threat*100)`, actor name length, `hostile`, `level`, ASCII data) and states that `scenario`, `kind` and the player sub-table are not part of it. |
| R16 | info | console | No automatic flood mechanism exists for the built-in commands, but extra/illegal arguments were silently ignored. | **Accepted** | `test`/`bench` now report ignored unexpected arguments and reject non-positive iteration/budget values instead of silently using defaults. |
| R17 | info | performance | The offline numbers are exploratory samples of this adapter on this machine and cannot decide the voice/reaction runtime; `managed_delta` is not an allocation measurement. | **Accepted** | New README §7 "How to read the benchmark" states every one of these limits, including that `allocated` is `n/a` (never 0), that Gen0/1/2 only see managed activity, and that nothing here establishes tail latency or frame-budget behaviour. |
| R18 | minor | docs | The xLua write-up overstated the saved evidence: the `EntryPointNotFoundException` run was not kept, and the standalone script built `XLua.dll` while the assessment described `XLuaManaged.dll`. | **Accepted** | The hazard-1 run is now saved as `docs/xlua-runtime-smoke-hazard1.log`; both scripts take explicit revision/assembly-name parameters (`-SourceRef`, `-AssemblyName`) and default to the collision-free `XLuaManaged`; `docs/xlua-assessment.md` now labels which logs are saved raw and which steps are historical author observations rather than reviewer reproductions. |

---

## Effect of the fixes on the recorded evidence

| | before review | after review |
| --- | --- | --- |
| assertions per backend | 41 pass / 0 fail / 8 info | **43 pass / 0 fail / 9 info** |
| language level reported for MoonSharp | `5.2 (no // operator, bit32 present)` — right answer from a rule that would have mislabelled a 5.3 runtime | `5.2 (no // operator, bit32 present)` from a rule that now requires a 5.4-only signal |
| language level reported for NLua | `5.4-compatible (… utf8 library)` — an unsound inference | `5.4 (5.4-only feature present: <close> attribute)` |
| `readonly_player_context` | could not take the "world available" branch | branches on the real `has_world` boolean; validates string-typed fields |
| benchmark rows verified | possible to "verify" a zero-iteration row | zero/aborted/skipped rows can never verify; callback verified by delivered-call counters |
| `lua_cold_init` GC columns | `0/0/0` (unmeasured, displayed as measured) | real collection counts |
| `lua_hot_call.add` note | claimed "cached function" while re-resolving each call | true: handles are cached (and the numbers moved accordingly) |
| post-fix offline rerun | — | `RESULT: all assertions passed`, exit code 0, every benchmark row `ok=Y` with flag `-` |

Raw post-fix run: `docs/offline-verification.txt`.

---

# Round 2 — inline review of the xLua integration, cross-frame benchmark and doc reconciliation

* **Reviewer route:** `openai-codex` / **`gpt-6.1-sol`**, via the DSH `workflow` tool (`luaprobe-round2-review`), one `agent()` call with an explicit provider/model override. No separate review conversation.
* **Reviewer's own verification:** read sources, logs and build configuration; decompiled the staged assemblies; read-only checked the specified hashes. It did **not** build, run tests, or touch the game, and said so explicitly.
* **Verdict: `reject`** as submitted — with 16 findings, of which 12 are `important`. Its summary: the General integration is legitimate and the staged identities check out, but budget enforcement, warm-up timing, manual-drive result saving and record consistency still had important defects.
* **Not reviewed** (reviewer's words, abridged): it did not run the game or drive the driver paths, did not rebuild or re-run the suites, did not re-download the vendor tag or re-verify upstream sources byte-for-byte, and did not measure long-run xLua native memory.

## Findings and dispositions

| id | sev. | area | claim (condensed) | Disposition |
| --- | --- | --- | --- | --- |
| R2-01 | important | kernel | The total budget is only checked when entering a new metric; the per-metric budget never terminates a metric, so `T` was unreachable and the budget was a lie per row. | **Accepted — real regression.** `Advance` now re-checks a per-metric **and** the total cap at every step boundary and finishes the row early with `T` (keeping its partial checksum). Verified: `--bench 1200` produces T rows on NLua (3) and xLua (2 T + 1 S), all `ok=Y`; `--bench 8000` produces none. Evidence: `docs/offline-budget-truncation.txt`. |
| R2-02 | important | kernel | The final warm-up chunk shares a step with the first measured iterations, so warm-up time lands in `MeasuredMs` and inflates `us/op`. | **Accepted — real bug.** The final warm-up chunk is now returned as its own warm-up step; measurement starts in the next step. |
| R2-03 | important | bounding | `active_ms` covers only `Step`, so `idle_ms = wall - active` mislabels setup/verify/GC as cross-frame waiting, and an aborting step's time was lost. | **Accepted.** Step time is charged in a `finally`; `AdvanceBusyMs`/`OverheadMs` were added; `idle` is now `wall - advanceBusy` (true waiting only) and all four numbers are reported. The synchronous run indeed shows `idle≈0.1-17.9 ms` and `overhead≈125-335 ms`. |
| R2-04 | important | bounding | 4 ms is a cooperative step budget, not a frame bound; the synchronous `Create` (init + module load) is unbounded and ran inside the console command; xLua's cold init contains a forced GC. | **Accepted.** Backend setup moved **out of the console command** into the driver's first frame; `BenchJob` records `SetupMs` and every report prints it; README now states plainly what is and is not bounded (worst case = one chunk; setup reported not hidden). |
| R2-05 | important | driver | `bench step` finishing a non-last backend called `StartNext` without saving that backend's result — one backend's measurements silently lost. | **Accepted — real bug.** A single `CompleteCurrentJob()` now records a result exactly once and is shared by the per-frame driver and `AdvanceByStep`. |
| R2-06 | important | driver | Cancellation stopped the job but left the in-flight row uncompleted (`us/op=0`, no checksum, flag `-`), and cleared `Pending` without `Planned`. | **Accepted.** Cancellation now finishes the partial row (timing + checksum + verification) and flags it `C` — distinct from budget `T`; queued ids are removed from the visible queue and named in the queue notes. |
| R2-07 | important | driver | A cross-frame job shares the cached backends, yet `test`/`run`/`reset`/`bench sync` could re-initialise or swap the bridge of a backend mid-measurement. | **Accepted.** Those commands now refuse while a queue is driving, naming the reason; `status`/`cancel`/`step` stay available. |
| R2-08 | important | xlua | Temporary xLua tables/functions are not released and nothing drains xLua's finalizer queue during a run, so DTO measurements accumulate registry references. | **Accepted.** Added `ILuaProbeBackend.Tick()`; the kernel calls it on every advance, and `XLuaBackend.Tick()` calls `LuaEnv.Tick()`, which is what actually performs the deferred releases (the General build still drains that queue). Ownership and the cost are now documented. |
| R2-09 | important | consistency | The assessment's performance table quoted numbers that do not match the referenced raw log, and the cold-start cause was over-claimed. | **Accepted — real inconsistency.** The table was re-derived from `docs/offline-verification.txt`; the cold-start sentence is now limited to the end-to-end setup total and explicitly declines to attribute it to a single step. |
| R2-10 | important | consistency | README simultaneously said xLua was integrated and not integrated, denied the operator's in-game testing, and generalised 43 assertions to the in-game run; the no-caps evidence covered only one backend and the program exited failed. | **Accepted — real inconsistency.** README now records the operator's actual results (43/0/9 main menu, 42/0/1 in world), explains the 42→43 cause (the structural check was unreachable in the world-available branch), keeps the operator's logs untouched, and states the assertion counts per mode. |
| R2-11 | important | consistency | The offline `--filter` run still validated every backend at the end, so a valid single-backend run was reported as failed — which had already produced a misleading `RESULT: 2 failed`. | **Accepted — real bug.** Validation now uses the same selection as the run; `docs/offline-world-available.txt` was regenerated and passes. |
| R2-12 | important | process | Two default builds deployed to the live mod folder, so the running session's build and the on-disk / staged builds diverge. | **Accepted as a deviation, disclosed.** The manifest records both hashes and states that the live on-disk default build is not the staged payload and not the loaded build. Mitigation adopted: every build command now passes an explicit `-p:ModDeployDir`, so the live folder is never the default target. The reviewer noted the manifest is adequate disclosure but not authorisation — correct, and the deployment decision stays with the operator. |
| R2-13 | important | xlua | The `#if !XLUA_GENERAL` enumeration was incomplete and partly wrong; the switch also changes code generation, string decoding and callback rooting, so the General numbers cannot stand in for the earlier Unity-flavoured smoke. | **Accepted — real doc error.** The guard table was rebuilt from the tagged sources and now covers both guard polarities, including `CodeEmit`/`ObjectTranslator` enabling runtime wrapper generation, `LuaDLL` `GCHandle.Alloc` rooting and explicit UTF-8 decoding. The assessment states that the numbers describe the General adapter and why the staged payload is the General build. |
| R2-14 | important | process | The General smoke script exits 0 even when the smoke times out or the process exits non-zero. | **Accepted.** The script now exits 5 on timeout, on non-zero child exit, or when the success marker is missing from the output. |
| R2-15 | info | kernel | Confirms the index/task-start fixes are present, skip vs unverified are distinguishable, and the current raw log really is 30 `-` rows. | **Noted.** It also correctly warns that row counts and checksums do not validate the budget — which is why R2-01/R2-06 are now tested explicitly. |
| R2-16 | info | xlua | Confirms the staged managed/native pairing and that the General configuration needs no upstream modification; did not re-verify upstream bytes. | **Noted**, and its caveat about the operator coordinating the restart is carried into the manifest. |

## Effect of the round-2 fixes on the record

| | round-2 as submitted | after the fixes |
| --- | --- | --- |
| per-metric budget | never truncated; `T` unreachable | `T` reachable and demonstrated (`--bench 1200`) |
| warm-up | leaked into `us/op` | separate warm-up step; excluded from `MeasuredMs` |
| idle vs overhead | one mislabelled number | `active` / `wall` / `idle` (waiting) / `overhead` (setup+verify+GC) / `setup` |
| `bench step` on a queue | could lose a backend's result | single completion path; recorded exactly once |
| cancel | left an unverified placeholder row | completes and verifies the partial row as `C` |
| concurrent commands during a run | could corrupt the measurement | refused with a reason |
| xLua deferred releases | only at shutdown | `Tick()` on every advance |
| offline `--filter` | validated backends it never ran | validates the selected set |
| suite count | 43/0/9 offline; 42/0/1 in world (pre-fix) | 43/0/9 offline with and without `--caps`; world-available branch covered via `LUAPROBE_FAKE_WORLD=1` |
| smoke script | exit 0 on failure | exit 5 on failure |
| raw evidence | numbers not matching the linked log | every table re-derived from the log it cites |

Raw post-round-2 run: `docs/offline-verification.txt` (three backends, 43/0/9 each,
`rows=10/10 measured=10` each, `RESULT: all assertions passed`).

---

# Round 3 — verification review of the round-2 fixes, the 42/43 dispute, and the deploy plan

* **Reviewer route:** `openai-codex` / **`gpt-6.1-sol`**, DSH `workflow` (`luaprobe-round3-verify`), one `agent()` call with an explicit provider/model override.
* **Verdict: `reject`** — 8 of the 10 reviewed fixes `implemented`, 2 `implemented_but_unsound`, plus 3 new important findings and several deploy-plan defects.
* **It reproduced one of the new bugs in memory** (cancellation at a boundary with no row in flight → `NullReferenceException` → `status=Failed`), which is exactly the kind of evidence this record wants.

## Fix verification (reviewer's own status column)

| finding | reviewer status | note |
| --- | --- | --- |
| R2-01 per-metric budget → `T` reachable | implemented | cited the step-boundary check and the existing T rows in `docs/offline-budget-truncation.txt` |
| R2-02 final warm-up chunk isolated | implemented | cited the separate `More(true)` return and the `WasWarmup` accounting |
| R2-03 `finally` timing, busy/overhead fields | **implemented_but_unsound** | `Tick()` ran *after* the busy stopwatch stopped, so Tick work landed in `IdleMs` |
| R2-04 setup out of the console command, `SetupMs` reported | implemented | |
| R2-05 single completion path for manual step | implemented | flagged a residual double-record path (→ R3-03) |
| R2-06 cancellation completes the partial row as `C` | **implemented_but_unsound** | crashed when `_current == null` (cancel before the first step or between metrics) |
| R2-07 refuse mutating commands while driving | implemented | |
| R2-08 `Tick()` hook + kernel call | implemented | |
| R2-11 `--filter` validation selection | implemented | |
| R2-14 smoke script exit code | implemented | (read from code, not executed, because it writes files) |

## The 42/43 dispute — adjudicated in the implementer's favour

The operator asked for the README to be corrected from 43 to 42 for the no-`--caps` case.
The reviewer checked the operator's own unmodified log and the current code and concluded the
implementer was right: the missing check in that log is **only**
`readonly_bridge_surface_shape` (plus the 8 `capability_*` checks that simply were not
requested), that check used to be unreachable whenever a world was loaded, and the fixed
build must therefore report **43/0/1** in world. It explicitly stated that the expectation
should **not** be changed to 42, and that the README's accounting — operator pre-fix numbers
attributed to the operator's build, 43 for the current build, plus a tripwire — is honest.

The README was nevertheless reworked, because the operator's request was legitimate at the
level of *claim shape*: it no longer asserts a single universal number, it tabulates which
build produced which count, and it states that a 42 on the staged build is a regression
signal. Measured modes and the check-name diff: `docs/offline-assertion-modes.txt`.

## New findings and dispositions

| id | sev. | area | claim | Disposition |
| --- | --- | --- | --- | --- |
| R3-01 | important | kernel | `Tick()` executes after `advanceWatch.Stop()`, so its work is charged to `IdleMs` instead of overhead. | **Accepted.** `Tick()` now runs inside the busy window; `advanceWatch.Stop()` moved after it. |
| R3-02 | important | kernel | Cancelling before the first `Advance` or between two metrics leaves `_current == null`, and `FinishCurrentEarly` dereferenced it → `NullReferenceException`, `status=Failed`. **Reproduced by the reviewer.** | **Accepted — real crash.** The cancel path now only finishes a row when one is in flight; otherwise it cancels cleanly. Mechanically regression-tested: `--selfcheck` case A (cancel before first advance) and B (cancel between metrics) both PASS. |
| R3-03 | important | driver | `CompleteCurrentJob` was not idempotent: a manual `bench step` after the queue finished re-recorded the last result, and the "recorded X" label could name the next backend. | **Accepted.** A `_recordedJob` reference guard makes recording idempotent per job; the finished backend's id is captured before switching; the completed job is cleared. |
| R3-06 | important | deploy plan | The acceptance table had impossible/incorrect expectations: `nativeLoaded` cannot list the natives before a backend initialises, `ctx` does not print `has_world=true`, and the budget/cancel expectations were not conditioned. | **Accepted.** `docs/DEPLOY-PLAN.md` §3 corrected: `status` after a functional command; `ctx` expects `available = true`; the budget step now asserts "some row shows T or S" rather than a fixed row list; all three cancel boundaries are described. |
| R3-07 | important | deploy plan | The live mod folder is **not** the file the deviation section described — it holds the 02:42:30 xLua-generation build. | **Accepted — the reviewer was right and the earlier live-state claim was wrong.** Re-measured, §6 rewritten with the true state, and the root cause found: PowerShell mangled a `-p:ModDeployDir` value ending in a backslash, so the "staged" builds silently deployed to the default (live) target. Fixed by dropping the trailing separator and asserting each build's own `deployed to …` line. |
| R3-08 | important | deploy plan | The rollback claim was wrong: rebuilding without `-p:WithXLua=true` does not remove the xLua row or its files. | **Accepted.** §4 now states it is a functional downgrade to the stub, that `XLua.Mini.dll`/`native\xlua.dll` remain on disk, and that removal is a separate operator-approved cleanup. |
| R3-09 | minor | deploy plan | The startup `LOADABILITY HAZARD` self-check cannot be relied on as the symptom of a native DLL in the mod root, because the mod fails to load before it runs. | **Accepted.** §2 now names the game's own `Failed loading DLL` / `BadImageFormatException` as the real symptom and demotes the self-check to supplementary. |

## Effect of the round-3 fixes

| | round-3 as submitted | after the fixes |
| --- | --- | --- |
| cancel before first advance / between metrics | `NullReferenceException`, `status=Failed` | `status=Cancelled`, `error=<none>` — `--selfcheck` cases A and B |
| cancel mid-metric | partial row completed as `C` | unchanged, still verified (`--selfcheck` case C: `flag=C performed=2242560 checksumOk=True`) |
| `Tick()` accounting | charged to `IdleMs` | inside the busy window → counted as overhead |
| manual step after a finished queue | possible double record / wrong label | idempotent record, correct label |
| live-folder claim | wrong (said 120 320 @ 02:25:48) | measured truth (127 488 @ 02:42:30) + root cause + assertion-based safeguard |
| rollback description | implied removal | described as a downgrade, cleanup called out as separate |
| deploy acceptance table | several impossible expectations | corrected, with the natural variance in the budget step stated |
| 42/43 expectation | operator requested 42 | reviewer confirmed 43 for the fixed build; README tabulates both builds and adds a tripwire |

Self-check evidence: `--selfcheck` output is reproduced in the handover; it is part of the
offline harness and can be re-run with
`LuaRuntimeProbe.OfflineProbe\bin\Release\net48\LuaRuntimeProbe.OfflineProbe.exe --selfcheck`.

**Open item, stated plainly:** the round-3 verdict was `reject`, and the fixes above are
verified by the implementer with a mechanical self-check plus re-reading — they have **not**
been through a fourth external review round, and no in-game run has exercised them. The
operator's in-game acceptance on the staged payload remains the gate.


