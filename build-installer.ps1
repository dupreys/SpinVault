# Gera o executável e o instalador. Uso: .\build-installer.ps1 [-Version 1.2.0]
param([string]$Version = "1.2.0")
$ErrorActionPreference = "Stop"
Set-Location $PSScriptRoot

Write-Host "1/2 Publicando o aplicativo..." -ForegroundColor Cyan
if (Test-Path publish) { Remove-Item publish -Recurse -Force }
dotnet publish SpinVault.csproj -c Release -r win-x64 --self-contained true `
    -p:PublishSingleFile=true -p:IncludeNativeLibrariesForSelfExtract=true -p:Version=$Version -o publish
if ($LASTEXITCODE -ne 0) { throw "Falha no dotnet publish" }

Write-Host "2/2 Gerando o instalador..." -ForegroundColor Cyan
$iscc = @(
    "$env:ProgramFiles(x86)\Inno Setup 6\ISCC.exe",
    "$env:ProgramFiles\Inno Setup 6\ISCC.exe",
    "$env:LOCALAPPDATA\Programs\Inno Setup 6\ISCC.exe"
) | Where-Object { Test-Path $_ } | Select-Object -First 1
if (-not $iscc) { throw "Inno Setup 6 não encontrado. Instale em https://jrsoftware.org/isdl.php" }

& $iscc "/DAppVersion=$Version" "installer\SpinVault.iss"
if ($LASTEXITCODE -ne 0) { throw "Falha no Inno Setup" }

Write-Host "Pronto: dist\SpinVault-Setup-$Version.exe" -ForegroundColor Green
