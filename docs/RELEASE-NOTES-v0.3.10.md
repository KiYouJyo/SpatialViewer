# SpatialViewer v0.3.10 湘源用地面填充修复

简体中文 | [日本語](RELEASE-NOTES-v0.3.10.ja.md) | [English](RELEASE-NOTES-v0.3.10.en.md)

SpatialViewer v0.3.10 是针对 v0.3.9 实机验收中“湘源用地仍只有轮廓、没有面色”的第二轮定向修复。

## 修复内容

- 发布包将嵌入 **SpatialViewer.CadCore v0.12.9**。
- 新内核开始处理 ObjectARX `ProxySubentFillon`，保留明确的 FillAlways / FillNever 状态。
- 对严格平面且结构合法的 `ProxyMesh` / `ProxyShell`，不再只保存 edge，而是同时保存 face geometry 与 face color / visibility，再由 Scene 绘制填充面并叠加原边线。
- FaceTraits 缺失、数量冲突、法向非平面或面退化时继续 fail-closed 为 edge-only，不猜测色面。
- 显式 FillNever 会关闭 ProxyPolygon 填充；Polyline 即使闭合也不会被错误填充。

## 对应实机现象

v0.3.8/v0.3.9 能够显示道路、地块边界和图例，但主用地仍只有彩色轮廓。进一步排查确认，湘源图很可能使用了 Mesh/Shell face 这类 ObjectARX filled primitive，而旧 CadCore 只保留了它们的边线。

## 保持不变

- 不修改 WinUI 3 UI、标题栏、标签页、导航和主题。
- CAD ABI / Host Contract 不变。
- 这仍是通用 ObjectARX display-fidelity 修复，不把 Mesh/Shell 自动认定为湘源地块，不添加未经实样本证明的 proprietary semantic。
