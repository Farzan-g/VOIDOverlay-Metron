$ErrorActionPreference = "Stop"
$repoRoot = Split-Path -Parent $PSScriptRoot
$project = Join-Path $repoRoot "VOIDOverlay.Metron.sln"

if (-not (Get-Command dotnet -ErrorAction SilentlyContinue)) {
    throw ".NET SDK is required. Install the .NET 10 SDK and retry."
}

Push-Location $repoRoot
try {
    & (Join-Path $PSScriptRoot "convert-icon.ps1")
    dotnet build $project --configuration Release
    if ($LASTEXITCODE -ne 0) {
        throw "Release build failed with exit code $LASTEXITCODE."
    }
}
finally {
    Pop-Location
}
