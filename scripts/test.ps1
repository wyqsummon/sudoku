# 运行核心单元测试
. (Join-Path $PSScriptRoot 'dev-env.ps1')

$root = Split-Path -Parent $PSScriptRoot
$project = Join-Path $root 'Sudoku.Core.Tests\Sudoku.Core.Tests.csproj'

& dotnet test $project --nologo
exit $LASTEXITCODE
