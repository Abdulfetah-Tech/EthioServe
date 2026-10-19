$root = Split-Path -Parent $PSScriptRoot

Push-Location $root
docker compose up -d --wait postgres | Out-Null
$port = (docker compose port postgres 5432).Split(':')[-1]
Pop-Location

Push-Location (Join-Path $root 'backend')
$current = (dotnet user-secrets list --project EthioServe.API | Where-Object { $_ -like 'ConnectionStrings:Default*' }) -replace '^ConnectionStrings:Default = ', ''
$updated = $current -replace 'Port=[^;]*', "Port=$port"
dotnet user-secrets set 'ConnectionStrings:Default' $updated --project EthioServe.API | Out-Null
Pop-Location

Write-Host "Database is on port $port and the connection string is in sync." -ForegroundColor Green