# SpatialViewer v0.3.8 湘源控规内核实机验收

简体中文 | [日本語](RELEASE-NOTES-v0.3.8.ja.md) | [English](RELEASE-NOTES-v0.3.8.en.md)

SpatialViewer v0.3.8 是面向 **湘源控规 CAD 实图验证** 的内核验收版本。本次不重新设计界面、不调整标题栏、标签页、导航、主题或既有 CAD 显示样式；发布包会按现有 kernel-integration contract 自动解析并嵌入最新稳定的 SpatialViewer.CadCore。

## 本次验收重点

- 发布包将嵌入 **SpatialViewer.CadCore v0.12.7**（稳定 ABI `1.0.0.0`，`SpatialViewer.CadHost 1.0.0` 兼容线）。
- v0.12.7 新增完整的湘源控规 evidence-driven 兼容研究链，包括对象发现、native→converted diff、multi-pair candidate consensus、地块单变量实验、Proxy Graphics 几何证据、对象引用与端点证据，以及整图 A/B 匹配。
- 整图 A/B 只使用相同且唯一的 CAD handle + exact class identity 匹配对象，不按坐标、图层、文字或几何近邻猜测。
- Unknown candidate 仍保持全局 `Unknown`，不会因为“来自湘源图”就自动提升为湘源原生语义。
- 未被证明的地块编号、用地性质、容积率、建筑密度、绿地率、限高、边界和控制指标关系仍严格 fail closed。

## 建议实机检查

1. 使用现有普通 DWG/DXF 回归：确认打开速度、颜色、文字、圆弧、轴网、图框等既有功能没有退化。
2. 打开湘源原生控规图：检查 custom object 是否能够被安全保留，Proxy Graphics 是否能显示可用的地块边界、标注和辅助图形。
3. 对同一湘源图制作只修改一个地块属性或一个边界点的副本，确认后续 A/B evidence 只出现对应结构变化。
4. 使用湘源“对象转块/全部炸开”或成果输出生成普通 CAD 版本，与原生图做 conversion diff，验证重复消失的 custom class/profile 是否稳定。
5. 若发现对象缺失、位置/颜色/文字异常或崩溃，请保留原图与操作步骤用于下一轮 Reader/display regression。

## 保持不变

- 不改现有 WinUI 3 页面布局和视觉设计。
- 不改变 CAD ABI/Host Contract。
- 不宣称 v0.13 原生湘源地块语义已经完成。
- 应用仍通过正式签名的 x64 MSIXBundle 与 one-click 安装包发布。
