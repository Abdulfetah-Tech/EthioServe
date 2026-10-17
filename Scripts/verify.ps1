$root = Split-Path -Parent $PSScriptRoot

function Show-Problems([string[]]$lines) {
    $shown = 0
    for ($i = 0; $i -lt $lines.Count -and $shown -lt 8; $i++) {
        if ($lines[$i] -match '^\s+Failed ' -or $lines[$i] -match ': error ') {
            $end = [Math]::Min($i + 7, $lines.Count - 1)
            $lines[$i..$end]
            ''
            $shown++
        }
    }
}

function Step([string]$name, [string]$folder, [scriptblock]$command) {
    Write-Host "`n=== $name ===" -ForegroundColor Cyan
    Push-Location (Join-Path $root $folder)
    try {
        $output = & $command 2>&1 | ForEach-Object { $_.ToString() }
        $code = $LASTEXITCODE
    }
    finally {
        Pop-Location
    }

    $log = Join-Path $env:TEMP ("ethioserve-" + ($name -replace '\s', '-') + ".log")
    $output | Set-Content $log

    if ($code -eq 0) {
        $output | Select-String 'Test summary|Build succeeded|Application bundle generation complete|Tests  ' | ForEach-Object { $_.Line }
    }
    else {
        Show-Problems $output
        $output | Select-String 'Test summary|Failed!' | ForEach-Object { $_.Line }
        Write-Host "`n$name FAILED. Do not commit. Full log: $log" -ForegroundColor Red
        exit 1
    }
}

Step 'Backend build'  'backend'              { dotnet build --nologo -v quiet }
Step 'Backend tests'  'backend'              { dotnet test --nologo --no-build }
Step 'Frontend build' 'frontend\ethioserve'  { npm run build }
Step 'Frontend tests' 'frontend\ethioserve'  { npm test -- --no-watch }

Write-Host "`nALL CHECKS PASSED. Safe to commit." -ForegroundColor Green