# Game Assembly Virtualization Design

> **⚠ 本文档描述的是 2026-08-30 的设计，已被 2026-09-18 的工作部分取代。**
> 最新状态见 `docs/handoff/2026-09-21-assembly-virtualizer.md`。
>
> 已被取代的部分（详见文内行内标注）：
> - 「Objective」写「**两个**固定游戏方法」→ 现行是一张**规则表**（4 条）。
> - 「Assembly transformation」第 3、4 条 —— **归属规则被推翻，且与原意相反**（最需要当心的一条）。
> - 「Assembly transformation」的 `Assembly-CSharp.dll.kfbackup.<sha256>.bak` **备份机制已删除**。
> - 「Module patch restoration」的「六个临时普通 Harmony 实现」之后又做过一轮转换。
>
> 原文保留不改写，以便追溯当时的设计意图。

## Objective

Keep KF's module-specific transpilers attached to generated subclasses by making two fixed game methods virtual before normal play starts:

- `ItemActionRanged.TryExecuteAction(ItemActionData)`
- `MinEventActionModifyStats.execute(MinEventParams)`

> **【已扩展 2026-09-18】** 当时是「两个固定方法」；现行是一张 **编号规则表**，每条由
> `所有者类型 + 方法名 + 返回类型 + 精确参数表` 唯一确定。当前 4 条（另两条为
> `ItemActionDynamicMelee.canStartAttack` 与 `ItemActionRanged.SwapSelectedAmmo`，
> 二者都带模块级补丁）。另有 4 个方法被**明确排除**并从规则表移除，因为它们是
> **静态**方法 —— 静态方法永远无法拥有 vtable 槽，再怎么修复也无法把模块派发引到它们上面：
> `ItemAction.ExecuteBuffActions`、`ItemActionAttack.Hit`、`ItemClass.LateInitAll`、
> `MinEventActionBase.ParseAction`。规则表与排除清单都在
> `AssemblyPatcher/AssemblyVirtualizer.cs` 的 `Rules` 与其上方注释里。

The compatibility check runs from `ModEvents.GameAwake`. An incompatible client shows one confirmation button; confirmation starts an external repair workflow and quits. An incompatible dedicated server shows a Yes/No confirmation in its console window and only repairs and quits when the operator picks Yes; picking No keeps the server running for inspection. Windows, Linux, and macOS are supported.

## Runtime gate

`GameAssemblyCompatibilityGate` resolves both methods by exact declaring type, method name, return type, and parameter types. Compatibility requires that each method exists, is virtual, and is not final. The game assembly path comes exclusively from Unity's `Application.dataPath` as `Managed/Assembly-CSharp.dll`, is normalized with `Path.GetFullPath`, and must be rooted and present. The second target must be in the same loaded assembly.

The tool location is resolved independently through `ModManager.GetMod("CommonUtilityLib", true).Path`. This uses the directory of the mod instance actually loaded by the game, including a centralized Mods directory. The target assembly still comes from Unity's data path, so multiple installed game versions cannot be confused with one another.

On a client, `XUiC_MessageBoxWindowGroup.ShowOk` displays a modal, one-button message. `GameAwake` occurs after the current game creates and parses XUi, so `GameManager.Instance.windowManager.playerUI.xui` is available. On confirmation, the launcher is started exactly once and `Application.Quit()` invokes the game's existing graceful quit path.

On a dedicated server, no XUi is touched — the server assembly does not contain `XUiC_MessageBoxWindowGroup`. The server console window is itself a WinForms form (`WinFormConnection : System.Windows.Forms.Form`) with a live message loop, and the vanilla close-confirmation already uses `MessageBox.Show`, so the gate shows a Yes/No `MessageBox` carrying the same failure text that is written to the log. Yes starts the launcher and then quits; No keeps the process alive so the operator can inspect it, the failure having already been logged in full. If the dialog itself cannot be shown, the gate falls back to fail-closed: log, start the launcher, quit.

Runtime discovery occurs only when at least one target method needs repair. The game-side .NET Framework code searches `DOTNET_ROOT*`, `PATH`, and standard official installation directories for an absolute `dotnet` host, then requires `Microsoft.NETCore.App 10.x` in `dotnet --list-runtimes`. A missing runtime causes a client confirmation dialog that opens Microsoft's official .NET 10 download page and exits. A dedicated server logs the same URL and shows it in its console confirmation dialog; it never attempts to open a graphical browser automatically.

## Cross-platform launcher

The mod contains:

- `launch-patcher.ps1` for Windows.
- `launch-patcher.sh` for Linux and macOS.
- One framework-dependent .NET 10 publication shared by Windows, Linux, and macOS.

The game passes the wrapper:

- the absolute `Assembly-CSharp.dll` path derived from this Unity process's data directory;
- the current process ID;
- the absolute `dotnet` host path that passed runtime validation;
- a Base64-encoded UTF-8 JSON restart request.

The restart request records the absolute current process executable from `Process.MainModule.FileName`, its working directory, and `Environment.GetCommandLineArgs()` excluding element zero. Encoding the structured request as one argument avoids reconstructing or reparsing game arguments in shell code.

The platform wrapper invokes the shared `KFAssemblyPatcher.dll` through the verified host. The patcher waits for the originating process to exit, modifies the assembly, and only after a verified successful patch attempts to start the same executable with the same argument array and working directory. Restart failure is logged and does not roll back a successful assembly patch. Patch failure does not restart, preventing an automatic repair loop.

## Assembly transformation

The patcher uses Mono.Cecil and a fixed allowlist. For each target it:

1. Resolves exactly one method by full signature.
2. Sets `Virtual` and `NewSlot`, clears `Final`, and preserves visibility and the method body.
3. ~~Changes direct `call` instructions to `callvirt` only when the caller is the declaring type itself or one of its nested compiler-generated types.~~
4. ~~Leaves calls in derived or unrelated types unchanged, preserving explicit base-call semantics.~~

> **【第 3、4 条已于 2026-09-18 作废，且与现行规则语义相反 —— 照本条实现会得到相反的补丁器。】**
>
> **现行规则**：**只跳过落在目标自身 override 体内的调用点** —— 只有在那里 mod 作者才有
> `base.X()`（直接调用）与 `X()`（虚调用）的决定权；**其余所有调用点都由补丁器改写，
> 包括来自派生类型的调用。**
>
> 推翻过程（用户连续四次纠正，会话 `seq 3642 → 3670 → 3678 → 3691 → 3779 → 3807 → 3825 → 3843`）：
> 最初实现按本条原文只改「所有者或其嵌套类型内」的调用点，用户质疑「难道 mod 程序集里
> 出现的调用点就不改写了吗？」；最终定义见 `seq 3825`：
> 「『对于当前虚化函数的调用点也不需要改』**指的是当前虚化函数内部的调用点**，
> 只有在这里 mod 作者有 base 和 this 的决定权，在其他函数内的调用点仍然是补丁器的职责范围内。」
>
> 现行实现见 `AssemblyPatcher/AssemblyVirtualizer.cs` 的 `FindOverrides` + `FindDispatchCalls`
> （注释「The bodies of a target's overrides are the one place the repair must not touch」）。

The current game has two owned calls to `TryExecuteAction` and two owned calls to `execute`, including the delayed coroutine state machine. The transformer requires at least one owned call per target and rejects ambiguous or missing targets.

> **【部分作废 2026-09-18】** 上面「two owned calls」是当时对那版 `Assembly-CSharp.dll` 的观测，数字随游戏版本变化，不再是设计的一部分。
> 「at least one owned call per target」改为 **「每个规则至少要有一个*会被改写的*调用点」**（`RequireRewritableCalls`）——
> 若某个目标的全部调用点都落在它自己的 override 体内，它就没有可用来派发的调用点，列进表里只会让整次修复中止。

After writing a temporary file in the target directory, the patcher reopens it and verifies both method flags and owned dispatch instructions. ~~It then preserves the unmodified source as `Assembly-CSharp.dll.kfbackup.<sha256>.bak` and replaces the target. Existing identical backups are reused.~~ Temporary files are removed on failure.

> **【备份机制已删除 2026-09-18】** 用户指令（`seq 3173`）：
> 「**不需要留备份污染目录，游戏程序集可以通过 steam 验证文件直接恢复。**」
> 因此 `.kfbackup.<sha256>.bak` 不再生成 —— 现已实测 `AssemblyPatcher/` 内 `kfbackup` **0 命中**。
> 恢复官方程序集的途径是 Steam 的「验证文件完整性」，而不是本地备份。

The operation is idempotent: an already valid assembly is not rewritten. A game update that restores either method to nonvirtual state is detected on the next `GameAwake` and repaired against the assembly under that launch's Unity data directory.

## Module patch restoration

The six temporary global Harmony implementations are moved back into their owning module classes as `MethodTargetTranspiler` methods:

> **【后续又做了一轮 2026-09-18】** 这次迁移之后，用户又要求把
> 「**新增到补丁器目标方法表内的**那批普通 Harmony 补丁也改写成对应模块的补丁」，
> 范围限定为「**只检查与模块类同处一个 cs 文件**的 Harmony 补丁类」（`seq 6819`），
> 并纠正过一处误报：`GetExecuteActionGrazeTarget` 对应的是 `ItemActionDynamic`
> 而非 `ItemModuleInversedAction`（`seq 6962`）。所以「六个」是 2026-08-30 的数字，不是终值。

- five module transpilers targeting `ItemActionRanged.TryExecuteAction`;
- the CVar replacer transpiler targeting `MinEventActionModifyStats.execute`.

This restores per-generated-type patching and avoids running module logic against unrelated game instances.

## Failure and recovery

- Missing or ambiguous method: no binary replacement and no restart; log requires a mod update.
- Empty Unity data path, missing game assembly, or invalid loaded-mod path: fail closed without guessing another installation or mod directory.
- Permission or lock failure: original remains intact; backup/temp paths and exception are logged.
- Wrapper or patcher missing: game exits and logs the exact expected absolute path.
- Missing .NET 10 runtime: no patch is attempted; clients open the official download page after confirmation, while dedicated servers log it and exit.
- Restart failure: patched assembly remains valid; player or service manager can restart manually.
- Steam validation/game update: may restore the original assembly; the gate runs again next launch.

This mechanism is intended for modded, anti-cheat-disabled launches. It does not attempt to bypass platform integrity or anti-cheat checks.
