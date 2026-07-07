<#
    Run this once (no admin required). Creates the local code-signing certificate
    that lets Windows grant LockScreenWallpaper.exe the uiAccess privilege it
    needs to render above the lock screen -- see README.md, "Why signing is
    required" for the full explanation.

    The private key is generated as non-exportable, so it can never be copied
    off this machine (even by something running as you). Safe to re-run: it
    reuses the existing certificate if one is already present.
#>

$ErrorActionPreference = 'Stop'

# The Cert: PSDrive isn't always auto-mounted when a script is launched via
# `powershell -File` (e.g. from install.cmd) rather than an interactive
# session -- make sure it exists before relying on Cert:\ paths below.
if (-not (Get-PSDrive -Name Cert -ErrorAction SilentlyContinue)) {
    New-PSDrive -Name Cert -PSProvider Certificate -Root '\' -Scope Global | Out-Null
}

$Subject = 'CN=LockScreenWallpaper Local Dev'
$CerPath = Join-Path $PSScriptRoot 'LockScreenWallpaper.cer'

$cert = Get-ChildItem Cert:\CurrentUser\My | Where-Object { $_.Subject -eq $Subject } | Select-Object -First 1

if ($cert) {
    Write-Host "Certificate already exists (thumbprint $($cert.Thumbprint)); reusing it."
} else {
    Write-Host "Creating a new non-exportable code-signing certificate..."
    $cert = New-SelfSignedCertificate `
        -Type CodeSigningCert `
        -Subject $Subject `
        -CertStoreLocation Cert:\CurrentUser\My `
        -KeyUsage DigitalSignature `
        -FriendlyName 'LockScreenWallpaper Code Signing' `
        -KeyExportPolicy NonExportable `
        -NotAfter (Get-Date).AddYears(5)
    Write-Host "Created certificate with thumbprint $($cert.Thumbprint)."
}

Export-Certificate -Cert $cert -FilePath $CerPath -Force | Out-Null
Write-Host "Exported public certificate to $CerPath"
Write-Host ""
Write-Host "Next: run .\publish-and-sign.ps1, then .\install-elevated.ps1 as Administrator."
