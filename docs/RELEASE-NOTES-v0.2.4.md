简体中文 | [日本語](RELEASE-NOTES-v0.2.4.ja.md) | [English](RELEASE-NOTES-v0.2.4.en.md)

# Spatial Viewer v0.2.4

本次更新针对 v0.2.3 中“Cad Core 已下载并重启，但实际仍绑定旧内核”的问题进行启动层修复。

## Cad Core 启动预加载

- Cad Core 激活时机从 WinUI `App` 构造函数提前到 CLR `ModuleInitializer`。
- 已暂存的新版内核会在 `Microsoft.UI.Xaml.Application` 构造、XAML 类型系统初始化以及产品代码首次访问 Cad Core 类型之前完成加载。
- 保留内置 Cad Core 作为可靠回退；只有通过版本、清单和程序集验证的更新包才会成为 active 版本。
- “重启更新”仍使用 Windows App SDK `AppInstance.Restart`，但新进程现在会在 WinUI 启动之前完成内核绑定。

## 针对实机问题的验收升级

- 新增静态绑定启动测试：测试进程本身编译时引用旧版 Cad Core，同时将线上最新版写入 pending。
- 测试要求在 `Main` 执行前完成 pending → active，并在 `Main` 中直接访问真实 `ACadSharpCadImporter` 类型，确认其程序集版本已经是线上新版，而不是旧的静态引用版本。
- 测试完成后恢复产品实际 Cad Core gitlink，最终 MSIX 不会因为测试而降级内置内核。

## 包版本

- Spatial Viewer：0.2.4
- MSIX：0.2.4.0
- 架构：x64
