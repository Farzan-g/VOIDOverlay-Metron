$ErrorActionPreference = "Stop"
$repoRoot = Split-Path -Parent $PSScriptRoot
$project = Join-Path $repoRoot "src\VOIDOverlay.Metron\VOIDOverlay.Metron.csproj"
$output = Join-Path $repoRoot "artifacts\publish"
$outputParent = Split-Path -Parent $output
$runId = [Guid]::NewGuid().ToString("N")
$staging = Join-Path $outputParent "publish.staging-$runId"
$backup = Join-Path $outputParent "publish.backup-$runId"
$locationPushed = $false

if (-not (Get-Command dotnet -ErrorAction SilentlyContinue)) {
    throw ".NET SDK is required. Install the .NET 10 SDK and retry."
}

New-Item -ItemType Directory -Path $outputParent -Force | Out-Null
try {
    Push-Location $repoRoot
    $locationPushed = $true
    & (Join-Path $PSScriptRoot "convert-icon.ps1")
    try {
        dotnet publish $project --configuration Release --runtime win-x64 --no-self-contained `
            -p:PublishSingleFile=true -p:DebugType=None -p:DebugSymbols=false --output $staging
        if ($LASTEXITCODE -ne 0) {
            throw "Windows x64 publish failed with exit code $LASTEXITCODE."
        }

        if (-not (Test-Path -LiteralPath (Join-Path $staging "VOIDOverlay.Metron.exe") -PathType Leaf)) {
            throw "The publish output is missing VOIDOverlay.Metron.exe."
        }

        $hadPreviousOutput = Test-Path -LiteralPath $output
        $previousOutputMoved = $false
        try {
            if ($hadPreviousOutput) {
                Move-Item -LiteralPath $output -Destination $backup
                $previousOutputMoved = $true
            }
            Move-Item -LiteralPath $staging -Destination $output
        }
        catch {
            if ((-not $hadPreviousOutput -or $previousOutputMoved) -and
                (Test-Path -LiteralPath $output)) {
                Remove-Item -LiteralPath $output -Recurse -Force
            }
            if ($previousOutputMoved -and (Test-Path -LiteralPath $backup)) {
                Move-Item -LiteralPath $backup -Destination $output
            }
            throw
        }

        if (Test-Path -LiteralPath $backup) {
            Remove-Item -LiteralPath $backup -Recurse -Force
        }
    }
    finally {
        if (Test-Path -LiteralPath $staging) {
            Remove-Item -LiteralPath $staging -Recurse -Force
        }
    }
}
finally {
    if ($locationPushed) {
        Pop-Location
    }
}

Write-Output "Framework-dependent Windows x64 publish: $output"
