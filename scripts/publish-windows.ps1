# 发布 Windows 绿色版 EXE（自包含 .NET 与 Windows App SDK，目标机器无需预装任何运行时）
param(
    [switch]$Zip
)

. (Join-Path $PSScriptRoot 'dev-env.ps1')

$root = Split-Path -Parent $PSScriptRoot
$project = Join-Path $root 'Sudoku.App\Sudoku.App.csproj'
$tfm = 'net10.0-windows10.0.19041.0'
$out = Join-Path $root 'publish\windows'

if (Test-Path $out) {
    Remove-Item $out -Recurse -Force
}

& dotnet publish $project -f $tfm -c Release -o $out --nologo
if ($LASTEXITCODE -ne 0) {
    throw '发布失败，请查看上方错误信息。'
}

# 去掉调试符号，减小体积
Get-ChildItem $out -Filter '*.pdb' -ErrorAction SilentlyContinue | Remove-Item -Force

$size = [math]::Round(((Get-ChildItem $out -Recurse -File | Measure-Object Length -Sum).Sum / 1MB), 1)
Write-Host "发布完成：$out\Sudoku.exe（目录共 $size MB）" -ForegroundColor Green

if ($Zip) {
    $zip = Join-Path $root 'publish\Sudoku-win-x64.zip'
    if (Test-Path $zip) {
        Remove-Item $zip -Force
    }

    Compress-Archive -Path (Join-Path $out '*') -DestinationPath $zip -Force
    Write-Host "已打包：$zip" -ForegroundColor Green
}
