$ProfilesRoot = "$env:APPDATA\Antigravity-Profiles"

for ($i = 1; $i -le 3; $i++) {
    $accDir = Join-Path $ProfilesRoot "Account$i"
    $storagePath = Join-Path $accDir "app_storage.json"
    $tokenPath = Join-Path $accDir "Home\.gemini\jetski-standalone-oauth-token"
    $hasToken = (Test-Path $tokenPath) -and ((Get-Item $tokenPath).Length -gt 10)

    Write-Host "Checking Account $i (Token exists: $hasToken)..."

    if (Test-Path $storagePath) {
        $raw = [System.IO.File]::ReadAllText($storagePath).TrimStart([char]0xFEFF)
        $json = $raw | ConvertFrom-Json
        $dict = [ordered]@{}
        foreach ($prop in $json.PSObject.Properties) {
            if (-not $hasToken -and ($prop.Name -eq "jetski.onboarding.lastLoginUsername" -or $prop.Name -eq "jetski.onboarding.lastLoginIsGcpTos" -or $prop.Name -eq "agy.onboarding.lastLoginUsername" -or $prop.Name -eq "agy.onboarding.lastLoginIsGcpTos")) {
                Write-Host "  [-] Stripped stale login credential: $($prop.Name)" -ForegroundColor Yellow
                continue
            }
            $dict[$prop.Name] = $prop.Value
        }
        $out = $dict | ConvertTo-Json -Depth 10
        [System.IO.File]::WriteAllText($storagePath, $out, (New-Object System.Text.UTF8Encoding($false)))
        Write-Host "  [+] Account $i app_storage.json verified and saved without BOM." -ForegroundColor Green
    }
}
