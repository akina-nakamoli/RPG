$ErrorActionPreference = 'Stop'
$tailscalePath = 'C:\Program Files\Tailscale\tailscale.exe'
if (-not (Test-Path -LiteralPath $tailscalePath)) { throw 'Install and log in to Tailscale on the host first.' }
$request = [System.Net.HttpWebRequest]::Create('http://127.0.0.1:5080/health')
$request.Proxy = $null
$request.Timeout = 5000
$response = $request.GetResponse()
try {
    $reader = New-Object System.IO.StreamReader($response.GetResponseStream())
    try { $health = $reader.ReadToEnd() | ConvertFrom-Json } finally { $reader.Dispose() }
} finally { $response.Dispose() }
if (-not $health.ok -or $health.protocol -ne 2 -or -not $health.registrationInviteRequired -or -not $health.registrationReady) {
    throw 'Do not publish: restart the updated backend and verify the host invitation configuration first.'
}
Write-Host 'Publishing ONLY the game API over public HTTPS. MongoDB and other services are not published.'
Write-Host 'If prompted, personally approve Funnel on the Tailscale web page. Never share your Tailscale account.'
& $tailscalePath funnel --bg --yes --https=443 http://127.0.0.1:5080
if ($LASTEXITCODE -ne 0) { throw 'Funnel did not finish. Check its message before distributing the game.' }
& $tailscalePath funnel status
