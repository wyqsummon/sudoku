# 把 APK 安装到已连接的 Android 手机 / 模拟器，并启动应用
param(
    [string]$Apk,
    [string]$SdkDirectory,
    [switch]$Uninstall
)

$ErrorActionPreference = 'Stop'
$root = Split-Path -Parent $PSScriptRoot

# 定位 adb
if (-not $SdkDirectory) {
    $candidates = @('D:\AndroidSdk', (Join-Path $env:LOCALAPPDATA 'Android\Sdk'), (Join-Path $root '.android-sdk'))
    foreach ($c in $candidates) {
        if (Test-Path (Join-Path $c 'platform-tools\adb.exe')) { $SdkDirectory = $c; break }
    }
}
if (-not $SdkDirectory) { throw '未找到 Android SDK（platform-tools\adb.exe）' }
$adb = Join-Path $SdkDirectory 'platform-tools\adb.exe'

# 定位 APK
if (-not $Apk) {
    $Apk = Get-ChildItem (Join-Path $root 'publish\android') -Filter '*.apk' -ErrorAction SilentlyContinue |
        Sort-Object LastWriteTime -Descending | Select-Object -First 1 -ExpandProperty FullName
}
if (-not $Apk -or -not (Test-Path $Apk)) { throw '未找到 APK，请先运行 scripts\build-android.ps1' }
Write-Host "APK：$Apk" -ForegroundColor Cyan

& $adb start-server | Out-Null
$devices = & $adb devices | Select-Object -Skip 1 | Where-Object { $_ -match '\tdevice$' }
if (-not $devices) {
    throw '没有检测到设备。请用 USB 连接手机并打开「USB 调试」，或先启动模拟器。'
}
Write-Host "检测到设备：$($devices -join ', ')" -ForegroundColor Green

$package = 'com.sudoku.app'
if ($Uninstall) {
    & $adb uninstall $package
}

Write-Host '正在安装（首次安装请在手机上允许"安装未知应用"）...' -ForegroundColor Cyan
& $adb install -r $Apk
if ($LASTEXITCODE -ne 0) { throw '安装失败，请查看上方 adb 输出。' }

Write-Host '安装成功，正在启动应用...' -ForegroundColor Green
& $adb shell monkey -p $package -c android.intent.category.LAUNCHER 1 | Out-Null
Write-Host '已启动。可用 adb logcat 查看日志：' -ForegroundColor Green
Write-Host "  & '$adb' logcat -s DOTNET:V mono-stdout:V"
