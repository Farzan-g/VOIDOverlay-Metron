$ErrorActionPreference = "Stop"
$repoRoot = Split-Path -Parent $PSScriptRoot
$publishDirectory = Join-Path $repoRoot "artifacts\publish"
$archive = Join-Path $repoRoot "artifacts\VOIDOverlay-Metron-win-x64.zip"

if (-not (Test-Path (Join-Path $publishDirectory "VOIDOverlay.Metron.exe"))) {
    & (Join-Path $PSScriptRoot "publish.ps1")
    if ($LASTEXITCODE -ne 0) {
        throw "Publishing failed; ZIP archive was not created."
    }
}

New-Item -ItemType Directory -Path (Split-Path -Parent $archive) -Force | Out-Null
if (Test-Path $archive) {
    Remove-Item -LiteralPath $archive -Force
}
Compress-Archive -Path (Join-Path $publishDirectory "*") -DestinationPath $archive -CompressionLevel Optimal
Write-Output "Created $archive"
