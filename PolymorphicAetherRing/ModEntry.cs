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

    public override void Entry(IModHelper helper)
    {
        // 注册事件
        helper.Events.Content.AssetRequested += OnAssetRequested;
        helper.Events.GameLoop.SaveLoaded += OnSaveLoaded;
        helper.Events.GameLoop.UpdateTicked += OnUpdateTicked;
        helper.Events.Input.ButtonPressed += OnButtonPressed;
        
        Monitor.Log("Polymorphic Aether Ring mod loaded!", LogLevel.Info);
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
                    DisplayName = "以太多态戒指",
                    Description = "一个嗡嗡作响的神秘装置，似乎渴望吞噬武器的灵魂。",
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
                data.Data["AetherRing_Name"] = "以太多态戒指";
                data.Data["AetherRing_Description"] = "一个嗡嗡作响的神秘装置，似乎渴望吞噬武器的灵魂。";
            });
        }
    }

    /// <summary>存档加载时赠送戒指</summary>
    private void OnSaveLoaded(object? sender, SaveLoadedEventArgs e)
    {
        _combatManager = new RingCombatManager(Helper, Monitor);
        
        // 检查玩家是否已拥有戒指（检查背包和戒指槽）
        var player = Game1.player;
        bool hasRing = false;
        
        // 检查背包
        foreach (var item in player.Items)
        {
            if (item?.QualifiedItemId == QualifiedRingId)
            {
                hasRing = true;
                break;
            }
        }
        
        // 检查戒指槽
        if (!hasRing)
        {
            if (player.leftRing.Value?.QualifiedItemId == QualifiedRingId ||
                player.rightRing.Value?.QualifiedItemId == QualifiedRingId)
            {
                hasRing = true;
            }
        }
        
        if (!hasRing)
        {
            try
            {
                var ring = ItemRegistry.Create(QualifiedRingId);
                player.addItemByMenuIfNecessary(ring);
                Monitor.Log("Granted Polymorphic Aether Ring to player.", LogLevel.Info);
            }
            catch (Exception ex)
            {
                Monitor.Log($"Failed to create ring: {ex.Message}", LogLevel.Error);
            }
        }
    }


    /// <summary>每帧更新战斗逻辑</summary>
    private void OnUpdateTicked(object? sender, UpdateTickedEventArgs e)
    {
        if (!Context.IsWorldReady || !Context.IsPlayerFree)
            return;
            
        _combatManager?.Update();
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
        Game1.activeClickableMenu = new FusionMenu(currentItem, Helper, Monitor);
        Monitor.Log("Opened Fusion Menu", LogLevel.Debug);
    }
}
