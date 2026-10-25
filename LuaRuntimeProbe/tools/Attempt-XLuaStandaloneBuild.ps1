<#
.SYNOPSIS
    Bounded, reproducible attempt to build xLua's managed runtime STANDALONE
    (i.e. as a library drop-in, the way MoonSharp and NLua are consumed).

.DESCRIPTION
    This script exists so the "xLua is not integrated" claim in the mod's
    console output is backed by a re-runnable experiment rather than an
    assertion.  It:

      1. downloads the Tencent/xLua master branch source archive (if missing);
      2. generates a throw-away SDK-style csproj that compiles
         Assets/XLua/Src/*.cs (top level only - the Editor/ and TemplateEngine/
         subtrees are excluded, exactly as a library consumer would have to);
      3. references the GAME's UnityEngine / Assembly-CSharp assemblies plus
         Mono.Cecil and 0Harmony from the local 7 Days to Die install;
      4. runs `dotnet build` and reports the first errors verbatim.

    Everything happens under %TEMP%\xlua-probe; the repository is not modified.

.NOTES
    Expected outcome: the build FAILS.  The failure list is the evidence.
#>
[CmdletBinding()]
param(
    [string]$WorkDir = (Join-Path $env:TEMP 'xlua-probe'),
    [string]$GameRoot = 'E:\SteamLibrary\steamapps\common\7 Days To Die',
    [string]$SourceRef = 'v2.1.16_ndkr21b_with_win',
    [string]$SourceUrl = ''
)

$ErrorActionPreference = 'Continue'

function Write-Section([string]$Text) {
    Write-Output ''
    Write-Output ('=' * 78)
    Write-Output $Text
    Write-Output ('=' * 78)
}

New-Item -ItemType Directory -Force -Path $WorkDir | Out-Null

# ---------------------------------------------------------------- 1. sources
# The revision matters: the managed sources must match the native library's xLua
# C API.  Default is the tag whose release bundle supplied the Windows binaries.
if ([string]::IsNullOrEmpty($SourceUrl)) {
    if ($SourceRef -eq 'master') {
        $SourceUrl = 'https://github.com/Tencent/xLua/archive/refs/heads/master.zip'
    }
    else {
        $SourceUrl = "https://github.com/Tencent/xLua/archive/refs/tags/$SourceRef.zip"
    }
}

$safeRef = $SourceRef -replace '[^\w\.\-]', '_'
$zip = Join-Path $WorkDir "xlua-src-$safeRef.zip"
$srcRoot = Join-Path $WorkDir "src-$safeRef"
if (-not (Test-Path (Join-Path $srcRoot 'xLua-*'))) {
    if (-not (Test-Path $zip)) {
        Write-Section "Downloading xLua $SourceRef from $SourceUrl"
        try {
            Invoke-WebRequest -Uri $SourceUrl -OutFile $zip -TimeoutSec 300 -UseBasicParsing
        }
        catch {
            Write-Output "DOWNLOAD FAILED: $($_.Exception.Message)"
            exit 3
        }
    }
    Expand-Archive -Path $zip -DestinationPath $srcRoot -Force
}

$checkout = Get-ChildItem -Directory $srcRoot -ErrorAction SilentlyContinue |
    Where-Object { $_.Name -like 'xLua-*' } | Select-Object -First 1
if ($checkout -eq $null) {
    Write-Output "Could not locate the extracted xLua checkout under $srcRoot"
    exit 3
}

Write-Output ("source revision : {0} ({1})" -f $SourceRef, $checkout.Name)
$xLuaSrc = Join-Path $checkout.FullName 'Assets\XLua\Src'
if (-not (Test-Path $xLuaSrc)) {
    Write-Output "xLua source tree not found at $xLuaSrc"
    exit 3
}

$sourceFiles = Get-ChildItem -File -Filter '*.cs' $xLuaSrc
Write-Section 'xLua managed runtime sources found (top level of Assets/XLua/Src)'
Write-Output ("count = {0}" -f $sourceFiles.Count)
$sourceFiles | ForEach-Object { Write-Output ('  ' + $_.Name + '  ' + $_.Length) }

$editorFiles = Get-ChildItem -Recurse -File -Filter '*.cs' (Join-Path $xLuaSrc 'Editor') -ErrorAction SilentlyContinue
Write-Output ''
Write-Output ("Editor-only sources excluded from a library build: {0}" -f $editorFiles.Count)
$editorFiles | ForEach-Object { Write-Output ('  Editor\' + $_.Name) }

$templateFiles = Get-ChildItem -File -Filter '*.cs' (Join-Path $xLuaSrc 'TemplateEngine') -ErrorAction SilentlyContinue
Write-Output ''
Write-Output ("TemplateEngine sources REQUIRED at runtime by LuaEnv.cs line 103 (unconditional call to TemplateEngine.LuaTemplate.OpenLib): {0}" -f $templateFiles.Count)
$templateFiles | ForEach-Object { Write-Output ('  TemplateEngine\' + $_.Name) }

# ---------------------------------------------------- 2. generated files check
Write-Section 'Are DelegateBridge.cs / GenericDelegateBridge.cs real sources or generated stubs?'
foreach ($name in @('DelegateBridge.cs', 'GenericDelegateBridge.cs')) {
    $path = Join-Path $xLuaSrc $name
    if (Test-Path $path) {
        $head = Get-Content $path -TotalCount 24
        Write-Output ''
        Write-Output "--- $name (first 24 lines) ---"
        $head | ForEach-Object { Write-Output ('  ' + $_) }
    }
    else {
        Write-Output "$name : MISSING"
    }
}

# ------------------------------------------------------------- 3. build project
$buildDir = Join-Path $WorkDir 'build'
New-Item -ItemType Directory -Force -Path $buildDir | Out-Null

$managed = Join-Path $GameRoot '7DaysToDie_Data\Managed'
$harmonyDir = Join-Path $GameRoot 'Mods\0_TFP_Harmony'

$csproj = @"
<?xml version="1.0" encoding="utf-8"?>
<Project Sdk="Microsoft.NET.Sdk">
  <PropertyGroup>
    <TargetFramework>net48</TargetFramework>
    <LangVersion>9.0</LangVersion>
    <!--
      Deliberately NOT "XLua": Windows file lookup is case-insensitive, so a managed
      XLua.dll next to the native xlua.dll would be found by [DllImport("xlua")]
      instead of the native module (see docs/xlua-assessment.md, hazard 1).
      The collision is reproduced on purpose by
      Attempt-XLuaRuntimeSmoke.ps1 with -AssemblyName XLua.
    -->
    <AssemblyName>XLuaManaged</AssemblyName>
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
    <Reference Include="UnityEngine"><HintPath>$managed\UnityEngine.dll</HintPath><Private>false</Private></Reference>
    <Reference Include="UnityEngine.CoreModule"><HintPath>$managed\UnityEngine.CoreModule.dll</HintPath><Private>false</Private></Reference>
    <Reference Include="Assembly-CSharp"><HintPath>$managed\Assembly-CSharp.dll</HintPath><Private>false</Private></Reference>
    <Reference Include="Mono.Cecil"><HintPath>$harmonyDir\Mono.Cecil.dll</HintPath><Private>false</Private></Reference>
    <Reference Include="0Harmony"><HintPath>$harmonyDir\0Harmony.dll</HintPath><Private>false</Private></Reference>
  </ItemGroup>
</Project>
"@

$csprojPath = Join-Path $buildDir 'XLuaStandaloneAttempt.csproj'
Set-Content -Path $csprojPath -Value $csproj -Encoding UTF8
Write-Section "Generated throw-away project: $csprojPath"
Write-Output 'NOTE: this script builds the managed half as XLuaManaged.dll (no name collision).'
Write-Output '      The collision itself is reproduced by Attempt-XLuaRuntimeSmoke.ps1 -AssemblyName XLua.'

# ------------------------------------------------------------------ 4. build
Write-Section 'dotnet build (bounded to 180s)'
$logPath = Join-Path $buildDir 'build.log'
$errPath = Join-Path $buildDir 'build.err'

$proc = Start-Process -FilePath 'dotnet' `
    -ArgumentList @('build', $csprojPath, '-c', 'Release', '-v', 'q', '--nologo') `
    -WorkingDirectory $buildDir -NoNewWindow -PassThru `
    -RedirectStandardOutput $logPath -RedirectStandardError $errPath

$finished = $proc.WaitForExit(180000)
if (-not $finished) {
    $proc.Kill($true)
    Write-Output 'BUILD TIMED OUT after 180s (recorded as a separate outcome)'
    $exitCode = 'timeout'
}
else {
    $exitCode = $proc.ExitCode
}

Write-Output "exit code = $exitCode"
Write-Output ''
Write-Output '--- stdout ---'
if (Test-Path $logPath) { Get-Content $logPath | Select-Object -First 200 | ForEach-Object { Write-Output $_ } }
Write-Output ''
Write-Output '--- stderr ---'
if (Test-Path $errPath) { Get-Content $errPath | Select-Object -First 80 | ForEach-Object { Write-Output $_ } }

Write-Section 'Distinct error codes observed'
if (Test-Path $logPath) {
    Get-Content $logPath |
        Select-String -Pattern 'error [A-Z]{2}\d+' -AllMatches |
        ForEach-Object { $_.Matches } |
        ForEach-Object { $_.Value } |
        Group-Object |
        Sort-Object Count -Descending |
        ForEach-Object { Write-Output ("  {0}  x{1}" -f $_.Name, $_.Count) }
}

Write-Section 'Missing symbols reported by the compiler'
if (Test-Path $logPath) {
    Get-Content $logPath |
        Select-String -Pattern "error CS(0103|0246|0234)" |
        ForEach-Object { $_.Line.Trim() } |
        Select-Object -First 30 |
        ForEach-Object { Write-Output ('  ' + $_) }
}

exit 0
