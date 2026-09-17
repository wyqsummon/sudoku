# 安装项目内工具链：.NET 10 SDK + MAUI workload（约 2~4GB 下载）
# 适用于在新机器上复现本项目；无需管理员权限。
$ErrorActionPreference = 'Stop'

$root = Split-Path -Parent $PSScriptRoot
$toolchain = Join-Path $root '.toolchain'
New-Item -ItemType Directory -Force -Path $toolchain | Out-Null

$installer = Join-Path $toolchain 'dotnet-install.ps1'
if (-not (Test-Path $installer)) {
    Write-Host '下载 dotnet-install.ps1 ...' -ForegroundColor Cyan
    Invoke-WebRequest -Uri 'https://dot.net/v1/dotnet-install.ps1' -OutFile $installer -UseBasicParsing
}

$sdkDir = Join-Path $toolchain 'dotnet'
Write-Host "安装 .NET 10 SDK 到 $sdkDir ..." -ForegroundColor Cyan
& $installer -Channel 10.0 -InstallDir $sdkDir -NoPath

. (Join-Path $PSScriptRoot 'dev-env.ps1')

Write-Host '安装 MAUI workload（较大，请耐心等待）...' -ForegroundColor Cyan
& dotnet workload install maui

Write-Host '当前 SDK 版本：' -NoNewline
& dotnet --version
& dotnet workload list
Write-Host '工具链安装完成 ✅' -ForegroundColor Green
