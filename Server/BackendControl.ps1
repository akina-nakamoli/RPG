# Shared by the manual controls. No startup tasks/services are installed.
function Test-RpgBackendProcess {
    param([object]$Process, [string]$ServerScript)
    if ($null -eq $Process -or $Process.Name -ne 'node.exe' -or
        [string]::IsNullOrWhiteSpace($Process.ExecutablePath) -or
        [string]::IsNullOrWhiteSpace($Process.CommandLine)) { return $false }
    $command = [regex]::Match($Process.CommandLine,
        '^\s*(?:"(?<exe>[^"]+)"|(?<exe>[^\s"]+))\s+(?:"(?<script>[^"]+)"|(?<script>[^\s"]+))\s*$',
        [System.Text.RegularExpressions.RegexOptions]::IgnoreCase)
    if (-not $command.Success) { return $false }
    # A different app may also run ./server.mjs. CIM does not tell us its working directory.
    # Never resolve another process's relative argument against this controller's own directory.
    if ($command.Groups['script'].Value -notmatch '^(?:[A-Za-z]:[\\/]|\\\\)' -or
        $command.Groups['exe'].Value -notmatch '^(?:[A-Za-z]:[\\/]|\\\\)') { return $false }
    try {
        $expected = [System.IO.Path]::GetFullPath($ServerScript)
        $actual = [System.IO.Path]::GetFullPath($command.Groups['script'].Value)
        $exe = [System.IO.Path]::GetFullPath($command.Groups['exe'].Value)
        return [string]::Equals($expected, $actual, [StringComparison]::OrdinalIgnoreCase) -and
            [string]::Equals($exe, $Process.ExecutablePath, [StringComparison]::OrdinalIgnoreCase) -and
            [System.IO.Path]::GetFileName($exe) -eq 'node.exe'
    } catch { return $false }
}

function Get-RpgBackendProcesses {
    param([string]$ServerScript)
    foreach ($candidate in (Get-CimInstance Win32_Process -Filter "Name='node.exe'" -ErrorAction Stop)) {
        if (-not (Test-RpgBackendProcess -Process $candidate -ServerScript $ServerScript)) { continue }
        # Windows can briefly retain a terminated process's CIM record; it is not a running backend.
        $liveProcess = Get-Process -Id $candidate.ProcessId -ErrorAction SilentlyContinue
        if ($null -eq $liveProcess) { continue }
        try { if (-not $liveProcess.HasExited) { Write-Output $candidate } }
        finally { $liveProcess.Dispose() }
    }
}
