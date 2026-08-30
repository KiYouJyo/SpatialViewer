简体中文 | [日本語](README.ja.md) | [English](README.en.md)

# Spatial Viewer · 图览

面向 CAD、GIS、BIM/IFC 与 Rhino 数据的现代 Windows 看图器。

[![Windows](https://img.shields.io/badge/Windows-WinUI%203-0078D4?logo=windows&logoColor=white)](https://github.com/KiYouJyo/SpatialViewer) [![Languages](https://img.shields.io/badge/Languages-%E4%B8%AD%E6%96%87%20%7C%20%E6%97%A5%E6%9C%AC%E8%AA%9E%20%7C%20English-6F42C1)](#语言) [![MIT License](https://img.shields.io/badge/License-MIT-D4A72C)](LICENSE)

> Spatial Viewer 当前处于早期开发阶段。下列格式与能力表示项目目标，不代表当前版本已经完整支持。

## 项目定位

Spatial Viewer 希望把常见的工程图纸、地理空间数据与三维模型放进一个统一、简洁、现代的 Windows 查看环境中。

- **查看优先**：专注快速打开、浏览、检查与轻量信息读取，而不是替代专业编辑软件。
- **统一体验**：尽量用一致的标签页、导航、视图控制和属性查看方式处理不同数据类型。
- **原生 Windows**：使用 WinUI 3 构建现代桌面界面，并遵循 Windows 的交互与主题体验。
- **模块化内核**：不同格式通过相对独立的读取、解析与渲染模块接入，降低格式之间的耦合。

## 计划覆盖

| 领域 | 主要来源 / 格式 | 目标 |
| --- | --- | --- |
| CAD | AutoCAD / DWG / DXF | 二维图纸查看、图层与基础对象信息 |
| GIS | 常见矢量、栅格与地图数据 | 空间数据浏览、图层管理、坐标与地图显示 |
| BIM | Revit 工作流 / IFC | IFC 模型查看、构件层级与属性读取 |
| 3D | Rhino / 3DM | 几何模型、图层与基础对象信息查看 |

具体支持范围会随内核开发逐步确定，并在 [ROADMAP.md](ROADMAP.md) 与后续版本说明中维护。

## 设计边界

Spatial Viewer 是**看图器**，不是 AutoCAD、GIS 桌面编辑器、Revit 或 Rhino 的完整替代品。项目优先保证文件打开、视觉还原、导航、图层/对象信息与查看性能，再按实际需要增加测量、查询等轻量工具。

## 语言

计划提供：

- 简体中文：**图览**
- 日本語：**図覧**
- English：**Spatial Viewer**

界面文本与仓库主要文档将尽量保持三语同步。

## 开发状态

仓库目前处于 Preview 阶段。除 Debug Host 外，`SpatialViewer.App` 提供正式 WinUI 3 产品壳：主页、最近文件、DWG/DXF 打开、真实 CAD 画布、多标签、图层、选择、属性与诊断。GIS、IFC 与 Rhino 仍未接入；完整边界见 [CAD 兼容矩阵](docs/compatibility/cad.md) 与 [UI Stage 1 验证](docs/verification/ui-stage1-verification.md)。

## 下载与安装

首个 Preview 版本请从 [GitHub Releases](https://github.com/KiYouJyo/SpatialViewer/releases/latest) 下载：

- `SpatialViewer-v0.1.0-x64-one-click.zip`：推荐入口。完整解压后运行“① 安装图览.cmd”；它会下载、校验并安装对应的 MSIXBundle。
- `SpatialViewer_0.1.0.0_x64.msixbundle`：用于需要手动部署的场景。请先校验同一 Release 中的 `SHA256SUMS.txt`，并使用该 Release 随附的公钥证书建立信任。

项目主页：https://kiyoujyo.github.io/SpatialViewer/

## 文档

- [路线图](ROADMAP.md)
- [更改日志](CHANGELOG.md)
- [贡献指南](CONTRIBUTING.md)
- [支持与问题反馈](SUPPORT.md)
- [安全政策](SECURITY.md)
- [行为准则](CODE_OF_CONDUCT.md)
- [隐私说明](PRIVACY.md)
- [第三方声明](THIRD-PARTY-NOTICES.md)
- [Stage 1 架构文档](docs/architecture/core-architecture.md)
- [CAD 兼容矩阵（Stage 2）](docs/compatibility/cad.md)
- [UI Stage 1 Figma 实现](docs/ui/figma-implementation.md)
- [UI Stage 1 验证](docs/verification/ui-stage1-verification.md)

## 贡献

欢迎通过 GitHub Issues 提交问题、格式兼容性反馈与功能建议。提交代码前请阅读 [CONTRIBUTING.md](CONTRIBUTING.md)。涉及样例图纸、模型或空间数据时，请确认你有权公开这些文件，并移除个人、项目或位置敏感信息。

## License

本项目采用 [MIT License](LICENSE)。
