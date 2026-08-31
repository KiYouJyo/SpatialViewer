简体中文 | [日本語](RELEASE-NOTES-v0.2.3.ja.md) | [English](RELEASE-NOTES-v0.2.3.en.md)

# Spatial Viewer v0.2.3

本次更新针对 CadCore 下载完成后的最后一步交互进行修正。

## CadCore 重启更新

- CadCore 更新下载并验证完成后，操作按钮不再变灰。
- 按钮改为“重启更新”，点击后由 Windows App SDK `AppInstance.Restart` 主动重启 SpatialViewer。
- 新进程启动时会在 XAML 初始化前读取已暂存的 CadCore，并立即启用下载完成的新内核，不进行运行中热替换。
- 如果 Windows 无法完成应用重启，界面会保留可重试状态并记录具体的重启失败原因。

## 更新管理显示修正

- 内核名称列由 `SpatialViewer.CadCore` 简化为 `CadCore`。
- 更新来源列由 `GitHub Releases` 改为 `SpatialViewer.CadCore`，明确表示更新来自独立 CadCore 仓库。

## 验收

v0.2.3 签名验收包只有在三语资源、重启更新 UI 契约、Debug/Release 编译、测试、启动 smoke、CadCore Release 检查、真实下载与暂存、MSIX 生成和 Authenticode 签名全部通过后才会生成。
