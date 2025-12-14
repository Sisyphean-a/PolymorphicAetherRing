namespace PolymorphicAetherRing;

/// <summary>模组配置类</summary>
public class ModConfig
{
    /// <summary>伤害倍率 (0.1 - 5.0)</summary>
    public float DamageMultiplier { get; set; } = 1.0f;

    /// <summary>范围倍率 (0.5 - 3.0)</summary>
    public float RangeMultiplier { get; set; } = 1.0f;

    /// <summary>冷却倍率 (0.1 - 2.0)</summary>
    public float CooldownMultiplier { get; set; } = 1.0f;

    /// <summary>熔铸时是否返还旧武器</summary>
    public bool ReturnFusedWeapon { get; set; } = false;
}
