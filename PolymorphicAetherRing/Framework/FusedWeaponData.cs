using StardewValley;
using StardewValley.Tools;

namespace PolymorphicAetherRing.Framework;

/// <summary>熔铸武器的数据模型</summary>
public class FusedWeaponData
{
    /// <summary>modData 键前缀</summary>
    private const string ModDataPrefix = "xixifu.AetherTrinket/";
    
    /// <summary>武器ID</summary>
    public string WeaponId { get; set; } = string.Empty;

    private const string EnchantmentsKey = ModDataPrefix + "EnchantmentsV2";
    private const string LegacyEnchantmentIdsKey = ModDataPrefix + "EnchantmentIds";

    /// <summary>附魔状态列表。</summary>
    public List<FusedEnchantmentData> Enchantments { get; set; } = new();

    /// <summary>数据是否来自未保存附魔等级的旧格式。</summary>
    public bool HasLegacyEnchantmentData { get; private set; }
    
    /// <summary>武器名称</summary>
    public string WeaponName { get; set; } = string.Empty;
    
    /// <summary>最小伤害</summary>
    public int MinDamage { get; set; }
    
    /// <summary>最大伤害</summary>
    public int MaxDamage { get; set; }
    
    /// <summary>武器速度</summary>
    public int Speed { get; set; }
    
    /// <summary>暴击几率</summary>
    public float CritChance { get; set; }
    
    /// <summary>暴击倍率</summary>
    public float CritMultiplier { get; set; }
    
    /// <summary>击退力度</summary>
    public float Knockback { get; set; }
    
    /// <summary>攻击范围</summary>
    public int AreaOfEffect { get; set; }
    
    /// <summary>武器类型 (0=剑, 1=匕首, 2=锤子, 3=剑(精确))</summary>
    public int WeaponType { get; set; }

    /// <summary>是否有有效的熔铸数据</summary>
    public bool IsValid => !string.IsNullOrEmpty(WeaponId);

    /// <summary>获取熔铸数据的稳定签名</summary>
    public static string GetModDataSignature(Item item)
    {
        return string.Join(
            "\u001F",
            item.modData
                .SelectMany(page => page)
                .Where(pair => pair.Key.StartsWith(ModDataPrefix, StringComparison.Ordinal))
                .OrderBy(pair => pair.Key, StringComparer.Ordinal)
                .Select(pair => $"{pair.Key.Length}:{pair.Key}{pair.Value.Length}:{pair.Value}"));
    }

    /// <summary>从武器对象提取数据</summary>
    public static FusedWeaponData FromWeapon(MeleeWeapon weapon)
    {
        var data = new FusedWeaponData
        {
            WeaponId = weapon.QualifiedItemId,
            WeaponName = weapon.DisplayName,
            MinDamage = weapon.minDamage.Value,
            MaxDamage = weapon.maxDamage.Value,
            Speed = weapon.speed.Value,
            CritChance = weapon.critChance.Value,
            CritMultiplier = weapon.critMultiplier.Value,
            Knockback = weapon.knockback.Value,
            AreaOfEffect = weapon.addedAreaOfEffect.Value,
            WeaponType = (int)weapon.type.Value
        };

        foreach (var enchantment in weapon.enchantments)
        {
            Type enchantmentType = enchantment.GetType();
            data.Enchantments.Add(new FusedEnchantmentData
            {
                TypeName = enchantmentType.FullName ?? enchantmentType.Name,
                AssemblyName = enchantmentType.Assembly.GetName().Name,
                Level = enchantment.Level
            });
        }

        return data;
    }

    /// <summary>从物品的 modData 读取熔铸数据</summary>
    public static FusedWeaponData? FromModData(Item item)
    {
        var modData = item.modData;
        
        if (!modData.TryGetValue(ModDataPrefix + "WeaponId", out var weaponId))
            return null;

        var data = new FusedWeaponData { WeaponId = weaponId };
        
        if (modData.TryGetValue(ModDataPrefix + "WeaponName", out var name))
            data.WeaponName = name;
        if (modData.TryGetValue(ModDataPrefix + "MinDamage", out var minDmg))
            data.MinDamage = int.Parse(minDmg);
        if (modData.TryGetValue(ModDataPrefix + "MaxDamage", out var maxDmg))
            data.MaxDamage = int.Parse(maxDmg);
        if (modData.TryGetValue(ModDataPrefix + "Speed", out var speed))
            data.Speed = int.Parse(speed);
        if (modData.TryGetValue(ModDataPrefix + "CritChance", out var critChance))
            data.CritChance = float.Parse(critChance);
        if (modData.TryGetValue(ModDataPrefix + "CritMultiplier", out var critMult))
            data.CritMultiplier = float.Parse(critMult);
        if (modData.TryGetValue(ModDataPrefix + "Knockback", out var knockback))
            data.Knockback = float.Parse(knockback);
        if (modData.TryGetValue(ModDataPrefix + "AreaOfEffect", out var aoe))
            data.AreaOfEffect = int.Parse(aoe);
        if (modData.TryGetValue(ModDataPrefix + "WeaponType", out var weaponType))
            data.WeaponType = int.Parse(weaponType);

        if (modData.TryGetValue(EnchantmentsKey, out string? enchantmentsJson))
        {
            data.Enchantments = FusedEnchantmentDataCodec.Deserialize(enchantmentsJson);
        }
        else if (modData.TryGetValue(LegacyEnchantmentIdsKey, out string? legacyEnchantments)
                 && !string.IsNullOrWhiteSpace(legacyEnchantments))
        {
            data.Enchantments = FusedEnchantmentDataCodec.DeserializeLegacy(legacyEnchantments);
            data.HasLegacyEnchantmentData = true;
        }
            
        return data;
    }

    /// <summary>预先序列化并验证待写入的熔铸数据。</summary>
    internal FusedWeaponModDataUpdate PrepareSave()
    {
        var values = new Dictionary<string, string>
        {
            [ModDataPrefix + "WeaponId"] = WeaponId,
            [ModDataPrefix + "WeaponName"] = WeaponName,
            [ModDataPrefix + "MinDamage"] = MinDamage.ToString(),
            [ModDataPrefix + "MaxDamage"] = MaxDamage.ToString(),
            [ModDataPrefix + "Speed"] = Speed.ToString(),
            [ModDataPrefix + "CritChance"] = CritChance.ToString(),
            [ModDataPrefix + "CritMultiplier"] = CritMultiplier.ToString(),
            [ModDataPrefix + "Knockback"] = Knockback.ToString(),
            [ModDataPrefix + "AreaOfEffect"] = AreaOfEffect.ToString(),
            [ModDataPrefix + "WeaponType"] = WeaponType.ToString()
        };
        var removedKeys = new List<string> { LegacyEnchantmentIdsKey };

        if (Enchantments.Count > 0)
            values[EnchantmentsKey] = FusedEnchantmentDataCodec.Serialize(Enchantments);
        else
            removedKeys.Add(EnchantmentsKey);

        return new FusedWeaponModDataUpdate(values, removedKeys);
    }

    /// <summary>将熔铸数据写入物品的 modData。</summary>
    public void SaveToModData(Item item)
    {
        PrepareSave().ApplyTo(item);
        HasLegacyEnchantmentData = false;
    }

    /// <summary>计算攻击冷却时间（毫秒）</summary>
    public int GetAttackIntervalMs()
    {
        // 基础挥动时间根据武器类型不同
        int baseTime = WeaponType switch
        {
            1 => 250, // 匕首更快
            2 => 500, // 锤子更慢
            _ => 400  // 剑类标准
        };
        
        // 速度每点减少40ms
        return Math.Max(100, baseTime - Speed * 40);
    }

    /// <summary>计算攻击半径（像素）</summary>
    public float GetAttackRadius()
    {
        // 基础半径 + 范围加成
        float baseRadius = 80f; // 约1.2格
        return baseRadius + AreaOfEffect * 16f;
    }
}
