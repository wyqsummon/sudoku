# 签名密钥库（keystore）

Android 发布版 APK 需要签名。`scripts/build-android.ps1` 默认读取本目录下的
`keystore/sudoku.keystore`（别名 `sudoku`）。

**密钥库文件本身不入库**（见根目录 `.gitignore` 里的 `*.keystore`），克隆仓库后需要自己生成一个：

```powershell
keytool -genkeypair -v -keystore keystore\sudoku.keystore -alias sudoku `
        -keyalg RSA -keysize 2048 -validity 10000 `
        -dname "CN=Your Name, OU=Dev, O=Your Org, L=City, ST=Province, C=CN"
```

## 口令怎么提供给构建脚本

脚本里**没有**写死任何口令，按下面优先级取（都没有就产出未签名 APK）：

| 优先级 | 方式 |
| --- | --- |
| 1 | 命令行参数 `-KeyPass` / `-StorePass` / `-KeyAlias` / `-KeystorePath` |
| 2 | 环境变量 `SUDOKU_KEY_PASS` / `SUDOKU_STORE_PASS` / `SUDOKU_KEY_ALIAS` / `SUDOKU_KEYSTORE` |
| 3 | 本地私有文件 `scripts/local-signing.ps1`（脚本自动加载，已被 `.gitignore` 忽略） |

日常使用推荐第 3 种，例如：

```powershell
# scripts\local-signing.ps1 —— 该文件不会被提交
$env:SUDOKU_KEY_ALIAS  = 'sudoku'
$env:SUDOKU_KEY_PASS   = '你的密钥口令'
$env:SUDOKU_STORE_PASS = '你的库口令'   # 与密钥口令相同时可省略
```

在 CI 里用第 2 种（把口令放进仓库 Secret，构建时导出成环境变量）。

## 注意事项

- 升级安装新版本必须用**同一个密钥库**签名，否则手机会拒绝覆盖安装；密钥库丢了只能卸载重装。
- 口令若含 `;` 或 `$`，建议改用 `-KeyPass` 参数传入，避免被 shell / MSBuild 解释。
- 想显式产出未签名 APK：加 `-Unsigned` 开关。

> ⚠️ 千万不要把 `*.keystore` / `*.jks` / `*.p12` / `*.pfx` / `local-signing.ps1` 提交进仓库：
> 一旦泄露，任何人都能用你的名义发布「冒牌更新」。这些都已经在 `.gitignore` 里了。
