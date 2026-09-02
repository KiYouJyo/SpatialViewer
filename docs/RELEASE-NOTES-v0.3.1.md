# SpatialViewer v0.3.1

## 内核兼容性修复

v0.3.1 重构了可独立更新的 CAD 内核兼容机制，彻底移除“主程序产品版本必须与内核声明的 0.x.x 系列一致”这一错误约束。

- 内核更新资格不再由 SpatialViewer 0.2.x / 0.3.x 等产品版本判断。
- 新增独立 Host Contract：`SpatialViewer.CadHost 1.0.0`。
- CLR ABI `1.0.0.0` 继续负责程序集绑定兼容性；Host Contract 单独负责宿主能力兼容性。
- “检查更新”阶段会先下载很小的 `cadcore-release.json` 独立 manifest，完成 schema、ABI、Host Contract、版本、运行时与来源仓库预检；只有通过预检才会把内核标记为可安装更新。
- 完整 ZIP 下载后仍会再次校验包内 manifest、程序集 ABI、FileVersion 和预检 manifest 一致性。
- Release 打包器和 CI 使用同一套 ABI + Host Contract 规则，不再硬编码任何 SpatialViewer minor 版本。
- 保持失败回退：不兼容或损坏的外置内核不会替换内置稳定内核。

v0.3.1 不改变项目、收藏、标题栏、导航、主题或现有看图交互设计。
