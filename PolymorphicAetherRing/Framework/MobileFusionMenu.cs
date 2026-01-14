using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using StardewModdingAPI;
using StardewValley;
using StardewValley.Menus;
using StardewValley.Tools;
using StardewValley.Enchantments;
using System;
using System.Collections.Generic;
using System.Linq;

namespace PolymorphicAetherRing.Framework;

/// <summary>安卓端简化熔铸面板 - 左右布局，更大触控区域</summary>
public class MobileFusionMenu : IClickableMenu
{
    private readonly Item _trinket;
    private readonly IModHelper _helper;
    private readonly IMonitor _monitor;
    private readonly ModConfig _config;
    
    // 状态
    private MeleeWeapon? _slottedWeapon;
    private FusedWeaponData? _currentFusion;
    
    // 可熔铸武器列表
    private List<MeleeWeapon> _availableWeapons = new();
    private int _scrollOffset = 0;
    private const int MaxVisibleWeapons = 5;
    
    // UI元素边界
    private Rectangle _leftPanelBounds;
    private Rectangle _rightPanelBounds;
    private Rectangle _weaponSlotBounds;
    private Rectangle _fuseButtonBounds;
    private Rectangle _closeButtonBounds;
    private List<Rectangle> _weaponListBounds = new();
    private Rectangle _scrollUpBounds;
    private Rectangle _scrollDownBounds;
    
    // 悬停状态
    private int _hoveringWeaponIndex = -1;
    private bool _hoveringFuseButton;
    private bool _hoveringWeaponSlot;
    private bool _hoveringCloseButton;
    
    private Texture2D _trinketTexture;

    public MobileFusionMenu(Item trinket, IModHelper helper, IMonitor monitor, ModConfig config)
        : base(0, 0, 0, 0, showUpperRightCloseButton: false)
    {
        _trinket = trinket;
        _helper = helper;
        _monitor = monitor;
        _config = config;
        
        // 加载纹理
        _trinketTexture = _helper.ModContent.Load<Texture2D>("assets/trinket.png");
        
        // 读取当前熔铸状态
        _currentFusion = FusedWeaponData.FromModData(trinket);
        
        // 收集可熔铸的武器
        CollectAvailableWeapons();
        
        // 初始化布局
        InitializeLayout();
    }

    /// <summary>收集玩家所有可熔铸的武器</summary>
    private void CollectAvailableWeapons()
    {
        _availableWeapons.Clear();
        
        // 从背包和工具栏收集所有近战武器
        foreach (var item in Game1.player.Items)
        {
            if (item is MeleeWeapon weapon)
            {
                _availableWeapons.Add(weapon);
            }
        }
    }

    private void InitializeLayout()
    {
        // 全屏布局，留出边距
        int margin = 32;
        int screenWidth = Game1.uiViewport.Width;
        int screenHeight = Game1.uiViewport.Height;
        
        // 窗口尺寸 - 适配手机屏幕
        int menuWidth = Math.Min(800, screenWidth - margin * 2);
        int menuHeight = Math.Min(480, screenHeight - margin * 2);
        
        this.width = menuWidth;
        this.height = menuHeight;
        this.xPositionOnScreen = (screenWidth - menuWidth) / 2;
        this.yPositionOnScreen = (screenHeight - menuHeight) / 2;
        
        // 左右分栏（各占50%）
        int panelWidth = (menuWidth - 32) / 2; // 32为中间间距
        int panelHeight = menuHeight - 32; // 上下各16边距
        
        _leftPanelBounds = new Rectangle(
            xPositionOnScreen + 16,
            yPositionOnScreen + 16,
            panelWidth,
            panelHeight
        );
        
        _rightPanelBounds = new Rectangle(
            xPositionOnScreen + 16 + panelWidth + 16,
            yPositionOnScreen + 16,
            panelWidth,
            panelHeight
        );
        
        // === 左侧面板布局 ===
        // 关闭按钮（左上角）
        _closeButtonBounds = new Rectangle(
            _leftPanelBounds.X,
            _leftPanelBounds.Y,
            64,
            64
        );
        
        // 武器插槽（居中偏上）
        int slotSize = 80;
        _weaponSlotBounds = new Rectangle(
            _leftPanelBounds.X + (_leftPanelBounds.Width - slotSize) / 2,
            _leftPanelBounds.Y + 80,
            slotSize,
            slotSize
        );
        
        // 熔铸按钮（底部）
        int btnWidth = Math.Min(160, _leftPanelBounds.Width - 32);
        int btnHeight = 64;
        _fuseButtonBounds = new Rectangle(
            _leftPanelBounds.X + (_leftPanelBounds.Width - btnWidth) / 2,
            _leftPanelBounds.Bottom - btnHeight - 16,
            btnWidth,
            btnHeight
        );
        
        // === 右侧面板布局 ===
        // 武器列表项
        _weaponListBounds.Clear();
        int itemHeight = 72;
        int itemSpacing = 8;
        int listStartY = _rightPanelBounds.Y + 48; // 留出滚动按钮空间
        
        for (int i = 0; i < MaxVisibleWeapons; i++)
        {
            _weaponListBounds.Add(new Rectangle(
                _rightPanelBounds.X + 8,
                listStartY + i * (itemHeight + itemSpacing),
                _rightPanelBounds.Width - 16,
                itemHeight
            ));
        }
        
        // 滚动按钮
        int scrollBtnSize = 40;
        _scrollUpBounds = new Rectangle(
            _rightPanelBounds.X + (_rightPanelBounds.Width - scrollBtnSize) / 2,
            _rightPanelBounds.Y + 4,
            scrollBtnSize,
            scrollBtnSize
        );
        
        _scrollDownBounds = new Rectangle(
            _rightPanelBounds.X + (_rightPanelBounds.Width - scrollBtnSize) / 2,
            _rightPanelBounds.Bottom - scrollBtnSize - 4,
            scrollBtnSize,
            scrollBtnSize
        );
    }

    public override void gameWindowSizeChanged(Rectangle oldBounds, Rectangle newBounds)
    {
        base.gameWindowSizeChanged(oldBounds, newBounds);
        InitializeLayout();
    }

    public override void draw(SpriteBatch b)
    {
        // 1. 绘制半透明背景遮罩
        b.Draw(Game1.fadeToBlackRect, Game1.graphics.GraphicsDevice.Viewport.Bounds, Color.Black * 0.75f);
        
        // 2. 绘制主面板背景
        Game1.drawDialogueBox(
            xPositionOnScreen - 16,
            yPositionOnScreen - 16,
            width + 32,
            height + 32,
            speaker: false,
            drawOnlyBox: true
        );
        
        // === 左侧面板 ===
        DrawLeftPanel(b);
        
        // === 右侧面板 ===
        DrawRightPanel(b);
        
        // 绘制鼠标
        drawMouse(b);
    }

    private void DrawLeftPanel(SpriteBatch b)
    {
        // 关闭按钮
        var closeBtnColor = _hoveringCloseButton ? Color.Red : Color.White;
        IClickableMenu.drawTextureBox(
            b,
            Game1.menuTexture,
            new Rectangle(0, 256, 60, 60),
            _closeButtonBounds.X,
            _closeButtonBounds.Y,
            _closeButtonBounds.Width,
            _closeButtonBounds.Height,
            closeBtnColor,
            1f,
            false
        );
        // X 符号
        string closeText = "X";
        var closeTextPos = new Vector2(
            _closeButtonBounds.X + (_closeButtonBounds.Width - Game1.dialogueFont.MeasureString(closeText).X) / 2,
            _closeButtonBounds.Y + (_closeButtonBounds.Height - Game1.dialogueFont.MeasureString(closeText).Y) / 2
        );
        Utility.drawTextWithShadow(b, closeText, Game1.dialogueFont, closeTextPos, 
            _hoveringCloseButton ? Color.White : Game1.textColor);
        
        // 当前熔铸状态（插槽上方）
        if (_currentFusion != null && _currentFusion.IsValid)
        {
            string info = _helper.Translation.Get("menu.fusion.current_fusion", new { weaponName = _currentFusion.WeaponName });
            var infoSize = Game1.smallFont.MeasureString(info);
            // 限制宽度
            if (infoSize.X > _leftPanelBounds.Width - 16)
            {
                info = _currentFusion.WeaponName;
                infoSize = Game1.smallFont.MeasureString(info);
            }
            Utility.drawTextWithShadow(
                b,
                info,
                Game1.smallFont,
                new Vector2(_leftPanelBounds.X + (_leftPanelBounds.Width - infoSize.X) / 2, _weaponSlotBounds.Y - 28),
                Color.LimeGreen
            );
        }
        
        // 武器插槽
        IClickableMenu.drawTextureBox(
            b,
            Game1.menuTexture,
            new Rectangle(0, 256, 60, 60),
            _weaponSlotBounds.X,
            _weaponSlotBounds.Y,
            _weaponSlotBounds.Width,
            _weaponSlotBounds.Height,
            _hoveringWeaponSlot ? Color.LightGoldenrodYellow : Color.White,
            1f,
            false
        );
        
        // 绘制插槽中的武器
        if (_slottedWeapon != null)
        {
            _slottedWeapon.drawInMenu(b, new Vector2(_weaponSlotBounds.X + 8, _weaponSlotBounds.Y + 8), 1f);
        }
        else
        {
            // 空槽提示
            string emptyHint = "?";
            var hintPos = new Vector2(
                _weaponSlotBounds.X + (_weaponSlotBounds.Width - Game1.dialogueFont.MeasureString(emptyHint).X) / 2,
                _weaponSlotBounds.Y + (_weaponSlotBounds.Height - Game1.dialogueFont.MeasureString(emptyHint).Y) / 2
            );
            Utility.drawTextWithShadow(b, emptyHint, Game1.dialogueFont, hintPos, Color.Gray);
        }
        
        // 熔铸按钮
        bool canFuse = _slottedWeapon != null;
        var btnColor = canFuse ? (_hoveringFuseButton ? Color.LightGreen : Color.White) : Color.Gray;
        
        IClickableMenu.drawTextureBox(
            b,
            Game1.menuTexture,
            new Rectangle(0, 256, 60, 60),
            _fuseButtonBounds.X,
            _fuseButtonBounds.Y,
            _fuseButtonBounds.Width,
            _fuseButtonBounds.Height,
            btnColor,
            1f,
            true
        );
        
        string btnText = _helper.Translation.Get("menu.fusion.fuse_button");
        var btnTextSize = Game1.dialogueFont.MeasureString(btnText);
        Utility.drawTextWithShadow(
            b,
            btnText,
            Game1.dialogueFont,
            new Vector2(
                _fuseButtonBounds.X + (_fuseButtonBounds.Width - btnTextSize.X) / 2,
                _fuseButtonBounds.Y + (_fuseButtonBounds.Height - btnTextSize.Y) / 2
            ),
            canFuse ? Game1.textColor : Color.DarkGray
        );
    }

    private void DrawRightPanel(SpriteBatch b)
    {
        // 滚动按钮（上）
        if (_scrollOffset > 0)
        {
            IClickableMenu.drawTextureBox(
                b, Game1.menuTexture, new Rectangle(0, 256, 60, 60),
                _scrollUpBounds.X, _scrollUpBounds.Y, _scrollUpBounds.Width, _scrollUpBounds.Height,
                Color.White, 1f, false
            );
            Utility.drawTextWithShadow(b, "▲", Game1.smallFont,
                new Vector2(_scrollUpBounds.X + 10, _scrollUpBounds.Y + 8), Game1.textColor);
        }
        
        // 滚动按钮（下）
        if (_scrollOffset + MaxVisibleWeapons < _availableWeapons.Count)
        {
            IClickableMenu.drawTextureBox(
                b, Game1.menuTexture, new Rectangle(0, 256, 60, 60),
                _scrollDownBounds.X, _scrollDownBounds.Y, _scrollDownBounds.Width, _scrollDownBounds.Height,
                Color.White, 1f, false
            );
            Utility.drawTextWithShadow(b, "▼", Game1.smallFont,
                new Vector2(_scrollDownBounds.X + 10, _scrollDownBounds.Y + 8), Game1.textColor);
        }
        
        // 绘制武器列表
        for (int i = 0; i < MaxVisibleWeapons && i + _scrollOffset < _availableWeapons.Count; i++)
        {
            var weapon = _availableWeapons[i + _scrollOffset];
            var bounds = _weaponListBounds[i];
            
            bool isHovering = _hoveringWeaponIndex == i;
            bool isSlotted = _slottedWeapon == weapon;
            
            var itemColor = isSlotted ? Color.DarkGray : (isHovering ? Color.LightGoldenrodYellow : Color.White);
            
            // 背景
            IClickableMenu.drawTextureBox(
                b, Game1.menuTexture, new Rectangle(0, 256, 60, 60),
                bounds.X, bounds.Y, bounds.Width, bounds.Height,
                itemColor, 1f, false
            );
            
            // 武器图标
            weapon.drawInMenu(b, new Vector2(bounds.X + 4, bounds.Y + 4), 1f);
            
            // 武器名称
            string weaponName = weapon.DisplayName;
            var nameSize = Game1.smallFont.MeasureString(weaponName);
            // 截断过长名称
            if (nameSize.X > bounds.Width - 80)
            {
                while (weaponName.Length > 3 && Game1.smallFont.MeasureString(weaponName + "...").X > bounds.Width - 80)
                {
                    weaponName = weaponName.Substring(0, weaponName.Length - 1);
                }
                weaponName += "...";
            }
            
            Utility.drawTextWithShadow(
                b, weaponName, Game1.smallFont,
                new Vector2(bounds.X + 72, bounds.Y + (bounds.Height - Game1.smallFont.MeasureString(weaponName).Y) / 2),
                isSlotted ? Color.Gray : Game1.textColor
            );
        }
        
        // 无武器提示
        if (_availableWeapons.Count == 0)
        {
            string noWeapons = _helper.Translation.Get("menu.fusion.no_weapons");
            var noWeaponsSize = Game1.smallFont.MeasureString(noWeapons);
            Utility.drawTextWithShadow(
                b, noWeapons, Game1.smallFont,
                new Vector2(
                    _rightPanelBounds.X + (_rightPanelBounds.Width - noWeaponsSize.X) / 2,
                    _rightPanelBounds.Y + (_rightPanelBounds.Height - noWeaponsSize.Y) / 2
                ),
                Color.Gray
            );
        }
    }

    public override void performHoverAction(int x, int y)
    {
        base.performHoverAction(x, y);
        
        _hoveringCloseButton = _closeButtonBounds.Contains(x, y);
        _hoveringWeaponSlot = _weaponSlotBounds.Contains(x, y);
        _hoveringFuseButton = _fuseButtonBounds.Contains(x, y);
        
        // 检查武器列表悬停
        _hoveringWeaponIndex = -1;
        for (int i = 0; i < _weaponListBounds.Count && i + _scrollOffset < _availableWeapons.Count; i++)
        {
            if (_weaponListBounds[i].Contains(x, y))
            {
                _hoveringWeaponIndex = i;
                break;
            }
        }
    }

    public override void receiveLeftClick(int x, int y, bool playSound = true)
    {
        // 关闭按钮
        if (_closeButtonBounds.Contains(x, y))
        {
            exitThisMenu();
            Game1.playSound("bigDeSelect");
            return;
        }
        
        // 滚动按钮
        if (_scrollUpBounds.Contains(x, y) && _scrollOffset > 0)
        {
            _scrollOffset--;
            Game1.playSound("shwip");
            return;
        }
        
        if (_scrollDownBounds.Contains(x, y) && _scrollOffset + MaxVisibleWeapons < _availableWeapons.Count)
        {
            _scrollOffset++;
            Game1.playSound("shwip");
            return;
        }
        
        // 武器列表点击
        for (int i = 0; i < _weaponListBounds.Count && i + _scrollOffset < _availableWeapons.Count; i++)
        {
            if (_weaponListBounds[i].Contains(x, y))
            {
                var weapon = _availableWeapons[i + _scrollOffset];
                
                // 如果已在插槽中则跳过
                if (_slottedWeapon == weapon)
                {
                    Game1.playSound("cancel");
                    return;
                }
                
                // 从背包移除
                int itemIndex = Game1.player.Items.IndexOf(weapon);
                if (itemIndex >= 0)
                {
                    Game1.player.Items[itemIndex] = null;
                }
                
                // 如果插槽已有武器，放回背包
                if (_slottedWeapon != null)
                {
                    if (itemIndex >= 0)
                    {
                        Game1.player.Items[itemIndex] = _slottedWeapon;
                    }
                    else
                    {
                        Game1.player.addItemToInventory(_slottedWeapon);
                    }
                }
                
                // 装入插槽
                _slottedWeapon = weapon;
                
                // 刷新可用武器列表
                CollectAvailableWeapons();
                
                Game1.playSound("stoneStep");
                _monitor.Log($"Selected weapon: {weapon.DisplayName}", LogLevel.Debug);
                return;
            }
        }
        
        // 武器插槽点击（取出武器）
        if (_weaponSlotBounds.Contains(x, y) && _slottedWeapon != null)
        {
            Game1.player.addItemToInventory(_slottedWeapon);
            _slottedWeapon = null;
            CollectAvailableWeapons();
            Game1.playSound("coin");
            return;
        }
        
        // 熔铸按钮
        if (_fuseButtonBounds.Contains(x, y) && _slottedWeapon != null)
        {
            PerformFusion();
            return;
        }
    }

    public override void receiveScrollWheelAction(int direction)
    {
        if (direction > 0 && _scrollOffset > 0)
        {
            _scrollOffset--;
            Game1.playSound("shiny4");
        }
        else if (direction < 0 && _scrollOffset + MaxVisibleWeapons < _availableWeapons.Count)
        {
            _scrollOffset++;
            Game1.playSound("shiny4");
        }
    }

    private void PerformFusion()
    {
        if (_slottedWeapon == null) return;

        // 0. 检查是否需要返还旧武器
        if (_config.ReturnFusedWeapon && _currentFusion != null && _currentFusion.IsValid)
        {
            try
            {
                Item oldWeapon = ItemRegistry.Create(_currentFusion.WeaponId);
                
                // 恢复附魔
                if (oldWeapon is Tool tool && _currentFusion.EnchantmentIds.Count > 0)
                {
                    foreach (var enchantName in _currentFusion.EnchantmentIds)
                    {
                        Type? type = null;
                        var svAssembly = typeof(Game1).Assembly;
                        type = svAssembly.GetTypes().FirstOrDefault(t => t.Name == enchantName && typeof(BaseEnchantment).IsAssignableFrom(t));
                        
                        if (type == null)
                        {
                            foreach (var asm in AppDomain.CurrentDomain.GetAssemblies())
                            {
                                try 
                                {
                                    type = asm.GetTypes().FirstOrDefault(t => t.Name == enchantName && typeof(BaseEnchantment).IsAssignableFrom(t));
                                    if (type != null) break;
                                }
                                catch { }
                            }
                        }
                        
                        if (type != null)
                        {
                            try 
                            {
                                if (Activator.CreateInstance(type) is BaseEnchantment enchantment)
                                {
                                    tool.enchantments.Add(enchantment);
                                }
                            }
                            catch (Exception ex)
                            { 
                                _monitor.Log($"Failed to restore enchantment '{enchantName}': {ex.Message}", LogLevel.Warn);
                            }
                        }
                    }
                }

                var added = Game1.player.addItemToInventory(oldWeapon);
                
                if (added == null)
                {
                    Game1.showGlobalMessage(_helper.Translation.Get("menu.fusion.returned", new { weaponName = _currentFusion.WeaponName }));
                }
                else
                {
                    Game1.createItemDebris(oldWeapon, Game1.player.getStandingPosition(), -1);
                    Game1.showGlobalMessage(_helper.Translation.Get("menu.fusion.inventory_full", new { weaponName = _currentFusion.WeaponName }));
                }
            }
            catch (Exception ex)
            {
                _monitor.Log($"Failed to return old weapon ({_currentFusion.WeaponName}): {ex}", LogLevel.Error);
                Game1.showRedMessage(_helper.Translation.Get("menu.fusion.error.return_failed"));
            }
        }

        // 1. 提取数据
        var fusionData = FusedWeaponData.FromWeapon(_slottedWeapon);
        
        // 2. 保存进饰品
        fusionData.SaveToModData(_trinket);
        _currentFusion = fusionData;

        // 3. 特效与音效
        Game1.playSound("furnace");
        Game1.playSound("powerup");
        _monitor.Log($"Fused: {fusionData.WeaponName}", LogLevel.Info);
        
        // 4. 消耗武器
        _slottedWeapon = null;
        
        // 5. 刷新列表
        CollectAvailableWeapons();
        
        // 6. 反馈
        Game1.showGlobalMessage(_helper.Translation.Get("menu.fusion.success"));
    }

    protected override void cleanupBeforeExit()
    {
        // 菜单关闭时，如果插槽有武器，返还给玩家
        if (_slottedWeapon != null)
        {
            Game1.player.addItemByMenuIfNecessary(_slottedWeapon);
            _slottedWeapon = null;
        }

        base.cleanupBeforeExit();
    }
}
