# SpatialViewer v0.3.9 湘源用地色块修复

简体中文 | [日本語](RELEASE-NOTES-v0.3.9.ja.md) | [English](RELEASE-NOTES-v0.3.9.en.md)

SpatialViewer v0.3.9 是针对 v0.3.8 实机验收中发现的 **湘源控规用地色块未填充** 问题的定向修复版。

## 修复

- 发布包将嵌入 **SpatialViewer.CadCore v0.12.8**。
- 修复 ObjectARX `ProxyPolygon` 在 Scene 转换中只保留描边颜色、丢失面填充的问题。
- Proxy Polygon 现在使用当前有效 Proxy 颜色进行填充，ACI / TrueColor primitive override 同时作用于描边和面色。
- 普通 Polyline、closed LwPolyline、Circle、Arc 以及 Mesh/Shell edge fallback 继续保持不填充，避免把道路、轮廓线等误涂成色块。

## 对应实机现象

v0.3.8 中，图例色块、道路和地块边界能够正常显示，但主图地块只有彩色轮廓，没有按用地性质显示填充色。v0.3.9 专门修复这一显示链路。

## 保持不变

- 不修改 WinUI 3 界面、标题栏、标签页、主题或导航。
- 不改变 CAD ABI / Host Contract。
- 不把任意 Proxy Polygon 认定为湘源地块，不新增未经实样本证明的原生湘源语义。
