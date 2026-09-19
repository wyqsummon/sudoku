# 发布 Windows 绿色版 EXE（自包含 .NET 与 Windows App SDK，目标机器无需预装任何运行时）
#
# 目录结构（顶层一眼就能看到入口）：
#   publish\windows\Sudoku.exe      ← 启动器（双击即用）
#   publish\windows\使用说明.txt
#   publish\windows\app\            ← 程序本体（真正的 exe + 全部运行库 DLL）
param(
    [switch]$Zip
)

. (Join-Path $PSScriptRoot 'dev-env.ps1')

$root = Split-Path -Parent $PSScriptRoot
$project = Join-Path $root 'Sudoku.App\Sudoku.App.csproj'
$tfm = 'net10.0-windows10.0.19041.0'
$out = Join-Path $root 'publish\windows'
$app = Join-Path $out 'app'

if (Test-Path $out) {
    Remove-Item $out -Recurse -Force
}

& dotnet publish $project -f $tfm -c Release -o $app --nologo
if ($LASTEXITCODE -ne 0) {
    throw '发布失败，请查看上方错误信息。'
}

# 去掉调试符号，减小体积
Get-ChildItem $app -Filter '*.pdb' -ErrorAction SilentlyContinue | Remove-Item -Force

# 只保留中文 / 英文的本地化资源，删掉一百多个用不到的语言目录（.mui 与 resources.dll）
$keepLanguages = @('zh-CN', 'zh-Hans', 'zh-Hant', 'zh-HK', 'zh-TW', 'en-us', 'en-GB')
$removedDirs = 0
Get-ChildItem $app -Directory | Where-Object { $keepLanguages -notcontains $_.Name } | ForEach-Object {
    $hasResources = Get-ChildItem $_.FullName -Recurse -File -Include '*.mui', '*.resources.dll' -ErrorAction SilentlyContinue
    if ($hasResources) {
        Remove-Item $_.FullName -Recurse -Force
        $removedDirs++
    }
}

# 顶层放一个极小的启动器：WinUI 的原生 DLL 必须与主 exe 同目录，
# 所以程序本体整体收进 app\，由这个启动器转发启动，顶层就只剩一个 exe。
$launcher = Join-Path $out 'Sudoku.exe'
if (Test-Path $launcher) {
    Remove-Item $launcher -Force
}

$launcherCode = @'
using System;
using System.Diagnostics;
using System.IO;
using System.Reflection;

public static class Launcher
{
    public static void Main()
    {
        string dir = Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location);
        string app = Path.Combine(dir, "app");
        ProcessStartInfo info = new ProcessStartInfo(Path.Combine(app, "Sudoku.exe"));
        info.WorkingDirectory = app;
        Process.Start(info);
    }
}
'@

Add-Type -TypeDefinition $launcherCode -OutputAssembly $launcher -OutputType WindowsApplication -ReferencedAssemblies 'System.dll'

$readme = @"
数独 · Windows 绿色版
----------------------------------
1. 双击本目录下的 Sudoku.exe 即可开始游戏（无需安装任何运行时）。
2. app 子目录是程序本体（.NET 运行时、Windows App SDK 与全部资源都在里面），
   请不要单独删除或移动里面的文件；也可以直接双击 app\Sudoku.exe 启动。
3. 拷贝给别的电脑时，请把整个 windows 文件夹一起拷贝，然后双击 Sudoku.exe。
4. 存档与设置保存在系统用户目录（AppData）里，与本目录无关，删除本目录不会丢存档。
"@

Set-Content -Path (Join-Path $out '使用说明.txt') -Value $readme -Encoding UTF8

$files = Get-ChildItem $out -Recurse -File
$size = [math]::Round((($files | Measure-Object Length -Sum).Sum / 1MB), 1)
Write-Host "发布完成：$launcher（顶层仅启动器 + 说明，程序本体在 app\；共 $($files.Count) 个文件，$size MB；已清理 $removedDirs 个多余语言目录）" -ForegroundColor Green

if ($Zip) {
    # 注意：变量名不能叫 $zip —— PowerShell 变量名大小写不敏感，会和开关参数 $Zip 冲突
    $zipPath = Join-Path $root 'publish\Sudoku-win-x64.zip'
    if (Test-Path $zipPath) {
        Remove-Item $zipPath -Force
    }

    Compress-Archive -Path (Join-Path $out '*') -DestinationPath $zipPath -Force
    $zipSize = [math]::Round((Get-Item $zipPath).Length / 1MB, 1)
    Write-Host "已打包：$zipPath（$zipSize MB）" -ForegroundColor Green
}
