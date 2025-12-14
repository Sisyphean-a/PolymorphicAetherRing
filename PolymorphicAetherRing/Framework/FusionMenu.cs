using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using StardewModdingAPI;
using StardewValley;
using StardewValley.Menus;
using StardewValley.Tools;
using PolymorphicAetherRing;

namespace PolymorphicAetherRing.Framework;

/// <summary>熔铸面板UI - 允许玩家将武器熔铸进饰品</summary>
public class FusionMenu : IClickableMenu
{
    private readonly Item _trinket;
    private readonly IModHelper _helper;
    private readonly IMonitor _monitor;
    private readonly ModConfig _config;
    
    // UI Components
    private InventoryMenu _inventory = null!; // Suppress null warning as it is initialized in InitializeLayout
    
    // Layout
    private Rectangle _weaponSlotBounds;
    private Rectangle _fuseButtonBounds;
    
    // State
    private MeleeWeapon? _slottedWeapon;
    private FusedWeaponData? _currentFusion;
    private bool _hoveringFuseButton;
    private bool _hoveringWeaponSlot;
    
    private readonly string _hoverText = "";

    private Texture2D _trinketTexture;

    public FusionMenu(Item trinket, IModHelper helper, IMonitor monitor, ModConfig config)
        : base(
            (Game1.uiViewport.Width - 832) / 2,
            (Game1.uiViewport.Height - 576) / 2,
            832,
            576,
            showUpperRightCloseButton: true
        )
    {
        _trinket = trinket;
        _helper = helper;
        _monitor = monitor;
        _config = config;
        
        // 加载自定义纹理
        _trinketTexture = _helper.ModContent.Load<Texture2D>("assets/trinket.png");
        
        // 读取当前熔铸状态
        _currentFusion = FusedWeaponData.FromModData(trinket);
        
        // 初始化UI区域
        InitializeLayout();
    }

    private void InitializeLayout()
    {
        // ... (layout initialization code)
        // 1. 初始化库存菜单 (放置在窗口下半部分)
        // InventoryMenu 通常宽为 12 * 64 + 边距
        _inventory = new InventoryMenu(
            this.xPositionOnScreen + (this.width - (12 * 64)) / 2, // 居中
            this.yPositionOnScreen + this.height - (3 * 64) - 64,  // 底部留出空间
            playerInventory: true,
            highlightMethod: HighlightWeapons, // 只高亮武器
            capacity: 36,
            rows: 3
        );

        // 2. 武器插槽 (上半部分居中)
        int slotX = this.xPositionOnScreen + (this.width - 64) / 2;
        int slotY = this.yPositionOnScreen + 128; // 标题下方
        _weaponSlotBounds = new Rectangle(slotX, slotY, 64, 64);

        // 3. 熔铸按钮 (插槽下方)
        int btnWidth = 180;
        int btnHeight = 64;
        _fuseButtonBounds = new Rectangle(
            this.xPositionOnScreen + (this.width - btnWidth) / 2,
            slotY + 64 + 32, // 插槽下方 32px
            btnWidth,
            btnHeight
        );
    }
    
    /// <summary>高亮过滤：只允许近战武器</summary>
    private bool HighlightWeapons(Item item)
    {
        return item is MeleeWeapon;
    }
    
    public override void draw(SpriteBatch b)
    {
        // 1. 绘制黑色遮罩
        b.Draw(Game1.fadeToBlackRect, Game1.graphics.GraphicsDevice.Viewport.Bounds, Color.Black * 0.75f);

        // 2. 绘制主窗口背景
        Game1.drawDialogueBox(
            xPositionOnScreen, 
            yPositionOnScreen, 
            width, 
            height, 
            speaker: false, 
            drawOnlyBox: true
        );

        // 3. 绘制标题
        string title = _helper.Translation.Get("menu.fusion.title");
        Utility.drawTextWithShadow(
            b,
            title,
            Game1.dialogueFont,
            new Vector2(xPositionOnScreen + (width - Game1.dialogueFont.MeasureString(title).X) / 2, yPositionOnScreen + 32),
            Game1.textColor
        );

        // 3.1 绘制自定义饰品图标 (装饰)
        if (_trinketTexture != null)
        {
            // 绘制在标题左侧
            b.Draw(_trinketTexture, new Vector2(xPositionOnScreen + 64, yPositionOnScreen + 48), new Rectangle(0, 0, 16, 16), Color.White, 0f, Vector2.Zero, 4f, SpriteEffects.None, 0.89f);
            // 绘制在标题右侧 (对称)
            b.Draw(_trinketTexture, new Vector2(xPositionOnScreen + width - 64 - 64, yPositionOnScreen + 48), new Rectangle(0, 0, 16, 16), Color.White, 0f, Vector2.Zero, 4f, SpriteEffects.None, 0.89f);
        }

        // 4. 绘制当前熔铸信息
        if (_currentFusion != null && _currentFusion.IsValid)
        {
            string info = _helper.Translation.Get("menu.fusion.current_fusion", new { weaponName = _currentFusion.WeaponName });
            Utility.drawTextWithShadow(
                b,
                info,
                Game1.smallFont,
                new Vector2(xPositionOnScreen + (width - Game1.smallFont.MeasureString(info).X) / 2, yPositionOnScreen + 80),
                Color.LimeGreen
            );
        }

        // 5. 绘制武器插槽
        // 背景 (使用 drawTextureBox 替代 drawKibbleRect)
        IClickableMenu.drawTextureBox(
            b,
            Game1.menuTexture,
            new Rectangle(0, 256, 60, 60),
            _weaponSlotBounds.X,
            _weaponSlotBounds.Y,
            _weaponSlotBounds.Width,
            _weaponSlotBounds.Height,
            Color.White,
            1f,
            false
        );

        // 如果有物品，绘制物品
        if (_slottedWeapon != null)
        {
            _slottedWeapon.drawInMenu(b, new Vector2(_weaponSlotBounds.X, _weaponSlotBounds.Y), 1f);
        }
        else
        {
             // 提示文本或空槽视觉效果 (可选)
        }

        // 插槽高亮
        if (_hoveringWeaponSlot)
        {
            IClickableMenu.drawTextureBox(b, Game1.mouseCursors, new Rectangle(375, 357, 3, 3), _weaponSlotBounds.X, _weaponSlotBounds.Y, _weaponSlotBounds.Width, _weaponSlotBounds.Height, Color.White, 4f, false);
        }

        // 6. 绘制熔铸按钮
        bool canFuse = _slottedWeapon != null;
        var btnColor = canFuse ? (_hoveringFuseButton ? Color.Wheat : Color.White) : Color.Gray;
        
        // FIX: 使用标准菜单纹理 (Game1.menuTexture) 而不是不可靠的 mouseCursors 区域
        IClickableMenu.drawTextureBox(
            b, 
            Game1.menuTexture, 
            new Rectangle(0, 256, 60, 60), // 标准褐色背景
            _fuseButtonBounds.X, 
            _fuseButtonBounds.Y, 
            _fuseButtonBounds.Width, 
            _fuseButtonBounds.Height, 
            btnColor, 
            1f, 
            true
        );

        string btnText = _helper.Translation.Get("menu.fusion.fuse_button");
        Utility.drawTextWithShadow(
            b,
            btnText,
            Game1.dialogueFont,
            new Vector2(
                _fuseButtonBounds.X + (_fuseButtonBounds.Width - Game1.dialogueFont.MeasureString(btnText).X) / 2,
                _fuseButtonBounds.Y + (_fuseButtonBounds.Height - Game1.dialogueFont.MeasureString(btnText).Y) / 2 + 4 
            ),
            canFuse ? Game1.textColor : Color.DarkGray
        );

        // 7. 绘制玩家库存
        _inventory.draw(b);

        // 8. 绘制鼠标拖拽的物品
        base.draw(b); // 绘制关闭按钮
        drawMouse(b);
        
        // 9. 悬浮提示 (Tooltip)
        if (_hoverText.Length > 0)
        {
            IClickableMenu.drawHoverText(b, _hoverText, Game1.smallFont);
        }
    }

    public override void performHoverAction(int x, int y)
    {
        base.performHoverAction(x, y); // 处理关闭按钮高亮
        
        // 更新鼠标状态
        _hoveringWeaponSlot = _weaponSlotBounds.Contains(x, y);
        _hoveringFuseButton = _fuseButtonBounds.Contains(x, y);

        // 将悬停事件传递给库存菜单
        // InventoryMenu.hover 会返回高亮的物品，我们这里暂时不需要用到返回值
        _inventory.hover(x, y, Game1.player.CursorSlotItem);
    }

    public override void receiveLeftClick(int x, int y, bool playSound = true)
    {
        base.receiveLeftClick(x, y, playSound);

        // 1. 检查是否点击了库存中的物品
        // 将点击后的物品状态赋值回玩家光标物品 (CRITICAL FIX: 否则物品会消失)
        Game1.player.CursorSlotItem = _inventory.leftClick(x, y, Game1.player.CursorSlotItem, playSound);
        
        // 2. 检查是否点击了武器插槽
        if (_weaponSlotBounds.Contains(x, y))
        {
            HandleWeaponSlotInteract();
        }

        // 3. 检查熔铸按钮
        if (_fuseButtonBounds.Contains(x, y) && _slottedWeapon != null)
        {
            PerformFusion();
        }
    }

    /// <summary>处理武器槽的交互 (放入/取出)</summary>
    private void HandleWeaponSlotInteract()
    {
        var heldItem = Game1.player.CursorSlotItem;

        // 情况A: 手上有物品
        if (heldItem != null)
        {
            // 必须是近战武器
            if (heldItem is MeleeWeapon weapon)
            {
                // 如果插槽已有武器，交换
                if (_slottedWeapon != null)
                {
                    Game1.player.CursorSlotItem = _slottedWeapon;
                    _slottedWeapon = weapon;
                }
                else
                {
                    // 放入插槽
                    _slottedWeapon = weapon;
                    Game1.player.CursorSlotItem = null;
                }
                Game1.playSound("stoneStep");
                _monitor.Log($"Placed weapon in slot: {weapon.DisplayName}", LogLevel.Debug);
            }
            else
            {
                // 不是武器，播放错误音效或提示
                Game1.playSound("cancel");
                Game1.showRedMessage(_helper.Translation.Get("menu.fusion.error.only_melee"));
            }
        }
        // 情况B: 手上没物品，插槽有物品
        else if (_slottedWeapon != null)
        {
            Game1.player.CursorSlotItem = _slottedWeapon;
            _slottedWeapon = null;
            Game1.playSound("dwop");
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
                // 尝试给玩家
                var added = Game1.player.addItemToInventory(oldWeapon);
                
                if (added == null) // 成功加入背包
                {
                    Game1.addHUDMessage(new HUDMessage(_helper.Translation.Get("menu.fusion.returned", new { weaponName = _currentFusion.WeaponName })));
                }
                else // 背包已满，丢到地上
                {
                    Game1.createItemDebris(oldWeapon, Game1.player.getStandingPosition(), -1);
                    Game1.addHUDMessage(new HUDMessage(_helper.Translation.Get("menu.fusion.inventory_full", new { weaponName = _currentFusion.WeaponName })));
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
        
        // 4. 消耗武器 (设为null，不返还给 cursor)
        _slottedWeapon = null;
        
        // 5. 反馈
        Game1.addHUDMessage(new HUDMessage(_helper.Translation.Get("menu.fusion.success")));
    }

    public override void receiveRightClick(int x, int y, bool playSound = true)
    {
        // 允许右键快速取出武器
        if (_weaponSlotBounds.Contains(x, y) && _slottedWeapon != null && Game1.player.CursorSlotItem == null)
        {
            Game1.player.CursorSlotItem = _slottedWeapon;
            _slottedWeapon = null;
            Game1.playSound("dwop");
            return;
        }

        // 传递给库存 (Corrected: Assign result back to CursorSlotItem)
        Game1.player.CursorSlotItem = _inventory.rightClick(x, y, Game1.player.CursorSlotItem, playSound);
    }

    protected override void cleanupBeforeExit()
    {
        // 菜单关闭时，如果插槽有武器，返还给玩家
        if (_slottedWeapon != null)
        {
            Game1.player.addItemByMenuIfNecessary(_slottedWeapon);
            _slottedWeapon = null;
        }
        
        // 如果鼠标上还拿着东西，也返还
        if (Game1.player.CursorSlotItem != null)
        {
             Game1.player.addItemByMenuIfNecessary(Game1.player.CursorSlotItem);
             Game1.player.CursorSlotItem = null;
        }

        base.cleanupBeforeExit();
    }
}
