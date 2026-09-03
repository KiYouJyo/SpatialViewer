# SpatialViewer v0.3.7 启动界面与 CadCore v0.12.3 集成

SpatialViewer v0.3.7 采用与 UrbanPlanToolbox、PageArc 当前版本一致的两阶段启动机制，同时修正此前验收中的一个关键问题：开发构建与已发布 v0.3.6 实际没有运行本轮 CAD 实图修复所对应的 CadCore v0.12.3。

## 启动界面

- 保留 MSIX `uap:SplashScreen` 作为 Stage 1 冷启动引导层，继续提供 100%、125%、150%、200%、400% 五档 DPI 资源并保持 `uap5:Optional="true"`。
- Stage 2 在真实 WinUI 主窗口中使用透明 `StartupOverlay`；背景直接透出窗口自身的 `MicaBackdrop`。
- 主界面在启动 Logo 后方同步完成布局与初始化，启动层撤除前保持不可点击。
- Logo 首帧真正渲染后开始计时，快速启动时至少完整显示约 500 ms；随后以约 200 ms EaseOut 淡出。
- 增加 1 秒 Logo 解码兜底和 5 秒启动 watchdog，保持 fail-open。

## CadCore 集成修正

此前不能把“CadCore v0.12.3 的单元测试通过”视为看图器实图问题已经修复，因为：

- SpatialViewer v0.3.6 正式包随包内核仍为 CadCore v0.12.2；
- v0.3.7 源码项目此前仍声明 `CadCoreBundledVersion=0.9.0`，子模块也仍指向 CadCore v0.9.0；
- 因此直接运行/构建看图器并不会实际执行 v0.12.3 的代码。

v0.3.7 现在将源码子模块和 `CadCoreBundledVersion` 都推进到 **CadCore v0.12.3 / commit `2f150fbdcf380fba6f60df7f8a41361322afdd8f`**。验收流水线同时新增两道强制门禁：

1. 源码构建必须实际引用 v0.12.3，版本或 gitlink 不一致即失败；
2. 最终 MSIXBundle 解包后，`Kernels/Bundled/0.12.3` 中五个 CadCore 程序集必须与 CadCore v0.12.3 GitHub Release 逐文件 SHA-256 一致。

这样可以排除“内核仓库改了，但看图器仍在跑旧内核”的假修复。

## CAD 实图验收边界

CadCore v0.12.3 中针对长直角线、尺寸文字锚点/颜色/建筑 tick、旧式 CJK SHX fallback 的代码会随 v0.3.7 真正进入应用。但这些改动在没有原始问题 DWG 的情况下仍只能视为**候选修复**，不能仅凭合成测试宣布三个实图问题已解决。

后续验收必须直接使用复现问题的原始图纸，对比 AutoCAD 与 SpatialViewer 的实际画面；只有三项视觉差异均消失后才标记为完成。

## 保持不变

- 标题栏、汉堡菜单、NavigationView、页面背景、标签页及既有交互不因本次 CadCore 集成而重构。
- 不创建第二个独立 WinUI 窗口，不加入文字、按钮或伪进度条。
