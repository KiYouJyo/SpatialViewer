# Spatial Viewer v0.2.4

本版本针对 v0.2.3 中仍存在的 **Cad Core 下载后重启仍绑定旧内核** 问题进行架构级修复。

## 修复

- 将 Cad Core 激活从 `App()` 构造函数前移到程序集 `ModuleInitializer`，确保在 WinUI/XAML 生成代码和 `MainWindow` 的静态 Cad Core 引用发生绑定之前完成新版内核选择。
- 保留内置 Cad Core 作为安全回退；只有通过 manifest、架构、兼容性和程序集版本校验的外置内核才会被激活。
- “重启更新”仍使用 Windows App SDK `AppInstance.Restart`，但新进程现在会在 WinUI 初始化之前处理 `pending.json` 并绑定新版 Cad Core。
- 更新验收 probe：它现在与正式应用一样静态引用五个 Cad Core 项目，并在模块初始化后实际构造 `ACadSharpCadImporter`，验证不是只“加载到了 DLL”，而是应用静态类型真正绑定到新版程序集。
- 验收同时检查五个 Cad Core 程序集的版本和加载路径必须来自已暂存版本目录，而不是 MSIX 内置目录。

## 版本

- SpatialViewer：0.2.4
- MSIX：0.2.4.0
- 内置 Cad Core：继续作为回退基线
- 在线 Cad Core：按 `SpatialViewer.CadCore` 最新兼容 Release 独立更新
