# SpatialViewer v0.3.7 启动界面

SpatialViewer v0.3.7 为应用增加与 UrbanPlanToolbox、PageArc 一致思路的原生 Windows 启动界面。

## 主要变化

- 使用 MSIX `uap:SplashScreen` 作为冷启动界面，不额外创建第二个 WinUI 窗口，因此不会为了“展示启动页”人为延长启动时间。
- 启动界面采用固定 `#202020` 深色底面与居中的 SpatialViewer 产品图标；不加入额外文字、按钮或伪进度条。
- 提供 100%、125%、150%、200%、400% 五档独立位图资源，逻辑尺寸保持 620×300，Windows 根据 DPI 自动选择对应资源，避免高分屏模糊。
- 将 Splash 标记为 `uap5:Optional="true"`，快速启动时允许 Windows 直接进入主窗口；较慢的冷启动仍由系统原生启动界面覆盖初始化间隔。
- 将启动资源生成与尺寸/背景/Manifest 契约接入构建门禁，防止后续回归为单张图片硬缩放或错误背景色。

## 保持不变

- 不修改标题栏、汉堡菜单、NavigationView、页面背景、标签页、CAD 渲染与 Cad Core 行为。
- 不在主窗口中加入额外遮罩层或启动动画，不影响首帧交互与窗口恢复逻辑。
