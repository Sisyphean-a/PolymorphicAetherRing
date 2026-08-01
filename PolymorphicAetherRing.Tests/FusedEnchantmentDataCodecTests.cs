using System.IO;
using PolymorphicAetherRing.Framework;
using StardewValley.Enchantments;
using StardewValley.Tools;
using Xunit;

namespace PolymorphicAetherRing.Tests;

public class FusedEnchantmentDataCodecTests
{
    [Fact]
    public void RoundTripPreservesGalaxySoulLevelAndTypeIdentity()
    {
        var source = new List<FusedEnchantmentData>
        {
            new()
            {
                TypeName = "StardewValley.Enchantments.GalaxySoulEnchantment",
                AssemblyName = "Stardew Valley",
                Level = 2
            }
        };

        string json = FusedEnchantmentDataCodec.Serialize(source);
        FusedEnchantmentData restored = Assert.Single(FusedEnchantmentDataCodec.Deserialize(json));

        Assert.Equal(source[0].TypeName, restored.TypeName);
        Assert.Equal(source[0].AssemblyName, restored.AssemblyName);
        Assert.Equal(2, restored.Level);
    }

    [Fact]
    public void LegacyTypeNamesRemainReadableAtKnownDefaultLevel()
    {
        List<FusedEnchantmentData> restored =
            FusedEnchantmentDataCodec.DeserializeLegacy("RubyEnchantment,GalaxySoulEnchantment");

        Assert.Collection(
            restored,
            ruby =>
            {
                Assert.Equal("RubyEnchantment", ruby.TypeName);
                Assert.Null(ruby.AssemblyName);
                Assert.Equal(1, ruby.Level);
            },
            soul =>
            {
                Assert.Equal("GalaxySoulEnchantment", soul.TypeName);
                Assert.Null(soul.AssemblyName);
                Assert.Equal(1, soul.Level);
            });
    }

    [Fact]
    public void InvalidPersistedLevelIsRejected()
    {
        const string json = "[{\"TypeName\":\"GalaxySoulEnchantment\",\"Level\":0}]";

        Assert.Throws<InvalidDataException>(() => FusedEnchantmentDataCodec.Deserialize(json));
    }

    [Fact]
    public void ModDataRoundTripPreservesGalaxySoulLevel()
    {
        var holder = new MeleeWeapon();
        var source = new FusedWeaponData
        {
            WeaponId = "(W)4",
            WeaponName = "Galaxy Sword",
            Enchantments = new List<FusedEnchantmentData>
            {
                CreateSavedEnchantment(typeof(GalaxySoulEnchantment), 2)
            }
        };

        source.SaveToModData(holder);
        FusedWeaponData restored = Assert.IsType<FusedWeaponData>(FusedWeaponData.FromModData(holder));

        Assert.Equal("(W)4", restored.WeaponId);
        Assert.Equal(2, Assert.Single(restored.Enchantments).Level);
        Assert.False(restored.HasLegacyEnchantmentData);
    }

    [Fact]
    public void InvalidNewDataDoesNotPartiallyOverwriteExistingModData()
    {
        const string prefix = "xixifu.AetherTrinket/";
        var holder = new MeleeWeapon();
        holder.modData[prefix + "WeaponId"] = "(W)4";
        holder.modData[prefix + "WeaponName"] = "Existing weapon";
        var invalid = new FusedWeaponData
        {
            WeaponId = "(W)62",
            WeaponName = "Replacement",
            Enchantments = new List<FusedEnchantmentData>
            {
                CreateSavedEnchantment(typeof(GalaxySoulEnchantment), 0)
            }
        };

        Assert.Throws<InvalidDataException>(() => invalid.SaveToModData(holder));

        Assert.Equal("(W)4", holder.modData[prefix + "WeaponId"]);
        Assert.Equal("Existing weapon", holder.modData[prefix + "WeaponName"]);
        Assert.False(holder.modData.ContainsKey(prefix + "EnchantmentsV2"));
    }

    [Fact]
    public void CorruptNewSignatureClearsPreviousCombatCache()
    {
        const string prefix = "xixifu.AetherTrinket/";
        var holder = new MeleeWeapon();
        holder.modData[prefix + "WeaponId"] = "(W)4";
        holder.modData[prefix + "EnchantmentsV2"] = "{broken";
        string? cachedSignature = "previous-signature";
        FusedWeaponData? cachedData = new FusedWeaponData
        {
            WeaponId = "(W)62",
            WeaponName = "Previous weapon"
        };

        Assert.Throws<InvalidDataException>(() => RingCombatManager.RefreshFusionDataCache(
            holder,
            "corrupt-signature",
            ref cachedSignature,
            ref cachedData));

        Assert.Equal("corrupt-signature", cachedSignature);
        Assert.Null(cachedData);
    }

    [Fact]
    public void RestorerPreservesGalaxySoulLevel()
    {
        var weapon = new MeleeWeapon();

        FusedWeaponRestorer.RestoreEnchantment(
            weapon,
            CreateSavedEnchantment(typeof(GalaxySoulEnchantment), 2));

        BaseEnchantment restored = Assert.Single(weapon.enchantments);
        Assert.IsType<GalaxySoulEnchantment>(restored);
        Assert.Equal(2, restored.Level);
    }

    [Fact]
    public void CombatCacheRecreatesSavedEnchantmentEffectsAndLevels()
    {
        var ring = new MeleeWeapon();
        new FusedWeaponData
        {
            WeaponId = "(W)4",
            Enchantments = new List<FusedEnchantmentData>
            {
                CreateSavedEnchantment(typeof(CrusaderEnchantment), 1),
                CreateSavedEnchantment(typeof(GalaxySoulEnchantment), 2),
                CreateSavedEnchantment(typeof(TestForgeEnchantment), 3)
            }
        }.SaveToModData(ring);
        string? cachedSignature = null;
        FusedWeaponData? cachedData = null;

        MeleeWeapon combatWeapon = Assert.IsType<MeleeWeapon>(
            RingCombatManager.RefreshFusionDataCache(
                ring,
                FusedWeaponData.GetModDataSignature(ring),
                ref cachedSignature,
                ref cachedData));

        Assert.NotNull(cachedData);
        Assert.True(combatWeapon.hasEnchantmentOfType<CrusaderEnchantment>());
        Assert.Collection(
            combatWeapon.enchantments,
            crusader => Assert.Equal(1, Assert.IsType<CrusaderEnchantment>(crusader).Level),
            galaxySoul => Assert.Equal(2, Assert.IsType<GalaxySoulEnchantment>(galaxySoul).Level),
            forge => Assert.Equal(3, Assert.IsType<TestForgeEnchantment>(forge).Level));
    }

    [Fact]
    public void RestorerDoesNotMergeDuplicateForgeEntries()
    {
        var weapon = new MeleeWeapon();

        FusedWeaponRestorer.RestoreEnchantment(
            weapon,
            CreateSavedEnchantment(typeof(TestForgeEnchantment), 2));
        FusedWeaponRestorer.RestoreEnchantment(
            weapon,
            CreateSavedEnchantment(typeof(TestForgeEnchantment), 1));

        Assert.Collection(
            weapon.enchantments,
            first => Assert.Equal(2, Assert.IsType<TestForgeEnchantment>(first).Level),
            second => Assert.Equal(1, Assert.IsType<TestForgeEnchantment>(second).Level));
    }

    [Fact]
    public void RestorerDoesNotReplaceMultiplePrimaryEnchantments()
    {
        var weapon = new MeleeWeapon();

        FusedWeaponRestorer.RestoreEnchantment(
            weapon,
            CreateSavedEnchantment(typeof(ArtfulEnchantment), 1));
        FusedWeaponRestorer.RestoreEnchantment(
            weapon,
            CreateSavedEnchantment(typeof(VampiricEnchantment), 1));

        Assert.Collection(
            weapon.enchantments,
            first => Assert.IsType<ArtfulEnchantment>(first),
            second => Assert.IsType<VampiricEnchantment>(second));
    }

    [Fact]
    public void AmbiguousLegacyModEnchantmentIsRejected()
    {
        var weapon = new MeleeWeapon();
        var saved = new FusedEnchantmentData
        {
            TypeName = "AmbiguousEnchantment",
            Level = 1
        };

        InvalidDataException exception = Assert.Throws<InvalidDataException>(() =>
            FusedWeaponRestorer.RestoreEnchantment(weapon, saved));

        Assert.Contains("ambiguous", exception.Message, StringComparison.OrdinalIgnoreCase);
        Assert.Empty(weapon.enchantments);
    }

    [Fact]
    public void MidWriteFailureRestoresAllPreviousValues()
    {
        var initial = new Dictionary<string, string>
        {
            ["WeaponId"] = "(W)4",
            ["WeaponName"] = "Existing",
            ["EnchantmentIds"] = "GalaxySoulEnchantment"
        };
        var store = new FailingModDataStore(initial, failOnMutation: 2);
        var update = new FusedWeaponModDataUpdate(
            new Dictionary<string, string>
            {
                ["WeaponId"] = "(W)62",
                ["WeaponName"] = "Replacement",
                ["EnchantmentsV2"] = "[]"
            },
            new[] { "EnchantmentIds" });

        Assert.Throws<InvalidOperationException>(() => update.ApplyTo(store));

        Assert.Equal(
            initial.OrderBy(pair => pair.Key),
            store.Values.OrderBy(pair => pair.Key));
    }

    private static FusedEnchantmentData CreateSavedEnchantment(Type type, int level)
    {
        return new FusedEnchantmentData
        {
            TypeName = type.FullName!,
            AssemblyName = type.Assembly.GetName().Name,
            Level = level
        };
    }

    private sealed class FailingModDataStore : IFusedWeaponModDataStore
    {
        private readonly Dictionary<string, string> _values;
        private readonly int _failOnMutation;
        private int _mutationCount;
        private bool _failureRaised;

        public FailingModDataStore(Dictionary<string, string> values, int failOnMutation)
        {
            _values = new Dictionary<string, string>(values);
            _failOnMutation = failOnMutation;
        }

        public IReadOnlyDictionary<string, string> Values => _values;

        public bool TryGetValue(string key, out string? value)
        {
            return _values.TryGetValue(key, out value);
        }

        public void SetValue(string key, string value)
        {
            FailIfRequested();
            _values[key] = value;
        }

        public void Remove(string key)
        {
            FailIfRequested();
            _values.Remove(key);
        }

        private void FailIfRequested()
        {
            _mutationCount++;
            if (!_failureRaised && _mutationCount == _failOnMutation)
            {
                _failureRaised = true;
                throw new InvalidOperationException("Injected write failure.");
            }
        }
    }
}

public sealed class TestForgeEnchantment : BaseEnchantment
{
    public override bool IsForge() => true;

    public override int GetMaximumLevel() => 3;
}
