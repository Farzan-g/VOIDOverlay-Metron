$ErrorActionPreference = "Stop"
$repoRoot = Split-Path -Parent $PSScriptRoot
$publishDirectory = Join-Path $repoRoot "artifacts\publish"
$archive = Join-Path $repoRoot "artifacts\VOIDOverlay-Metron-win-x64.zip"
$runId = [Guid]::NewGuid().ToString("N")
$archiveParent = Split-Path -Parent $archive
$stagingArchive = Join-Path $archiveParent "VOIDOverlay-Metron-win-x64.staging-$runId.zip"
$backupArchive = Join-Path $archiveParent "VOIDOverlay-Metron-win-x64.backup-$runId.zip"

if (-not (Test-Path (Join-Path $publishDirectory "VOIDOverlay.Metron.exe"))) {
    & (Join-Path $PSScriptRoot "publish.ps1")
    if ($LASTEXITCODE -ne 0) {
        throw "Publishing failed; ZIP archive was not created."
    }
}

New-Item -ItemType Directory -Path (Split-Path -Parent $archive) -Force | Out-Null
try {
    Compress-Archive -Path (Join-Path $publishDirectory "*") -DestinationPath $stagingArchive -CompressionLevel Optimal
    if (Test-Path -LiteralPath $archive) {
        [System.IO.File]::Replace($stagingArchive, $archive, $backupArchive)
        Remove-Item -LiteralPath $backupArchive -Force
    }
    else {
        [System.IO.File]::Move($stagingArchive, $archive)
    }
}
finally {
    if (Test-Path -LiteralPath $stagingArchive) {
        Remove-Item -LiteralPath $stagingArchive -Force
    }
}
Write-Output "Created $archive"
