# SpatialViewer v0.3.7 启动界面

SpatialViewer v0.3.7 采用与 UrbanPlanToolbox、PageArc 当前版本一致的两阶段启动机制：Windows 原生启动页负责最早期进程启动，主窗口创建后立即切换到 Mica 上的应用内启动层，并在真实界面准备完成后平滑淡出。

## 主要变化

- 保留 MSIX `uap:SplashScreen` 作为 Stage 1 冷启动引导层，继续提供 100%、125%、150%、200%、400% 五档 DPI 资源并保持 `uap5:Optional="true"`。
- Stage 2 在真实 WinUI 主窗口中使用透明 `StartupOverlay`；背景直接透出窗口自身的 `MicaBackdrop`，不再把原生 `#202020` 启动图直接跳转到完整界面。
- 主界面在启动 Logo 后方同步完成布局与初始化，启动层撤除前保持不可点击，避免出现半初始化界面或标题栏后黑屏。
- 启动 Logo 首次真正渲染后开始计时，快速启动时至少完整显示约 500 ms；初始化较慢时不会额外阻塞，而是自然等待 Shell 就绪。
- Shell、Logo 与最短展示时间均满足后，先显示完整主界面，再以约 200 ms EaseOut 淡出启动层。
- 增加 1 秒 Logo 解码兜底和 5 秒启动 watchdog；任何异常都必须 fail-open，启动界面本身不能成为卡死点。
- 将构建门禁改为验证“原生 Stage 1 + Mica Stage 2”的混合启动契约，同时锁定现有标题栏几何，防止启动功能再次误改标题栏。

## 保持不变

- 标题栏、汉堡菜单、NavigationView、页面背景、标签页、CAD 渲染与 Cad Core 行为保持原样。
- 不创建第二个独立 WinUI 窗口，不加入文字、按钮或伪进度条。
