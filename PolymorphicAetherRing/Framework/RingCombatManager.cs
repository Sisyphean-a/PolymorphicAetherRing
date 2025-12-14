using Microsoft.Xna.Framework;
using StardewModdingAPI;
using StardewValley;
using StardewValley.Monsters;
using StardewValley.Objects;

namespace PolymorphicAetherRing.Framework;

/// <summary>战斗光环管理器 - 处理360度自动攻击</summary>
public class RingCombatManager
{
    private readonly IModHelper _helper;
    private readonly IMonitor _monitor;
    
    /// <summary>上次攻击后经过的毫秒数</summary>
    private double _timeSinceLastAttack;
    
    /// <summary>当前的攻击冷却时间</summary>
    private int _currentCooldownMs;
    
    /// <summary>缓存的熔铸数据</summary>
    private FusedWeaponData? _cachedFusionData;
    
    /// <summary>缓存的戒指引用</summary>
    private Item? _cachedRing;

    public RingCombatManager(IModHelper helper, IMonitor monitor)
    {
        _helper = helper;
        _monitor = monitor;
        _timeSinceLastAttack = 0;
        _currentCooldownMs = 400; // 默认冷却
    }

    /// <summary>每帧更新</summary>
    public void Update()
    {
        var player = Game1.player;
        if (player == null || !Context.IsPlayerFree)
            return;

        // 检查玩家是否装备了我们的戒指
        var ring = GetEquippedAetherRing(player);
        if (ring == null)
        {
            _cachedRing = null;
            _cachedFusionData = null;
            return;
        }

        // 如果戒指改变了，重新读取熔铸数据
        if (ring != _cachedRing)
        {
            // _monitor.Log($"[Debug] Ring equipped! Name: {ring.Name}", LogLevel.Trace);
            _cachedRing = ring;
            _cachedFusionData = FusedWeaponData.FromModData(ring);
            
            if (_cachedFusionData != null)
            {
                _currentCooldownMs = _cachedFusionData.GetAttackIntervalMs();
                _monitor.Log($"Loaded fusion data: {_cachedFusionData.WeaponName}, cooldown: {_currentCooldownMs}ms", LogLevel.Debug);
            }
        }

        // 如果没有熔铸数据，不执行攻击
        if (_cachedFusionData == null || !_cachedFusionData.IsValid)
            return;

        // 累计时间
        _timeSinceLastAttack += Game1.currentGameTime.ElapsedGameTime.TotalMilliseconds;

        // 检查冷却
        // 只有当时间足够，并且成功执行了攻击（命中了目标）时，才扣除冷却时间
        if (_timeSinceLastAttack >= _currentCooldownMs)
        {
            if (ExecuteAuraAttack(player, _cachedFusionData))
            {
                // 使用减法而不是置0，以保持长期平均频率准确
                _timeSinceLastAttack -= _currentCooldownMs;
                
                // 如果累积时间仍然远大于冷却（例如卡顿后），重置为0以避免瞬间爆发多次攻击
                if (_timeSinceLastAttack > _currentCooldownMs)
                    _timeSinceLastAttack = 0;
            }
            // 如果没命中，保持 _timeSinceLastAttack 不变（满能量状态），下一帧继续尝试
        }
    }

    /// <summary>获取玩家装备的以太戒指（支持组合戒指）</summary>
    private Item? GetEquippedAetherRing(Farmer player)
    {
        // 检查左戒指槽
        var leftFound = FindAetherRing(player.leftRing.Value);
        if (leftFound != null) return leftFound;

        // 检查右戒指槽
        var rightFound = FindAetherRing(player.rightRing.Value);
        if (rightFound != null) return rightFound;
        
        return null;
    }

    /// <summary>递归查找以太戒指</summary>
    private Item? FindAetherRing(StardewValley.Objects.Ring? ring)
    {
        if (ring == null) return null;
        
        // 1. 直接匹配
        if (ring.QualifiedItemId == ModEntry.QualifiedRingId) 
            return ring;
            
        // 2. 检查组合戒指
        if (ring is StardewValley.Objects.CombinedRing combinedRing)
        {
            foreach (var childRing in combinedRing.combinedRings)
            {
                var found = FindAetherRing(childRing);
                if (found != null) return found;
            }
        }
        
        return null;
    }

    /// <summary>执行光环攻击</summary>
    /// <returns>是否命中任何目标</returns>
    private bool ExecuteAuraAttack(Farmer player, FusedWeaponData fusionData)
    {
        var location = player.currentLocation;
        if (location == null)
            return false;

        var playerCenter = player.getStandingPosition();
        var attackRadius = fusionData.GetAttackRadius();
        var radiusSquared = attackRadius * attackRadius;

        // 收集范围内的所有怪物
        var targetsHit = new List<Monster>();
        
        foreach (var character in location.characters)
        {
            if (character is not Monster monster)
                continue;

            // 跳过已死亡的怪物
            if (monster.Health <= 0)
                continue;

            // 计算距离（使用平方避免开根号）
            var monsterCenter = monster.getStandingPosition();
            var distanceSquared = Vector2.DistanceSquared(playerCenter, monsterCenter);

            if (distanceSquared <= radiusSquared)
            {
                targetsHit.Add(monster);
            }
        }

        // 如果没有命中任何目标，不播放效果，返回false
        if (targetsHit.Count == 0)
            return false;

        // 播放攻击音效
        PlayAttackSound(fusionData.WeaponType, location);

        // 对每个目标造成伤害
        foreach (var monster in targetsHit)
        {
            DealDamageToMonster(player, monster, fusionData, playerCenter);
        }

        // _monitor.Log($"Aura hit {targetsHit.Count} targets", LogLevel.Trace);
        return true;
    }

    /// <summary>对单个怪物造成伤害</summary>
    private void DealDamageToMonster(Farmer player, Monster monster, FusedWeaponData fusionData, Vector2 playerCenter)
    {
        var random = Game1.random;
        
        // 计算基础伤害
        int damage = random.Next(fusionData.MinDamage, fusionData.MaxDamage + 1);
        
        // 暴击判定
        bool isCrit = random.NextDouble() < fusionData.CritChance;
        if (isCrit)
        {
            damage = (int)(damage * fusionData.CritMultiplier);
        }

        // 计算击退方向（从玩家向外辐射）
        var monsterCenter = monster.getStandingPosition();
        var knockbackDirection = monsterCenter - playerCenter;
        if (knockbackDirection != Vector2.Zero)
            knockbackDirection.Normalize();

        var knockbackForce = fusionData.Knockback;
        int xTrajectory = (int)(knockbackDirection.X * knockbackForce * 10);
        int yTrajectory = (int)(knockbackDirection.Y * knockbackForce * 10);

        // 应用伤害
        var location = player.currentLocation;
        
        // 使用 damageMonster 方法（更完整的伤害流程）
        var hitBox = new Rectangle(
            (int)monsterCenter.X - 1, 
            (int)monsterCenter.Y - 1, 
            2, 
            2
        );
        
        location.damageMonster(
            areaOfEffect: hitBox,
            minDamage: damage,
            maxDamage: damage,
            isBomb: false,
            knockBackModifier: knockbackForce,
            addedPrecision: 0,
            critChance: 0f, // 我们已经处理了暴击
            critMultiplier: 1f,
            triggerMonsterInvincibleTimer: true,
            who: player
        );

        // 播放命中特效
        PlayHitEffect(location, monsterCenter, isCrit);
    }

    /// <summary>播放攻击音效</summary>
    private void PlayAttackSound(int weaponType, GameLocation location)
    {
        string soundName = weaponType switch
        {
            1 => "daggerswipe", // 匕首
            2 => "clubswipe",   // 锤子
            _ => "swordswipe"   // 剑
        };
        
        location.playSound(soundName);
    }

    /// <summary>播放命中特效</summary>
    private void PlayHitEffect(GameLocation location, Vector2 position, bool isCrit)
    {
        // 添加临时动画精灵
        var hitSprite = new TemporaryAnimatedSprite(
            textureName: "TileSheets\\animations",
            sourceRect: new Rectangle(0, 0, 64, 64),
            animationInterval: 50f,
            animationLength: 6,
            numberOfLoops: 0,
            position: position - new Vector2(32, 32),
            flicker: false,
            flipped: false
        )
        {
            scale = isCrit ? 1.5f : 1f,
            alpha = 0.75f
        };
        
        location.temporarySprites.Add(hitSprite);

        // 暴击额外特效
        if (isCrit)
        {
            location.playSound("crit");
        }
    }
}
