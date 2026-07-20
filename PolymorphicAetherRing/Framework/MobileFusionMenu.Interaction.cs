using Microsoft.Xna.Framework;
using StardewModdingAPI;
using StardewValley;
using StardewValley.Enchantments;
using StardewValley.Menus;
using StardewValley.Tools;
using System.Reflection;

namespace PolymorphicAetherRing.Framework;

public partial class MobileFusionMenu
{
    public override void performHoverAction(int x, int y)
    {
        base.performHoverAction(x, y);
        _hoveringCloseButton = _closeButtonBounds.Contains(x, y);
        _hoveringWeaponSlot = _weaponSlotBounds.Contains(x, y);
        _hoveringFuseButton = _fuseButtonBounds.Contains(x, y);
        _hoveringWeaponIndex = FindWeaponIndexAt(x, y);
    }

    public override void receiveLeftClick(int x, int y, bool playSound = true)
    {
        if (HandleTopLevelClick(x, y) || TrySelectWeapon(x, y))
            return;

        if (_weaponSlotBounds.Contains(x, y) && _slottedWeapon != null)
        {
            ReturnSlottedWeapon();
            return;
        }

        if (_fuseButtonBounds.Contains(x, y) && _slottedWeapon != null)
            PerformFusion();
    }

    public override void receiveScrollWheelAction(int direction)
    {
        if (direction > 0 && _scrollOffset > 0)
            ScrollWeapons(-1);
        else if (direction < 0 && CanScrollDown)
            ScrollWeapons(1);
    }

    private bool HandleTopLevelClick(int x, int y)
    {
        if (_closeButtonBounds.Contains(x, y))
        {
            exitThisMenu();
            Game1.playSound("bigDeSelect");
            return true;
        }

        if (_scrollUpBounds.Contains(x, y) && _scrollOffset > 0)
        {
            ScrollWeapons(-1);
            return true;
        }

        if (_scrollDownBounds.Contains(x, y) && CanScrollDown)
        {
            ScrollWeapons(1);
            return true;
        }

        return false;
    }

    private int FindWeaponIndexAt(int x, int y)
    {
        for (int index = 0; index < VisibleWeaponCount && index + _scrollOffset < _availableWeapons.Count; index++)
        {
            if (_weaponListBounds[index].Contains(x, y))
                return index;
        }

        return -1;
    }

    private bool TrySelectWeapon(int x, int y)
    {
        int index = FindWeaponIndexAt(x, y);
        if (index < 0)
            return false;

        MeleeWeapon weapon = _availableWeapons[index + _scrollOffset];
        if (_slottedWeapon == weapon)
        {
            Game1.playSound("cancel");
            return true;
        }

        int itemIndex = Game1.player.Items.IndexOf(weapon);
        if (itemIndex >= 0)
            Game1.player.Items[itemIndex] = _slottedWeapon;
        else if (_slottedWeapon != null)
            Game1.player.addItemToInventory(_slottedWeapon);

        _slottedWeapon = weapon;
        CollectAvailableWeapons();
        ClampScrollOffset();
        Game1.playSound("stoneStep");
        _monitor.Log($"Selected weapon: {weapon.DisplayName}", LogLevel.Debug);
        return true;
    }

    private void ScrollWeapons(int offset)
    {
        _scrollOffset += offset;
        ClampScrollOffset();
        Game1.playSound("shwip");
    }

    private void ReturnSlottedWeapon()
    {
        Game1.player.addItemByMenuIfNecessary(_slottedWeapon!);
        _slottedWeapon = null;
        CollectAvailableWeapons();
        ClampScrollOffset();
        Game1.playSound("coin");
    }

    private void PerformFusion()
    {
        if (_slottedWeapon == null)
            return;

        ReturnCurrentFusionWeapon();
        FusedWeaponData fusionData = FusedWeaponData.FromWeapon(_slottedWeapon);
        fusionData.SaveToModData(_trinket);
        _currentFusion = fusionData;
        _slottedWeapon = null;
        CollectAvailableWeapons();
        ClampScrollOffset();
        Game1.playSound("furnace");
        Game1.playSound("powerup");
        _monitor.Log($"Fused: {fusionData.WeaponName}", LogLevel.Info);
        Game1.showGlobalMessage(_helper.Translation.Get("menu.fusion.success"));
    }

    private void ReturnCurrentFusionWeapon()
    {
        if (!_config.ReturnFusedWeapon || _currentFusion is not { IsValid: true })
            return;

        try
        {
            Item weapon = ItemRegistry.Create(_currentFusion.WeaponId);
            RestoreEnchantments(weapon, _currentFusion.EnchantmentIds);
            Item? remainder = Game1.player.addItemToInventory(weapon);
            ShowReturnedWeaponMessage(weapon, remainder);
        }
        catch (Exception exception)
        {
            _monitor.Log($"Failed to return old weapon ({_currentFusion.WeaponName}): {exception}", LogLevel.Error);
            Game1.showRedMessage(_helper.Translation.Get("menu.fusion.error.return_failed"));
        }
    }

    private void RestoreEnchantments(Item item, IEnumerable<string> enchantmentNames)
    {
        if (item is not Tool tool)
            return;

        foreach (string name in enchantmentNames)
        {
            Type? type = FindEnchantmentType(name);
            if (type == null)
            {
                _monitor.Log($"Could not find enchantment type '{name}'", LogLevel.Warn);
                continue;
            }

            try
            {
                if (Activator.CreateInstance(type) is BaseEnchantment enchantment)
                    tool.enchantments.Add(enchantment);
            }
            catch (Exception exception)
            {
                _monitor.Log($"Failed to restore enchantment '{name}': {exception.Message}", LogLevel.Warn);
            }
        }
    }

    private Type? FindEnchantmentType(string name)
    {
        foreach (Assembly assembly in AppDomain.CurrentDomain.GetAssemblies())
        {
            try
            {
                Type? type = assembly.GetTypes().FirstOrDefault(candidate =>
                    candidate.Name == name && typeof(BaseEnchantment).IsAssignableFrom(candidate));
                if (type != null)
                    return type;
            }
            catch (ReflectionTypeLoadException exception)
            {
                _monitor.Log($"Could not inspect enchantments in {assembly.FullName}: {exception.Message}", LogLevel.Warn);
            }
        }

        return null;
    }

    private void ShowReturnedWeaponMessage(Item weapon, Item? remainder)
    {
        if (remainder == null)
        {
            Game1.showGlobalMessage(_helper.Translation.Get("menu.fusion.returned", new { weaponName = weapon.DisplayName }));
            return;
        }

        Game1.createItemDebris(weapon, Game1.player.getStandingPosition(), -1);
        Game1.showGlobalMessage(_helper.Translation.Get("menu.fusion.inventory_full", new { weaponName = weapon.DisplayName }));
    }

    protected override void cleanupBeforeExit()
    {
        if (_slottedWeapon != null)
            Game1.player.addItemByMenuIfNecessary(_slottedWeapon);

        _slottedWeapon = null;
        base.cleanupBeforeExit();
    }
}
