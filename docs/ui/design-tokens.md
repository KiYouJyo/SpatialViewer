# Design tokens

`src/SpatialViewer.App/Themes/Colors.xaml` is the single semantic-token source. It uses ThemeDictionaries and ThemeResource so changing a token updates all WinUI surfaces.

| Semantic token | Dark | Light |
| --- | --- | --- |
| TextPrimary | `#F0F5F5` | `#152020` |
| TextSecondary | `#A9B6B6` | `#586767` |
| BgApp | `#0E1717` | `#F2F5F5` |
| BgSurface | `#192424` | `#FAFCFC` |
| BgPanel | `#1D2828` | `#F6F9F9` |
| BgToolbar | `#202C2C` | `#F1F5F5` |
| BgSelection | `#263232` | `#E8EEEE` |
| Border | `#3A4848` | `#D6DEDE` |
| Accent | `#42B8E3` | `#087EA4` |

The CAD canvas stays black independently of application theme. Selection uses the Figma dark accent `#42B8E3` and neither setting changes CAD source colors.
