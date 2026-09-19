# 构建与发布

本项目**不依赖系统级 .NET 安装**：SDK 与 MAUI workload 都装在仓库内的 `.toolchain/`（不入库，约 2 GB），
Android SDK 默认装在 `.android-sdk/`（同样不入库，1~2 GB）。

## 环境要求

| 项 | 要求 |
| --- | --- |
| 操作系统 | Windows 10 1809+ / Windows 11（Windows App SDK 只支持 Windows；Android 侧可在别的系统构建，但脚本按 PowerShell 写） |
| 终端 | PowerShell 5.1+（`pwsh` 或 `powershell` 均可） |
| .NET | 无需预装；首次用 `scripts/install-toolchain.ps1` 装 .NET 10 SDK + MAUI workload |
| JDK | 构建 Android 需要 JDK 17+（脚本会去 `JAVA_HOME` 或 `C:\Program Files\Java` 找） |
| Android SDK | 可选；`build-android.ps1` 首次会自动装到 `.android-sdk/` |

## 首次准备

```powershell
# 1. 安装项目内 .NET 10 SDK + MAUI workload（约 2~4 GB，只需一次）
pwsh -File scripts\install-toolchain.ps1

# 2. 每次开新终端后启用项目内 SDK（设置 DOTNET_ROOT 与 PATH）
. .\scripts\dev-env.ps1
```

> `. scripts\dev-env.ps1` 前面的点号不能省（dot-source，让环境变量留在当前会话）。
> 之后所有 `dotnet` 命令都会走 `.toolchain\dotnet\dotnet.exe`，不会动系统里的 .NET。

## 常用命令

| 命令 | 作用 |
| --- | --- |
| `. .\scripts\dev-env.ps1` | 启用项目内 .NET SDK |
| `.\scripts\test.ps1` | 跑 `Sudoku.Core` 单元测试（165 个） |
| `.\scripts\run-windows.ps1` | 构建（Debug）并启动 Windows 版；`-NoBuild` 可跳过构建 |
| `.\scripts\publish-windows.ps1` | 发布 Windows 绿色版到 `publish\windows\`；加 `-Zip` 同时打包 ZIP |
| `.\scripts\build-android.ps1` | 构建 Android APK（Release）；`-Debug` / `-Unsigned` 可选 |
| `.\scripts\install-apk.ps1` | 用 adb 把 APK 装到已连接的手机/模拟器并启动；`-Uninstall` 先卸载 |

## Windows 绿色版

`scripts\publish-windows.ps1` 产出**自包含**版本：.NET 运行时与 Windows App SDK 都打包进去，
目标机器**不需要预装任何运行时**，整个目录拷过去双击即用。

```
publish\windows\
├── Sudoku.exe        ← 启动器，双击即用（约 4 KB）
├── 使用说明.txt
└── app\              ← 程序本体：真正的 exe + 400 个运行库 DLL 全在这里
```

- 为什么 exe 在顶层、运行库却在 `app\`：WinUI 的原生 DLL 必须与主 exe 同目录、无法分散存放，
  于是把程序本体整体收进 `app\`，顶层只留一个极小的启动器，避免「exe 埋在一堆 DLL 里」。
  启动器只是转发启动（依赖 Windows 自带的 .NET Framework），也可以直接双击 `app\Sudoku.exe`。
- 体积：约 226 MB / 408 个文件，其中绝大部分是自包含的 .NET 运行时 + Windows App SDK + WinUI。
  脚本会自动删掉调试符号与多余语言资源（只留 `zh-*` 与 `en-*`），文件数从 600+ 降到 400 左右。
- 加 `-Zip` 生成 `publish\Sudoku-win-x64.zip`（约 82.6 MB）。
- **不能**做成单文件 EXE：WinUI 3 不支持单文件部署，产出的 exe 会启动即崩（已实测）。
  想进一步瘦身只能改成依赖已安装运行时的发布方式（目标机器需先装 .NET 10 Desktop Runtime
  与 Windows App SDK Runtime）。

## Android APK

```powershell
. .\scripts\dev-env.ps1
.\scripts\build-android.ps1                 # 用本机密钥库签名（见下）
.\scripts\build-android.ps1 -Unsigned       # 显式产出未签名 APK
.\scripts\install-apk.ps1                   # adb 安装并启动
```

首次运行会调用 `dotnet build -t:InstallAndroidDependencies` 把 Android SDK 装到 `.android-sdk/`，
需要接受许可（脚本已带 `-p:AcceptAndroidSDKLicenses=true`），下载量 1~2 GB。

### 签名

APK 通过 `keystore/sudoku.keystore` 签名。**密钥库与口令都不入库**，脚本里也没有写死口令，
按下面优先级取值（详见 [`keystore/README.md`](../keystore/README.md)）：

1. 命令行参数 `-KeyPass` / `-StorePass` / `-KeyAlias` / `-KeystorePath`
2. 环境变量 `SUDOKU_KEY_PASS` / `SUDOKU_STORE_PASS` / `SUDOKU_KEY_ALIAS` / `SUDOKU_KEYSTORE`
3. 本地私有文件 `scripts/local-signing.ps1`（脚本自动加载，已被 `.gitignore` 忽略）

三者都没提供而密钥库存在时，脚本会打印警告并改为产出未签名 APK，**不会**静默签错。
CI 里用第 2 种（口令放仓库 Secret）。

> ⚠️ 升级安装必须用**同一个密钥库**签名，否则手机会拒绝覆盖安装。密钥库丢了只能卸载重装。

### 产物校验

签名 APK 输出在 `publish\android\com.sudoku.app-Signed.apk`（约 27.7 MB）。
发布前可以用 SDK 自带的工具核对：

```powershell
# 签名信息
& .android-sdk\build-tools\<版本>\apksigner.bat verify --print-certs publish\android\com.sudoku.app-Signed.apk
# 4 字节对齐
& .android-sdk\build-tools\<版本>\zipalign.exe -c -v 4 publish\android\com.sudoku.app-Signed.apk
```

包信息：包名 `com.sudoku.app`、应用名「数独」、minSdk 21、targetSdk 36。

## 构建产物一览

| 平台 | 产物 | 说明 |
| --- | --- | --- |
| Windows | `publish\windows\Sudoku.exe` | 绿色版启动器，程序本体在同目录 `app\` |
| Windows | `publish\Sudoku-win-x64.zip` | 上面整个目录的压缩包，约 82.6 MB |
| Android | `publish\android\com.sudoku.app-Signed.apk` | 已签名，拷到手机点击安装（需允许安装未知来源） |

这些目录都在 `.gitignore` 里，不会进版本库——分发请走 GitHub Releases。

## 常见问题

**1. `MSB3027 / 文件被 "testhost (N)" 锁定`**
上一轮 `dotnet test` 还没退出就又在构建。等它结束，或先 `Get-Process testhost | Stop-Process`。

**2. 中文脚本执行报编码错误 / 中文变乱码**
带中文的 `.cs` 与 `.ps1` 必须保存为 **UTF-8 with BOM**（Windows PowerShell 5.1 的默认读取方式），
否则会按 ANSI 解析。用 VS Code / Visual Studio 保存时选「UTF-8 with BOM」。

**3. `dotnet` 命令找不到**
忘了 dot-source `scripts\dev-env.ps1`。也可以手动：

```powershell
$env:DOTNET_ROOT = "$PWD\.toolchain\dotnet"; $env:PATH = "$env:DOTNET_ROOT;$env:PATH"
```

**4. Android 构建报找不到 JDK**
装 JDK 17+，或设置 `$env:JAVA_HOME`，或直接把 JDK 放到 `C:\Program Files\Java\jdk-*`（脚本会自动探测）。

**5. 想换一个 Android SDK 位置**
`.\scripts\build-android.ps1 -SdkDirectory D:\AndroidSdk`，加 `-InstallDependencies` 可让它先补齐依赖。

**6. Windows 发布时提示语言目录没删干净**
`publish-windows.ps1` 只保留 `zh-CN/zh-Hans/zh-Hant/zh-HK/zh-TW/en-us/en-GB`，
其余目录里若还有 `.mui` / `.resources.dll` 就整目录删除；这一步失败不影响程序运行，只是体积变大。
