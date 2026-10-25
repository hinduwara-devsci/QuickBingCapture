# Builds QuickBingCapture and packages the installer.
# Requirements: .NET SDK (you already have it) + Inno Setup 6  ->  winget install JRSoftware.InnoSetup
$ErrorActionPreference = "Stop"
Set-Location $PSScriptRoot

Write-Host "== Building app ==" -ForegroundColor Cyan
if (Test-Path .\publish) { Remove-Item .\publish -Recurse -Force }
dotnet publish .\QuickBingCapture\QuickBingCapture.csproj -c Release -o .\publish
if ($LASTEXITCODE -ne 0) { throw "dotnet publish failed" }

$iscc = @(
  "$env:LOCALAPPDATA\Programs\Inno Setup 6\ISCC.exe",
  "${env:ProgramFiles(x86)}\Inno Setup 6\ISCC.exe",
  "$env:ProgramFiles\Inno Setup 6\ISCC.exe"
) | Where-Object { Test-Path $_ } | Select-Object -First 1

if (-not $iscc) {
  Write-Host "Inno Setup 6 was not found. Install it with:  winget install JRSoftware.InnoSetup" -ForegroundColor Yellow
  Write-Host "The portable app is already in .\publish\QuickBingCapture.exe"
  exit 1
}

Write-Host "== Building installer ==" -ForegroundColor Cyan
& $iscc .\Installer\QuickBingCapture.iss
if ($LASTEXITCODE -ne 0) { throw "Inno Setup failed" }

$setup = Get-ChildItem .\dist\*.exe | Select-Object -First 1
Write-Host ("Done: {0}  ({1:N2} MB)" -f $setup.FullName, ($setup.Length / 1MB)) -ForegroundColor Green
