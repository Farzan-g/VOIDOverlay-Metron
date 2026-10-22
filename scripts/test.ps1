$ErrorActionPreference = "Stop"
$repoRoot = Split-Path -Parent $PSScriptRoot
$project = Join-Path $repoRoot "tests\VOIDOverlay.Metron.Tests\VOIDOverlay.Metron.Tests.csproj"

if (-not (Get-Command dotnet -ErrorAction SilentlyContinue)) {
    throw ".NET SDK is required. Install the .NET 10 SDK and retry."
}

Push-Location $repoRoot
try {
    dotnet test $project --configuration Release
    if ($LASTEXITCODE -ne 0) {
        throw "Tests failed with exit code $LASTEXITCODE."
    }
}
finally {
    Pop-Location
}
