param(
    [string]$GameDir = 'E:\SteamLibrary\steamapps\common\Slay the Spire 2',
    [switch]$Test
)
$ErrorActionPreference = 'Stop'
$repoDir = Split-Path -Parent $PSScriptRoot
Push-Location $repoDir
try {
    $env:DOTNET_CLI_HOME = Join-Path $repoDir '.tools/dotnet-home'
    $env:DOTNET_CLI_TELEMETRY_OPTOUT = '1'
    $env:MSBuildEnableWorkloadResolver = 'false'
    dotnet build src/SpireRange/SpireRange.csproj -c Release "-p:GameDir=$GameDir" --ignore-failed-sources
    if ($LASTEXITCODE -ne 0) { throw 'Mod build failed.' }
    if ($Test) {
        dotnet run --project tests/SpireRange.Smoke/SpireRange.Smoke.csproj -c Release "-p:GameDir=$GameDir"
        if ($LASTEXITCODE -ne 0) { throw 'Save isolation smoke test failed.' }
    }
    $packageDir = Join-Path $repoDir 'artifacts/SpireRange'
    New-Item -ItemType Directory -Force $packageDir | Out-Null
    foreach ($name in @('SpireRange.dll', 'SpireRange.json')) {
        Copy-Item -LiteralPath (Join-Path $repoDir "src/SpireRange/bin/Release/net9.0/$name") -Destination $packageDir -Force
    }
    Write-Host "Mod package: $packageDir"
}
finally { Pop-Location }
