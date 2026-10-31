[Console]::OutputEncoding = [System.Text.Encoding]::UTF8
$root = Split-Path -Parent $PSScriptRoot
$esc = [char]27

$running = Get-Process -Name 'EthioServe.API' -ErrorAction SilentlyContinue
if ($running) {
    Write-Host "The API is still running (PID $($running.Id -join ', ')). Stop it first (Ctrl+C in its terminal), then run this again." -ForegroundColor Red
    exit 1
}

# A wrong file name causes dozens of confusing build errors, so it is checked first (this takes a second).
$LASTEXITCODE = 0
& (Join-Path $PSScriptRoot 'check-files.ps1')
if ($LASTEXITCODE -ne 0) {
    Write-Host "`nFix the file names above first, then run this again." -ForegroundColor Red
    exit 1
}

$LASTEXITCODE = 0
& (Join-Path $PSScriptRoot 'check-backend-files.ps1')
if ($LASTEXITCODE -ne 0) {
    Write-Host "`nFix the backend file names above first, then run this again." -ForegroundColor Red
    exit 1
}
function Show-Problems([string[]]$lines) {
    $shown = 0
    for ($i = 0; $i -lt $lines.Count -and $shown -lt 8; $i++) {
        if ($lines[$i] -cmatch '^\s+Failed ' -or $lines[$i] -match ': error ' -or $lines[$i] -match '\[ERROR\]' -or $lines[$i] -cmatch '^\s*(FAIL|x |Error:|AssertionError)' -or $lines[$i] -match '\u00D7') {
            $end = [Math]::Min($i + 6, $lines.Count - 1)
            $lines[$i..$end]
            ''
            $shown++
            $i = $end
        }
    }
}

function Step([string]$name, [string]$folder, [scriptblock]$command) {
    Write-Host "`n=== $name ===" -ForegroundColor Cyan
    Push-Location (Join-Path $root $folder)
    try {
        $output = & $command 2>&1 |
            ForEach-Object { $_.ToString() -replace "$esc\[[0-9;]*[A-Za-z]", '' } |
            Where-Object { $_ -ne 'System.Management.Automation.RemoteException' }
        $code = $LASTEXITCODE
    }
    finally {
        Pop-Location
    }

    $log = Join-Path $env:TEMP ("ethioserve-" + ($name -replace '\s', '-') + ".log")
    $output | Set-Content $log

    if ($code -eq 0) {
        $output | Select-String -CaseSensitive 'Passed!|Build succeeded|Application bundle generation complete|Test Files|^\s*Tests\s' | ForEach-Object { $_.Line.Trim() }
    }
    else {
        Show-Problems $output
        $output | Select-String -CaseSensitive 'Passed!|Failed!|Test Files|^\s*Tests\s' | ForEach-Object { $_.Line.Trim() }
        Write-Host "`n$name FAILED. Do not commit. Full log: $log" -ForegroundColor Red
        exit 1
    }
}

Step 'Backend build'  'backend'              { dotnet build --nologo -v quiet }
Step 'Backend tests'  'backend'              { $env:VSTEST_CONNECTION_TIMEOUT = "300000"; dotnet test --nologo --no-build }
Step 'Frontend build' 'frontend\ethioserve'  { npm run build }
Step 'Frontend tests' 'frontend\ethioserve'  { npm test -- --no-watch }

Write-Host "`nALL CHECKS PASSED. Safe to commit." -ForegroundColor Green

