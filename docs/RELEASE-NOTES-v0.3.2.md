# SpatialViewer v0.3.2

简体中文 | [日本語](RELEASE-NOTES-v0.3.2.ja.md) | [English](RELEASE-NOTES-v0.3.2.en.md)

## 应用内更新

v0.3.2 将 SpatialViewer 主程序更新流程改为与 UrbanPlanToolbox 一致的已验证 MSIX 更新方式，不再只检查版本后跳转到浏览器。

- “关于图览”中的主程序更新采用进程级共享状态；离开页面再返回不会丢失检查或下载状态。
- 检查 GitHub Releases 后，只接受版本号高于当前安装版本的正式版本。
- 下载时要求 Release 中存在唯一的 `SpatialViewer_<version>.0_x64.msixbundle` 和 `SHA256SUMS.txt`。
- 安装包下载完成后依次执行 GitHub asset SHA-256、`SHA256SUMS.txt`、WinTrust/MSIX 签名、发布证书 Subject 和 Thumbprint 校验。
- 固定可信发布者为 `CN=AppPublisher`，证书 Thumbprint 为 `BD85AD77A651C86CA01A480C8E9BC64952993F98`。
- 校验通过后进入“等待安装”状态；用户再次确认后由 Windows `PackageManager` 使用 `ForceApplicationShutdown` 部署新 MSIXBundle，并通过应用重启注册完成更新后的恢复。
- 下载、验证、安装、失败和重试状态均在原“更新管理”区域显示，不引入独立 Launcher。

## 版本显示修复

- “显示版本”“内部版本”“当前版本”不再在 XAML 中写死。
- 显示版本统一来自 `AppVersionProvider`；已打包应用优先读取 `Package.Current.Id.Version`。
- v0.3.2 的产品版本为 `0.3.2`，MSIX 包版本为 `0.3.2.0`。

## 保持不变

v0.3.2 不修改标题栏、汉堡菜单、导航结构、项目与收藏页面、浅色/深色主题设计，也不改变 CadCore 独立更新机制。
