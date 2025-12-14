using StardewModdingAPI;
using StardewModdingAPI.Events;
using StardewValley;
using StardewValley.Menus;
using PolymorphicAetherRing.Framework;

namespace PolymorphicAetherRing;

/// <summary>模组入口类</summary>
public class ModEntry : Mod
{
    /// <summary>戒指的物品ID</summary>
    public const string RingId = "xixifu.AetherRing";
    
    /// <summary>完整物品ID（用于物品注册表）</summary>
    public const string QualifiedRingId = "(O)" + RingId;
    
    /// <summary>战斗管理器</summary>
    private RingCombatManager? _combatManager;

    /// <summary>配置项</summary>
    public ModConfig Config { get; private set; } = new();

    public override void Entry(IModHelper helper)
    {
        // 1. 读取配置
        Config = helper.ReadConfig<ModConfig>();
        
        // 2. 注册事件
        helper.Events.Content.AssetRequested += OnAssetRequested;
        helper.Events.GameLoop.GameLaunched += OnGameLaunched;
        helper.Events.GameLoop.SaveLoaded += OnSaveLoaded;
        helper.Events.GameLoop.UpdateTicked += OnUpdateTicked;
        helper.Events.Input.ButtonPressed += OnButtonPressed;
        
        Monitor.Log("Polymorphic Aether Ring mod loaded!", LogLevel.Info);
    }

    /// <summary>游戏启动时注册GMCM</summary>
    private void OnGameLaunched(object? sender, GameLaunchedEventArgs e)
    {
        // 获取 GMCM API
        var configMenu = Helper.ModRegistry.GetApi<IGenericModConfigMenuApi>("spacechase0.GenericModConfigMenu");
        if (configMenu == null)
        {
            Monitor.Log("Generic Mod Config Menu not found or API mismatch.", LogLevel.Debug);
            return;
        }

        // 注册模组配置
        configMenu.Register(
            mod: ModManifest,
            reset: () => Config = new ModConfig(),
            save: () => Helper.WriteConfig(Config)
        );

        // 添加配置项
        configMenu.AddSectionTitle(ModManifest, () => Helper.Translation.Get("config.combat_settings"));

        configMenu.AddNumberOption(
            mod: ModManifest,
            name: () => Helper.Translation.Get("config.damage_multiplier"),
            tooltip: () => Helper.Translation.Get("config.damage_multiplier.tooltip"),
            getValue: () => Config.DamageMultiplier,
            setValue: value => Config.DamageMultiplier = value,
            min: 0.1f,
            max: 5.0f,
            interval: 0.1f
        );

        configMenu.AddNumberOption(
            mod: ModManifest,
            name: () => Helper.Translation.Get("config.range_multiplier"),
            tooltip: () => Helper.Translation.Get("config.range_multiplier.tooltip"),
            getValue: () => Config.RangeMultiplier,
            setValue: value => Config.RangeMultiplier = value,
            min: 0.5f,
            max: 3.0f,
            interval: 0.1f
        );
        
        configMenu.AddNumberOption(
            mod: ModManifest,
            name: () => Helper.Translation.Get("config.cooldown_multiplier"),
            tooltip: () => Helper.Translation.Get("config.cooldown_multiplier.tooltip"),
            getValue: () => Config.CooldownMultiplier,
            setValue: value => Config.CooldownMultiplier = value,
            min: 0.1f,
            max: 2.0f,
            interval: 0.1f
        );
        
        configMenu.AddSectionTitle(ModManifest, () => Helper.Translation.Get("config.fusion_settings"));
        
        configMenu.AddBoolOption(
            mod: ModManifest,
            name: () => Helper.Translation.Get("config.return_fused_weapon"),
            tooltip: () => Helper.Translation.Get("config.return_fused_weapon.tooltip"),
            getValue: () => Config.ReturnFusedWeapon,
            setValue: value => Config.ReturnFusedWeapon = value
        );
    }

    /// <summary>注册戒指数据资产</summary>
    private void OnAssetRequested(object? sender, AssetRequestedEventArgs e)
    {
        // 注册戒指数据
        if (e.NameWithoutLocale.IsEquivalentTo("Data/Objects"))
        {
            e.Edit(asset =>
            {
                var data = asset.AsDictionary<string, StardewValley.GameData.Objects.ObjectData>();
                
                // 创建戒指数据
                var ringData = new StardewValley.GameData.Objects.ObjectData
                {
                    Name = RingId,
                    DisplayName = Helper.Translation.Get("item.ring.name"),
                    Description = Helper.Translation.Get("item.ring.description"),
                    Type = "Ring",
                    Category = StardewValley.Object.ringCategory,
                    Price = 5000,
                    Texture = Helper.ModContent.GetInternalAssetName("assets/trinket").Name,
                    SpriteIndex = 0
                };
                
                data.Data[RingId] = ringData;
                Monitor.Log($"Registered ring: {RingId}", LogLevel.Debug);
            });
        }
        
        // 注册本地化字符串（可选，已在ObjectData中定义）
        if (e.NameWithoutLocale.IsEquivalentTo("Strings/Objects"))
        {
            e.Edit(asset =>
            {
                var data = asset.AsDictionary<string, string>();
                data.Data["AetherRing_Name"] = Helper.Translation.Get("item.ring.name");
                data.Data["AetherRing_Description"] = Helper.Translation.Get("item.ring.description");
            });
        }
    }

    /// <summary>存档加载时赠送戒指</summary>
    private void OnSaveLoaded(object? sender, SaveLoadedEventArgs e)
    {
        // 传递 Config
        _combatManager = new RingCombatManager(Helper, Monitor, Config);
        
        var player = Game1.player;
        string mailFlag = "xixifu.AetherRing_Received";
        // ... (remaining gifting logic logic unchanged) ...
        
        // 如果已经收到过（有标记），直接返回
        if (player.hasOrWillReceiveMail(mailFlag))
        {
            return;
        }
        
        // 深度检查玩家是否已拥有戒指（包括组合戒指的情况）
        bool hasRing = IsRingOwned(player);
        
        if (hasRing)
        {
            // 补上标记
            player.mailReceived.Add(mailFlag);
            Monitor.Log("Legacy player detected (Checked deep storage): Added missing mail flag for Aether Ring.", LogLevel.Info);
        }
        else
        {
            // 真正的新玩家：赠送戒指并添加标记
            try
            {
                var ring = ItemRegistry.Create(QualifiedRingId);
                player.addItemByMenuIfNecessary(ring);
                player.mailReceived.Add(mailFlag);
                Monitor.Log("Granted Polymorphic Aether Ring to player.", LogLevel.Info);
            }
            catch (Exception ex)
            {
                Monitor.Log($"Failed to create ring: {ex.Message}", LogLevel.Error);
            }
        }
    }

    /// <summary>检查玩家是否拥有戒指（递归检查组合戒指）</summary>
    private bool IsRingOwned(Farmer player)
    {
        // 1. 检查背包
        foreach (var item in player.Items)
        {
            if (item == null) continue;
            if (IsItemTargetRing(item)) return true;
        }
        
        // 2. 检查装备槽
        if (IsItemTargetRing(player.leftRing.Value)) return true;
        if (IsItemTargetRing(player.rightRing.Value)) return true;
        
        return false;
    }

    /// <summary>递归检查物品是否为目标戒指</summary>
    private bool IsItemTargetRing(Item? item)
    {
        if (item == null) return false;

        // 直接匹配
        if (item.QualifiedItemId == QualifiedRingId) return true;

        // 检查组合戒指
        if (item is StardewValley.Objects.CombinedRing combinedRing)
        {
            foreach (var child in combinedRing.combinedRings)
            {
                if (IsItemTargetRing(child)) return true;
            }
        }
        
        return false;
    }


    /// <summary>每帧更新战斗逻辑</summary>
    private void OnUpdateTicked(object? sender, UpdateTickedEventArgs e)
    {
        if (!Context.IsWorldReady)
            return;

        if (_combatManager == null)
        {
            // 防御性检查，理论上不应该发生
            if (e.IsMultipleOf(60)) Monitor.Log("_combatManager is null despite OnSaveLoaded!", LogLevel.Error);
            return;
        }
            
        _combatManager.Update();
    }

    /// <summary>监听按键打开熔铸面板</summary>
    private void OnButtonPressed(object? sender, ButtonPressedEventArgs e)
    {
        if (!Context.IsWorldReady || !Context.IsPlayerFree)
            return;

        // 检查是否按下左键或使用键
        if (e.Button != SButton.MouseLeft && e.Button != SButton.ControllerA)
            return;

        var player = Game1.player;
        var currentItem = player.CurrentItem;
        
        // 检查当前手持物品是否为我们的戒指
        if (currentItem == null)
            return;
            
        if (currentItem.QualifiedItemId != QualifiedRingId)
            return;

        // 拦截默认行为并打开熔铸菜单
        Helper.Input.Suppress(e.Button);
        // 传递 Config
        Game1.activeClickableMenu = new FusionMenu(currentItem, Helper, Monitor, Config);
        Monitor.Log("Opened Fusion Menu", LogLevel.Debug);
    }
}
