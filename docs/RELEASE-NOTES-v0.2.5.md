# Spatial Viewer v0.2.5

本版本集中修复主界面层级、更新状态生命周期与 CAD 响应式属性栏。

## 修复

- 恢复 WinUI 3 / Fluent 原生的窗口层级：标题栏与汉堡导航区保留 Mica，内容区重新使用 `LayerFillColorDefaultBrush`，深色和浅色主题下都能明确区分应用 chrome 与页面内容。
- 不再把 `NavigationView` 内容层强制透明化；导航项的普通、悬停、按下与选中状态重新交由 WinUI 原生模板管理。
- 更新检查状态改为进程级共享 session。检查 SpatialViewer 或 Cad Core 后，切换到其他页面再返回“关于”页，会继续显示上一次检查结果，而不是回到“尚未检查”。
- Cad Core 的 updater service 与检查结果一同保留在 session 中，因此在“发现更新”后切走页面再回来，仍可继续下载并暂存更新。
- CAD 属性区域在 Large / Medium / Small 三种窗口宽度下统一使用右侧 `SplitView` 边栏，不再在中小尺寸变成覆盖图面的 Flyout。
- 左右边栏使用相同的响应式宽度规则（300 / 240 / 220 DIP）；窄窗口工具栏支持水平滚动，图层与属性按钮仍保持可达。

## 验收

- 增加 UI 契约门禁：禁止重新引入透明 `NavigationViewContentBackground`、禁止属性 Flyout 回归、要求 About 页使用进程级更新 session。
- 保留 v0.2.4 的 Cad Core 0.3.0 → 0.3.1 真实重启升级、稳定 ABI、MSIX resolver 隔离、版本一致性与签名验收。

## 版本

- SpatialViewer：0.2.5
- MSIX：0.2.5.0
- 内置 Cad Core：0.3.0 回退基线
- 在线 Cad Core：继续按 `SpatialViewer.CadCore` 最新兼容 Release 独立更新
