# Execution State

Plan: `Documents/Plans/2026-08-30-framework-dependent-dotnet10-patcher.md`

Status: Complete. No game/runtime launch was performed.

Known facts:

- The physical current `Assembly-CSharp.dll` exposes both targets as public, nonvirtual, nonfinal methods.
- `ItemActionRanged.TryExecuteAction` has owned direct `call` sites in `ExecuteAction` and `OnHoldingUpdate`.
- `MinEventActionModifyStats.execute` has owned direct `call` sites in `Execute` and nested coroutine state-machine `MoveNext`.
- `ModEvents.GameAwake` runs after XUi creation and window parsing in the current game.
- The working tree contains many user changes; no worktree and no commits are authorized for this task.
- The patcher now targets framework-dependent `net10.0` with no apphost and no RID-specific publication.
- Runtime discovery runs only when a target method needs repair. It requires an absolute host exposing `Microsoft.NETCore.App 10.x` and searches `DOTNET_ROOT*`, `PATH`, and official default locations.
- Missing runtime behavior is client confirmation -> official Microsoft download page -> quit; on a dedicated server the URL is logged and shown in the console confirmation dialog, and no browser is opened automatically.
- The packaged tool directory contains one portable DLL, runtimeconfig, deps, Mono.Cecil, and two wrappers. The four former self-contained platform directories were removed.

Verification evidence:

> **2026-09-18 — `AssemblyPatcher.Tests/` 已移出本仓库**（连同其解决方案条目），现存于工作区
> `E:\SteamLibrary\steamapps\common\7 Days To Die\_dsh-workspace\ModsRepo\AssemblyPatcher.Tests\`。
> 下面第一条命令因此在当前检出中无法直接运行；记录保留为当时的验证结果，未作改写。

- `dotnet test AssemblyPatcher.Tests/KFAssemblyPatcher.Tests.csproj -c Release --no-restore --nologo`: 17 passed, 0 failed on `.NETCoreApp,Version=v10.0`.
- `dotnet build 0-KFCommonUtilityLib/KFCommonUtilityLib.csproj -c Debug --no-restore --nologo`: exit 0, 0 errors, 51 pre-existing warnings.
- `dotnet publish AssemblyPatcher/KFAssemblyPatcher.csproj -c Release --self-contained false -p:UseAppHost=false`: exit 0; packaged runtimeconfig requests `Microsoft.NETCore.App 10.0.0`.
- Packaged tool size is 391,125 bytes, down from 276,331,286 bytes; no RID-specific runtime directories remain.
- PowerShell parser and `bash -n`: both wrapper scripts passed syntax verification.
- Direct host-load check through `C:\Program Files\dotnet\dotnet.exe KFAssemblyPatcher.dll` reached the patcher and returned its expected missing-arguments exit code 1.
- ILSpy inspection of the rebuilt KF DLL confirmed that runtime discovery happens after the virtual-method check, client URL opening is limited to confirmed runtime-missing state, dedicated servers show a WinForms Yes/No confirmation and only quit on Yes (falling back to log-and-quit if the dialog cannot be shown), and the verified absolute host is passed to wrappers.
- Cecil inspection of the built KF DLL: 6 restored `MethodTargetTranspiler` methods and 0 old global Harmony patch types.
- Windows wrapper integration against a copy of the current real `Assembly-CSharp.dll` in its Managed directory: 2 methods virtualized, 4 owned calls changed to `callvirt`, SHA-256 backup matched the untouched official DLL, and all integration artifacts were removed.
- Real-copy integration exposed and fixed two issues before completion: ambiguous `Split-Path` parameter binding in PowerShell and missing Cecil resolver search path during full assembly write.
- Stub scan and `git diff --check`: clean, aside from Git's existing LF-to-CRLF notices.
