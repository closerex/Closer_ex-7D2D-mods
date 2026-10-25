param(
    [Parameter(Mandatory = $true)]
    [string]$DotNetPath,

    [Parameter(Mandatory = $true)]
    [string]$AssemblyPath,

    [Parameter(Mandatory = $true)]
    [int]$ProcessId,

    [Parameter(Mandatory = $true)]
    [string]$Request,

    [Parameter(Mandatory = $true)]
    [string]$LogPath
)

$ErrorActionPreference = 'Stop'
$scriptDirectory = [System.IO.Path]::GetDirectoryName($PSCommandPath)
$patcherPath = Join-Path $scriptDirectory 'KFAssemblyPatcher.dll'

if (-not [System.IO.Path]::IsPathRooted($DotNetPath)) {
    throw "DotNetPath must be absolute: $DotNetPath"
}

if (-not [System.IO.Path]::IsPathRooted($AssemblyPath)) {
    throw "AssemblyPath must be absolute: $AssemblyPath"
}

if (-not (Test-Path -LiteralPath $patcherPath -PathType Leaf)) {
    $message = "KF assembly patcher is missing: $patcherPath"
    [System.IO.File]::AppendAllText($LogPath, "$(Get-Date -Format o) $message$([Environment]::NewLine)")
    throw $message
}

if (-not (Test-Path -LiteralPath $DotNetPath -PathType Leaf)) {
    $message = "Verified dotnet host is missing: $DotNetPath"
    [System.IO.File]::AppendAllText($LogPath, "$(Get-Date -Format o) $message$([Environment]::NewLine)")
    throw $message
}

& $DotNetPath $patcherPath `
    --assembly $AssemblyPath `
    --pid $ProcessId `
    --request $Request `
    --log $LogPath
exit $LASTEXITCODE
