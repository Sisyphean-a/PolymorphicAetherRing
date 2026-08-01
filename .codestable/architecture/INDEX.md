# 架构索引

## 范围地图

| 范围 | 代码归属 | 当前态入口 | 领域上下文 |
| --- | --- | --- | --- |
| `workspace` | `PolymorphicAetherRing.sln`、`global.json` | 本页 | [`context:aether-ring`](../requirements/contexts/aether-ring.md) |
| `package:PolymorphicAetherRing` | `PolymorphicAetherRing/` | [模组包](packages/polymorphic-aether-ring.md) | [`context:aether-ring`](../requirements/contexts/aether-ring.md) |

这是单仓单包项目，没有跨包共享架构页面。包的外部入口是 `PolymorphicAetherRing/manifest.json` 与 `ModEntry.Entry`。

## 按改动定位

- 启动、内容注册、存档赠送、输入路由或配置：加载[模组包](packages/polymorphic-aether-ring.md)和[`context:aether-ring`](../requirements/contexts/aether-ring.md)。代码锚点：`ModEntry`、`ModConfig`。
- 熔铸数据或战斗公式：加载同一包和领域上下文。代码锚点：`FusedWeaponData`、`RingCombatManager`。
- 桌面或紧凑菜单：加载同一包和领域上下文。代码锚点：`FusionMenu`、`MobileFusionMenu` 及其 `Interaction`、`Rendering` 分部。
- 本地化和物品展示：加载同一包和领域上下文。代码锚点：`i18n/*.json`、`ModEntry.OnAssetRequested`。

询问“为什么这样布局”“以前是否有标题”“移动菜单替代了什么”时，再读[熔铸菜单布局演进](../history/ui-layout-evolution.md)。
