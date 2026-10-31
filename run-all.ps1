# run-all.ps1
param(
    [switch]$NoBackend   # use this flag if you only want the frontend
)

$repoRoot = $PSScriptRoot

# ------------------------------------------------------------------
function Start-Backend {
    Write-Host "`n=== Starting Backend API ===`n" -ForegroundColor Cyan

    # Run the API as a background PowerShell job.
    # Using absolute paths avoids any working‑directory confusion.
    $apiJob = Start-Job -ArgumentList $repoRoot -ScriptBlock {
        param([string]$root)
        # Switch to the repository root (important for relative paths inside the API)
        Set-Location $root
        # The `dotnet run` process writes its own console output; we keep it alive.
        dotnet run --project (Join-Path $root 'backend\EthioServe.API\EthioServe.API.csproj')
    } -Name BackendJob

    # Give the API a moment to start before the frontend begins proxying.
    Write-Host "Waiting a few seconds for the API to become healthy…" -ForegroundColor DarkGray
    Start-Sleep -Seconds 3
}
# ------------------------------------------------------------------
function Start-Frontend {
    Write-Host "`n=== Starting Frontend (ng serve) ===`n" -ForegroundColor Cyan
    Push-Location (Join-Path $repoRoot 'frontend\ethioserve')
    npm start
    Pop-Location
}
# ------------------------------------------------------------------

if (-not $NoBackend) {
    Start-Backend
}
Start-Frontend
