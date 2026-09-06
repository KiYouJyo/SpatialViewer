# SpatialViewer v0.4.0

v0.4.0 正式接入 Rhino 3DM 看图能力，并保持既有 CAD 看图与 WinUI 3 外壳行为不变。

## Rhino 3DM
- 接入 SpatialViewer.3DMCore 1.0.1。
- 支持直接打开 .3dm、渐进加载、图层开关、标准视图与 Rhino 命名视图。
- 支持透视/正交相机、旋转、平移、缩放、适合窗口。
- 支持着色、着色并显示边线、线框三种显示模式。
- 支持对象选择、高亮及 Rhino 属性查看，块实例保持独立选择标识。
- 支持最近文件、上次会话恢复、外部修改自动重载与 Windows .3dm 文件关联。

## 界面一致性
Rhino 看图页沿用 CAD 看图页的 64 DIP 工具栏、原生 AppBarButton / AppBarToggleButton、左右 Inline SplitView、300 / 240 / 220 DIP 响应式侧栏、属性列表和底部状态栏设计语言。

## 保持不变
本版本不重构 CAD 显示链，不修改既有 CadCore 运行时更新机制，也不改变标题栏、标签页、汉堡菜单、主题、项目和收藏等既有效果。
