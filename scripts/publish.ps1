$ErrorActionPreference = "Stop"
$repoRoot = Split-Path -Parent $PSScriptRoot
$project = Join-Path $repoRoot "src\VOIDOverlay.Metron\VOIDOverlay.Metron.csproj"
$output = Join-Path $repoRoot "artifacts\publish"

if (-not (Get-Command dotnet -ErrorAction SilentlyContinue)) {
    throw ".NET SDK is required. Install the .NET 10 SDK and retry."
}

if (Test-Path $output) {
    Remove-Item -LiteralPath $output -Recurse -Force
}

Push-Location $repoRoot
try {
    & (Join-Path $PSScriptRoot "convert-icon.ps1")
    dotnet publish $project --configuration Release --runtime win-x64 --no-self-contained `
        -p:PublishSingleFile=true -p:DebugType=None -p:DebugSymbols=false --output $output
    if ($LASTEXITCODE -ne 0) {
        throw "Windows x64 publish failed with exit code $LASTEXITCODE."
    }
}
finally {
    Pop-Location
}
