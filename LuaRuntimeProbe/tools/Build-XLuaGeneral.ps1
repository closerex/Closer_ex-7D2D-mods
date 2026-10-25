<#
.SYNOPSIS
    Builds the OFFICIAL xLua "General" (通用版本) managed runtime and stages it
    with the matching native library.

.DESCRIPTION
    xLua's own repository ships a non-Unity configuration: General/vs2013/XLua.Mini.csproj,
    which defines XLUA_GENERAL, references only System + System.Core (no UnityEngine)
    and compiles the same 23 sources the Unity build uses
    (Assets/XLua/Src/*.cs + Assets/XLua/Src/TemplateEngine/TemplateEngine.cs).

    Its README states the Unity-only removals explicitly: print redirection to the
    console and the Unity-specific script loader; "everything else is preserved".
    That is precisely the branch guarded by `#if !XLUA_GENERAL`, which is why this
    configuration can be exercised on plain .NET Framework - with NO modification to
    upstream sources, only the vendor's own compile-time switch.

    Nothing here is copied into the repository; the output goes to a staging folder.

.PARAMETER OutDir
    Staging folder. Receives XLua.Mini.dll, native\xlua.dll and build-manifest.txt.

.PARAMETER Smoke
    Additionally builds and runs a tiny console program that constructs LuaEnv on
    this machine and reports whether the General build really works offline.
#>
[CmdletBinding()]
param(
    [string]$WorkDir = (Join-Path $env:TEMP 'xlua-probe'),
    [string]$OutDir = '',
    [string]$GameRoot = 'E:\SteamLibrary\steamapps\common\7 Days To Die',
    [string]$SourceRef = 'v2.1.16_ndkr21b_with_win',
    [string]$NativeUrl = 'https://github.com/Tencent/xLua/releases/download/v2.1.16_ndkr21b_with_win/lua54_v2.1.16_ndkr21b_with_win.tgz',
    [switch]$Smoke
)

$ErrorActionPreference = 'Continue'

if ([string]::IsNullOrEmpty($OutDir)) {
    $OutDir = Join-Path $GameRoot 'ModsRepo\LuaRuntimeProbe\staging\xlua'
}

function Write-Section([string]$Text) {
    Write-Output ''
    Write-Output ('=' * 78)
    Write-Output $Text
    Write-Output ('=' * 78)
}

New-Item -ItemType Directory -Force -Path $WorkDir | Out-Null
New-Item -ItemType Directory -Force -Path $OutDir | Out-Null

# ------------------------------------------------------------------ sources
$safeRef = $SourceRef -replace '[^\w\.\-]', '_'
$srcRoot = Join-Path $WorkDir "src-$safeRef"
$checkout = if (Test-Path $srcRoot) {
    Get-ChildItem -Directory $srcRoot | Where-Object { $_.Name -like 'xLua-*' } | Select-Object -First 1
} else { $null }

if ($checkout -eq $null) {
    $zip = Join-Path $WorkDir "xlua-src-$safeRef.zip"
    if (-not (Test-Path $zip)) {
        $url = if ($SourceRef -eq 'master') {
            'https://github.com/Tencent/xLua/archive/refs/heads/master.zip'
        } else {
            "https://github.com/Tencent/xLua/archive/refs/tags/$SourceRef.zip"
        }
        Write-Section "Downloading xLua $SourceRef"
        try { Invoke-WebRequest -Uri $url -OutFile $zip -TimeoutSec 300 -UseBasicParsing }
        catch { Write-Output "DOWNLOAD FAILED: $($_.Exception.Message)"; exit 3 }
    }
    Expand-Archive -Path $zip -DestinationPath $srcRoot -Force
    $checkout = Get-ChildItem -Directory $srcRoot | Where-Object { $_.Name -like 'xLua-*' } | Select-Object -First 1
}

if ($checkout -eq $null) { Write-Output "xLua checkout not found under $srcRoot"; exit 3 }
$xLuaSrc = Join-Path $checkout.FullName 'Assets\XLua\Src'
Write-Output ("source revision : {0} ({1})" -f $SourceRef, $checkout.Name)

# ------------------------------------------------------------------ native
$nativeTgz = Join-Path $WorkDir 'lua54_win.tgz'
$nativeRoot = Join-Path $WorkDir 'native'
$x64Native = Join-Path $nativeRoot 'Plugins\x86_64\xlua.dll'
if (-not (Test-Path $x64Native)) {
    if (-not (Test-Path $nativeTgz)) {
        Write-Section 'Downloading the native xLua bundle'
        try { Invoke-WebRequest -Uri $NativeUrl -OutFile $nativeTgz -TimeoutSec 300 -UseBasicParsing }
        catch { Write-Output "DOWNLOAD FAILED: $($_.Exception.Message)"; exit 3 }
    }
    New-Item -ItemType Directory -Force -Path $nativeRoot | Out-Null
    tar -xzf $nativeTgz -C $nativeRoot
}
if (-not (Test-Path $x64Native)) { Write-Output "native xlua.dll not found at $x64Native"; exit 3 }

# ------------------------------------------- official General build (XLua.Mini)
# Mirrors General/vs2013/XLua.Mini.csproj: DefineConstants=XLUA_GENERAL,
# references System + System.Core only, same 23 sources.  TargetFramework is
# raised to net48 to match the mod; that is the only deviation from the file, and
# it does not affect the compile-time branches.
$buildDir = Join-Path $WorkDir "general-build-$safeRef"
New-Item -ItemType Directory -Force -Path $buildDir | Out-Null

$sourceFiles = Get-ChildItem -File -Filter '*.cs' $xLuaSrc | Sort-Object Name
$templateFile = Join-Path $xLuaSrc 'TemplateEngine\TemplateEngine.cs'

$compileItems = ($sourceFiles | ForEach-Object { "    <Compile Include=`"$($_.FullName)`" />" }) -join "`r`n"
$compileItems += "`r`n    <Compile Include=`"$templateFile`" />"

$csproj = @"
<?xml version="1.0" encoding="utf-8"?>
<Project Sdk="Microsoft.NET.Sdk">
  <PropertyGroup>
    <TargetFramework>net48</TargetFramework>
    <LangVersion>9.0</LangVersion>
    <!-- Official General configuration: no UnityEngine reference anywhere. -->
    <AssemblyName>XLua.Mini</AssemblyName>
    <RootNamespace>XLua</RootNamespace>
    <EnableDefaultCompileItems>false</EnableDefaultCompileItems>
    <GenerateAssemblyInfo>true</GenerateAssemblyInfo>
    <DefineConstants>`$(DefineConstants);XLUA_GENERAL</DefineConstants>
    <NoWarn>`$(NoWarn);CS0168;CS0219;CS0414;CS0649;CS0618</NoWarn>
    <ShouldIncludeNativeLua>False</ShouldIncludeNativeLua>
  </PropertyGroup>
  <ItemGroup>
    <Reference Include="System" />
    <Reference Include="System.Core" />
  </ItemGroup>
  <ItemGroup>
$compileItems
  </ItemGroup>
</Project>
"@

$csprojPath = Join-Path $buildDir 'XLua.Mini.csproj'
Set-Content -Path $csprojPath -Value $csproj -Encoding UTF8

Write-Section 'Building XLua.Mini.dll (XLUA_GENERAL, net48, no UnityEngine)'
Write-Output ("sources: {0} top-level + TemplateEngine/TemplateEngine.cs" -f $sourceFiles.Count)
$buildLog = Join-Path $buildDir 'build.log'
$proc = Start-Process -FilePath 'dotnet' -ArgumentList @('build', $csprojPath, '-c', 'Release', '-v', 'q', '--nologo') `
    -WorkingDirectory $buildDir -NoNewWindow -PassThru `
    -RedirectStandardOutput $buildLog -RedirectStandardError (Join-Path $buildDir 'build.err')
if (-not $proc.WaitForExit(240000)) { $proc.Kill($true); Write-Output 'BUILD TIMEOUT'; exit 4 }
Write-Output ("build exit code = {0}" -f $proc.ExitCode)
if ($proc.ExitCode -ne 0) {
    Get-Content $buildLog | Select-Object -First 60 | ForEach-Object { Write-Output ('  ' + $_) }
    exit 4
}

$builtDll = Join-Path $buildDir 'bin\Release\net48\XLua.Mini.dll'
if (-not (Test-Path $builtDll)) { Write-Output "expected output missing: $builtDll"; exit 4 }

# ------------------------------------------------------------------- stage
$stageNative = Join-Path $OutDir 'native'
New-Item -ItemType Directory -Force -Path $stageNative | Out-Null

Copy-Item $builtDll (Join-Path $OutDir 'XLua.Mini.dll') -Force
Copy-Item $x64Native (Join-Path $stageNative 'xlua.dll') -Force

$managedHash = (Get-FileHash (Join-Path $OutDir 'XLua.Mini.dll') -Algorithm SHA256).Hash
$nativeHash = (Get-FileHash (Join-Path $stageNative 'xlua.dll') -Algorithm SHA256).Hash

$manifest = @()
$manifest += "xLua general (XLua.Mini) staging manifest"
$manifest += "source_ref        = $SourceRef"
$manifest += "source_url        = https://github.com/Tencent/xLua"
$manifest += "configuration     = official General: DefineConstants=XLUA_GENERAL; references System+System.Core only"
$manifest += "target_framework  = net48 (upstream file targets v3.5; the define set is unchanged)"
$manifest += "managed_assembly  = XLua.Mini.dll"
$manifest += "managed_sha256    = $managedHash"
$manifest += "managed_bytes     = $((Get-Item (Join-Path $OutDir 'XLua.Mini.dll')).Length)"
$manifest += "native_library    = native\xlua.dll"
$manifest += "native_sha256     = $nativeHash"
$manifest += "native_bytes      = $((Get-Item (Join-Path $stageNative 'xlua.dll')).Length)"
$manifest += "sources           = $($sourceFiles.Count + 1) files (Assets/XLua/Src/*.cs + TemplateEngine/TemplateEngine.cs)"
$manifest += "upstream_sources_modified = no"
$manifest += ""
$manifest += "Unity-only code excluded by the upstream #if !XLUA_GENERAL guards:"
$manifest += "  LuaEnv.cs:95   print() registered through xlua_setglobal (Unity console redirect)"
$manifest += "  LuaEnv.cs:108  LoadFromResource / LoadFromStreamingAssetsPath searchers"
$manifest += "  LuaEnv.cs:164  ObjectTranslator.Alias(typeof(Type), `"System.MonoType`")"
$manifest += "  LuaEnv.cs:325  Unity-specific loader handling"
$manifest += "  LuaEnv.cs:353  Unity-specific loader handling"
$manifest += "  ObjectTranslator.cs:170, :357, StaticLuaCallbacks.cs:601, :683, GenAttributes.cs:130"
$manifest += ""
$manifest += "Full source file list:"
foreach ($f in $sourceFiles) { $manifest += "  Src/$($f.Name)" }
$manifest += "  Src/TemplateEngine/TemplateEngine.cs"
Set-Content -Path (Join-Path $OutDir 'build-manifest.txt') -Value $manifest -Encoding UTF8

Write-Section 'Staged'
Write-Output ("OutDir       : {0}" -f $OutDir)
Write-Output ("XLua.Mini.dll: {0} bytes  sha256={1}" -f (Get-Item (Join-Path $OutDir 'XLua.Mini.dll')).Length, $managedHash)
Write-Output ("native       : {0} bytes  sha256={1}" -f (Get-Item (Join-Path $stageNative 'xlua.dll')).Length, $nativeHash)
Write-Output ("manifest     : {0}" -f (Join-Path $OutDir 'build-manifest.txt'))

# ------------------------------------------------------------------- smoke
if ($Smoke) {
    $smokeDir = Join-Path $WorkDir "general-smoke-$safeRef"
    New-Item -ItemType Directory -Force -Path $smokeDir | Out-Null

    $program = @'
using System;
using System.IO;
using System.Runtime.InteropServices;
using XLua;

internal static class GeneralSmoke
{
    [DllImport("kernel32.dll", SetLastError = true, CharSet = CharSet.Unicode)]
    private static extern IntPtr LoadLibraryExW(string path, IntPtr file, uint flags);

    private static int Main()
    {
        string native = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "native", "xlua.dll");
        Console.WriteLine("[general-smoke] native exists : " + File.Exists(native));
        IntPtr h = LoadLibraryExW(native, IntPtr.Zero, 0x00000008);
        Console.WriteLine("[general-smoke] preload     : handle=0x" + h.ToInt64().ToString("X"));
        if (h == IntPtr.Zero) { Console.WriteLine("[general-smoke] RESULT: native load failed"); return 2; }

        try
        {
            using (var env = new LuaEnv())
            {
                Console.WriteLine("[general-smoke] LuaEnv constructed (no System.MonoType alias needed)");
                object[] v = env.DoString("return _VERSION, 1 + 1, tostring(7 // 2), math.type(7)", "smoke_version");
                Console.WriteLine("[general-smoke] version/arith: " + Join(v));

                object[] n = env.DoString("local t = { a = 1, b = { c = 42 } }; return t.b.c", "smoke_nested");
                Console.WriteLine("[general-smoke] nested table : " + Join(n));

                var bridge = new GeneralSmokeBridge();
                env.Global.Set("probe", bridge);

                // xLua's member resolution is measured, not assumed: the two call
                // styles are probed separately so a failure in one does not hide
                // the other.
                object[] b = env.DoString(
                    "local dot_ok, dot_err = pcall(function() probe.Emit(1) end)\n" +
                    "local colon_ok, colon_err = pcall(function() probe:Emit(2) end)\n" +
                    "local version_ok, version = pcall(function() return probe:Version() end)\n" +
                    "return type(probe), type(probe.Emit), type(probe.emit)\n" +
                    "  , 'dot=' .. tostring(dot_ok) .. '/' .. tostring(dot_err)\n" +
                    "  , 'colon=' .. tostring(colon_ok) .. '/' .. tostring(colon_err)\n" +
                    "  , 'version=' .. tostring(version_ok) .. '/' .. tostring(version)",
                    "smoke_bridge");
                Console.WriteLine("[general-smoke] bridge       : " + Join(b));
                Console.WriteLine("[general-smoke] emitSum      : " + bridge.EmitSum
                                  + " (2 means only the colon call crossed the boundary)");

                // The portable probe module must run unchanged on this backend.
                object[] p = env.DoString(
                    "local ok, err = pcall(function() probe:Emit(41) end)\n" +
                    "return tostring(ok), tostring(err)",
                    "smoke_portable");
                Console.WriteLine("[general-smoke] colon emit 41: " + Join(p) + " emitSum=" + bridge.EmitSum);
            }
            Console.WriteLine("[general-smoke] RESULT: General build works on .NET Framework");
            return 0;
        }
        catch (Exception e)
        {
            Console.WriteLine("[general-smoke] EXCEPTION " + e.GetType().FullName + ": " + e.Message);
            Console.WriteLine(e.StackTrace);
            Console.WriteLine("[general-smoke] RESULT: General build failed at runtime");
            return 1;
        }
    }

    private static string Join(object[] values)
    {
        if (values == null) { return "<null>"; }
        var parts = new string[values.Length];
        for (int i = 0; i < values.Length; i++) { parts[i] = values[i] == null ? "nil" : values[i].ToString(); }
        return string.Join(" | ", parts);
    }
}

public class GeneralSmokeBridge
{
    public int EmitSum;
    public void Emit(int id) { EmitSum += id; }
    public string Version() { return "general-smoke/1.0"; }
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
    <AssemblyName>XLuaGeneralSmoke</AssemblyName>
    <EnableDefaultCompileItems>false</EnableDefaultCompileItems>
    <PlatformTarget>x64</PlatformTarget>
    <NoWarn>`$(NoWarn);CS0168</NoWarn>
  </PropertyGroup>
  <ItemGroup>
    <Compile Include="Program.cs" />
    <Reference Include="XLua.Mini">
      <HintPath>$OutDir\XLua.Mini.dll</HintPath>
      <Private>true</Private>
    </Reference>
  </ItemGroup>
  <Target Name="StageNative" AfterTargets="Build">
    <Copy SourceFiles="$OutDir\native\xlua.dll" DestinationFolder="`$(TargetDir)native" />
  </Target>
</Project>
"@

    Set-Content -Path (Join-Path $smokeDir 'XLuaGeneralSmoke.csproj') -Value $smokeCsproj -Encoding UTF8

    Write-Section 'Building the General-build smoke test'
    $smokeLog = Join-Path $smokeDir 'build.log'
    $p2 = Start-Process -FilePath 'dotnet' -ArgumentList @('build', (Join-Path $smokeDir 'XLuaGeneralSmoke.csproj'), '-c', 'Release', '-v', 'q', '--nologo') `
        -WorkingDirectory $smokeDir -NoNewWindow -PassThru `
        -RedirectStandardOutput $smokeLog -RedirectStandardError (Join-Path $smokeDir 'build.err')
    if (-not $p2.WaitForExit(240000)) { $p2.Kill($true); Write-Output 'SMOKE BUILD TIMEOUT'; exit 4 }
    Write-Output ("build exit code = {0}" -f $p2.ExitCode)
    if ($p2.ExitCode -ne 0) { Get-Content $smokeLog | Select-Object -First 40 | ForEach-Object { Write-Output ('  ' + $_) }; exit 4 }

    Write-Section 'Running the General-build smoke test (bounded to 60s)'
    $exe = Join-Path $smokeDir 'bin\Release\net48\XLuaGeneralSmoke.exe'
    $runLog = Join-Path $smokeDir 'run.log'
    $p3 = Start-Process -FilePath $exe -WorkingDirectory (Split-Path $exe) -NoNewWindow -PassThru `
        -RedirectStandardOutput $runLog -RedirectStandardError (Join-Path $smokeDir 'run.err')

    $smokeFailed = $false
    if (-not $p3.WaitForExit(60000)) {
        $p3.Kill($true)
        Write-Output 'SMOKE TIMEOUT'
        $smokeFailed = $true
    }
    else {
        Write-Output ("process exit code = {0}" -f $p3.ExitCode)
        if ($p3.ExitCode -ne 0) { $smokeFailed = $true }
    }

    Write-Output ''
    Get-Content $runLog | ForEach-Object { Write-Output $_ }
    Write-Output ''
    Write-Output ("smoke log: {0}" -f $runLog)

    if ($smokeFailed) {
        # A smoke failure must NOT be reported as success: this script's exit code is
        # what CI/reviewers read.
        Write-Output 'SMOKE FAILED - see the output above'
        exit 5
    }

    if (-not (Select-String -Path $runLog -Pattern 'RESULT: General build works' -Quiet)) {
        Write-Output 'SMOKE FAILED - the exit code was 0 but the success marker is missing from the output'
        exit 5
    }
}

exit 0
