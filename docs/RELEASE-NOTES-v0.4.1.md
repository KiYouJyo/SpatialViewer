# SpatialViewer v0.4.1

简体中文 | [日本語](RELEASE-NOTES-v0.4.1.ja.md) | [English](RELEASE-NOTES-v0.4.1.en.md)

v0.4.1 是针对 v0.4 实机验收中 Rhino 黑屏与未响应问题的热修。

## 修复
- 3DM 文件即使未保存 Rhino render mesh，Brep 与 Extrusion 也会由 3DMCore 1.0.2 从语义几何生成显示网格。
- 3DM render scene 的曲面离散移出 WinUI UI 线程，避免模型载入完成后窗口长时间“未响应”。
- Win2D 视口由逐三角创建 CanvasGeometry 改为按 Mesh instance 批量绘制，并设置单帧 fill / wire / curve / point 预算。
- 黑色或近黑 Rhino 图层颜色在黑色画布上自动做显示对比度修正，不修改原始模型颜色。
- 复杂 Mesh 的选择高亮改为有预算的批量线框，避免生成巨型边集合。
- 鼠标滚轮缩放不再受当前选择/旋转/平移工具限制。

## 保持不变
本热修不修改 CAD 看图链、CadCore、CAD 工具条及既有标题栏、标签页、导航、项目、收藏和主题效果。
