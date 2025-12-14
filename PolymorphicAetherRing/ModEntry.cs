using StardewModdingAPI;
using StardewModdingAPI.Events;
using StardewValley;
using StardewValley.Menus;
using PolymorphicAetherRing.Framework;

namespace PolymorphicAetherRing;

/// <summary>模组入口类</summary>
public class ModEntry : Mod
{
    /// <summary>饰品的物品ID</summary>
    public const string TrinketId = "xixifu.AetherTrinket";
    
    /// <summary>完整物品ID（用于物品注册表）</summary>
    public const string QualifiedTrinketId = "(TR)" + TrinketId;
    
    /// <summary>战斗管理器</summary>
    private TrinketCombatManager? _combatManager;

    public override void Entry(IModHelper helper)
    {
        // 注册事件
        helper.Events.Content.AssetRequested += OnAssetRequested;
        helper.Events.GameLoop.SaveLoaded += OnSaveLoaded;
        helper.Events.GameLoop.UpdateTicked += OnUpdateTicked;
        helper.Events.Input.ButtonPressed += OnButtonPressed;
        
        Monitor.Log("Polymorphic Aether Trinket mod loaded!", LogLevel.Info);
    }

    /// <summary>注册饰品数据资产</summary>
    private void OnAssetRequested(object? sender, AssetRequestedEventArgs e)
    {
        // 注册饰品数据
        if (e.NameWithoutLocale.IsEquivalentTo("Data/Trinkets"))
        {
            e.Edit(asset =>
            {
                // Robust type search across all loaded assemblies
                Type? trinketDataType = null;
                foreach (var assembly in AppDomain.CurrentDomain.GetAssemblies())
                {
                    // Update preferred namespace based on logs
                    trinketDataType = assembly.GetType("StardewValley.GameData.TrinketData"); 
                    if (trinketDataType == null)
                        trinketDataType = assembly.GetType("StardewValley.GameData.Trinkets.TrinketData");

                    if (trinketDataType != null)
                        break;
                }
                
                if (trinketDataType == null)
                {
                    Monitor.Log("Could not find TrinketData type. Searching by name...", LogLevel.Warn);
                    // Fallback: search by name only
                    foreach (var assembly in AppDomain.CurrentDomain.GetAssemblies())
                    {
                        foreach (var type in assembly.GetTypes())
                        {
                            if (type.Name == "TrinketData")
                            {
                                trinketDataType = type;
                                Monitor.Log($"Found TrinketData type: {type.FullName} in {assembly.FullName}", LogLevel.Info);
                                break;
                            }
                        }
                        if (trinketDataType != null) break;
                    }
                }

                if (trinketDataType == null)
                {
                    Monitor.Log("Could not find TrinketData type in any assembly.", LogLevel.Error);
                    return;
                }
                
                // Use reflection to get the generic dictionary type
                var getData = asset.GetType().GetMethod("GetData")!.MakeGenericMethod(
                    typeof(Dictionary<,>).MakeGenericType(typeof(string), trinketDataType)
                );
                var dataDict = getData.Invoke(asset, null);
                
                // Create TrinketData instance using reflection/dynamic
                dynamic trinketData = Activator.CreateInstance(trinketDataType)!;
                // trinketData.Id = TrinketId; // Error: 'StardewValley.GameData.TrinketData' does not contain a definition for 'Id'
                trinketData.DisplayName = "以太多态饰品";
                trinketData.Description = "一个嗡嗡作响的神秘装置，似乎渴望吞噬武器的灵魂。";
                trinketData.Texture = Helper.ModContent.GetInternalAssetName("assets/trinket").Name;
                trinketData.SheetIndex = 0;
                trinketData.TrinketEffectClass = "StardewValley.Objects.Trinkets.TrinketEffect, Stardew Valley";
                trinketData.DropsNaturally = false;
                
                // Add to dictionary
                var addMethod = dataDict!.GetType().GetProperty("Item")!;
                addMethod.SetValue(dataDict, trinketData, new object[] { TrinketId });
            });
        }
        
        // 注册本地化字符串
        if (e.NameWithoutLocale.IsEquivalentTo("Strings/Objects"))
        {
            e.Edit(asset =>
            {
                var data = asset.AsDictionary<string, string>();
                data.Data["AetherTrinket_Name"] = "以太多态饰品";
                data.Data["AetherTrinket_Description"] = "一个嗡嗡作响的神秘装置，似乎渴望吞噬武器的灵魂。";
            });
        }
    }

    /// <summary>存档加载时赠送饰品</summary>
    private void OnSaveLoaded(object? sender, SaveLoadedEventArgs e)
    {
        _combatManager = new TrinketCombatManager(Helper, Monitor);
        
        // 检查玩家是否已拥有饰品（检查背包和饰品槽）
        var player = Game1.player;
        bool hasTrinket = false;
        
        // 检查背包
        foreach (var item in player.Items)
        {
            if (item?.QualifiedItemId == QualifiedTrinketId)
            {
                hasTrinket = true;
                break;
            }
        }
        
        // 检查饰品槽
        if (!hasTrinket)
        {
            foreach (var trinket in player.trinketItems)
            {
                if (trinket?.QualifiedItemId == QualifiedTrinketId)
                {
                    hasTrinket = true;
                    break;
                }
            }
        }
        
        if (!hasTrinket)
        {
            try
            {
                var trinket = ItemRegistry.Create(QualifiedTrinketId);
                player.addItemByMenuIfNecessary(trinket);
                Monitor.Log("Granted Polymorphic Aether Trinket to player.", LogLevel.Info);
            }
            catch (Exception ex)
            {
                Monitor.Log($"Failed to create trinket: {ex.Message}", LogLevel.Error);
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
        
        // 检查当前手持物品是否为我们的饰品
        if (currentItem == null)
            return;
            
        if (currentItem.QualifiedItemId != QualifiedTrinketId)
            return;

        // 拦截默认行为并打开熔铸菜单
        Helper.Input.Suppress(e.Button);
        Game1.activeClickableMenu = new FusionMenu(currentItem, Helper, Monitor);
        Monitor.Log("Opened Fusion Menu", LogLevel.Debug);
    }
}
