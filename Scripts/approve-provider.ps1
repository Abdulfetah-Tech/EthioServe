param(
    [Parameter(Mandatory = $true)][string]$Name,
    [ValidateSet('approve', 'reject', 'suspend', 'reinstate')][string]$Action = 'approve',
    [string]$Reason = 'Reviewed during development'
)

$root = Split-Path -Parent $PSScriptRoot
$base = 'https://localhost:7080'

Push-Location (Join-Path $root 'backend')
$secrets = dotnet user-secrets list --project EthioServe.API
Pop-Location

function Get-Secret([string]$key) {
    $line = $secrets | Where-Object { $_.StartsWith("$key =") } | Select-Object -First 1
    if (-not $line) { throw "Secret $key is not set." }
    return $line.Substring($line.IndexOf('=') + 1).Trim()
}

$loginFile = Join-Path $env:TEMP 'ethioserve-admin-login.json'
@{ email = (Get-Secret 'Seed:AdminEmail'); password = (Get-Secret 'Seed:AdminPassword') } | ConvertTo-Json | Set-Content -Encoding ascii $loginFile
$login = curl.exe -s -X POST "$base/api/auth/login" -H "Content-Type: application/json" --data-binary "@$loginFile" | ConvertFrom-Json
Remove-Item $loginFile
if (-not $login.accessToken) { throw 'Admin sign-in failed. Is the API running?' }
$auth = "Authorization: Bearer $($login.accessToken)"

$list = curl.exe -s -H $auth "$base/api/admin/providers?search=$([uri]::EscapeDataString($Name))&pageSize=20" | ConvertFrom-Json
$match = @(@($list.items) | Where-Object { $_.businessName -like "*$Name*" })
if ($match.Count -ne 1) { throw "Expected exactly one provider matching '$Name' but found $($match.Count)." }
$provider = $match[0]

$url = "$base/api/admin/providers/$($provider.id)/$Action"
if ($Action -eq 'reject' -or $Action -eq 'suspend') {
    $bodyFile = Join-Path $env:TEMP 'ethioserve-reason.json'
    @{ reason = $Reason } | ConvertTo-Json | Set-Content -Encoding ascii $bodyFile
    $raw = curl.exe -s -X POST -H $auth -H "Content-Type: application/json" --data-binary "@$bodyFile" $url
    Remove-Item $bodyFile
}
else {
    $raw = curl.exe -s -X POST -H $auth $url
}

$result = $raw | ConvertFrom-Json
if ($result.profile) {
    Write-Host "$($provider.businessName): now $($result.profile.verificationStatus)" -ForegroundColor Green
}
else {
    Write-Host "Could not $Action '$($provider.businessName)': $($result.detail)" -ForegroundColor Red
}