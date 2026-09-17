# 构建 Android APK（首次会自动安装 Android SDK 依赖到项目内 .android-sdk）
param(
    [string]$SdkDirectory,
    [switch]$InstallDependencies,
    [switch]$Debug,
    [switch]$Unsigned
)

. (Join-Path $PSScriptRoot 'dev-env.ps1')

$root = Split-Path -Parent $PSScriptRoot
$project = Join-Path $root 'Sudoku.App\Sudoku.App.csproj'
$tfm = 'net10.0-android'
$configuration = if ($Debug) { 'Debug' } else { 'Release' }

# Android SDK：优先命令行参数，其次自动探测常见位置
if (-not $SdkDirectory) {
    $sdkCandidates = @(
        (Join-Path $root '.android-sdk'),
        'D:\AndroidSdk',
        (Join-Path $env:LOCALAPPDATA 'Android\Sdk'),
        'C:\Program Files (x86)\Android\android-sdk',
        'C:\Program Files\Android\android-sdk'
    )
    foreach ($c in $sdkCandidates) {
        if (Test-Path (Join-Path $c 'platforms')) { $SdkDirectory = $c; break }
    }
}
if (-not $SdkDirectory) {
    $SdkDirectory = Join-Path $root '.android-sdk'
    Write-Host '未探测到 Android SDK，将尝试安装到项目内 .android-sdk' -ForegroundColor Yellow
}
$sdk = $SdkDirectory
Write-Host "使用 Android SDK：$sdk" -ForegroundColor Cyan

# JDK（MAUI Android 构建需要 JDK 17+）
$jdk = $env:JAVA_HOME
if (-not $jdk -or -not (Test-Path (Join-Path $jdk 'bin\java.exe'))) {
    $candidates = Get-ChildItem 'C:\Program Files\Java' -Directory -ErrorAction SilentlyContinue |
        Where-Object { $_.Name -like 'jdk*' } | Sort-Object Name -Descending
    if ($candidates.Count -gt 0) { $jdk = $candidates[0].FullName }
}
if (-not $jdk) { throw '未找到 JDK，请安装 JDK 17+ 或设置 JAVA_HOME。' }
Write-Host "使用 JDK：$jdk" -ForegroundColor Cyan

$sdkReady = Test-Path (Join-Path $sdk 'platform-tools')
if ($InstallDependencies -or -not $sdkReady) {
    Write-Host "安装 Android SDK 依赖到 $sdk（首次约 1~2GB）..." -ForegroundColor Cyan
    & dotnet build $project -f $tfm -t:InstallAndroidDependencies `
        -p:AndroidSdkDirectory=$sdk `
        -p:AcceptAndroidSDKLicenses=true `
        -p:JavaSdkDirectory=$jdk `
        --nologo

    if ($LASTEXITCODE -ne 0) {
        throw 'Android SDK 安装失败，请查看上方错误信息。'
    }
}

$signing = @()
$keystore = Join-Path $root 'keystore\sudoku.keystore'
if (-not $Unsigned -and (Test-Path $keystore)) {
    Write-Host '使用 keystore\sudoku.keystore 签名' -ForegroundColor Cyan
    $signing = @(
        '-p:AndroidKeyStore=true',
        "-p:AndroidSigningKeyStore=$keystore",
        '-p:AndroidSigningKeyAlias=sudoku',
        '-p:AndroidSigningKeyPass=sudoku2026',
        '-p:AndroidSigningStorePass=sudoku2026'
    )
} else {
    Write-Host '未找到密钥库，将输出未签名/调试签名 APK' -ForegroundColor Yellow
}

& dotnet publish $project -f $tfm -c $configuration `
    -p:AndroidSdkDirectory=$sdk `
    -p:JavaSdkDirectory=$jdk `
    -p:AndroidPackageFormat=apk `
    @signing `
    --nologo

if ($LASTEXITCODE -ne 0) {
    throw 'APK 构建失败，请查看上方错误信息。'
}

$outDir = Join-Path $root 'publish\android'
New-Item -ItemType Directory -Force -Path $outDir | Out-Null

$apks = Get-ChildItem (Join-Path $root "Sudoku.App\bin\$configuration\$tfm") -Recurse -Filter '*.apk' -ErrorAction SilentlyContinue |
    Sort-Object LastWriteTime -Descending
foreach ($apk in $apks) {
    Copy-Item $apk.FullName -Destination $outDir -Force
    Write-Host "APK：$(Join-Path $outDir $apk.Name)（$([math]::Round($apk.Length / 1MB, 1)) MB）" -ForegroundColor Green
}

if ($apks.Count -eq 0) {
    Write-Host '未找到 APK 输出，请检查构建日志。' -ForegroundColor Red
}
