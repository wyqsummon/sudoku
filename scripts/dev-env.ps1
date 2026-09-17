# 启用项目内自带的 .NET SDK（无需管理员权限，不污染系统环境）
# 用法： . .\scripts\dev-env.ps1
$ErrorActionPreference = 'Stop'

$root = Split-Path -Parent $PSScriptRoot
$dotnetRoot = Join-Path $root '.toolchain\dotnet'

if (-not (Test-Path (Join-Path $dotnetRoot 'dotnet.exe'))) {
    throw "未找到项目内 SDK：$dotnetRoot`n请先运行： pwsh -File scripts\install-toolchain.ps1"
}

$env:DOTNET_ROOT = $dotnetRoot
$env:PATH = "$dotnetRoot;$env:PATH"
$env:DOTNET_NOLOGO = '1'
$env:DOTNET_CLI_TELEMETRY_OPTOUT = '1'
$env:DOTNET_SKIP_FIRST_TIME_EXPERIENCE = '1'

Write-Host "已启用项目内 .NET SDK：$dotnetRoot" -ForegroundColor Green
