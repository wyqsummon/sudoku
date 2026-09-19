# 构建 Android APK（首次会自动安装 Android SDK 依赖到项目内 .android-sdk）
#
# 签名口令不写死在脚本里（本仓库是公开的），按下面的优先级取：
#   1. 命令行参数  -KeyPass / -StorePass / -KeyAlias / -KeystorePath
#   2. 环境变量    SUDOKU_KEY_PASS / SUDOKU_STORE_PASS / SUDOKU_KEY_ALIAS / SUDOKU_KEYSTORE
#   3. 本地私有文件 scripts\local-signing.ps1（已被 .gitignore 忽略，里面 set 上面几个环境变量）
# 一个都没给、但密钥库存在时会明确警告并改为产出未签名 APK（不会静默签错）。
param(
    [string]$SdkDirectory,
    [switch]$InstallDependencies,
    [switch]$Debug,
    [switch]$Unsigned,
    [string]$KeystorePath,
    [string]$KeyAlias,
    [string]$KeyPass,
    [string]$StorePass
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

# ---------------------------------------------------------------------------
# 签名：口令只从参数 / 环境变量（含本地私有文件）取，绝不写死在脚本里
# ---------------------------------------------------------------------------
$localSigning = Join-Path $PSScriptRoot 'local-signing.ps1'
if (Test-Path $localSigning) {
    . $localSigning   # 本机私有配置（已在 .gitignore 中），只负责设置环境变量
}

if (-not $KeystorePath) {
    $KeystorePath = if ($env:SUDOKU_KEYSTORE) { $env:SUDOKU_KEYSTORE } else { Join-Path $root 'keystore\sudoku.keystore' }
}
if (-not $KeyAlias) { $KeyAlias = if ($env:SUDOKU_KEY_ALIAS) { $env:SUDOKU_KEY_ALIAS } else { 'sudoku' } }
if (-not $KeyPass) { $KeyPass = $env:SUDOKU_KEY_PASS }
if (-not $StorePass) { $StorePass = if ($env:SUDOKU_STORE_PASS) { $env:SUDOKU_STORE_PASS } else { $KeyPass } }

$signing = @()
if ($Unsigned) {
    Write-Host '已指定 -Unsigned：产出未签名 APK' -ForegroundColor Yellow
} elseif (-not (Test-Path $KeystorePath)) {
    Write-Host "未找到密钥库：$KeystorePath" -ForegroundColor Yellow
    Write-Host '  → 产出未签名 / 调试签名 APK（本地安装调试够用）。要签名请看 keystore\README.md' -ForegroundColor DarkGray
} elseif (-not $KeyPass) {
    Write-Host '⚠ 找到密钥库，但没有签名口令，无法签名，本次改为产出未签名 APK。' -ForegroundColor Yellow
    Write-Host '  请任选一种方式提供口令（详见 keystore\README.md）：' -ForegroundColor DarkGray
    Write-Host '    • 环境变量： $env:SUDOKU_KEY_PASS = ''你的口令''（库口令不同再加 SUDOKU_STORE_PASS）' -ForegroundColor DarkGray
    Write-Host '    • 本地文件： scripts\local-signing.ps1（已在 .gitignore 中，不会被提交）' -ForegroundColor DarkGray
    Write-Host '    • 命令行：   .\scripts\build-android.ps1 -KeyPass ''口令'' -StorePass ''库口令''' -ForegroundColor DarkGray
} else {
    Write-Host "使用密钥库签名：$KeystorePath（别名 $KeyAlias）" -ForegroundColor Cyan
    $signing = @(
        '-p:AndroidKeyStore=true',
        "-p:AndroidSigningKeyStore=$KeystorePath",
        "-p:AndroidSigningKeyAlias=$KeyAlias",
        "-p:AndroidSigningKeyPass=$KeyPass",
        "-p:AndroidSigningStorePass=$StorePass"
    )
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
