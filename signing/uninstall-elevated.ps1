<#
    Run as Administrator to fully remove LockScreenWallpaper:
      - Stops the running app.
      - Deletes the installed copy in C:\Program Files\LockScreenWallpaper.
      - Removes the LockScreenWallpaper certificate from every store it was
        trusted in (LocalMachine\Root, LocalMachine\TrustedPublisher, and
        your CurrentUser\My private key).

    Does NOT delete your per-user settings/log at
    %AppData%\LockScreenWallpaper -- remove that folder yourself if you want
    a completely clean slate. If you enabled "Start with Windows" from the
    tray menu, untick it before uninstalling so the HKCU Run entry is cleaned
    up properly; otherwise remove it manually from
    HKCU\Software\Microsoft\Windows\CurrentVersion\Run.
#>

$ErrorActionPreference = 'Stop'

# See create-certificate.ps1 for why this guard is needed.
if (-not (Get-PSDrive -Name Cert -ErrorAction SilentlyContinue)) {
    New-PSDrive -Name Cert -PSProvider Certificate -Root '\' -Scope Global | Out-Null
}

if (-not ([Security.Principal.WindowsPrincipal][Security.Principal.WindowsIdentity]::GetCurrent()).IsInRole([Security.Principal.WindowsBuiltInRole]::Administrator)) {
    Write-Error "This script must be run as Administrator (right-click -> Run with PowerShell as Administrator)."
    exit 1
}

$Subject    = 'CN=LockScreenWallpaper Local Dev'
$InstallDir = Join-Path $env:ProgramFiles 'LockScreenWallpaper'

Write-Host "Stopping any running instance..."
# uiAccess-privileged processes have not reliably responded to Stop-Process
# here even when run elevated; taskkill.exe has been the reliable option.
# Routed through cmd /c so its "process not found" stderr (expected when
# nothing is running) never reaches PowerShell's error stream -- with
# $ErrorActionPreference = 'Stop', a native command's stderr can get promoted
# to a terminating error before a plain `2>$null` redirect can suppress it.
cmd.exe /c "taskkill /F /IM LockScreenWallpaper.exe >nul 2>nul"
Start-Sleep -Seconds 1

if (Test-Path $InstallDir) {
    try {
        Write-Host "Removing $InstallDir..."
        Remove-Item -Path $InstallDir -Recurse -Force
    } catch {
        Write-Error "Could not remove $InstallDir -- the app may still be running. Right-click its tray icon and choose Exit, then re-run this script. Underlying error: $_"
        exit 1
    }
} else {
    Write-Host "$InstallDir not found; nothing to remove."
}

$shortcutPath = Join-Path $env:ProgramData 'Microsoft\Windows\Start Menu\Programs\LockScreenWallpaper.lnk'
if (Test-Path $shortcutPath) {
    Write-Host "Removing Start Menu shortcut..."
    Remove-Item -Path $shortcutPath -Force
}

foreach ($storePath in 'Cert:\LocalMachine\Root', 'Cert:\LocalMachine\TrustedPublisher', 'Cert:\CurrentUser\My') {
    Get-ChildItem $storePath -ErrorAction SilentlyContinue |
        Where-Object { $_.Subject -eq $Subject } |
        ForEach-Object {
            Write-Host "Removing cert $($_.Thumbprint) from $storePath"
            Remove-Item -Path "$storePath\$($_.Thumbprint)" -Force
        }
}

Write-Host ""
Write-Host "Done. LockScreenWallpaper has been uninstalled."
Write-Host "Your settings/log at $env:AppData\LockScreenWallpaper were left in place; delete that folder manually if you want a fully clean slate."

# Explicit success exit: an earlier native command (taskkill, via cmd /c) can
# leave a non-zero value sitting in $LASTEXITCODE, which callers checking our
# exit code would otherwise mistake for this script having failed.
exit 0
