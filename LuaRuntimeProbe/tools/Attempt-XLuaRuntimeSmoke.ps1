<#
.SYNOPSIS
    Bounded xLua RUNTIME smoke test.

.DESCRIPTION
    Follows up on Attempt-XLuaStandaloneBuild.ps1 (which shows the managed half
    compiles) by trying to actually RUN xLua outside Unity:

      1. builds XLua.dll from Assets/XLua/Src/*.cs + TemplateEngine/*.cs;
      2. extracts the native Plugins/x86_64/xlua.dll from the Tencent/xLua
         release bundle (v2.1.16_ndkr21b_with_win);
      3. compiles and runs a tiny console program that side-loads xlua.dll,
         constructs XLua.LuaEnv, evaluates Lua 5.4 code, exposes a CLR object
         and calls it with both `obj.M()` and `obj:M()`.

    Nothing here touches the game; everything happens under %TEMP%\xlua-probe.
    Outcomes are reported verbatim - including "it crashed", which is a result.
#>
[CmdletBinding()]
param(
    [string]$WorkDir = (Join-Path $env:TEMP 'xlua-probe'),
    [string]$GameRoot = 'E:\SteamLibrary\steamapps\common\7 Days To Die',
    [string]$SourceUrl = 'https://github.com/Tencent/xLua/archive/refs/heads/master.zip',
    [string]$SourceRef = 'master',
    [string]$NativeUrl = 'https://github.com/Tencent/xLua/releases/download/v2.1.16_ndkr21b_with_win/lua54_v2.1.16_ndkr21b_with_win.tgz',
    # 'XLua' reproduces hazard 1 (case-insensitive collision with native xlua.dll);
    # 'XLuaManaged' is the corrected, collision-free name.
    [string]$ManagedAssemblyName = 'XLuaManaged'
)

$ErrorActionPreference = 'Continue'

function Write-Section([string]$Text) {
    Write-Output ''
    Write-Output ('=' * 78)
    Write-Output $Text
    Write-Output ('=' * 78)
}

New-Item -ItemType Directory -Force -Path $WorkDir | Out-Null

# ------------------------------------------------------------------ sources
# The managed sources and the native library must come from a MATCHING xLua
# revision: the master sources call native exports (xlua_get_lib_version) that
# the prebuilt 2.1.16 bundle does not provide.  -SourceRef selects the revision.
$srcRoot = Join-Path $WorkDir ("src-" + ($SourceRef -replace '[^\w\.\-]', '_'))
if (-not (Test-Path $srcRoot)) {
    $zip = Join-Path $WorkDir ("xlua-src-" + ($SourceRef -replace '[^\w\.\-]', '_') + ".zip")
    if (-not (Test-Path $zip)) {
        if ($SourceRef -eq 'master') {
            $url = 'https://github.com/Tencent/xLua/archive/refs/heads/master.zip'
        }
        else {
            $url = "https://github.com/Tencent/xLua/archive/refs/tags/$SourceRef.zip"
        }
        Write-Section "Downloading xLua sources ($SourceRef) from $url"
        try {
            Invoke-WebRequest -Uri $url -OutFile $zip -TimeoutSec 300 -UseBasicParsing
        }
        catch {
            Write-Output "DOWNLOAD FAILED: $($_.Exception.Message)"
            exit 3
        }
    }
    Expand-Archive -Path $zip -DestinationPath $srcRoot -Force
}

$checkout = Get-ChildItem -Directory $srcRoot | Where-Object { $_.Name -like 'xLua-*' } | Select-Object -First 1
if ($checkout -eq $null) {
    Write-Output "Could not locate the extracted xLua checkout under $srcRoot"
    exit 3
}
$xLuaSrc = Join-Path $checkout.FullName 'Assets\XLua\Src'
Write-Output ("  source revision : {0} ({1})" -f $SourceRef, $checkout.Name)
if (-not (Test-Path $xLuaSrc)) {
    Write-Output "xLua source tree missing at $xLuaSrc"
    exit 3
}

# ----------------------------------------------------------- native payload
$nativeTgz = Join-Path $WorkDir 'lua54_win.tgz'
$nativeDir = Join-Path $WorkDir 'native'
if (-not (Test-Path (Join-Path $nativeDir 'Plugins\x86_64\xlua.dll'))) {
    if (-not (Test-Path $nativeTgz)) {
        Write-Section "Downloading native bundle $NativeUrl"
        try {
            Invoke-WebRequest -Uri $NativeUrl -OutFile $nativeTgz -TimeoutSec 300 -UseBasicParsing
        }
        catch {
            Write-Output "DOWNLOAD FAILED: $($_.Exception.Message)"
            exit 3
        }
    }
    New-Item -ItemType Directory -Force -Path $nativeDir | Out-Null
    tar -xzf $nativeTgz -C $nativeDir
}

$x64Native = Join-Path $nativeDir 'Plugins\x86_64\xlua.dll'
Write-Section 'Native payload'
if (Test-Path $x64Native) {
    $item = Get-Item $x64Native
    Write-Output ("  xlua.dll (win-x64) = {0} bytes, sha256={1}" -f $item.Length, (Get-FileHash $x64Native -Algorithm SHA256).Hash)
}
else {
    Write-Output "  xlua.dll (win-x64) MISSING"
    exit 3
}

# ------------------------------------------------------- managed XLua build
$managedDir = Join-Path $WorkDir ("managed-" + ($SourceRef -replace '[^\w\.\-]', '_') + "-" + $ManagedAssemblyName)
New-Item -ItemType Directory -Force -Path $managedDir | Out-Null

# NOTE: the managed assembly is deliberately NOT called XLua.dll.
# Windows file lookup is case-insensitive, so a managed XLua.dll sitting next to
# the executable shadows the native xlua.dll for [DllImport("xlua")]: the CLR
# loads the managed assembly as a module, finds no export table, and reports
#   EntryPointNotFoundException: cannot find 'xlua_get_lib_version' in 'xlua'
# even though GetProcAddress on the preloaded native module resolves it.
# Keeping the file names distinct removes the ambiguity entirely.
$csproj = @"
<?xml version="1.0" encoding="utf-8"?>
<Project Sdk="Microsoft.NET.Sdk">
  <PropertyGroup>
    <TargetFramework>net48</TargetFramework>
    <LangVersion>9.0</LangVersion>
    <AssemblyName>$ManagedAssemblyName</AssemblyName>
    <RootNamespace>XLua</RootNamespace>
    <EnableDefaultCompileItems>false</EnableDefaultCompileItems>
    <GenerateAssemblyInfo>true</GenerateAssemblyInfo>
    <NoWarn>`$(NoWarn);CS0168;CS0219;CS0414;CS0649</NoWarn>
  </PropertyGroup>
  <ItemGroup>
    <Compile Include="$xLuaSrc\*.cs" />
    <Compile Include="$xLuaSrc\TemplateEngine\*.cs" />
  </ItemGroup>
  <ItemGroup>
    <Reference Include="UnityEngine"><HintPath>$GameRoot\7DaysToDie_Data\Managed\UnityEngine.dll</HintPath><Private>false</Private></Reference>
    <Reference Include="UnityEngine.CoreModule"><HintPath>$GameRoot\7DaysToDie_Data\Managed\UnityEngine.CoreModule.dll</HintPath><Private>false</Private></Reference>
    <Reference Include="Mono.Cecil"><HintPath>$GameRoot\Mods\0_TFP_Harmony\Mono.Cecil.dll</HintPath><Private>false</Private></Reference>
  </ItemGroup>
</Project>
"@

$csprojPath = Join-Path $managedDir 'XLuaManaged.csproj'
Set-Content -Path $csprojPath -Value $csproj -Encoding UTF8

Write-Section 'Building managed XLua.dll'
$buildLog = Join-Path $managedDir 'build.log'
$proc = Start-Process -FilePath 'dotnet' -ArgumentList @('build', $csprojPath, '-c', 'Release', '-v', 'q', '--nologo') `
    -WorkingDirectory $managedDir -NoNewWindow -PassThru `
    -RedirectStandardOutput $buildLog -RedirectStandardError (Join-Path $managedDir 'build.err')
if (-not $proc.WaitForExit(180000)) { $proc.Kill($true); Write-Output 'BUILD TIMEOUT'; exit 4 }
Write-Output ("  build exit code = {0}" -f $proc.ExitCode)
if ($proc.ExitCode -ne 0) {
    Get-Content $buildLog | Select-Object -First 60 | ForEach-Object { Write-Output ('  ' + $_) }
    exit 4
}

$xLuaDll = Join-Path $managedDir ("bin\Release\net48\" + $ManagedAssemblyName + ".dll")
Write-Output ("  {0} = {1} bytes" -f $ManagedAssemblyName, (Get-Item $xLuaDll).Length)

# ------------------------------------------------------------ smoke program
$smokeDir = Join-Path $WorkDir ("smoke-" + ($SourceRef -replace '[^\w\.\-]', '_'))
New-Item -ItemType Directory -Force -Path $smokeDir | Out-Null

$program = @'
using System;
using System.IO;
using System.Runtime.InteropServices;
using XLua;

internal static class Smoke
{
    [DllImport("kernel32.dll", SetLastError = true, CharSet = CharSet.Unicode)]
    private static extern IntPtr LoadLibraryW(string path);

    [DllImport("kernel32.dll", SetLastError = true, CharSet = CharSet.Ansi, BestFitMapping = false)]
    private static extern IntPtr GetProcAddress(IntPtr module, string name);

    private static int Main()
    {
        string native = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "native", "xlua.dll");
        Console.WriteLine("[smoke] native exists   : " + File.Exists(native));
        IntPtr h = LoadLibraryW(native);
        Console.WriteLine("[smoke] LoadLibraryW    : handle=0x" + h.ToInt64().ToString("X") + " lastError=" + Marshal.GetLastWin32Error());
        if (h == IntPtr.Zero) { Console.WriteLine("[smoke] RESULT: native load failed"); return 2; }

        // Export-table diagnostics: the failure mode is "entry point not found",
        // so resolve a stock Lua symbol and an xLua-specific symbol by name.
        ProbeExport(h, "luaL_newstate");
        ProbeExport(h, "lua_gettop");
        ProbeExport(h, "xlua_get_lib_version");
        ProbeExport(h, "xlua_setglobal");

        try
        {
            using (var env = new LuaEnv())
            {
                Console.WriteLine("[smoke] LuaEnv created");

                object[] v = env.DoString("return _VERSION, 1 + 1, tostring(7 // 2), math.type(7)", "smoke_version");
                Console.WriteLine("[smoke] version/arith : " + Join(v));

                object[] n = env.DoString("local t = { a = 1, b = { c = 42 } }; return t.b.c", "smoke_nested");
                Console.WriteLine("[smoke] nested table  : " + Join(n));

                var bridge = new SmokeBridge();
                env.Global.Set("probe", bridge);

                object[] b = env.DoString(
                    "local dot_ok, dot_err = pcall(function() probe.Emit(1) end)\n" +
                    "local colon_ok, colon_err = pcall(function() probe:Emit(2) end)\n" +
                    "return type(probe), type(probe.Emit), tostring(dot_ok), tostring(colon_ok), probe:Version()",
                    "smoke_bridge");
                Console.WriteLine("[smoke] bridge        : " + Join(b));
                Console.WriteLine("[smoke] emitSum       : " + bridge.EmitSum);

                object[] c = env.DoString("return 'lua-ok'", "smoke_final");
                Console.WriteLine("[smoke] final         : " + Join(c));
            }

            Console.WriteLine("[smoke] LuaEnv disposed");
            Console.WriteLine("[smoke] RESULT: xLua ran outside Unity");
            return 0;
        }
        catch (Exception e)
        {
            Console.WriteLine("[smoke] EXCEPTION " + e.GetType().FullName + ": " + e.Message);
            Console.WriteLine(e.StackTrace);
            Console.WriteLine("[smoke] RESULT: xLua failed at runtime");
            return 1;
        }
    }

    private static void ProbeExport(IntPtr module, string name)
    {
        IntPtr p = GetProcAddress(module, name);
        int err = Marshal.GetLastWin32Error();
        Console.WriteLine("[smoke] GetProcAddress " + name.PadRight(24) + " -> "
                          + (p == IntPtr.Zero ? "NOT FOUND" : "0x" + p.ToInt64().ToString("X"))
                          + " (lastError=" + err + ")");
    }

    private static string Join(object[] values)
    {
        if (values == null) { return "<null>"; }
        var parts = new string[values.Length];
        for (int i = 0; i < values.Length; i++) { parts[i] = values[i] == null ? "nil" : values[i].ToString(); }
        return string.Join(" | ", parts);
    }
}

public class SmokeBridge
{
    public int EmitSum;
    public void Emit(int id) { EmitSum += id; }
    public string Version() { return "smoke/1.0"; }
}
'@

Set-Content -Path (Join-Path $smokeDir 'Program.cs') -Value $program -Encoding UTF8

$smokeCsproj = @"
<?xml version="1.0" encoding="utf-8"?>
<Project Sdk="Microsoft.NET.Sdk">
  <PropertyGroup>
    <OutputType>Exe</OutputType>
    <TargetFramework>net48</TargetFramework>
    <LangVersion>9.0</LangVersion>
    <AssemblyName>XLuaSmoke</AssemblyName>
    <EnableDefaultCompileItems>false</EnableDefaultCompileItems>
    <PlatformTarget>x64</PlatformTarget>
    <NoWarn>`$(NoWarn);CS0168</NoWarn>
  </PropertyGroup>
  <ItemGroup>
    <Compile Include="Program.cs" />
  </ItemGroup>
  <ItemGroup>
    <Reference Include="$ManagedAssemblyName"><HintPath>$xLuaDll</HintPath><Private>true</Private></Reference>
    <Reference Include="UnityEngine"><HintPath>$GameRoot\7DaysToDie_Data\Managed\UnityEngine.dll</HintPath><Private>true</Private></Reference>
    <Reference Include="UnityEngine.CoreModule"><HintPath>$GameRoot\7DaysToDie_Data\Managed\UnityEngine.CoreModule.dll</HintPath><Private>true</Private></Reference>
  </ItemGroup>
  <Target Name="StageNative" AfterTargets="Build">
    <ItemGroup>
      <NativeXlua Include="$x64Native" />
    </ItemGroup>
    <Copy SourceFiles="@(NativeXlua)" DestinationFolder="`$(TargetDir)native" />
  </Target>
</Project>
"@

$smokeCsprojPath = Join-Path $smokeDir 'XLuaSmoke.csproj'
Set-Content -Path $smokeCsprojPath -Value $smokeCsproj -Encoding UTF8

Write-Section 'Building smoke test'
$smokeBuildLog = Join-Path $smokeDir 'build.log'
$proc2 = Start-Process -FilePath 'dotnet' -ArgumentList @('build', $smokeCsprojPath, '-c', 'Release', '-v', 'q', '--nologo') `
    -WorkingDirectory $smokeDir -NoNewWindow -PassThru `
    -RedirectStandardOutput $smokeBuildLog -RedirectStandardError (Join-Path $smokeDir 'build.err')
if (-not $proc2.WaitForExit(180000)) { $proc2.Kill($true); Write-Output 'SMOKE BUILD TIMEOUT'; exit 4 }
Write-Output ("  build exit code = {0}" -f $proc2.ExitCode)
if ($proc2.ExitCode -ne 0) {
    Get-Content $smokeBuildLog | Select-Object -First 60 | ForEach-Object { Write-Output ('  ' + $_) }
    exit 4
}

# -------------------------------------------------------------- run the smoke
Write-Section 'Running smoke test (bounded to 60s)'
$exe = Join-Path $smokeDir 'bin\Release\net48\XLuaSmoke.exe'
$runLog = Join-Path $smokeDir 'run.log'
$proc3 = Start-Process -FilePath $exe -WorkingDirectory (Join-Path $smokeDir 'bin\Release\net48') `
    -NoNewWindow -PassThru -RedirectStandardOutput $runLog -RedirectStandardError (Join-Path $smokeDir 'run.err')

if (-not $proc3.WaitForExit(60000)) {
    $proc3.Kill($true)
    Write-Output 'RUNTIME TIMEOUT - xlua.dll did not return within 60s (recorded as a hang)'
}
else {
    Write-Output ("  process exit code = {0}" -f $proc3.ExitCode)
}

Write-Output ''
Write-Output '--- smoke stdout ---'
if (Test-Path $runLog) { Get-Content $runLog | ForEach-Object { Write-Output $_ } }
Write-Output '--- smoke stderr ---'
if (Test-Path (Join-Path $smokeDir 'run.err')) { Get-Content (Join-Path $smokeDir 'run.err') | Select-Object -First 40 | ForEach-Object { Write-Output $_ } }

exit 0
