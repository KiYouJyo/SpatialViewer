# SpatialViewer v0.3.6

## 中文

v0.3.6 是针对真实建筑 CAD 图纸显示差异的修复版本，并正式集成 **SpatialViewer.CadCore v0.12.2**。

### CAD 显示修复

- 修复建筑尺寸样式中的 `_Oblique` / `ARCHTICK` / `_ArchTick` 斜短划端点，避免退化为普通 V 形箭头。
- 修复尺寸文字在 180° 等等价旋转下倒置的问题，使 `2250` 等标注保持可读方向。
- 当 DWG 中存在具有实际 Paper Space 内容和活动视口的布局时，查看器会显示组合后的 Layout Scene，不再固定只显示 Model Space。
- Fit、绘制与 HitTest 使用同一当前 CAD Scene，避免图框显示与交互状态不一致。
- 空白的默认 Layout1 / Layout2 不会触发误切换，普通模型空间图纸保持原行为。

### 内核集成

- 内置 CAD 内核更新为 **SpatialViewer.CadCore v0.12.2**。
- bundled kernel 运行时目录同步为 `Kernels/Bundled/0.12.2`，消除旧 `0.9.0` 目录标识与实际内核版本不一致的问题。
- CadCore ABI 保持 `1.0.0.0`；宿主契约保持 `SpatialViewer.CadHost 1.0.0`。
- 应用仍采用 `latest-stable` 内核集成策略，不将应用版本与 CadCore 产品版本硬绑定。

### 边界

本版本没有通过猜测扩展天正私有对象语义。`TCH_AXIS_LABEL`、`TCH_DRAWINGINDEX` / `TCH_INDEXPOINTER`、`TCH_DIMENSION2` 与 modern `TCH_DIMENSION` 继续遵循既有 raw / opaque / proxy 与证据策略。

### 验收重点

请重点复核同一真实 DWG 中：

1. 建筑尺寸端点是否恢复为斜短划；
2. `2250` 等尺寸文字是否保持正向可读；
3. 整套图框、标题栏和 Paper Space 内容是否完整出现；
4. 已正常的墙体、门窗、家具、颜色、线宽与现有 UI 是否保持不变。
