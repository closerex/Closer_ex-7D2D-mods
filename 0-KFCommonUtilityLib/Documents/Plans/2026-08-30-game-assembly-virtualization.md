# Game Assembly Virtualization Implementation Plan

> **⚠ 本文档是 2026-08-30 的执行计划，其任务清单已被 2026-09-18 的工作部分取代。**
> 最新状态见 `docs/handoff/2026-09-21-assembly-virtualizer.md`。
>
> 仍然成立的部分：跨平台启动器/发布、`GameAwake` 兼容门、in-process 的规则修复路径。
> 已被取代的部分：
> - **Task 1 的转换规则** —— 「只改所有者及其嵌套类型内的调用点」这条归属规则已被推翻，
>   现行规则相反（只跳过目标自身 override 体内的调用点）。详见
>   `Documents/Specs/2026-08-30-game-assembly-virtualization-design.md` 中「Assembly transformation」的行内标注。
> - **备份机制已删除**（`seq 3173`：改用 Steam 验证文件恢复，不留备份污染目录）。
> - **Task 5 的「六个」Harmony 转换**之后又做过一轮（`seq 6819`）。
> - Task 6 的验证命令中，`AssemblyPatcher.Tests` 已移出仓库（见下方 Task 4 后的注记）。

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers-optimized:executing-plans to implement this plan task-by-task.

**Goal:** Detect nonvirtual KF module targets during `GameAwake`, repair the exact loaded game assembly out of process on Windows/Linux/macOS, restart with the original arguments, and restore per-module transpilers.

**Architecture:** The game mod performs a reflection-based startup gate, verifies an installed .NET 10 runtime, and starts an OS wrapper with absolute host/assembly paths and an encoded restart request. A framework-dependent Mono.Cecil console application waits for game exit, transforms and verifies a temporary assembly, replaces the original with a hash-keyed backup, and restarts the same executable. Fixed target descriptors prevent speculative game assembly edits.

**Tech Stack:** C# 9, .NET Framework 4.8 game mod, .NET 10 LTS framework-dependent console tool, Mono.Cecil 0.11.5, xUnit, PowerShell, POSIX shell.

**Assumptions:** Supports Windows, Linux, and macOS hosts on architectures supported by the installed .NET 10 Runtime. Uses Unity's `Application.dataPath` for the launched game's `Managed/Assembly-CSharp.dll` and `ModManager` for the actually loaded KF mod directory; it will not guess either path. Assumes an anti-cheat-disabled modded launch — it does not bypass integrity enforcement.

---

## File structure

Project paths are relative to the `ModsRepo` root. Runtime publications remain inside the mod so they are available from the path returned by `ModManager`.

- `AssemblyPatcher/KFAssemblyPatcher.csproj`: cross-platform patcher executable.
- `AssemblyPatcher/Program.cs`: argument parsing, waiting, logging, patch orchestration, restart.
- `AssemblyPatcher/AssemblyVirtualizer.cs`: fixed Cecil transformation and verification.
- `AssemblyPatcher/PatchRequest.cs`: restart request contract and Base64 JSON decoding.
- `AssemblyPatcher.Tests/*`: synthetic assembly tests for transformation and failure safety.
  **（2026-09-18：该目录已移出本仓库 —— 解决方案条目已移除，文件现存于 `_dsh-workspace\ModsRepo\AssemblyPatcher.Tests\`。）**
- `0-KFCommonUtilityLib/Tools/AssemblyPatcher/launch-patcher.ps1`: Windows publication launcher.
- `0-KFCommonUtilityLib/Tools/AssemblyPatcher/launch-patcher.sh`: Linux/macOS publication launcher.
- `0-KFCommonUtilityLib/Harmony/GameAssemblyCompatibilityGate.cs`: GameAwake probe, UI/dedicated branching, absolute-path launch request.
- `0-KFCommonUtilityLib/Harmony/Init.cs`: registers the compatibility gate first.
- Six existing module source files: restore target transpilers to generated module classes.
- `KFCommonUtilityLib.csproj` and solution: include new game-side source and tool projects.

### Task 1: Patcher transformation core

**Security flag:** security

**Does NOT cover:** process waiting, wrapper selection, game restart, or game UI.

- [x] Create an xUnit test project and synthetic Cecil assembly builder.
- [x] Add failing tests proving both target methods are nonvirtual and owned call sites use `call` before transformation.
- [x] Implement fixed target resolution, `Virtual | NewSlot`, `Final` clearing, and owned `call` to `callvirt` conversion.
- [x] Add and pass tests for nested state-machine calls, unrelated derived base calls, idempotency, and missing-target failure.
- [x] Verify with `dotnet test AssemblyPatcher.Tests/KFAssemblyPatcher.Tests.csproj`.

### Task 2: Safe file replacement and restart runner

**Security flag:** security

**Does NOT cover:** privilege elevation or restarting through Steam/EAC instead of the recorded process executable.

- [x] Add failing integration tests using temporary assemblies for backup creation, verified replacement, and no replacement after validation failure.
- [x] Implement wait-by-PID, Base64 JSON request parsing, temporary-file verification, SHA-256 backup naming, replacement, logging, and argument-array restart.
- [x] Pass the patcher test project and build the patcher executable.

### Task 3: Platform wrappers and publications

**Security flag:** security

**Does NOT cover:** Windows ARM, Linux ARM, or platforms not supported by the game.

- [x] Add PowerShell and POSIX wrappers that accept the absolute assembly path, PID, and encoded request without deriving a game directory.
- [x] Make the POSIX wrapper validate and invoke the absolute `dotnet` host with `exec`.
- [x] Publish one framework-dependent .NET 10 tool into `0-KFCommonUtilityLib/Tools/AssemblyPatcher`.

### Task 4: GameAwake compatibility gate

**Security flag:** security

**Does NOT cover:** methods whose names or signatures changed; those produce a fatal update-required result without patching.

- [x] Implement exact reflection checks and absolute target/mod path validation.
- [x] Capture process executable, working directory, and argument array into an encoded request.
- [x] Implement one-button client dialog and guarded launch-on-confirm.
- [x] Implement dedicated-server confirmation dialog (WinForms `MessageBox`, Yes = repair and quit, No = keep running) replacing the earlier immediate fail-closed quit. *(Revised 2026-09-18: the silent quit gave the operator no choice and no visible reason.)*
- [x] Register the gate before existing `GameAwake` handlers and include the source in the legacy project.

### Task 5: Restore module-scoped transpilers

**Security flag:** none

**Does NOT cover:** other global Harmony patches on the same original methods that are not module-specific.

- [x] Move five `TryExecuteAction` transpiler bodies and helpers into their owning action module classes with `MethodTargetTranspiler`.
- [x] Move the `MinEventActionModifyStats.execute` transpiler body into `CVarReplacer` with `MethodTargetTranspiler`.
- [x] Remove the six temporary ordinary Harmony patch classes without reverting unrelated current-version logic.

### Task 6: Focused verification

**Security flag:** none

**Does NOT cover:** starting the game, UI automation, live dedicated-server execution, or cross-host execution of macOS/Linux binaries.

- [x] Run patcher tests and inspect the resulting synthetic IL.
- [x] Build only `KFAssemblyPatcher`, `KFAssemblyPatcher.Tests`, and `KFCommonUtilityLib`.
- [x] Scan modified production files for stubs and inspect the scoped diff for unrelated changes.
- [x] Record exact commands and outcomes in `state.md`.
