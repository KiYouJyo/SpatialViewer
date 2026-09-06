# SpatialViewer v0.4.2

简体中文 | [日本語](RELEASE-NOTES-v0.4.2.ja.md) | [English](RELEASE-NOTES-v0.4.2.en.md)

v0.4.2 完成 CAD 内核诊断链的正式发布接入，并把已经起草的 CAD 兼容性报告导出能力接到现有看图 UI。

## CAD 兼容性报告

- 在 CAD 页面右侧“属性”侧栏底部新增“导出 CAD 兼容性报告”入口，沿用现有 WinUI 3 控件、侧栏宽度和交互层级，不改变工具栏及画布布局。
- 报告直接从当前已载入的 reader-independent `CadDocument` 生成 JSON，包含：
  - SpatialViewer / CadCore / CAD adapter 版本；
  - 安全的文档级汇总元数据；
  - custom entity 的结构类别、vendor 与 representation；
  - Proxy Graphics primitive 覆盖与 translated / unsupported 汇总；
  - CadCore v0.12.10 提供的 raw proxy-command structural signature、unknown type IDs 与 malformed / truncated 汇总。
- 默认保存到桌面的 `SpatialViewer Diagnostics` 文件夹；桌面路径不可用时回退到 LocalAppData。
- 导出成功后自动复制报告路径，并通过现有 InfoBar 显示成功/失败状态。

## 隐私边界

兼容性报告采用显式 allow-list，不导出源图纸文件名/路径、entity handle、target handle、图层名、坐标、原始属性值、颜色、文字内容、raw DWG/DXF bytes 或完整 entity metadata。

raw proxy-command 仅输出结构证据，不把 unknown command 猜测为湘源或其他 proprietary semantic。

## 内核

v0.4.2 的正式发布流程继续按照独立 Host Contract 解析并嵌入最新稳定 CadCore。对应正式内核为 SpatialViewer.CadCore v0.12.10，CLR ABI 保持 `1.0.0.0`。

## 保持不变

本次不修改既有 CAD 渲染结果、CAD 工具栏几何、左右侧栏响应式规则、标题栏、标签页、NavigationView、项目/收藏，也不回退 v0.4.1 已完成的 Rhino 3DM 黑屏和响应性修复。
