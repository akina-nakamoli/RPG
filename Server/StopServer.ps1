$ErrorActionPreference = 'Stop'
. (Join-Path $PSScriptRoot 'BackendControl.ps1')
$serverScript = [System.IO.Path]::GetFullPath((Join-Path $PSScriptRoot 'server.mjs'))
$backends = @(Get-RpgBackendProcesses -ServerScript $serverScript)
if ($backends.Count -eq 0) { Write-Host 'This project backend is already stopped.'; return }
foreach ($backend in $backends) {
    # Re-read before stopping: do not kill a different process if a PID was reused.
    $confirmed = Get-CimInstance Win32_Process -Filter ('ProcessId=' + $backend.ProcessId)
    if ($null -eq $confirmed) { continue }
    if ($confirmed.CreationDate -ne $backend.CreationDate -or
        -not (Test-RpgBackendProcess -Process $confirmed -ServerScript $serverScript)) {
        throw 'Process identity changed. Nothing else was stopped.'
    }
    Stop-Process -Id $confirmed.ProcessId -ErrorAction Stop
    Wait-Process -Id $confirmed.ProcessId -Timeout 10 -ErrorAction SilentlyContinue
    $remaining = Get-CimInstance Win32_Process -Filter ('ProcessId=' + $confirmed.ProcessId)
    if ($remaining -and $remaining.CreationDate -eq $confirmed.CreationDate) { throw 'Backend did not stop.' }
    Write-Host ('Stopped project backend PID ' + $confirmed.ProcessId + '.')
}
Write-Host 'MongoDB, Tailscale, other Node programs, accounts and saves were not stopped or removed.'
