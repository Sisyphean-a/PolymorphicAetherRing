using System.IO;
using System.Reflection;
using StardewValley;
using StardewValley.Enchantments;
using StardewValley.Tools;

namespace PolymorphicAetherRing.Framework;

internal static class FusedWeaponRestorer
{
    /// <summary>
    /// Flow: 按物品 ID 创建武器，按原版复制语义恢复附魔列表，再用熔铸快照校准战斗属性。
    /// Failure: 任一附魔无法完整恢复时抛出异常，禁止调用方覆盖仍保存在饰品中的旧数据。
    /// </summary>
    public static MeleeWeapon CreateWeapon(FusedWeaponData data)
    {
        if (!data.IsValid)
            throw new InvalidDataException("Fused weapon ID is missing.");

        Item item = ItemRegistry.Create(data.WeaponId);
        if (item is not MeleeWeapon weapon)
            throw new InvalidDataException($"Fused item '{data.WeaponId}' is not a melee weapon.");

        foreach (FusedEnchantmentData savedEnchantment in data.Enchantments)
            RestoreEnchantment(weapon, savedEnchantment);

        RestoreCombatStats(weapon, data);
        return weapon;
    }

    internal static void RestoreEnchantment(MeleeWeapon weapon, FusedEnchantmentData saved)
    {
        Type type = FindEnchantmentType(saved)
            ?? throw new InvalidDataException($"Enchantment type '{saved.TypeName}' is not loaded.");

        if (type.IsAbstract || !typeof(BaseEnchantment).IsAssignableFrom(type))
            throw new InvalidDataException($"Type '{saved.TypeName}' is not a concrete enchantment.");

        if (Activator.CreateInstance(type) is not BaseEnchantment enchantment)
            throw new InvalidDataException($"Enchantment '{saved.TypeName}' could not be created.");

        int maximumLevel = enchantment.GetMaximumLevel();
        if (saved.Level < 1 || maximumLevel >= 0 && saved.Level > maximumLevel)
        {
            throw new InvalidDataException(
                $"Enchantment '{saved.TypeName}' has invalid level {saved.Level} (maximum {maximumLevel}).");
        }

        enchantment.Level = saved.Level;
        weapon.enchantments.Add(enchantment);
        enchantment.ApplyTo(weapon);
    }

    private static Type? FindEnchantmentType(FusedEnchantmentData saved)
    {
        Assembly[] assemblies = AppDomain.CurrentDomain.GetAssemblies();

        if (!string.IsNullOrWhiteSpace(saved.AssemblyName))
        {
            Assembly? owner = assemblies.FirstOrDefault(assembly =>
                string.Equals(assembly.GetName().Name, saved.AssemblyName, StringComparison.Ordinal));
            return owner?.GetType(saved.TypeName, throwOnError: false, ignoreCase: false);
        }

        if (saved.TypeName.Contains('.', StringComparison.Ordinal))
        {
            return SelectUnambiguousType(
                saved.TypeName,
                assemblies
                    .Select(assembly => assembly.GetType(saved.TypeName, throwOnError: false, ignoreCase: false))
                    .OfType<Type>()
                    .Where(type => typeof(BaseEnchantment).IsAssignableFrom(type)));
        }

        Assembly gameAssembly = typeof(Game1).Assembly;
        Type? vanillaType = GetLoadableTypes(gameAssembly).FirstOrDefault(type =>
            type.Name == saved.TypeName && typeof(BaseEnchantment).IsAssignableFrom(type));
        if (vanillaType != null)
            return vanillaType;

        return SelectUnambiguousType(
            saved.TypeName,
            assemblies
                .Where(assembly => assembly != gameAssembly)
                .SelectMany(GetLoadableTypes)
                .Where(type =>
                    type.Name == saved.TypeName
                    && typeof(BaseEnchantment).IsAssignableFrom(type)));
    }

    private static Type? SelectUnambiguousType(string savedName, IEnumerable<Type> candidates)
    {
        Type[] matches = candidates.Distinct().ToArray();
        if (matches.Length <= 1)
            return matches.SingleOrDefault();

        string identities = string.Join(", ", matches.Select(type => type.AssemblyQualifiedName));
        throw new InvalidDataException(
            $"Legacy enchantment name '{savedName}' is ambiguous between: {identities}.");
    }

    private static IEnumerable<Type> GetLoadableTypes(Assembly assembly)
    {
        try
        {
            return assembly.GetTypes();
        }
        catch (ReflectionTypeLoadException exception)
        {
            return exception.Types.OfType<Type>();
        }
    }

    private static void RestoreCombatStats(MeleeWeapon weapon, FusedWeaponData data)
    {
        weapon.minDamage.Value = data.MinDamage;
        weapon.maxDamage.Value = data.MaxDamage;
        weapon.speed.Value = data.Speed;
        weapon.critChance.Value = data.CritChance;
        weapon.critMultiplier.Value = data.CritMultiplier;
        weapon.knockback.Value = data.Knockback;
        weapon.addedAreaOfEffect.Value = data.AreaOfEffect;
    }
}
