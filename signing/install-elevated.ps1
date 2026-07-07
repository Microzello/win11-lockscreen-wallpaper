<#
    Run this ONCE, as Administrator, after any rebuild+resign of the app.

    What it does (all require elevation):
      1. Trusts LockScreenWallpaper.cer machine-wide (Trusted Root CAs + Trusted
         Publishers) so the exe's Authenticode signature verifies as trusted --
         required for Windows to grant it the uiAccess z-order privilege.
      2. Copies the signed, published build into C:\Program Files\LockScreenWallpaper
         -- uiAccess also requires the exe to run from a "secure location"
         (Program Files or System32), not from a dev folder or AppData.

    Re-run this script after every `dotnet publish` + re-sign, so Program Files
    gets the freshly signed binary.
#>

$ErrorActionPreference = 'Stop'

# See create-certificate.ps1 for why this guard is needed.
if (-not (Get-PSDrive -Name Cert -ErrorAction SilentlyContinue)) {
    New-PSDrive -Name Cert -PSProvider Certificate -Root '\' -Scope Global | Out-Null
}

$scriptDir  = $PSScriptRoot
$repoRoot   = Split-Path $scriptDir -Parent
$publishDir = Join-Path $repoRoot 'src\LockScreenWallpaper\bin\Release\net8.0-windows\publish'
$cerPath    = Join-Path $scriptDir 'LockScreenWallpaper.cer'
$installDir = Join-Path $env:ProgramFiles 'LockScreenWallpaper'

if (-not ([Security.Principal.WindowsPrincipal][Security.Principal.WindowsIdentity]::GetCurrent()).IsInRole([Security.Principal.WindowsBuiltInRole]::Administrator)) {
    Write-Error "This script must be run as Administrator (right-click -> Run with PowerShell as Administrator)."
    exit 1
}

if (-not (Test-Path $cerPath)) {
    Write-Error "Certificate not found at $cerPath -- run .\create-certificate.ps1 first."
    exit 1
}

if (-not (Test-Path $publishDir)) {
    Write-Error "Publish output not found at $publishDir -- run .\publish-and-sign.ps1 first."
    exit 1
}

# Drop any previously trusted LockScreenWallpaper cert whose thumbprint no longer
# matches the current one (e.g. after regenerating with a non-exportable key), so
# stale/superseded certs don't linger in the machine trust stores.
$currentThumbprint = (Get-PfxCertificate -FilePath $cerPath).Thumbprint
foreach ($storePath in 'Cert:\LocalMachine\Root', 'Cert:\LocalMachine\TrustedPublisher') {
    Get-ChildItem $storePath |
        Where-Object { $_.Subject -eq 'CN=LockScreenWallpaper Local Dev' -and $_.Thumbprint -ne $currentThumbprint } |
        ForEach-Object {
            Write-Host "Removing superseded cert $($_.Thumbprint) from $storePath"
            Remove-Item -Path "$storePath\$($_.Thumbprint)" -Force
        }
}

Write-Host "Trusting $cerPath in LocalMachine\Root and LocalMachine\TrustedPublisher..."
Import-Certificate -FilePath $cerPath -CertStoreLocation Cert:\LocalMachine\Root | Out-Null
Import-Certificate -FilePath $cerPath -CertStoreLocation Cert:\LocalMachine\TrustedPublisher | Out-Null

Write-Host "Stopping any running instance..."
# uiAccess-privileged processes have not reliably responded to Stop-Process
# here even when run elevated; taskkill.exe has been the reliable option.
# Routed through cmd /c so its "process not found" stderr (expected when
# nothing is running) never reaches PowerShell's error stream -- with
# $ErrorActionPreference = 'Stop', a native command's stderr can get promoted
# to a terminating error before a plain `2>$null` redirect can suppress it.
cmd.exe /c "taskkill /F /IM LockScreenWallpaper.exe >nul 2>nul"
Start-Sleep -Seconds 1

Write-Host "Installing signed build to $installDir..."
New-Item -ItemType Directory -Force -Path $installDir | Out-Null
try {
    Copy-Item -Path (Join-Path $publishDir '*') -Destination $installDir -Recurse -Force
} catch {
    Write-Error "Could not copy to $installDir -- the app may still be running. Right-click its tray icon and choose Exit, then re-run this script. Underlying error: $_"
    exit 1
}

Write-Host "Creating a Start Menu shortcut..."
# So there's always an easy way to relaunch the app (Start menu search) even
# if the tray icon is ever lost for some reason, without having to remember
# or hunt down the Program Files path.
$startMenuDir = Join-Path $env:ProgramData 'Microsoft\Windows\Start Menu\Programs'
$shortcutPath = Join-Path $startMenuDir 'LockScreenWallpaper.lnk'
$wshShell = New-Object -ComObject WScript.Shell
$shortcut = $wshShell.CreateShortcut($shortcutPath)
$shortcut.TargetPath = Join-Path $installDir 'LockScreenWallpaper.exe'
$shortcut.WorkingDirectory = $installDir
$shortcut.Description = 'Lock Screen Wallpaper (multi-monitor)'
$shortcut.Save()

Write-Host ""
Write-Host "Done. Verifying signature trust from the installed copy:"
Get-AuthenticodeSignature -FilePath (Join-Path $installDir 'LockScreenWallpaper.exe') | Format-List Status, StatusMessage

Write-Host "Launch it from the Start Menu (search ""LockScreenWallpaper"") or: $installDir\LockScreenWallpaper.exe"

# Explicit success exit: an earlier native command (taskkill, via cmd /c) can
# leave a non-zero value sitting in $LASTEXITCODE, which callers checking our
# exit code would otherwise mistake for this script having failed.
exit 0
