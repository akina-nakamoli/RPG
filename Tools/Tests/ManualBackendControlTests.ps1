param([switch]$Integration)
$ErrorActionPreference = 'Stop'
$projectRoot = Split-Path (Split-Path $PSScriptRoot -Parent) -Parent
$serverDir = Join-Path $projectRoot 'Server'
$serverScript = Join-Path $serverDir 'server.mjs'
. (Join-Path $serverDir 'BackendControl.ps1')
function Assert-ManualControl([bool]$Condition, [string]$Message) {
    if (-not $Condition) { throw $Message }
}
$nodePath = (Get-Command node -ErrorAction Stop).Source
$mock = [pscustomobject]@{ Name='node.exe'; ExecutablePath=$nodePath; CommandLine=('"' + $nodePath + '" "' + $serverScript + '"') }
Assert-ManualControl (Test-RpgBackendProcess $mock $serverScript) 'Quoted project command must match.'
$mock.CommandLine = $nodePath + ' "' + $serverScript + '"'
Assert-ManualControl (Test-RpgBackendProcess $mock $serverScript) 'Unquoted executable with quoted script must match.'
$mock.CommandLine = '"' + $nodePath + '" "' + (Join-Path $projectRoot 'OtherProject/server.mjs') + '"'
Assert-ManualControl (-not (Test-RpgBackendProcess $mock $serverScript)) 'Must not match another project.'
$mock.CommandLine = '"' + $nodePath + '" "' + $serverScript + '" --init-db'
Assert-ManualControl (-not (Test-RpgBackendProcess $mock $serverScript)) 'Must not stop a database initialization command.'
$mock.CommandLine = '"' + $nodePath + '" "other-script.mjs" "' + $serverScript + '"'
Assert-ManualControl (-not (Test-RpgBackendProcess $mock $serverScript)) 'Script must be the executed script, not another argument.'
foreach ($relative in './server.mjs','server.mjs','D:server.mjs','\server.mjs') {
    $mock.CommandLine = '"' + $nodePath + '" "' + $relative + '"'
    Assert-ManualControl (-not (Test-RpgBackendProcess $mock $serverScript)) 'Unknown process working directories must never be inferred.'
}
$mock.CommandLine = '"' + $nodePath + '" "' + $serverScript + '"'
$mock.ExecutablePath = 'C:\Other\node.exe'
Assert-ManualControl (-not (Test-RpgBackendProcess $mock $serverScript)) 'Executable identity must match.'
$mock.ExecutablePath = $null
Assert-ManualControl (-not (Test-RpgBackendProcess $mock $serverScript)) 'Missing process identity must fail safely.'
foreach ($source in 'Assets/Scripts/Networking/DatabaseAccountApi.cs','Assets/Scripts/UI/Account/LoginSceneUI.cs','Assets/Resources/DatabaseConfig.json') {
    $content = Get-Content -LiteralPath (Join-Path $projectRoot $source) -Raw
    Assert-ManualControl ($content -notmatch 'LocalBackendLauncher|Process\.Start|autoStartLocalServer|localServerDirectory') 'Game-side autostart must be removed.'
}
Write-Output 'PASS: exact process matching, unrelated process protection, no game-side automatic startup.'
if (-not $Integration) { return }

# Explicit integration mode stops any existing project backend, tests manual controls, and LEAVES IT OFF.
# It never reads or writes account/save records and never stops MongoDB/Tailscale.
$apiPort = if ($env:RPG_API_PORT) { [int]$env:RPG_API_PORT } else { 5080 }
$mongoBefore = @(Get-NetTCPConnection -LocalPort 27017 -State Listen | Select-Object -ExpandProperty OwningProcess -Unique)
$stopScript = Join-Path $serverDir 'StopServer.ps1'
$startScript = Join-Path $serverDir 'StartServer.ps1'
& powershell.exe -NoProfile -ExecutionPolicy Bypass -File $stopScript
Assert-ManualControl ($LASTEXITCODE -eq 0) 'Initial manual stop failed.'
Assert-ManualControl (@(Get-RpgBackendProcesses $serverScript).Count -eq 0) 'Old project backend must be stopped.'
$testDir = Join-Path $projectRoot 'Temp/ManualBackendValidation'
[void](New-Item -ItemType Directory -Path $testDir -Force)
$controller = $null
$ownedBackendId = $null
try {
    # Hidden only for this non-interactive diagnostic run. Users double-click the visible cmd wrapper.
    $controller = Start-Process -FilePath 'powershell.exe' -ArgumentList @('-NoLogo','-NoProfile','-ExecutionPolicy','Bypass','-File',('"' + $startScript + '"')) -WorkingDirectory $serverDir -WindowStyle Hidden -RedirectStandardOutput (Join-Path $testDir 'start.stdout.log') -RedirectStandardError (Join-Path $testDir 'start.stderr.log') -PassThru
    $ready = $false
    for ($attempt = 0; $attempt -lt 50; $attempt++) {
        if ($controller.HasExited) { throw 'Manual start exited before API ready. Inspect Temp/ManualBackendValidation/start.stderr.log.' }
        try {
            $request = [Net.HttpWebRequest]::Create("http://127.0.0.1:$apiPort/health")
            $request.Proxy = $null; $request.Timeout = 500
            $response = $request.GetResponse()
            try {
                $reader = New-Object IO.StreamReader($response.GetResponseStream())
                try { $health = $reader.ReadToEnd() | ConvertFrom-Json } finally { $reader.Dispose() }
            } finally { $response.Dispose() }
            if ($health.ok -and $health.protocol -eq 2) { $ready = $true; break }
        } catch { }
        Start-Sleep -Milliseconds 150
    }
    Assert-ManualControl $ready 'Manually started backend must become healthy.'
    $running = @(Get-RpgBackendProcesses $serverScript)
    Assert-ManualControl ($running.Count -eq 1) 'Exactly one project backend should run.'
    $ownedBackendId = $running[0].ProcessId
    Write-Output 'PASS: manual startup and healthy API.'
    & powershell.exe -NoProfile -ExecutionPolicy Bypass -File $startScript
    Assert-ManualControl ($LASTEXITCODE -eq 0) 'Duplicate start should be a safe no-op.'
    $afterDuplicate = @(Get-RpgBackendProcesses $serverScript)
    Assert-ManualControl ($afterDuplicate.Count -eq 1 -and $afterDuplicate[0].ProcessId -eq $ownedBackendId) 'Duplicate start must retain the original process.'
    Write-Output 'PASS: duplicate manual start does not launch or replace a backend.'
    & powershell.exe -NoProfile -ExecutionPolicy Bypass -File $stopScript
    Assert-ManualControl ($LASTEXITCODE -eq 0) 'Manual stop failed.'
    $ownedBackendId = $null
    $controller.WaitForExit(10000) | Out-Null
    Assert-ManualControl $controller.HasExited 'Manual start controller should exit when its backend stops.'
    & powershell.exe -NoProfile -ExecutionPolicy Bypass -File $stopScript
    Assert-ManualControl ($LASTEXITCODE -eq 0) 'Repeated stop should be a safe no-op.'
    Start-Sleep -Milliseconds 1500
    Assert-ManualControl (@(Get-RpgBackendProcesses $serverScript).Count -eq 0) 'Project backend must stay off.'
    $listeners = [Net.NetworkInformation.IPGlobalProperties]::GetIPGlobalProperties().GetActiveTcpListeners()
    Assert-ManualControl (@($listeners | Where-Object { $_.Port -eq $apiPort }).Count -eq 0) 'API port should be closed.'
    $mongoAfter = @(Get-NetTCPConnection -LocalPort 27017 -State Listen | Select-Object -ExpandProperty OwningProcess -Unique)
    Assert-ManualControl (($mongoBefore -join ',') -eq ($mongoAfter -join ',')) 'MongoDB must not be stopped or replaced.'
    Write-Output 'PASS: manual stop, repeated stop, controller exits, API stays off, MongoDB unchanged.'
} finally {
    if ($ownedBackendId -and @(Get-RpgBackendProcesses $serverScript | Where-Object { $_.ProcessId -eq $ownedBackendId }).Count -gt 0) {
        & powershell.exe -NoProfile -ExecutionPolicy Bypass -File $stopScript
    }
    if ($controller) { $controller.Dispose() }
}
