---
scope: package:PolymorphicAetherRing
code-paths:
  - PolymorphicAetherRing
contexts:
  - context:aether-ring
---

# PolymorphicAetherRing 模组包

## 职责

该包交付一个 `net6.0` SMAPI 模组，负责注册以太多态戒指、保存熔铸武器数据、提供桌面与紧凑熔铸菜单，并用已装备戒指执行范围战斗。

## 外部边界

- `manifest.json` 定义模组标识 `xixifu.PolymorphicAetherTrinket`、入口 DLL、最低 SMAPI 4.0 和 Stardew Valley 1.6。
- `ModEntry.Entry` 是唯一运行入口，订阅资源请求、游戏启动、读档、逐帧更新、按下和释放事件。
- `ModEntry.OnAssetRequested` 向 `Data/Objects` 与 `Strings/Objects` 注册物品 `xixifu.AetherRing`；当前售价是 5000。
- Generic Mod Config Menu 是可选消费依赖；本包不对其他模组导出 API。

## 运行流程

1. `ModEntry.OnSaveLoaded` 创建 `RingCombatManager`。若领取标记尚不存在，它会递归检查背包、已装备戒指和组合戒指：已有目标戒指则补记标记，否则赠送一个。
2. `ModEntry.OnButtonPressed` 与 `UpdateAndroidLongPress` 处理开菜单输入；Android 使用长按，其他平台使用普通确认输入。
3. `ModEntry.CreateFusionMenu` 选择界面：Android，或 UI 视口任一边小于 `1064×768` 时使用 `MobileFusionMenu`，否则使用 `FusionMenu`。
4. 两种菜单都只选择近战武器，并通过 `FusedWeaponData` 把一把武器的战斗属性、附魔类型身份和等级写入戒指 `modData`。
5. `RingCombatManager.Update` 从已装备戒指读取熔铸签名；签名变化时刷新战斗属性和临时熔铸武器缓存，冷却结束且范围内存在存活怪物时执行一次 360 度光环攻击，并在本次攻击内切换该武器、注册附魔以触发原版伤害与击杀回调。

## 状态与不变量

- 熔铸状态只存放在戒指自身、前缀为 `xixifu.AetherTrinket/` 的 `modData` 中；战斗缓存按数据签名失效，不能依赖物品对象引用。
- 武器返回按原版复制语义逐项恢复附魔类型及等级，不使用会合并锻造或替换主附魔的 `AddEnchantment`；背包已满时把武器掉落到玩家位置。
- 旧 `EnchantmentIds` 数据没有等级，只能按一级返还并明确警告；新数据在返还前完成验证和旧武器重建，写入失败回滚，不能覆盖旧熔铸状态或消耗新武器。
- 熔铸数据解析失败时清空战斗缓存，禁止沿用上一枚戒指的光环数据。
- 基础攻击半径是 `80 + AreaOfEffect × 16` 像素，再乘范围配置；基础间隔按匕首、锤、其他武器分别为 250、500、400 毫秒，再按速度和冷却配置调整，最终不少于 100 毫秒。
- 没有命中目标时不消耗已积累冷却；卡顿造成的过量积累不会触发同帧连发。

## 界面分工

- `FusionMenu` 负责桌面布局。当前界面无标题和装饰标题纹理，空槽显示“+”。
- `MobileFusionMenu` 保存紧凑界面状态；`MobileFusionMenu.Rendering` 与 `MobileFusionMenu.Interaction` 分别负责绘制和命中处理。
- 绘制矩形与点击/触控命中必须来自同一组组件边界。紧凑界面直接显示武器名并截断溢出文本。

## 依赖

- 编译与打包：`Pathoschild.Stardew.ModBuildConfig` 4.x。
- 运行：SMAPI、Stardew Valley 与 XNA/MonoGame 类型。
- 可选运行集成：Generic Mod Config Menu。

## 决定与历史线索

- 附魔等级格式、旧数据边界和返还事务顺序见[保留附魔等级并按复制语义返还武器](../../requirements/adrs/0001-preserve-fused-weapon-enchantment-levels.md)。
- 桌面放大布局、无标题调整和移动布局的原因、替代关系与未完成验证见[熔铸菜单布局演进](../../history/ui-layout-evolution.md)。
