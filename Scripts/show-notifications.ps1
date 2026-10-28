param(
    [Parameter(Mandatory = $true)][string]$Email,
    [switch]$UnreadOnly
)

$root = Split-Path -Parent $PSScriptRoot
$base = 'https://localhost:7080'

Push-Location (Join-Path $root 'backend')
$secrets = dotnet user-secrets list --project EthioServe.API
Pop-Location

$line = $secrets | Where-Object { $_.StartsWith('Seed:DemoPassword =') } | Select-Object -First 1
if (-not $line) { throw 'Secret Seed:DemoPassword is not set.' }
$password = $line.Substring($line.IndexOf('=') + 1).Trim()

$loginFile = Join-Path $env:TEMP 'ethioserve-notify-login.json'
@{ email = $Email; password = $password } | ConvertTo-Json | Set-Content -Encoding ascii $loginFile
$login = curl.exe -s -X POST "$base/api/auth/login" -H "Content-Type: application/json" --data-binary "@$loginFile" | ConvertFrom-Json
Remove-Item $loginFile
if (-not $login.accessToken) { throw 'Sign-in failed. Is the API running, and is the email right?' }

$auth = "Authorization: Bearer $($login.accessToken)"
$query = '?pageSize=20'
if ($UnreadOnly) { $query = '?unreadOnly=true&pageSize=20' }

$result = curl.exe -s -H $auth "$base/api/notifications$query" | ConvertFrom-Json
$unread = curl.exe -s -H $auth "$base/api/notifications/unread-count" | ConvertFrom-Json

Write-Host "$Email : $($unread.count) unread, $($result.totalCount) in this list" -ForegroundColor Cyan
foreach ($n in $result.items) {
    $mark = ' '
    if (-not $n.isRead) { $mark = '*' }
    Write-Host "$mark [$($n.type)] $($n.title)" -ForegroundColor Green
    Write-Host "    $($n.message)"
}
