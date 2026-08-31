简体中文 | [日本語](RELEASE-NOTES-v0.2.7.ja.md) | [English](RELEASE-NOTES-v0.2.7.en.md)

# Spatial Viewer v0.2.7

v0.2.7 集中修复浅色 / 深色主题下页面内容层的背景异常，并把正式 Release 的内核打包规则固化为自动流程。标题栏、标签栏、汉堡菜单、导航面板与既有布局不在本次视觉修复范围内。

## 深浅色页面修复

- 移除会把浅色页面 Brush 静态带入深色主题的页面背景覆盖，重新由 WinUI `NavigationView` 的原生主题资源管理内容背景。
- 修复浅色模式的页面点击态 / 非点击态显示，同时消除深色模式下整页被半透明白色蒙层覆盖的问题。
- 主题切换继续使用动态主题资源，浅色、深色与系统主题之间切换时不再保留上一主题的页面底色。

## Release 内核策略

- 从本版本起，正式 Release 在发布时自动解析并嵌入所有“已完成应用功能接入”的最新稳定内核；尚未完成应用接入的内核不会进入安装包。
- 当前只有 CAD 功能完成内核接入，因此 v0.2.7 仅随包嵌入最新稳定版 `SpatialViewer.CadCore v0.4.0`。
- 发布流程会校验 CadCore Release 的来源仓库、x64 运行时、`SpatialViewer 0.2.x` 兼容范围、ABI `1.0.0.0`、发布摘要与五个核心程序集，再将经过校验的官方 Release 二进制写入 `Kernels/Bundled/0.4.0/`。
- 安装包中的随包内核会与在线 Release 下载结果逐文件 SHA-256 比对，避免把源码子模块中的旧构建或错误版本误打进正式包。

## 验收

- 三语资源、Debug / Release x64 构建、Release 测试与 DebugHost 启动 Smoke 全部通过。
- CadCore 在线更新、ABI、下载 / 暂存与重启激活链路通过验收。
- 0.2.7.0 MSIXBundle 的内核隔离布局、最新 CadCore 注入、数字签名与一键安装包均在发布流程中再次校验。

> 本次 Release 的应用版本为 `0.2.7`，MSIX 包版本为 `0.2.7.0`。正式包随附 `SpatialViewer.CadCore v0.4.0`，ABI 为 `1.0.0.0`。