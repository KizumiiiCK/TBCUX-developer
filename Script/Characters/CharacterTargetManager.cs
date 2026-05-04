using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 统一的角色目标管理器 - 使用基于x轴位置的帧判定替代碰撞箱系统
/// </summary>
public class CharacterTargetManager : MonoBehaviour
{
    private const float CharacterTargetVolumeLength = 2f;

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
    
    // 缓存：避免每帧重新分配
    private List<Character> tempTargets = new List<Character>();
    private readonly List<GameObject> tempTargetGameObjects = new List<GameObject>(16);
    
    // 更新频率控制（可选优化）
    private int updateFrameInterval = 1; // 每N帧更新一次
    private int frameCounter = 0;

    private void Awake()
    {
        if (_instance == null)
        {
            _instance = this;
            DontDestroyOnLoad(gameObject);
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
                allCats.Add(character);
        }
        else
        {
            if (!allEnemies.Contains(character))
                allEnemies.Add(character);
        }
        
        // 初始化Friendly模式为false（默认攻击敌对阵营）
        if (!friendlyModes.ContainsKey(character))
            friendlyModes[character] = false;
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
    }
}
