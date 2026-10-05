# 本地构建

在仓库根目录执行：

```sh
./build.sh
```

默认顺序构建四个 Release、自包含版本，运行目标 App 不需要另外安装 .NET。
也可以只构建指定平台，例如 `./build.sh osx-arm64 win-x64`。
脚本可以从其他目录调用，输出路径始终是本仓库的 `build/`。

| 参数 | 输出 |
| --- | --- |
| `osx-arm64` | `build/osx-arm64/publish/PKHeX.Avalonia.app` 和 macOS ZIP |
| `win-x64` | `build/win-x64/publish/` 和 Windows ZIP |
| `linux-x64` | `build/linux-x64/PKHeX-Avalonia-linux-x86_64.AppImage` |
| `linux-arm64` | `build/linux-arm64/PKHeX-Avalonia-linux-aarch64.AppImage` |

## 构建环境

- .NET 10 SDK、Bash、Git、Python 3。
- macOS 目标需要在 macOS 上构建，自动进行 ad-hoc 签名和签名验证；不进行 Apple 公证。
- Linux AppImage 打包需要安装并启动 Docker。Docker 构建 Debian 打包镜像，使用容器
  本机架构的 appimagetool，并分别嵌入 x86_64/aarch64 runtime；无需运行目标架构程序。
- 首次构建需要联网下载 NuGet 包、容器镜像和 AppImage 工具。Linux 目标仍需要桌面系统的
  图形库和 .NET Linux 原生依赖；自包含发布并不等于捆绑整个操作系统。
- 在 Windows 上可使用 WSL 构建 Windows/Linux 目标；完整四平台构建请在 macOS 上运行。

指定 SDK 路径（路径中可以包含空格）：

```sh
DOTNET=/path/to/dotnet ./build.sh
```

`DOCKER` 可以指定 Docker 可执行文件。`APPIMAGETOOL_RELEASE` 和
`APPIMAGE_RUNTIME_RELEASE` 默认使用 `continuous`，与现有发布流程一致；需要固定工具版本时
可分别指定对应上游仓库存在的 release tag。
打包方案参考 [AppImage 官方 appimagetool](https://github.com/AppImage/appimagetool)。

`build/` 已被 Git 忽略。每个目标使用全新临时目录，成功后才替换该目标的旧产物；失败时保留
旧产物和临时目录供检查。异常终止后如果留下 `build/.build-lock`，确认没有其他构建运行后可
删除该空目录再重试。脚本不会修改项目版本，也不会上传或发布产物。
