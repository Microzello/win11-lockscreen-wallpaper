<#
    Builds a Release publish of the app and signs it with the local
    LockScreenWallpaper certificate. Run create-certificate.ps1 first if you
    haven't already.

    Re-run this after every code change, then re-run install-elevated.ps1
    (as Administrator) to push the updated build into Program Files.
#>

$ErrorActionPreference = 'Stop'

# See create-certificate.ps1 for why this guard is needed.
if (-not (Get-PSDrive -Name Cert -ErrorAction SilentlyContinue)) {
    New-PSDrive -Name Cert -PSProvider Certificate -Root '\' -Scope Global | Out-Null
}

$Subject     = 'CN=LockScreenWallpaper Local Dev'
$RepoRoot    = Split-Path $PSScriptRoot -Parent
$ProjectPath = Join-Path $RepoRoot 'src\LockScreenWallpaper\LockScreenWallpaper.csproj'
$PublishDir  = Join-Path $RepoRoot 'src\LockScreenWallpaper\bin\Release\net8.0-windows\publish'
$ExePath     = Join-Path $PublishDir 'LockScreenWallpaper.exe'

$cert = Get-ChildItem Cert:\CurrentUser\My | Where-Object { $_.Subject -eq $Subject } | Select-Object -First 1
if (-not $cert) {
    Write-Error "No signing certificate found. Run .\create-certificate.ps1 first."
    exit 1
}

Write-Host "Publishing Release build..."
dotnet publish $ProjectPath -c Release
if ($LASTEXITCODE -ne 0) {
    Write-Error "dotnet publish failed."
    exit 1
}

Write-Host "Signing $ExePath..."
$result = Set-AuthenticodeSignature -FilePath $ExePath -Certificate $cert -HashAlgorithm SHA256
Write-Host "Signature status: $($result.Status) - $($result.StatusMessage)"
Write-Host '("untrusted root" is expected until install-elevated.ps1 has been run at least once.)'
Write-Host ""
Write-Host "Next: run .\install-elevated.ps1 as Administrator."
