using System.Collections.Generic;
using System.Linq;
using UnityEngine;

/// <summary>
/// 统一的角色目标管理器 - 使用基于x轴位置的帧判定替代碰撞箱系统
/// </summary>
public class CharacterTargetManager : MonoBehaviour
{
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

    // 角色列表（按x轴位置排序）
    // 注意：包括CatBase和DogeBase，它们全局不可删除
    private List<Character> allCats = new List<Character>();
    private List<Character> allEnemies = new List<Character>();

    // 投射物列表（按x轴位置排序）
    // 投射物不存在Friendly攻击
    private List<Character> catProjectiles = new List<Character>();
    private List<Character> enemyProjectiles = new List<Character>();
    
    // 攻击范围存储：角色 -> (nearRange, farRange)
    private Dictionary<Character, Vector2> attackRanges = new Dictionary<Character, Vector2>();
    
    // Friendly攻击模式存储：角色 -> 是否在Friendly模式（攻击同阵营）
    private Dictionary<Character, bool> friendlyModes = new Dictionary<Character, bool>();
    
    // 缓存：避免每帧重新分配
    private List<Character> tempTargets = new List<Character>();
    
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
        
        // Base单位不可被注销
        if (character.gameObject.name.Contains("Base")) return;
        
        if (character.IsCat())
            allCats.Remove(character);
        else
            allEnemies.Remove(character);
        
        // 清理攻击范围数据和Friendly模式
        attackRanges.Remove(character);
        friendlyModes.Remove(character);
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
            bool isFriendly = friendlyModes.ContainsKey(attacker) && friendlyModes[attacker];
            bool isCat = attacker.IsCat();
            
            List<Character> targetList = isFriendly
                ? (isCat ? allCats : allEnemies)      // Friendly模式：同阵营
                : (isCat ? allEnemies : allCats);     // 非Friendly模式：敌对阵营
            
            UpdateTargetsForCharacter(attacker, targetList);
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
            UpdateTargetsForCharacter(projectile, targetList);
        }
    }

    /// <summary>
    /// 立即刷新投射物目标（单帧判定用）
    /// </summary>
    public void RefreshTargetsForProjectile(Character projectile)
    {
        if (projectile == null) return;

        List<Character> targets = projectile.IsCat() ? allEnemies : allCats;
        UpdateTargetsForCharacter(projectile, targets);
    }

    /// <summary>
    /// 为单个角色更新目标列表（基于x轴距离）
    /// </summary>
    private void UpdateTargetsForCharacter(Character attacker, List<Character> potentialTargets)
    {
        if (attacker == null || potentialTargets == null || potentialTargets.Count == 0) return;

        // KB状态下不参与判定，直接清空Targets
        if (attacker.IsOnKB())
        {
            UpdateCharacterTargets(attacker, tempTargets);
            return;
        }

        float attackerX = attacker.transform.position.x;
        
        // 注意：目标列表已经在UpdateTargetsForCharacters中根据Friendly模式选择好了
        // 这里不需要再次检查阵营，直接使用传入的目标列表即可
        if (potentialTargets == null || potentialTargets.Count == 0) return;
        
        // 获取当前范围（SetCharacterAttackRange已写入方向后的相对范围）
        if (!attackRanges.ContainsKey(attacker))
        {
            // 确保有默认检测范围
            SetCharacterAttackRange(attacker, 0, attacker.DetectionRange);
        }
        Vector2 currentRange = attackRanges[attacker];
        float nearRange = currentRange.x;
        float farRange = currentRange.y;

        float minRange = Mathf.Min(nearRange, farRange);
        float maxRange = Mathf.Max(nearRange, farRange);
        
        // 清空临时列表
        tempTargets.Clear();
        
        // 使用二分查找优化：找到可能范围内的目标
        // 由于列表已按x排序，可以快速定位范围
        // 注意：目标列表已经在UpdateTargetsForCharacters中根据Friendly模式选择好了，这里不需要再次检查阵营
        foreach (var target in potentialTargets)
        {
            if (target == null || target == attacker) continue; // 跳过自己和null
            if (!target.gameObject.activeInHierarchy) continue;
            // if (target.GetHealth() <= 0) continue;
            if (target.IsOnKB()) continue; // KB状态不参与判定
            
            float targetX = target.transform.position.x;
            
            // 使用相对范围直接判定（无需区分阵营方向）
            float relativeX = targetX - attackerX;
            bool inRange = relativeX >= minRange && relativeX <= maxRange;
            
            if (inRange)
            {
                tempTargets.Add(target);
            }
        }
        
        // 更新角色的Targets列表
        UpdateCharacterTargets(attacker, tempTargets);
    }

    /// <summary>
    /// 更新角色的Targets列表（保持与原有接口兼容）
    /// </summary>
    private void UpdateCharacterTargets(Character character, List<Character> newTargets)
    {
        if (character == null) return;
        
        // 转换为GameObject列表（保持兼容性）
        var newGameObjectTargets = newTargets.Select(c => c.gameObject).ToList();
        
        // 更新Targets列表
        character.Targets.Clear();
        character.Targets.AddRange(newGameObjectTargets);
        
        // 清理null引用
        character.Targets.RemoveAll(go => go == null);
    }

    /// <summary>
    /// 获取角色在指定范围内的所有目标（用于攻击范围判定）
    /// </summary>
    public List<Character> GetTargetsInRange(Character attacker, float nearRange, float farRange)
    {
        if (attacker == null) return new List<Character>();
        
        bool isCat = attacker.IsCat();
        bool isFriendly = friendlyModes.ContainsKey(attacker) && friendlyModes[attacker];
        
        // 根据Friendly模式选择目标列表
        List<Character> targets = isFriendly 
            ? (isCat ? allCats : allEnemies)  // Friendly模式：同阵营
            : (isCat ? allEnemies : allCats); // 非Friendly模式：敌对阵营
        
        List<Character> result = new List<Character>();
        
        float attackerX = attacker.transform.position.x;
        float near = nearRange / 10f;
        float far = farRange / 10f;
        
        foreach (var target in targets)
        {
            if (target == null || !target.gameObject.activeInHierarchy) continue;
            // if (target.GetHealth() <= 0) continue;
            
            float targetX = target.transform.position.x;
            float distance = Mathf.Abs(targetX - attackerX);
            
            // 检查是否在攻击范围内
            // 猫左向：检测左边，范围 (-far, -near)
            // 敌人右向：检测右边，范围 (near, far)
            bool inRange = false;
            if (isCat)
            {
                inRange = targetX < attackerX && distance >= near && distance <= far;
            }
            else
            {
                inRange = targetX > attackerX && distance >= near && distance <= far;
            }
            
            if (inRange)
            {
                result.Add(target);
            }
        }
        
        return result;
    }

    /// <summary>
    /// 设置更新频率（性能调优）
    /// </summary>
    public void SetUpdateInterval(int frames)
    {
        updateFrameInterval = Mathf.Max(1, frames);
    }

    /// <summary>
    /// 清理所有注册的角色（场景切换时调用）
    /// </summary>
    public void ClearAll()
    {
        allCats.Clear();
        allEnemies.Clear();
        catProjectiles.Clear();
        enemyProjectiles.Clear();
        attackRanges.Clear();
        friendlyModes.Clear();
    }
}
