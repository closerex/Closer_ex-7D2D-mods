# Framework-dependent .NET 10 Patcher Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers-optimized:executing-plans to implement this plan task-by-task. Steps use checkbox syntax for tracking.

**Goal:** Replace four self-contained patcher executables with one framework-dependent .NET 10 tool and guide players to the official runtime download when `Microsoft.NETCore.App 10.x` is unavailable.

**Architecture:** The .NET Framework 4.8 game mod detects a usable absolute `dotnet` host only after it finds methods that need repair. It passes that verified host to the existing platform wrapper, which launches one portable `KFAssemblyPatcher.dll`. A missing runtime produces a one-button client dialog that opens Microsoft's .NET 10 download page and exits; dedicated servers log the URL and exit without attempting GUI browser interaction.

**Tech Stack:** C# 9 game mod on .NET Framework 4.8, C# 9 patcher on .NET 10 LTS, xUnit, Mono.Cecil, PowerShell, POSIX shell.

**Assumptions:** Official .NET installs expose an absolute `dotnet` host through `DOTNET_ROOT*`, `PATH`, or standard install directories. Custom unregistered hosts are not discovered. Runtime installation remains a player/server-admin action; the mod does not download or execute an installer.

---

### Task 1: Testable .NET runtime discovery

> **2026-09-18 注记：** `AssemblyPatcher.Tests/` 已移出本仓库（解决方案条目也已移除）。本任务中针对该测试工程的动作
> （`Create: AssemblyPatcher.Tests/...`、`Modify: AssemblyPatcher.Tests/...`）现位于
> `_dsh-workspace\ModsRepo\AssemblyPatcher.Tests\`，不再作用于本检出。

**Files:**
- Create: `0-KFCommonUtilityLib/Harmony/DotNetRuntimeLocator.cs`
- Create: `AssemblyPatcher.Tests/DotNetRuntimeLocatorTests.cs`
- Modify: `AssemblyPatcher.Tests/KFAssemblyPatcher.Tests.csproj`
- Modify: `0-KFCommonUtilityLib/KFCommonUtilityLib.csproj`

**Security flag:** security

**Does NOT cover:** Runtime installations outside environment variables, `PATH`, and standard official installation directories.

- [x] Write tests for accepting `Microsoft.NETCore.App 10.x`, rejecting wrong/shared frameworks, candidate fallback, and diagnostic failure.
- [x] Run the tests and observe failure because the locator does not exist.
- [x] Implement candidate discovery, bounded `dotnet --list-runtimes` execution, exact major-version parsing, and absolute host validation.
- [x] Run the focused and complete patcher test suite.

### Task 2: Integrate runtime state into GameAwake and wrappers

**Files:**
- Modify: `0-KFCommonUtilityLib/Harmony/GameAssemblyCompatibilityGate.cs`
- Modify: `0-KFCommonUtilityLib/Tools/AssemblyPatcher/launch-patcher.ps1`
- Modify: `0-KFCommonUtilityLib/Tools/AssemblyPatcher/launch-patcher.sh`

**Security flag:** security

**Does NOT cover:** Automatically installing the runtime or opening a browser on dedicated servers.

- [x] Add a missing-runtime compatibility state and client/server handling.
- [x] Open the official .NET 10 download page only after client confirmation, then quit.
- [x] Pass the verified absolute host path to both wrappers and launch the portable DLL through that host.
- [x] Verify PowerShell and POSIX script syntax.

### Task 3: Migrate and publish the portable tool

**Files:**
- Modify: `AssemblyPatcher/KFAssemblyPatcher.csproj`
- Modify: `AssemblyPatcher.Tests/KFAssemblyPatcher.Tests.csproj`
- Replace generated files under: `0-KFCommonUtilityLib/Tools/AssemblyPatcher/`

**Security flag:** none

**Does NOT cover:** .NET 8 or cross-major runtime roll-forward.

- [x] Change both projects to `net10.0`; set the patcher to framework-dependent with no apphost.
- [x] Run restore, tests, and builds before replacing packaged files.
- [x] Publish one portable output and replace the four self-contained platform directories.
- [x] Verify packaged dependencies, runtimeconfig framework, and total size.

### Task 4: Documentation and focused verification

**Files:**
- Modify: `0-KFCommonUtilityLib/Documents/Specs/2026-08-30-game-assembly-virtualization-design.md`
- Modify: `0-KFCommonUtilityLib/Documents/Plans/2026-08-30-game-assembly-virtualization.md`
- Modify: `0-KFCommonUtilityLib/state.md`

**Security flag:** none

**Does NOT cover:** Starting the game, installing .NET on another host, or live client/server UI testing.

- [x] Build only the patcher, tests, and KF project; run the patcher tests.
- [x] Inspect compiled runtime detection and packaged runtime configuration.
- [x] Scan for stale self-contained/RID references, stubs, and invalid diffs.
- [x] Update execution evidence and Git staging without committing.
