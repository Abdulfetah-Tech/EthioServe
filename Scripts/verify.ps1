$root = Split-Path -Parent $PSScriptRoot

function Step([string]$name, [string]$folder, [scriptblock]$command) {
    Write-Host "`n=== $name ===" -ForegroundColor Cyan
    Push-Location (Join-Path $root $folder)
    try {
        $output = & $command 2>&1
        $code = $LASTEXITCODE
    }
    finally {
        Pop-Location
    }

    if ($code -eq 0) {
        $output | Select-String 'Test summary|Build succeeded|Application bundle generation complete|Tests  ' | ForEach-Object { $_.Line }
    }
    else {
        $output | Select-String 'error|FAIL|Test summary|failed' | Select-Object -First 15 | ForEach-Object { $_.Line }
        Write-Host "`n$name FAILED. Do not commit." -ForegroundColor Red
        exit 1
    }
}

Step 'Backend build'  'backend'              { dotnet build --nologo -v quiet }
Step 'Backend tests'  'backend'              { dotnet test --nologo --no-build }
Step 'Frontend build' 'frontend\ethioserve'  { npm run build }
Step 'Frontend tests' 'frontend\ethioserve'  { npm test -- --no-watch }

Write-Host "`nALL CHECKS PASSED. Safe to commit." -ForegroundColor Green