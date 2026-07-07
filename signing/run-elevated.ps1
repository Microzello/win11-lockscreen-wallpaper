<#
    Runs another PowerShell script elevated (triggers one UAC prompt), then
    relays its output back to this console and exits with its exit code.

    Start-Process -Verb RunAs (needed to trigger the UAC prompt) doesn't
    support -RedirectStandardOutput/-RedirectStandardError directly, so the
    elevated child instead redirects its own output (via PowerShell's *>
    "all streams" operator) to a temp log file, which we then read back here
    -- otherwise a failure in the elevated script would just flash in a
    window that closes immediately, with no way to see what went wrong.

    Usage: .\run-elevated.ps1 -ScriptPath .\install-elevated.ps1
#>
param(
    [Parameter(Mandatory = $true)]
    [string]$ScriptPath
)

$ErrorActionPreference = 'Stop'

$ScriptPath = (Resolve-Path $ScriptPath).Path
$log = Join-Path $env:TEMP "LockScreenWallpaper-$([IO.Path]::GetFileNameWithoutExtension($ScriptPath)).log"
Remove-Item $log -ErrorAction SilentlyContinue

# Escape embedded single quotes for safe interpolation into a single-quoted
# PowerShell string (doubling the quote is PowerShell's own escape form).
$escapedScript = $ScriptPath.Replace("'", "''")
$escapedLog    = $log.Replace("'", "''")
$inner = "& '$escapedScript' *> '$escapedLog'; exit `$LASTEXITCODE"

$p = Start-Process powershell -ArgumentList @('-NoProfile', '-ExecutionPolicy', 'Bypass', '-Command', $inner) -Verb RunAs -Wait -PassThru

if (Test-Path $log) {
    Get-Content $log | ForEach-Object { Write-Host $_ }
}

exit $p.ExitCode
