param(
  [string]$Version = "",
  [string]$Rid = "win-x64",
  [switch]$CreateRelease,
  [switch]$SingleFile,
  [switch]$NoSelfContained
)

$ErrorActionPreference = "Stop"

# locate the first .csproj in this folder
$csproj = Get-ChildItem -Path $PSScriptRoot -Filter *.csproj | Select-Object -First 1
if (-not $csproj) { throw "No .csproj found in $PSScriptRoot" }

$projectName = [IO.Path]::GetFileNameWithoutExtension($csproj.Name)

function Get-CsprojVersion {
  try {
    [xml]$xml = Get-Content -LiteralPath $csproj.FullName
    $v = $xml.Project.PropertyGroup.Version
    if ($v -and $v.Trim().Length -gt 0) { return $v.Trim() }
  } catch { }
  return "0.0.0"
}

if ([string]::IsNullOrWhiteSpace($Version)) {
  $v = Get-CsprojVersion
  $Version = "v$($v.TrimStart('v'))"
} else {
  $Version = "v$($Version.Trim().TrimStart('v'))"
}

$scArg = if ($NoSelfContained) { "false" } else { "true" }
$singleFileArg = if ($SingleFile) { "true" } else { "false" }

Write-Host "Project        : $projectName"
Write-Host "Version        : $Version"
Write-Host "RID            : $Rid"
Write-Host "Self-contained : $scArg"
Write-Host "SingleFile     : $singleFileArg"
Write-Host "csproj         : $($csproj.FullName)"
Write-Host ""

dotnet clean | Out-Null
dotnet restore | Out-Null

$publishArgs = @(
  "publish","-c","Release",
  "-r",$Rid,
  "--self-contained",$scArg,
  "-p:PublishSingleFile=$singleFileArg",
  "-p:IncludeNativeLibrariesForSelfExtract=true"
)
& dotnet @publishArgs | Out-Host

$publishDir = Join-Path $PSScriptRoot "bin\Release\net8.0-windows\$Rid\publish"
if (-not (Test-Path -LiteralPath $publishDir)) { throw "Publish dir not found: $publishDir" }

# ensure config.json present in publish dir
$configSrc = Join-Path $PSScriptRoot "config.json"
$configDst = Join-Path $publishDir "config.json"
if ((Test-Path -LiteralPath $configSrc) -and (-not (Test-Path -LiteralPath $configDst))) {
  Copy-Item -LiteralPath $configSrc -Destination $configDst -Force
}

# zip to releases/
$releasesDir = Join-Path $PSScriptRoot "releases"
if (-not (Test-Path -LiteralPath $releasesDir)) { New-Item -ItemType Directory -Path $releasesDir | Out-Null }

$zipName = "$projectName-$Version-$Rid.zip"
$zipPath = Join-Path $releasesDir $zipName
if (Test-Path -LiteralPath $zipPath) { Remove-Item -LiteralPath $zipPath -Force }

Compress-Archive -Path (Join-Path $publishDir "*") -DestinationPath $zipPath -Force
Write-Host "ZIP created   : $zipPath"

# optional GitHub release
if ($CreateRelease) {
  $gh = Get-Command gh -ErrorAction SilentlyContinue
  if ($gh) {
    Write-Host "Creating GitHub release $Version ..."
    & gh release create $Version $zipPath -t $Version -n "Release $Version" | Out-Host
  } else {
    Write-Host "gh CLI not found. Skipping GitHub release."
  }
}

Write-Host "Done."
