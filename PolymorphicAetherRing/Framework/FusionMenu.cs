using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using StardewModdingAPI;
using StardewValley;
using StardewValley.Menus;
using StardewValley.Tools;

namespace PolymorphicAetherRing.Framework;

/// <summary>熔铸面板UI - 允许玩家将武器熔铸进饰品</summary>
public class FusionMenu : IClickableMenu
{
    private readonly Item _trinket;
    private readonly IModHelper _helper;
    private readonly IMonitor _monitor;
    
    /// <summary>武器插槽区域</summary>
    private Rectangle _weaponSlotBounds;
    
    /// <summary>熔铸按钮区域</summary>
    private Rectangle _fuseButtonBounds;
    
    /// <summary>当前放入插槽的武器</summary>
    private MeleeWeapon? _slottedWeapon;
    
    /// <summary>当前的熔铸数据（如果已熔铸）</summary>
    private FusedWeaponData? _currentFusion;
    
    /// <summary>鼠标悬停在熔铸按钮上</summary>
    private bool _hoveringFuseButton;
    
    /// <summary>鼠标悬停在武器槽上</summary>
    private bool _hoveringWeaponSlot;

    // UI尺寸常量
    private const int MenuWidth = 400;
    private const int MenuHeight = 300;
    private const int SlotSize = 64;
    private const int ButtonWidth = 120;
    private const int ButtonHeight = 48;

    public FusionMenu(Item trinket, IModHelper helper, IMonitor monitor)
        : base(
            (Game1.uiViewport.Width - MenuWidth) / 2,
            (Game1.uiViewport.Height - MenuHeight) / 2,
            MenuWidth,
            MenuHeight,
            showUpperRightCloseButton: true
        )
    {
        _trinket = trinket;
        _helper = helper;
        _monitor = monitor;
        
        // 读取当前熔铸状态
        _currentFusion = FusedWeaponData.FromModData(trinket);
        
        // 初始化UI区域
        InitializeLayout();
    }

    private void InitializeLayout()
    {
        // 武器插槽居中偏上
        _weaponSlotBounds = new Rectangle(
            xPositionOnScreen + (width - SlotSize) / 2,
            yPositionOnScreen + 80,
            SlotSize,
            SlotSize
        );

        // 熔铸按钮在下方
        _fuseButtonBounds = new Rectangle(
            xPositionOnScreen + (width - ButtonWidth) / 2,
            yPositionOnScreen + height - 80,
            ButtonWidth,
            ButtonHeight
        );
    }

    public override void draw(SpriteBatch b)
    {
        // 绘制半透明背景遮罩
        b.Draw(Game1.fadeToBlackRect, Game1.graphics.GraphicsDevice.Viewport.Bounds, Color.Black * 0.6f);

        // 绘制菜单背景框
        Game1.drawDialogueBox(
            xPositionOnScreen, 
            yPositionOnScreen, 
            width, 
            height, 
            speaker: false, 
            drawOnlyBox: true
        );

        // 绘制标题
        string title = "以太熔铸";
        var titleSize = Game1.dialogueFont.MeasureString(title);
        Utility.drawTextWithShadow(
            b,
            title,
            Game1.dialogueFont,
            new Vector2(xPositionOnScreen + (width - titleSize.X) / 2, yPositionOnScreen + 28),
            Game1.textColor
        );

        // 绘制当前熔铸状态
        if (_currentFusion != null && _currentFusion.IsValid)
        {
            string fusionStatus = $"已熔铸: {_currentFusion.WeaponName}";
            var statusSize = Game1.smallFont.MeasureString(fusionStatus);
            Utility.drawTextWithShadow(
                b,
                fusionStatus,
                Game1.smallFont,
                new Vector2(xPositionOnScreen + (width - statusSize.X) / 2, yPositionOnScreen + 55),
                Color.LimeGreen
            );
        }

        // 绘制武器插槽背景
        var slotColor = _hoveringWeaponSlot ? Color.Yellow : Color.White;
        IClickableMenu.drawTextureBox(
            b,
            Game1.menuTexture,
            new Rectangle(0, 256, 60, 60),
            _weaponSlotBounds.X - 4,
            _weaponSlotBounds.Y - 4,
            _weaponSlotBounds.Width + 8,
            _weaponSlotBounds.Height + 8,
            slotColor,
            1f,
            drawShadow: false
        );

        // 绘制插槽中的武器
        if (_slottedWeapon != null)
        {
            _slottedWeapon.drawInMenu(
                b,
                new Vector2(_weaponSlotBounds.X, _weaponSlotBounds.Y),
                1f
            );
        }
        else
        {
            // 绘制提示文本
            string hint = "放入武器";
            var hintSize = Game1.smallFont.MeasureString(hint);
            Utility.drawTextWithShadow(
                b,
                hint,
                Game1.smallFont,
                new Vector2(
                    _weaponSlotBounds.X + (_weaponSlotBounds.Width - hintSize.X) / 2,
                    _weaponSlotBounds.Y + (_weaponSlotBounds.Height - hintSize.Y) / 2
                ),
                Color.Gray
            );
        }

        // 绘制熔铸按钮
        var canFuse = _slottedWeapon != null;
        var buttonColor = canFuse 
            ? (_hoveringFuseButton ? Color.LightGreen : Color.White) 
            : Color.Gray;
        
        IClickableMenu.drawTextureBox(
            b,
            Game1.menuTexture,
            new Rectangle(0, 256, 60, 60),
            _fuseButtonBounds.X,
            _fuseButtonBounds.Y,
            _fuseButtonBounds.Width,
            _fuseButtonBounds.Height,
            buttonColor,
            1f,
            drawShadow: true
        );

        string buttonText = "熔铸";
        var buttonTextSize = Game1.dialogueFont.MeasureString(buttonText);
        Utility.drawTextWithShadow(
            b,
            buttonText,
            Game1.dialogueFont,
            new Vector2(
                _fuseButtonBounds.X + (_fuseButtonBounds.Width - buttonTextSize.X) / 2,
                _fuseButtonBounds.Y + (_fuseButtonBounds.Height - buttonTextSize.Y) / 2
            ),
            canFuse ? Game1.textColor : Color.DarkGray
        );

        // 绘制鼠标光标和拖拽的物品
        drawMouse(b);
        
        // 如果玩家正在拖拽物品，绘制在鼠标位置
        if (Game1.player.CursorSlotItem != null)
        {
            Game1.player.CursorSlotItem.drawInMenu(
                b,
                new Vector2(Game1.getMouseX() - 32, Game1.getMouseY() - 32),
                1f
            );
        }

        // 绘制关闭按钮
        base.draw(b);
    }

    public override void performHoverAction(int x, int y)
    {
        base.performHoverAction(x, y);
        
        _hoveringWeaponSlot = _weaponSlotBounds.Contains(x, y);
        _hoveringFuseButton = _fuseButtonBounds.Contains(x, y);
    }

    public override void receiveLeftClick(int x, int y, bool playSound = true)
    {
        base.receiveLeftClick(x, y, playSound);

        var player = Game1.player;
        var cursorItem = player.CursorSlotItem;

        // 点击武器插槽
        if (_weaponSlotBounds.Contains(x, y))
        {
            HandleWeaponSlotClick(player, cursorItem);
            return;
        }

        // 点击熔铸按钮
        if (_fuseButtonBounds.Contains(x, y) && _slottedWeapon != null)
        {
            PerformFusion();
            return;
        }
    }

    private void HandleWeaponSlotClick(Farmer player, Item? cursorItem)
    {
        // 如果光标上有武器，放入插槽
        if (cursorItem is MeleeWeapon weapon)
        {
            // 如果插槽已有武器，交换
            if (_slottedWeapon != null)
            {
                player.CursorSlotItem = _slottedWeapon;
            }
            else
            {
                player.CursorSlotItem = null;
            }
            
            _slottedWeapon = weapon;
            Game1.playSound("stoneStep");
            _monitor.Log($"Placed weapon in slot: {weapon.DisplayName}", LogLevel.Debug);
        }
        // 如果光标为空且插槽有武器，取出
        else if (cursorItem == null && _slottedWeapon != null)
        {
            player.CursorSlotItem = _slottedWeapon;
            _slottedWeapon = null;
            Game1.playSound("dwop");
        }
    }

    private void PerformFusion()
    {
        if (_slottedWeapon == null)
            return;

        // 从武器提取数据
        var fusionData = FusedWeaponData.FromWeapon(_slottedWeapon);
        
        // 保存到饰品的 modData
        fusionData.SaveToModData(_trinket);
        
        // 更新当前熔铸状态
        _currentFusion = fusionData;
        
        // 播放熔铸音效
        Game1.playSound("furnace");
        Game1.playSound("powerup");
        
        _monitor.Log($"Fused weapon: {fusionData.WeaponName} (Damage: {fusionData.MinDamage}-{fusionData.MaxDamage})", LogLevel.Info);
        
        // 销毁武器
        _slottedWeapon = null;
        
        // 显示成功消息
        Game1.addHUDMessage(new HUDMessage($"熔铸成功: {fusionData.WeaponName}"));
    }

    public override void receiveRightClick(int x, int y, bool playSound = true)
    {
        // 右键点击武器槽取出武器
        if (_weaponSlotBounds.Contains(x, y) && _slottedWeapon != null && Game1.player.CursorSlotItem == null)
        {
            Game1.player.CursorSlotItem = _slottedWeapon;
            _slottedWeapon = null;
            Game1.playSound("dwop");
        }
    }

    protected override void cleanupBeforeExit()
    {
        base.cleanupBeforeExit();
        
        // 如果插槽还有武器，返还给玩家
        if (_slottedWeapon != null)
        {
            Game1.player.addItemByMenuIfNecessary(_slottedWeapon);
            _slottedWeapon = null;
        }
    }
}
