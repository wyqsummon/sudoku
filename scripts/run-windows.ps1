# 构建并启动 Windows 版（绿色版，非打包）
param(
    [switch]$NoBuild
)

. (Join-Path $PSScriptRoot 'dev-env.ps1')

$root = Split-Path -Parent $PSScriptRoot
$project = Join-Path $root 'Sudoku.App\Sudoku.App.csproj'
$tfm = 'net10.0-windows10.0.19041.0'

if (-not $NoBuild) {
    & dotnet build $project -f $tfm -c Debug --nologo
    if ($LASTEXITCODE -ne 0) {
        throw '构建失败，请查看上方错误信息。'
    }
}

$exe = Join-Path $root "Sudoku.App\bin\Debug\$tfm\win-x64\Sudoku.exe"
if (-not (Test-Path $exe)) {
    throw "未找到可执行文件：$exe"
}

Write-Host "启动：$exe" -ForegroundColor Green
Start-Process -FilePath $exe
