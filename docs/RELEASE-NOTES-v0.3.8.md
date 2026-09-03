# SpatialViewer v0.3.8 CAD 实图验收版

简体中文 | [日本語](RELEASE-NOTES-v0.3.8.ja.md) | [English](RELEASE-NOTES-v0.3.8.en.md)

v0.3.8 面向真实建筑 CAD 图纸验收，正式把 **SpatialViewer.CadCore v0.12.2** 固化到应用源码依赖与发布 fallback，同时保留 v0.3.6 的正式图标和 v0.3.7 的原生 Windows 启动界面。

## CAD 实图修复

- 建筑尺寸样式 `_Oblique` / `ARCHTICK` / `_ArchTick` 按斜短划端点显示，不再退化为普通 V 形箭头。
- DIMENSION 文字方向归一到可读半平面，修复 `2250` 等尺寸文字上下倒置的问题；普通 TEXT / MTEXT 的原始旋转保持不变。
- 当 DWG 存在具有实际 Paper Space 内容和活动视口的布局时，查看器显示组合后的 Layout Scene，不再固定只显示 Model Space。
- Fit、绘制与 HitTest 使用同一当前 CAD Scene；空白默认 Layout 不会触发误切换。

## CadCore 集成

- 源码 gitlink 固定到官方 **CadCore v0.12.2** release commit `e765831523fd20bd5e21664cb777fb6f5b98be4f`。
- 本地 Release fallback 目录同步为 `Kernels/Bundled/0.12.2`。
- 正式发布工作流仍按 `latest-stable` 规则解析内核，并对最终 MSIX 中 5 个 CadCore DLL 与官方 v0.12.2 Release 逐文件执行 SHA-256 校验。
- CadCore ABI 保持 `1.0.0.0`，Host Contract 保持 `SpatialViewer.CadHost 1.0.0`。

## 保持不变

- 保留 v0.3.6 的产品图标、任务栏/开始菜单/关于页面资源。
- 保留 v0.3.7 的原生可选 MSIX 启动画面及 100%/125%/150%/200%/400% DPI 资源。
- 不修改已正常的墙体、门窗、家具、颜色、线宽，以及标题栏、汉堡菜单、标签页和页面背景。
- 不通过视觉结果猜测天正私有字段；`TCH_AXIS_LABEL`、`TCH_DRAWINGINDEX` / `TCH_INDEXPOINTER`、`TCH_DIMENSION2` 与 modern `TCH_DIMENSION` 继续遵循既有 raw / opaque / proxy 与证据策略。

## 验收重点

请使用此前同一张真实 DWG 重点核对：建筑尺寸斜短划、`2250` 等文字方向、整套图框/标题栏/Paper Space 内容，以及既有主体图形是否无回归。
