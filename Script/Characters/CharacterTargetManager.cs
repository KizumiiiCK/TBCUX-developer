using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 统一的角色目标管理器 - 使用基于x轴位置的帧判定替代碰撞箱系统
/// </summary>
public class CharacterTargetManager : MonoBehaviour
{
    private const float CharacterTargetVolumeLength = 3.2f;

    private static CharacterTargetManager _instance;
    public static CharacterTargetManager Instance
    {
        get
        {
            if (_instance == null)
            {
                GameObject go = new GameObject("[CharacterTargetManager]");
                _instance = go.AddComponent<CharacterTargetManager>();
                DontDestroyOnLoad(go);
            }
            return _instance;
        }
    }

    // 角色列表（按x轴位置排序，不包含基地塔）
    private List<Character> allCats = new List<Character>();
    private List<Character> allEnemies = new List<Character>();
    
    // 基地塔列表（单独注册，不参与主动检索Targets）
    private CatBase catTower;
    private DogeBase dogeTower;

    // 投射物列表（按x轴位置排序）
    // 投射物不存在Friendly攻击
    private List<Character> catProjectiles = new List<Character>();
    private List<Character> enemyProjectiles = new List<Character>();
    
    // 攻击范围存储：角色 -> (nearRange, farRange)
    private Dictionary<Character, Vector2> attackRanges = new Dictionary<Character, Vector2>();
    
    // Friendly攻击模式存储：角色 -> 是否在Friendly模式（攻击同阵营）
    private Dictionary<Character, bool> friendlyModes = new Dictionary<Character, bool>();

    // 不可被检测角色（例如遁地潜行中的僵尸）
    private HashSet<Character> undetectableCharacters = new HashSet<Character>();
    // 拥有死亡标记的角色
    private HashSet<Character> deathMarkedCharacters = new HashSet<Character>();
    
    // 缓存：避免每帧重新分配
    private List<Character> tempTargets = new List<Character>();
    private readonly List<GameObject> tempTargetGameObjects = new List<GameObject>(16);
    // Emotion System
    private const string EmotionEffectRoot = "emo";
    private const string EmotionRuntimeMaanimName = "maanim";
    private const int EmotionLifeFrames = 36;
    private const float AttackEmotionImmediateChance = 0.15f;
    private const float KbEmotionImmediateChance = 0.30f;
    private const float TeamTickMinSeconds = 0.35f;
    private const float TeamTickMaxSeconds = 0.85f;
    private const float EmotionCooldownMinSeconds = 4f;
    private const float EmotionCooldownMaxSeconds = 11f;
    private const float EmotionLateBattleRampSeconds = 200f;

    private readonly Dictionary<Character, EmotionRuntimeState> emotionStates = new Dictionary<Character, EmotionRuntimeState>(256);
    private readonly Dictionary<string, bool> emotionEffectAvailability = new Dictionary<string, bool>(32);
    private TeamEmotionTickContext catEmotionTick = new TeamEmotionTickContext();
    private TeamEmotionTickContext enemyEmotionTick = new TeamEmotionTickContext();
    private float cumulativeCatSpawnPower;
    private float cumulativeEnemySpawnPower;
    private float battleStartTime;

    private static readonly EmotionUX[] EmotionPool =
    {
        EmotionUX.flower1, EmotionUX.flower2,
        EmotionUX.melody1, EmotionUX.melody2,
        EmotionUX.pollen, EmotionUX.star,
        EmotionUX.shy, EmotionUX.idea,
        EmotionUX.silent, EmotionUX.sleepy,
        EmotionUX.query, EmotionUX.call,
        EmotionUX.impatient, EmotionUX.angry,
        EmotionUX.sigh, EmotionUX.hurt,
        EmotionUX.shock1, EmotionUX.shock2,
        EmotionUX.great_shock, EmotionUX.startled,
        EmotionUX.stun, EmotionUX.doomed, EmotionUX.putsu
    };

    // n x 5 static weights: emotion x (walk, idle, attack, kb, other)
    private static readonly Dictionary<EmotionUX, int[]> EmotionStateWeightTable = new Dictionary<EmotionUX, int[]>
    {
        { EmotionUX.flower1,     new[] { 18, 15,  9,  1, 7 } },
        { EmotionUX.flower2,     new[] { 16, 13,  8,  1, 6 } },
        { EmotionUX.melody1,     new[] { 14, 16,  6,  1, 8 } },
        { EmotionUX.melody2,     new[] { 12, 14,  6,  1, 7 } },
        { EmotionUX.pollen,      new[] { 10, 12,  5,  1, 6 } },
        { EmotionUX.star,        new[] {  9, 11,  9,  1, 5 } },
        { EmotionUX.shy,         new[] {  8, 10,  4,  2, 7 } },
        { EmotionUX.idea,        new[] {  7, 11,  5,  2, 9 } },
        { EmotionUX.silent,      new[] {  7, 13,  3,  2,11 } },
        { EmotionUX.sleepy,      new[] {  7, 12,  2,  1,10 } },
        { EmotionUX.query,       new[] {  5,  9,  4,  3, 9 } },
        { EmotionUX.call,        new[] {  4,  5, 12,  4, 6 } },
        { EmotionUX.impatient,   new[] {  3,  3, 15,  7, 5 } },
        { EmotionUX.angry,       new[] {  3,  3, 18,  8, 5 } },
        { EmotionUX.sigh,        new[] {  3,  5,  5, 10,10 } },
        { EmotionUX.hurt,        new[] {  1,  2,  4, 13, 7 } },
        { EmotionUX.shock1,      new[] {  1,  2,  6, 18, 6 } },
        { EmotionUX.shock2,      new[] {  1,  1,  4, 21, 6 } },
        { EmotionUX.great_shock, new[] {  1,  1,  3, 28, 5 } },
        { EmotionUX.startled,    new[] {  2,  2,  8, 17, 8 } },
        { EmotionUX.stun,        new[] {  1,  1,  3, 23, 4 } },
        { EmotionUX.doomed,      new[] {  1,  1,  3, 16,12 } },
        { EmotionUX.putsu,       new[] {  2,  2, 14,  6, 7 } },
    };
    
    // 更新频率控制（可选优化）
    private int updateFrameInterval = 1; // 每N帧更新一次
    private int frameCounter = 0;

    private void Awake()
    {
        if (_instance == null)
        {
            _instance = this;
            DontDestroyOnLoad(gameObject);
            battleStartTime = Time.time;
        }
        else if (_instance != this)
        {
            Destroy(gameObject);
        }
    }

    /// <summary>
    /// 注册角色到管理器
    /// </summary>
    public void RegisterCharacter(Character character)
    {
        if (character == null) return;

        if (character is CatBase catBase)
        {
            catTower = catBase;
            return;
        }
        if (character is DogeBase dogeBase)
        {
            dogeTower = dogeBase;
            return;
        }
        
        if (character.IsCat())
        {
            if (!allCats.Contains(character))
            {
                allCats.Add(character);
                cumulativeCatSpawnPower += Mathf.Max(1f, character.Health);
            }
        }
        else
        {
            if (!allEnemies.Contains(character))
            {
                allEnemies.Add(character);
                cumulativeEnemySpawnPower += Mathf.Max(1f, character.Health);
            }
        }
        
        // 初始化Friendly模式为false（默认攻击敌对阵营）
        if (!friendlyModes.ContainsKey(character))
            friendlyModes[character] = false;

        EnsureEmotionState(character);
    }

    /// <summary>
    /// 从管理器注销角色
    /// </summary>
    public void UnregisterCharacter(Character character)
    {
        if (character == null) return;

        if (character is CatBase)
        {
            if (catTower == character) catTower = null;
            return;
        }
        if (character is DogeBase)
        {
            if (dogeTower == character) dogeTower = null;
            return;
        }
        
        if (character.IsCat())
            allCats.Remove(character);
        else
            allEnemies.Remove(character);
        
        // 清理攻击范围数据和Friendly模式
        attackRanges.Remove(character);
        friendlyModes.Remove(character);
        undetectableCharacters.Remove(character);
        deathMarkedCharacters.Remove(character);
        emotionStates.Remove(character);
    }

    /// <summary>
    /// 注册投射物到管理器（不参与Friendly逻辑）
    /// </summary>
    public void RegisterProjectile(Character projectile)
    {
        if (projectile == null) return;

        if (projectile.IsCat())
        {
            if (!catProjectiles.Contains(projectile))
                catProjectiles.Add(projectile);
        }
        else
        {
            if (!enemyProjectiles.Contains(projectile))
                enemyProjectiles.Add(projectile);
        }
    }

    /// <summary>
    /// 从管理器注销投射物
    /// </summary>
    public void UnregisterProjectile(Character projectile)
    {
        if (projectile == null) return;

        if (projectile.IsCat())
            catProjectiles.Remove(projectile);
        else
            enemyProjectiles.Remove(projectile);

        // 清理攻击范围数据（投射物不使用Friendly）
        attackRanges.Remove(projectile);
        undetectableCharacters.Remove(projectile);
        deathMarkedCharacters.Remove(projectile);
    }
    
    /// <summary>
    /// 设置角色的攻击范围
    /// 注意：策划填写的范围是正数，但猫是左向的，实际范围是 (-far, -near) + position
    /// 这里直接根据IsCat计算并存储最终用于判定的相对范围
    /// </summary>
    public void SetCharacterAttackRange(Character character, float nearRange, float farRange)
    {
        if (character == null) return;

        float min = Mathf.Min(nearRange, farRange)/100f;
        float max = Mathf.Max(nearRange, farRange)/100f;

        // 猫左向：范围 (-far, -near)，敌人右向：范围 (near, far)
        attackRanges[character] = character.IsCat()
            ? new Vector2(-max, -min)
            : new Vector2(min, max);
    }
    
    /// <summary>
    /// 设置角色的Friendly攻击模式（攻击同阵营）
    /// </summary>
    public void SetCharacterFriendlyMode(Character character, bool friendly)
    {
        if (character == null) return;
        friendlyModes[character] = friendly;
    }

    public void SetCharacterUndetectable(Character character, bool undetectable)
    {
        if (character == null) return;
        character.SetUndetectableByTargeting(undetectable);
        if (undetectable) undetectableCharacters.Add(character);
        else undetectableCharacters.Remove(character);
    }

    public bool IsCharacterUndetectable(Character character)
    {
        if (character == null) return false;
        return character.IsUndetectableByTargeting() || undetectableCharacters.Contains(character);
    }

    public List<Character> GetUndetectableUnits()
    {
        List<Character> result = new List<Character>();
        foreach (var character in undetectableCharacters)
        {
            if (character == null) continue;
            if (!character.gameObject.activeInHierarchy) continue;
            result.Add(character);
        }
        return result;
    }

    public void RegisterDeathMarkedCharacter(Character character)
    {
        if (character == null) return;
        deathMarkedCharacters.Add(character);
    }

    public void UnregisterDeathMarkedCharacter(Character character)
    {
        if (character == null) return;
        deathMarkedCharacters.Remove(character);
    }

    public List<Character> GetDeathMarkedCharacters()
    {
        List<Character> result = new List<Character>();
        foreach (Character character in deathMarkedCharacters)
        {
            if (character == null) continue;
            if (!character.gameObject.activeInHierarchy) continue;
            result.Add(character);
        }
        return result;
    }

    public int FillDeathMarkedCharacters(List<Character> buffer, Character exclude = null)
    {
        if (buffer == null) return 0;
        buffer.Clear();
        foreach (Character character in deathMarkedCharacters)
        {
            if (character == null) continue;
            if (character == exclude) continue;
            if (!character.gameObject.activeInHierarchy) continue;
            buffer.Add(character);
        }
        return buffer.Count;
    }

    public void NotifyCharacterStatePulse(Character character, EmotionBattleState state)
    {
        if (!CanDriveEmotion(character)) return;
        if (state != EmotionBattleState.attack && state != EmotionBattleState.kb) return;

        EmotionRuntimeState rt = EnsureEmotionState(character);
        rt.lastState = state;

        float now = Time.time;
        if (now < rt.nextAvailableAt) return;

        float chance = state == EmotionBattleState.kb ? KbEmotionImmediateChance : AttackEmotionImmediateChance;
        BattlefieldEmotionPressure pressure = BuildBattlefieldPressure(character.IsCat() ? allCats : allEnemies, character.IsCat() ? allEnemies : allCats);
        chance *= GetStatePulseChanceMultiplier(character, rt, state, pressure);
        if (UnityEngine.Random.value > Mathf.Clamp01(chance)) return;

        TrySpawnEmotion(character, state, rt, pressure);
    }

    public void NotifyCharacterDamaged(Character character, float damageRatio)
    {
        if (!CanDriveEmotion(character)) return;
        EmotionRuntimeState rt = EnsureEmotionState(character);
        float ratio = Mathf.Clamp01(damageRatio);
        rt.recentDamageRatio = Mathf.Max(rt.recentDamageRatio, ratio);
        rt.stress = Mathf.Clamp01(rt.stress + ratio * 0.55f);
    }

    /// <summary>
    /// 每帧更新所有角色的目标列表
    /// </summary>
    private void FixedUpdate()
    {
        frameCounter++;
        if (frameCounter < updateFrameInterval) return;
        frameCounter = 0;

        // 清理已销毁的角色引用（但保留Base单位，Base单位全局不可删除）
        allCats.RemoveAll(c => c == null);
        allEnemies.RemoveAll(c => c == null);
        catProjectiles.RemoveAll(p => p == null);
        enemyProjectiles.RemoveAll(p => p == null);
        deathMarkedCharacters.RemoveWhere(c => c == null || c.gameObject == null || !c.gameObject.activeInHierarchy);
        if (catTower == null || !catTower.gameObject) catTower = null;
        if (dogeTower == null || !dogeTower.gameObject) dogeTower = null;

        // 按x轴位置排序（优化：只在位置变化较大时重新排序）
        SortCharactersByX(allCats);
        SortCharactersByX(allEnemies);
        SortCharactersByX(catProjectiles);
        SortCharactersByX(enemyProjectiles);

        // 更新所有猫的目标（根据Friendly模式选择目标列表）
        UpdateTargetsForCharacters(allCats);
        
        // 更新所有敌人的目标（根据Friendly模式选择目标列表）
        UpdateTargetsForCharacters(allEnemies);

        // 更新所有投射物的目标（投射物只攻击敌对阵营）
        UpdateTargetsForProjectiles(catProjectiles, allEnemies);
        UpdateTargetsForProjectiles(enemyProjectiles, allCats);

        UpdateEmotionSystem();
    }

    /// <summary>
    /// 按x轴位置排序角色列表
    /// </summary>
    private void SortCharactersByX(List<Character> characters)
    {
        characters.Sort((a, b) => 
        {
            if (a == null || b == null) return 0;
            return a.transform.position.x.CompareTo(b.transform.position.x);
        });
    }

    /// <summary>
    /// 为角色列表更新目标（根据Friendly模式自动选择目标列表）
    /// </summary>
    private void UpdateTargetsForCharacters(List<Character> attackers)
    {
        foreach (var attacker in attackers)
        {
            if (attacker == null || !attacker.gameObject.activeInHierarchy) continue;
            
            // 根据Friendly模式选择正确的目标列表
            bool isFriendly = friendlyModes.TryGetValue(attacker, out bool friendly) && friendly;
            bool isCat = attacker.IsCat();
            
            List<Character> targetList = isFriendly
                ? (isCat ? allCats : allEnemies)      // Friendly模式：同阵营
                : (isCat ? allEnemies : allCats);     // 非Friendly模式：敌对阵营
            
            UpdateTargetsForCharacter(attacker, targetList, !isFriendly);
        }
    }

    /// <summary>
    /// 为投射物列表更新目标（投射物只攻击敌对阵营）
    /// </summary>
    private void UpdateTargetsForProjectiles(List<Character> projectiles, List<Character> targetList)
    {
        foreach (var projectile in projectiles)
        {
            if (projectile == null || !projectile.gameObject.activeInHierarchy) continue;
            UpdateTargetsForCharacter(projectile, targetList, true);
        }
    }

    /// <summary>
    /// 立即刷新投射物目标（单帧判定用）
    /// </summary>
    public void RefreshTargetsForProjectile(Character projectile)
    {
        if (projectile == null) return;

        List<Character> targets = projectile.IsCat() ? allEnemies : allCats;
        UpdateTargetsForCharacter(projectile, targets, true);
    }

    /// <summary>
    /// 为单个角色更新目标列表（基于x轴距离）
    /// </summary>
    private void UpdateTargetsForCharacter(Character attacker, List<Character> potentialTargets, bool includeTowers)
    {
        if (attacker == null || potentialTargets == null) return;

        // KB状态下不参与判定，直接清空Targets
        if (attacker.IsOnKB())
        {
            UpdateCharacterTargets(attacker, tempTargets, null);
            return;
        }

        // 注意：目标列表已经在UpdateTargetsForCharacters中根据Friendly模式选择好了
        // 这里不需要再次检查阵营，直接使用传入的目标列表即可
        
        // 获取当前范围（SetCharacterAttackRange已写入方向后的相对范围）
        if (!attackRanges.TryGetValue(attacker, out Vector2 currentRange))
        {
            // 确保有默认检测范围
            SetCharacterAttackRange(attacker, 0, attacker.DetectionRange);
            currentRange = attackRanges[attacker];
        }
        float nearRange = currentRange.x;
        float farRange = currentRange.y;

        float minRange = Mathf.Min(nearRange, farRange);
        float maxRange = Mathf.Max(nearRange, farRange);
        
        // 清空临时列表
        tempTargets.Clear();
        
        // 使用二分查找优化：找到可能范围内的目标
        // 由于列表已按x排序，可以快速定位范围
        // 注意：目标列表已经在UpdateTargetsForCharacters中根据Friendly模式选择好了，这里不需要再次检查阵营
        float attackerX = attacker.transform.position.x;
        float worldMin = attackerX + minRange;
        float worldMax = attackerX + maxRange;
        int startIndex = FindFirstIndexByX(potentialTargets, worldMin - CharacterTargetVolumeLength);
        for (int i = startIndex; i < potentialTargets.Count; i++)
        {
            Character target = potentialTargets[i];
            if (target == null || target == attacker) continue; // 跳过自己和null
            if (!target.gameObject.activeInHierarchy) continue;
            // if (target.GetHealth() <= 0) continue;
            GetCharacterTargetVolumeRange(target, out float targetMinX, out float targetMaxX);
            if (targetMinX > worldMax)
            {
                break;
            }
            if (targetMaxX < worldMin) continue;
            if (target.IsOnKB()) continue; // KB状态不参与判定
            bool targetUndetectable = IsCharacterUndetectable(target);
            if (targetUndetectable && !attacker.CanTargetUndetectable()) continue;
            tempTargets.Add(target);
        }

        Character baseTarget = includeTowers ? GetEnemyBaseTarget(attacker, minRange, maxRange) : null;
        
        // 更新角色的Targets列表
        UpdateCharacterTargets(attacker, tempTargets, baseTarget);
    }

    private Character GetEnemyBaseTarget(Character attacker, float minRange, float maxRange)
    {
        Character tower = attacker.IsCat() ? dogeTower : catTower;
        if (tower == null) return null;
        if (!tower.gameObject.activeInHierarchy) return null;
        if (IsCharacterUndetectable(tower)) return null;
        return IsTargetInRange(attacker, tower, minRange, maxRange) ? tower : null;
    }

    private void TryAddTowerTarget(Character attacker, Character tower, float minRange, float maxRange)
    {
        if (tower == null || tower == attacker) return;
        if (!tower.gameObject.activeInHierarchy) return;
        if (IsCharacterUndetectable(tower)) return;
        if (tempTargets.Contains(tower)) return;
        if (IsTargetInRange(attacker, tower, minRange, maxRange))
        {
            tempTargets.Add(tower);
        }
    }

    private bool IsTargetInRange(Character attacker, Character target, float minRange, float maxRange)
    {
        float attackerX = attacker.transform.position.x;
        float worldMin = attackerX + minRange;
        float worldMax = attackerX + maxRange;

        // 塔不是单点：猫塔命中区间为 [catTowerX, +∞)，狗塔命中区间为 (-∞, dogeTowerX]
        if (target is CatBase)
        {
            return worldMax >= target.transform.position.x;
        }
        if (target is DogeBase)
        {
            return worldMin <= target.transform.position.x;
        }

        GetCharacterTargetVolumeRange(target, out float targetMinX, out float targetMaxX);
        return targetMaxX >= worldMin && targetMinX <= worldMax;
    }

    private static void GetCharacterTargetVolumeRange(Character target, out float minX, out float maxX)
    {
        float targetX = target.transform.position.x;
        if (target.IsCat())
        {
            minX = targetX;
            maxX = targetX + CharacterTargetVolumeLength;
        }
        else
        {
            minX = targetX - CharacterTargetVolumeLength;
            maxX = targetX;
        }
    }

    /// <summary>
    /// 更新角色的Targets列表（保持与原有接口兼容）
    /// </summary>
    private void UpdateCharacterTargets(Character character, List<Character> newTargets, Character baseTarget)
    {
        if (character == null) return;
        tempTargetGameObjects.Clear();
        for (int i = 0; i < newTargets.Count; i++)
        {
            Character target = newTargets[i];
            if (target == null) continue;
            GameObject go = target.gameObject;
            if (go == null || !go.activeInHierarchy) continue;
            tempTargetGameObjects.Add(go);
        }

        // 更新Targets列表
        character.Targets.Clear();
        character.Targets.AddRange(tempTargetGameObjects);
        character.BaseTarget = baseTarget != null ? baseTarget.gameObject : null;

        if (character.BaseTarget != null && !character.BaseTarget.activeInHierarchy)
            character.BaseTarget = null;
    }

    /// <summary>
    /// 获取角色在指定范围内的所有目标（用于攻击范围判定）
    /// </summary>
    public List<Character> GetTargetsInRange(Character attacker, float nearRange, float farRange)
    {
        if (attacker == null) return new List<Character>();
        
        bool isCat = attacker.IsCat();
        bool isFriendly = friendlyModes.TryGetValue(attacker, out bool friendly) && friendly;
        
        // 根据Friendly模式选择目标列表
        List<Character> targets = isFriendly 
            ? (isCat ? allCats : allEnemies)  // Friendly模式：同阵营
            : (isCat ? allEnemies : allCats); // 非Friendly模式：敌对阵营
        
        List<Character> result = new List<Character>();
        
        float near = nearRange / 10f;
        float far = farRange / 10f;
        float minRange = isCat ? -Mathf.Max(near, far) : Mathf.Min(near, far);
        float maxRange = isCat ? -Mathf.Min(near, far) : Mathf.Max(near, far);
        
        float attackerX = attacker.transform.position.x;
        float worldMin = attackerX + minRange;
        float worldMax = attackerX + maxRange;
        int startIndex = FindFirstIndexByX(targets, worldMin - CharacterTargetVolumeLength);
        for (int i = startIndex; i < targets.Count; i++)
        {
            Character target = targets[i];
            if (target == null || !target.gameObject.activeInHierarchy) continue;
            GetCharacterTargetVolumeRange(target, out float targetMinX, out float targetMaxX);
            if (targetMinX > worldMax) break;
            if (targetMaxX < worldMin) continue;
            // if (target.GetHealth() <= 0) continue;
            bool targetUndetectable = IsCharacterUndetectable(target);
            if (targetUndetectable && !attacker.CanTargetUndetectable()) continue;
            result.Add(target);
        }

        if (!isFriendly)
        {
            if (attacker.IsCat())
                TryAddRangeTowerResult(attacker, dogeTower, minRange, maxRange, result);
            else
                TryAddRangeTowerResult(attacker, catTower, minRange, maxRange, result);
        }
        
        return result;
    }

    private void TryAddRangeTowerResult(Character attacker, Character tower, float minRange, float maxRange, List<Character> result)
    {
        if (tower == null || tower == attacker) return;
        if (!tower.gameObject.activeInHierarchy) return;
        if (IsCharacterUndetectable(tower)) return;
        if (result.Contains(tower)) return;
        if (IsTargetInRange(attacker, tower, minRange, maxRange))
        {
            result.Add(tower);
        }
    }

    private void UpdateEmotionSystem()
    {
        float now = Time.time;
        ProcessTeamEmotionTick(allCats, allEnemies, ref catEmotionTick, now);
        ProcessTeamEmotionTick(allEnemies, allCats, ref enemyEmotionTick, now);
    }

    private void ProcessTeamEmotionTick(List<Character> allies, List<Character> enemies, ref TeamEmotionTickContext teamTick, float now)
    {
        if (allies == null || allies.Count == 0) return;
        if (now < teamTick.nextTickAt) return;

        BattlefieldEmotionPressure pressure = BuildBattlefieldPressure(allies, enemies);
        int aliveAllies = pressure.aliveAllies;
        if (aliveAllies <= 0)
        {
            teamTick.nextTickAt = now + TeamTickMaxSeconds;
            return;
        }

        int attempts = Mathf.Clamp(1 + aliveAllies / 8 + Mathf.RoundToInt(pressure.battleIntensity * 2f), 1, 8);
        for (int i = 0; i < attempts; i++)
        {
            Character candidate = PickRandomAliveCharacter(allies);
            if (!CanDriveEmotion(candidate)) continue;

            EmotionRuntimeState rt = EnsureEmotionState(candidate);
            EmotionBattleState state = ResolveEmotionState(candidate);
            rt.lastState = state;
            rt.stress = Mathf.Clamp01(rt.stress * 0.98f);

            // Attack/KB are handled by immediate pulse path on state entry.
            if (state == EmotionBattleState.attack || state == EmotionBattleState.kb) continue;
            if (now < rt.nextAvailableAt) continue;

            float chance = GetPeriodicTriggerChance(candidate, state, rt, pressure);
            if (UnityEngine.Random.value <= chance)
            {
                TrySpawnEmotion(candidate, state, rt, pressure);
            }
        }

        float teamDensity = Mathf.Clamp01(aliveAllies / 18f);
        float next = Mathf.Lerp(TeamTickMaxSeconds, TeamTickMinSeconds, teamDensity);
        next *= Mathf.Lerp(1f, 0.58f, pressure.battleIntensity);
        teamTick.nextTickAt = now + UnityEngine.Random.Range(next * 0.9f, next * 1.2f);
    }

    private bool TrySpawnEmotion(Character character, EmotionBattleState state, EmotionRuntimeState rt, BattlefieldEmotionPressure pressure)
    {
        EmotionUX selected = SelectEmotion(character, state, rt, pressure);
        if (selected == EmotionUX.none) return false;
        if (!PlayEmotionEffect(character, selected)) return false;

        float cooldown = GetNextEmotionCooldownSeconds(character, state, rt, pressure);
        rt.nextAvailableAt = Time.time + cooldown;
        rt.recentDamageRatio *= 0.45f;
        rt.stress *= 0.7f;
        return true;
    }

    private bool PlayEmotionEffect(Character character, EmotionUX emotion)
    {
        if (character == null || character.EM == null || emotion == EmotionUX.none) return false;
        Vector3 localOffset = new Vector3(0f, character.topPositionY + 1f, 1f);
        Vector3 worldPos = character.transform.TransformPoint(localOffset);

        string emotionName = emotion.ToString();
        if (!HasEmotionEffect(emotionName)) return false;

        string resourceRoot = $"Effects/{EmotionEffectRoot}/{emotionName}/";
        AnimationDisplayer ad = character.EM.InstantiateRuntimeBattleObject(
            resourceRoot,
            new[] { EmotionRuntimeMaanimName },
            worldPos,
            null,
            worldPositionStays: true);
        if (ad == null) return false;

        EmotionFollowAnchor follow = ad.GetComponent<EmotionFollowAnchor>();
        if (follow == null) follow = ad.gameObject.AddComponent<EmotionFollowAnchor>();
        follow.Bind(character, localOffset, character.EM, GetEmotionRuntimePoolKey(emotionName), EmotionLifeFrames);

        // Enemy emotion visuals should be mirrored.
        if (!character.IsCat())
        {
            Vector3 s = ad.transform.localScale;
            s.x = -Mathf.Abs(s.x);
            ad.transform.localScale = s;
        }
        return true;
    }

    private bool HasEmotionEffect(string effectName)
    {
        if (string.IsNullOrEmpty(effectName)) return false;
        if (emotionEffectAvailability.TryGetValue(effectName, out bool cached)) return cached;
        bool exists = Resources.Load<Texture2D>($"Effects/{EmotionEffectRoot}/{effectName}/sprite") != null;
        emotionEffectAvailability[effectName] = exists;
        return exists;
    }

    private static string GetEmotionRuntimePoolKey(string emotionName)
    {
        return $"runtime:Effects/{EmotionEffectRoot}/{emotionName}/|{EmotionRuntimeMaanimName}";
    }

    private EmotionUX SelectEmotion(Character character, EmotionBattleState state, EmotionRuntimeState rt, BattlefieldEmotionPressure pressure)
    {
        float allyVsEnemy = pressure.advantage;
        int stateIndex = (int)state;
        float total = 0f;
        float[] rollWeights = new float[EmotionPool.Length];

        for (int i = 0; i < EmotionPool.Length; i++)
        {
            EmotionUX emotion = EmotionPool[i];
            if (!EmotionStateWeightTable.TryGetValue(emotion, out int[] w)) continue;
            int baseW = w[stateIndex];
            if (baseW <= 0) continue;

            float mul = 1f;
            if (IsPositiveEmotion(emotion))
            {
                mul *= 1f + Mathf.Max(0f, allyVsEnemy) * 0.9f;
                if (state == EmotionBattleState.walk || state == EmotionBattleState.idle || state == EmotionBattleState.attack)
                    mul *= 1.12f;
                mul *= 1f + pressure.battleIntensity * 0.28f;
            }
            if (IsNegativeEmotion(emotion))
            {
                float disadvantage = Mathf.Max(0f, -allyVsEnemy);
                mul *= 1f + disadvantage * 1.05f;
                if (character.IsCat()) mul *= 1f + disadvantage * 0.45f;
                mul *= 1f + rt.stress * 1.35f;
                mul *= 1f + rt.recentDamageRatio * 1.6f;
                mul *= 1f + pressure.battleIntensity * 0.42f;
            }
            if (emotion == EmotionUX.doomed || emotion == EmotionUX.shock2 || emotion == EmotionUX.great_shock)
            {
                float disadvantage = Mathf.Max(0f, -allyVsEnemy);
                mul *= 1f + disadvantage * (character.IsCat() ? 2.25f : 1.55f);
                mul *= 1f + pressure.battleIntensity * 0.5f;
            }
            if (emotion == character.BaseEmotion)
            {
                mul *= 2.8f;
            }

            float finalW = Mathf.Max(0f, baseW * mul);
            rollWeights[i] = finalW;
            total += finalW;
        }

        if (total <= 0f) return EmotionUX.none;
        float roll = UnityEngine.Random.value * total;
        float acc = 0f;
        for (int i = 0; i < EmotionPool.Length; i++)
        {
            acc += rollWeights[i];
            if (roll <= acc) return EmotionPool[i];
        }
        return EmotionPool[EmotionPool.Length - 1];
    }

    private float GetPeriodicTriggerChance(Character character, EmotionBattleState state, EmotionRuntimeState rt, BattlefieldEmotionPressure pressure)
    {
        float baseChance = state switch
        {
            EmotionBattleState.walk => 0.125f,
            EmotionBattleState.idle => 0.10f,
            EmotionBattleState.other => 0.085f,
            _ => 0.05f
        };

        float advantage = pressure.advantage;
        float stateMoodMul = 1f;
        if (state == EmotionBattleState.walk || state == EmotionBattleState.idle)
        {
            stateMoodMul += Mathf.Max(0f, advantage) * 0.9f;
            stateMoodMul += Mathf.Max(0f, -advantage) * 0.25f;
        }
        else
        {
            stateMoodMul += Mathf.Max(0f, -advantage) * 0.95f;
        }

        float damageMul = 1f + rt.recentDamageRatio * 1.4f;
        float stressMul = 1f + rt.stress * 0.9f;
        float chance = baseChance * stateMoodMul * damageMul * stressMul;
        chance *= Mathf.Lerp(1f, 1.5f, pressure.battleIntensity);

        // Higher pressure yields slightly denser emotion feedback.
        chance *= Mathf.Lerp(0.92f, 1.22f, 1f - Mathf.Clamp01(character.GetHealth() / Mathf.Max(1f, character.GetMaxHealth())));
        return Mathf.Clamp01(chance);
    }

    private float GetNextEmotionCooldownSeconds(Character character, EmotionBattleState state, EmotionRuntimeState rt, BattlefieldEmotionPressure pressure)
    {
        float cd = UnityEngine.Random.Range(EmotionCooldownMinSeconds, EmotionCooldownMaxSeconds);
        cd *= Mathf.Lerp(1f, 0.65f, rt.stress);
        cd *= Mathf.Lerp(1f, 0.7f, rt.recentDamageRatio);
        if (state == EmotionBattleState.kb) cd *= 0.85f;
        if (state == EmotionBattleState.attack) cd *= 0.9f;
        if (character.BaseEmotion != EmotionUX.none) cd *= 0.92f;
        cd *= Mathf.Lerp(1f, 0.55f, pressure.battleIntensity);
        return Mathf.Clamp(cd, 1.6f, 18f);
    }

    private float GetStatePulseChanceMultiplier(Character character, EmotionRuntimeState rt, EmotionBattleState state, BattlefieldEmotionPressure pressure)
    {
        float mul = 1f;
        float advantage = pressure.advantage;
        if (state == EmotionBattleState.attack)
        {
            mul *= 1f + Mathf.Max(0f, advantage) * 0.35f;
            mul *= 1f + Mathf.Max(0f, -advantage) * 0.2f;
        }
        else if (state == EmotionBattleState.kb)
        {
            mul *= 1f + Mathf.Max(0f, -advantage) * 0.65f;
            mul *= 1f + rt.recentDamageRatio * 0.8f;
        }
        mul *= 1f + rt.stress * 0.35f;
        mul *= Mathf.Lerp(1f, 1.3f, pressure.battleIntensity);
        return mul;
    }

    private EmotionBattleState ResolveEmotionState(Character character)
    {
        if (character == null) return EmotionBattleState.other;
        if (character.IsOnKB()) return EmotionBattleState.kb;
        if (character.IsOnAttack()) return EmotionBattleState.attack;

        bool hasTarget = (character.Targets != null && character.Targets.Count > 0) || character.BaseTarget != null;
        if (hasTarget) return EmotionBattleState.idle;
        if (Mathf.Abs(character.GetRealSpeed()) > 0) return EmotionBattleState.walk;
        return EmotionBattleState.other;
    }

    private BattlefieldEmotionPressure BuildBattlefieldPressure(List<Character> allies, List<Character> enemies)
    {
        CountAliveAndMaxHealth(allies, out int allyCount, out float allyMaxHealth);
        CountAliveAndMaxHealth(enemies, out int enemyCount, out float enemyMaxHealth);

        int totalCount = Mathf.Max(1, allyCount + enemyCount);
        float countAdv = (allyCount - enemyCount) / (float)totalCount;

        float totalMaxHealth = Mathf.Max(1f, allyMaxHealth + enemyMaxHealth);
        float maxHealthAdv = (allyMaxHealth - enemyMaxHealth) / totalMaxHealth;

        float signedSpawnPower = cumulativeCatSpawnPower - cumulativeEnemySpawnPower;
        float spawnPowerNorm = Mathf.Max(1f, cumulativeCatSpawnPower + cumulativeEnemySpawnPower);
        float spawnAdvGlobal = Mathf.Clamp(signedSpawnPower / spawnPowerNorm, -1f, 1f);
        float spawnAdv = allies == allCats ? spawnAdvGlobal : -spawnAdvGlobal;

        float battleElapsed = Mathf.Max(0f, Time.time - battleStartTime);
        float battleIntensity = Mathf.Clamp01(battleElapsed / EmotionLateBattleRampSeconds);

        float earlyWeight = Mathf.Lerp(0.75f, 0.25f, battleIntensity);
        float lateCountWeight = Mathf.Lerp(0.25f, 0.75f, battleIntensity);
        float hpWeight = 0.35f;
        float advantage = spawnAdv * earlyWeight + countAdv * lateCountWeight + maxHealthAdv * hpWeight;
        advantage = Mathf.Clamp(advantage, -1f, 1f);

        return new BattlefieldEmotionPressure
        {
            aliveAllies = allyCount,
            aliveEnemies = enemyCount,
            allyMaxHealthTotal = allyMaxHealth,
            enemyMaxHealthTotal = enemyMaxHealth,
            countAdvantage = countAdv,
            hpAdvantage = maxHealthAdv,
            spawnAdvantage = spawnAdv,
            advantage = advantage,
            battleIntensity = battleIntensity
        };
    }

    private void CountAliveAndMaxHealth(List<Character> list, out int count, out float totalMaxHealth)
    {
        count = 0;
        totalMaxHealth = 0f;
        if (list == null) return;
        for (int i = 0; i < list.Count; i++)
        {
            Character c = list[i];
            if (!CanDriveEmotion(c)) continue;
            count++;
            totalMaxHealth += Mathf.Max(1f, c.GetMaxHealth());
        }
    }

    private Character PickRandomAliveCharacter(List<Character> list)
    {
        if (list == null || list.Count == 0) return null;
        int start = UnityEngine.Random.Range(0, list.Count);
        for (int i = 0; i < list.Count; i++)
        {
            Character c = list[(start + i) % list.Count];
            if (CanDriveEmotion(c)) return c;
        }
        return null;
    }

    private bool CanDriveEmotion(Character character)
    {
        return character != null
               && character.gameObject != null
               && character.gameObject.activeInHierarchy
               && !(character is CatBase)
               && !(character is DogeBase)
               && character.EM != null;
    }

    private EmotionRuntimeState EnsureEmotionState(Character character)
    {
        if (!emotionStates.TryGetValue(character, out EmotionRuntimeState rt) || rt == null)
        {
            rt = new EmotionRuntimeState
            {
                nextAvailableAt = Time.time + UnityEngine.Random.Range(1.2f, 3.8f),
                lastState = EmotionBattleState.other,
                recentDamageRatio = 0f,
                stress = 0f
            };
            emotionStates[character] = rt;
        }
        return rt;
    }

    private static bool IsPositiveEmotion(EmotionUX emotion)
    {
        return emotion == EmotionUX.flower1
               || emotion == EmotionUX.flower2
               || emotion == EmotionUX.melody1
               || emotion == EmotionUX.melody2
               || emotion == EmotionUX.pollen
               || emotion == EmotionUX.star
               || emotion == EmotionUX.shy
               || emotion == EmotionUX.idea
               || emotion == EmotionUX.silent
               || emotion == EmotionUX.sleepy;
    }

    private static bool IsNegativeEmotion(EmotionUX emotion)
    {
        return emotion == EmotionUX.impatient
               || emotion == EmotionUX.angry
               || emotion == EmotionUX.sigh
               || emotion == EmotionUX.hurt
               || emotion == EmotionUX.shock1
               || emotion == EmotionUX.shock2
               || emotion == EmotionUX.great_shock
               || emotion == EmotionUX.startled
               || emotion == EmotionUX.stun
               || emotion == EmotionUX.doomed
               || emotion == EmotionUX.putsu;
    }

    /// <summary>
    /// 设置更新频率（性能调优）
    /// </summary>
    public void SetUpdateInterval(int frames)
    {
        updateFrameInterval = Mathf.Max(1, frames);
    }

    public bool IsTargetInCurrentRange(Character attacker, Character target)
    {
        if (attacker == null || target == null) return false;
        if (!attackRanges.TryGetValue(attacker, out Vector2 currentRange))
        {
            SetCharacterAttackRange(attacker, 0, attacker.DetectionRange);
            currentRange = attackRanges[attacker];
        }
        float minRange = Mathf.Min(currentRange.x, currentRange.y);
        float maxRange = Mathf.Max(currentRange.x, currentRange.y);
        return IsTargetInRange(attacker, target, minRange, maxRange);
    }
    private static int FindFirstIndexByX(List<Character> characters, float worldMin)
    {
        int low = 0;
        int high = characters.Count - 1;
        while (low <= high)
        {
            int mid = (low + high) >> 1;
            Character c = characters[mid];
            float x = c != null ? c.transform.position.x : float.NegativeInfinity;
            if (x < worldMin) low = mid + 1;
            else high = mid - 1;
        }
        return Mathf.Clamp(low, 0, characters.Count);
    }

    private sealed class EmotionRuntimeState
    {
        public float nextAvailableAt;
        public EmotionBattleState lastState;
        public float recentDamageRatio;
        public float stress;
    }

    private struct BattlefieldEmotionPressure
    {
        public int aliveAllies;
        public int aliveEnemies;
        public float allyMaxHealthTotal;
        public float enemyMaxHealthTotal;
        public float countAdvantage;
        public float hpAdvantage;
        public float spawnAdvantage;
        public float advantage;
        public float battleIntensity;
    }

    private struct TeamEmotionTickContext
    {
        public float nextTickAt;
    }

    private sealed class EmotionFollowAnchor : MonoBehaviour
    {
        private Character target;
        private Vector3 localOffset;
        private EffectManager manager;
        private AnimationDisplayer display;
        private string poolKey;
        private int remainingFrames;

        public void Bind(Character followTarget, Vector3 offset, EffectManager effectManager, string key, int lifeFrames)
        {
            target = followTarget;
            localOffset = offset;
            manager = effectManager;
            poolKey = key;
            display = GetComponent<AnimationDisplayer>();
            remainingFrames = Mathf.Max(1, lifeFrames);
            enabled = true;
            UpdatePosition();
        }

        private void FixedUpdate()
        {
            if (!UpdatePosition())
            {
                RecycleNow();
                return;
            }

            remainingFrames--;
            if (remainingFrames <= 0)
            {
                RecycleNow();
            }
        }

        private bool UpdatePosition()
        {
            if (target == null || !target.gameObject.activeInHierarchy) return false;
            transform.position = target.transform.TransformPoint(localOffset);
            return true;
        }

        private void RecycleNow()
        {
            enabled = false;
            if (manager != null && display != null && !string.IsNullOrEmpty(poolKey))
            {
                manager.RecycleDisplay(display, poolKey);
            }
            else
            {
                gameObject.SetActive(false);
            }
        }
    }

    /// <summary>
    /// 清理所有注册的角色（场景切换时调用）
    /// </summary>
    public void ClearAll()
    {
        allCats.Clear();
        allEnemies.Clear();
        catTower = null;
        dogeTower = null;
        catProjectiles.Clear();
        enemyProjectiles.Clear();
        attackRanges.Clear();
        friendlyModes.Clear();
        undetectableCharacters.Clear();
        deathMarkedCharacters.Clear();
        emotionStates.Clear();
        catEmotionTick = new TeamEmotionTickContext();
        enemyEmotionTick = new TeamEmotionTickContext();
        cumulativeCatSpawnPower = 0f;
        cumulativeEnemySpawnPower = 0f;
        battleStartTime = Time.time;
    }
}
