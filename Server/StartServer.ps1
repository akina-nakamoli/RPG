$ErrorActionPreference = 'Stop'
. (Join-Path $PSScriptRoot 'BackendControl.ps1')
$serverScript = [System.IO.Path]::GetFullPath((Join-Path $PSScriptRoot 'server.mjs'))
# Hold a project-specific lock while the foreground backend runs, preventing two simultaneous starts.
$hashProvider = [System.Security.Cryptography.SHA256]::Create()
try { $lockHash = [BitConverter]::ToString($hashProvider.ComputeHash([Text.Encoding]::UTF8.GetBytes($serverScript.ToLowerInvariant()))).Replace('-', '') }
finally { $hashProvider.Dispose() }
$startupLock = New-Object System.Threading.Mutex($false, ('Local\RpgManualBackend_' + $lockHash))
$lockHeld = $false
try {
    try { $lockHeld = $startupLock.WaitOne(0) }
    catch [System.Threading.AbandonedMutexException] { $lockHeld = $true }
    if (-not $lockHeld) { Write-Host 'Backend already has an open manual controller. No duplicate started.'; return }
    $existing = @(Get-RpgBackendProcesses -ServerScript $serverScript)
    if ($existing.Count -gt 0) {
        Write-Host ('This project backend is already running (PID: ' + ($existing.ProcessId -join ', ') + '). Use StopServer.cmd before restarting.')
        return
    }
    $apiPort = 5080
    if ($env:RPG_API_PORT -and (-not [int]::TryParse($env:RPG_API_PORT, [ref]$apiPort) -or $apiPort -lt 1 -or $apiPort -gt 65535)) {
        throw 'Invalid RPG_API_PORT.'
    }
    $listeners = [System.Net.NetworkInformation.IPGlobalProperties]::GetIPGlobalProperties().GetActiveTcpListeners()
    if (@($listeners | Where-Object { $_.Port -eq $apiPort }).Count -gt 0) {
        throw "Port $apiPort is occupied. Nothing was stopped or replaced."
    }
$nodeCommand = Get-Command node -ErrorAction SilentlyContinue
if ($nodeCommand) {
    $nodePath = $nodeCommand.Source
} else {
    $profileDirectory = [Environment]::GetFolderPath('UserProfile')
    $nodePath = Join-Path $profileDirectory '.cache\codex-runtimes\codex-primary-runtime\dependencies\node\bin\node.exe'
}
if (-not (Test-Path -LiteralPath $nodePath)) { throw 'Install Node.js 20+ or add node.exe to PATH.' }
if (-not (Test-Path -LiteralPath (Join-Path $PSScriptRoot 'node_modules\mongodb'))) {
    throw 'MongoDB driver missing. Run npm install in Server first.'
}
Write-Host 'MANUAL backend startup. Wait for RPG API ready below.'
Write-Host 'Keep this window open. Ctrl+C or StopServer.cmd stops this project backend.'
Write-Host "Health check: http://127.0.0.1:$apiPort/health"
& $nodePath $serverScript
Write-Host "Backend stopped (exit code $LASTEXITCODE). Startup errors, if any, are shown above."
} finally {
    if ($lockHeld) { $startupLock.ReleaseMutex() }
    $startupLock.Dispose()
}
