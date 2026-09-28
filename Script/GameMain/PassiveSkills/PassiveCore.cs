using System;
using System.Collections;
using System.Collections.Generic;
using System.Reflection;
using UnityEngine;

public static class AbilityInstaller
{
    private static readonly Dictionary<AbilityName, Type> skillMap = new Dictionary<AbilityName, Type>
    {
        { AbilityName.strategic,  typeof(AffectByStrategy)},
        { AbilityName.practician,  typeof(Practician)},
        { AbilityName.support,    typeof(Supporter)},
        { AbilityName.survive,    typeof(Survive)},
        { AbilityName.strengthen, typeof(Strengthen)},
        { AbilityName.critical,   typeof(Critical)},
        { AbilityName.zombieKiller, typeof(ZombieKiller)},
        { AbilityName.soulStrike, typeof(SoulStrike)},
        { AbilityName.savage,     typeof(Savage)},
        { AbilityName.wave,       typeof(Wave)},
        { AbilityName.miniWave,   typeof(MiniWave)},
        { AbilityName.wave_stop,  typeof(WaveStop)},
        { AbilityName.surge,      typeof(Surge)},
        { AbilityName.miniSurge,  typeof(MiniSurge)},
        { AbilityName.metal,      typeof(Metal)},
        { AbilityName.maxShield,  typeof(MaxShield)},
        { AbilityName.shieldProvider, typeof(ShieldProvider)},
        { AbilityName.barrierProvider, typeof(BarrierProvider)},
        { AbilityName.ChangePhase, typeof(ChangePhase)},
        { AbilityName.oneoff,     typeof(OneOff)},
        { AbilityName.ATK_Buffer, typeof(ATK_Buffer)},
        { AbilityName.XP_PUNCH,   typeof(XP_PUNCH)},
        { AbilityName.sacrifice,  typeof(Sacrifice)},
        { AbilityName.projectile, typeof(ProjectileLauncher)},
        { AbilityName.ZombieDive, typeof(ZombieDiveAddon)},
        { AbilityName.ZombieRevive, typeof(ZombieReviveAddon)},
        { AbilityName.clearDebuffs, typeof(ClearDebuffs)},
        { AbilityName.barrier, typeof(Barrier)},
        { AbilityName.akuShield, typeof(AkuShield)},
        { AbilityName.barrierBreaker, typeof(BarrierBreaker)},
        { AbilityName.shieldPiercing, typeof(ShieldPiercing)},
        { AbilityName.impatience, typeof(Impatience)},
        { AbilityName.pressureLearn, typeof(PressureLearn)},
        { AbilityName.targetHighestHp, typeof(TargetHighestHp)},
        { AbilityName.deadSoul, typeof(DeadSoul)},
        { AbilityName.selfSlow, typeof(SelfSlowDebuff)},
        { AbilityName.selfWeaken, typeof(SelfWeakenDebuff)},
        { AbilityName.selfLacerate, typeof(SelfLacerateDebuff)},
        { AbilityName.selfDeathmark, typeof(SelfDeathmarkDebuff)},
        { AbilityName.BaseCharacter, typeof(BaseCharacter)},
        { AbilityName.invisible, typeof(Aux_InvisibleShow)},
        { AbilityName.dodge, typeof(DodgePassive)},
        { AbilityName.Aux_MaxDMGBlock, typeof(Aux_MaxDMGBlock)},
        { AbilityName.Aux_MinDMGBlock, typeof(Aux_MinDMGBlock)},
        { AbilityName.Aux_OneHit, typeof(Aux_OneHit)},
        { AbilityName.Aux_SelfDamage, typeof(Aux_SelfDamage)},
        { AbilityName.Aux_HealDamage, typeof(Aux_HealDamage)},
        { AbilityName.Aux_BossWave, typeof(Aux_BossWave)},
    };
    /// <summary>
    /// 只造出被动实例、不安装。<see cref="UnitSpawningPassive"/> 需要在不挂到角色上的前提下
    /// 问出「这个能力会生成什么单位」，用来做战前预热。
    /// </summary>
    public static PassiveSkill Create(CharacterAbility ca)
    {
        if (ca == null) return null;
        if (!skillMap.TryGetValue(ca.name, out var skillType)) return null;
        var passive = (PassiveSkill)Activator.CreateInstance(skillType);
        passive.SetPassiveValues(ca.name, ca.probability, ca.duration, ca.intensity);
        return passive;
    }
    public static void Install(Character C, CharacterAbility ca)
    {
        Install(C, Create(ca));
    }
    public static void Install(Character C, PassiveSkill passive)
    {
        if (passive == null) return;
        C.AddPassiveEffect(passive);
        passive.OnAddingAbility(C);
    }
}
[Flags]
public enum PassiveHooks
{
    None              = 0,
    OnStartingGame    = 1 << 0,
    OnDeployUnit      = 1 << 1,
    OnAddingAbility   = 1 << 2,
    OnBeforeTakeDamage= 1 << 3,
    OnMatchedTraits   = 1 << 4,
    OnAfterTakeDamage = 1 << 5,
    OnStartAttack     = 1 << 6,
    OnAttacking       = 1 << 7,
    OnAfterAttack     = 1 << 8,
    OnFinishAttack    = 1 << 9,
    OnAfterSwitchingAnim = 1 << 10,
    OnBeforeKB        = 1 << 11,
    OnAfterKB         = 1 << 12,
    OnDead            = 1 << 13,
}

public interface PassiveNode
{
    // Bitmask of the hooks this passive actually overrides, so hot paths can skip
    // dispatch entirely when no installed passive listens to a given hook.
    PassiveHooks Hooks { get; }
    // 负数 DMG 表示治疗。默认情况下 OnBeforeTakeDamage 里对治疗量的改写会被分发器丢弃，
    // 只有把这个属性置 true 的被动才真的能改治疗量（见 Character.Passive_OnBeforeTakeDamage）。
    bool CanModifyHealing { get; }
    void OnStartingGame();
    void OnDeployUnit(Character character);
    void OnAddingAbility(Character character);
    void OnBeforeTakeDamage(Character character, ref float DMG, List<AttackType> atkTypes);
    void OnMatchedTraits(Character character, List<AttackType> atkTypes);
    void OnAfterTakeDamage(Character character);
    void OnStartAttack(Character character);
    void OnAttacking(Character character, ref float dmg, ref List<AttackType> types);
    void OnAfterAttack(Character character, float dmg, List<CharacterEffect> ces, List<AttackType> types);
    void OnFinishAttack(Character character);
    void OnAfterSwitchingAnim(Character character, ref int index);
    void OnBeforeKB(Character character);
    void OnAfterKB(Character character);
    void OnDead(Character character);
}

public abstract class PassiveSkill : PassiveNode
{
    protected object name;
    protected int probability;
    protected int duration;
    protected int intensity;

    // Reflection is done once per concrete skill type, then cached. Each skill exposes only
    // the hooks it actually overrides, so Character can aggregate a mask and skip dead dispatch.
    private static readonly Dictionary<Type, PassiveHooks> hookCache = new Dictionary<Type, PassiveHooks>();
    private static readonly (string method, PassiveHooks flag)[] hookMethods =
    {
        (nameof(OnStartingGame),       PassiveHooks.OnStartingGame),
        (nameof(OnDeployUnit),         PassiveHooks.OnDeployUnit),
        (nameof(OnAddingAbility),      PassiveHooks.OnAddingAbility),
        (nameof(OnBeforeTakeDamage),   PassiveHooks.OnBeforeTakeDamage),
        (nameof(OnMatchedTraits),      PassiveHooks.OnMatchedTraits),
        (nameof(OnAfterTakeDamage),    PassiveHooks.OnAfterTakeDamage),
        (nameof(OnStartAttack),        PassiveHooks.OnStartAttack),
        (nameof(OnAttacking),          PassiveHooks.OnAttacking),
        (nameof(OnAfterAttack),        PassiveHooks.OnAfterAttack),
        (nameof(OnFinishAttack),       PassiveHooks.OnFinishAttack),
        (nameof(OnAfterSwitchingAnim), PassiveHooks.OnAfterSwitchingAnim),
        (nameof(OnBeforeKB),           PassiveHooks.OnBeforeKB),
        (nameof(OnAfterKB),            PassiveHooks.OnAfterKB),
        (nameof(OnDead),               PassiveHooks.OnDead),
    };

    private PassiveHooks cachedHooks = (PassiveHooks)(-1); // -1 = not computed yet
    public PassiveHooks Hooks
    {
        get
        {
            if (cachedHooks == (PassiveHooks)(-1)) cachedHooks = ResolveHooks(GetType());
            return cachedHooks;
        }
    }

    private static PassiveHooks ResolveHooks(Type type)
    {
        if (hookCache.TryGetValue(type, out PassiveHooks cached)) return cached;

        PassiveHooks mask = PassiveHooks.None;
        for (int i = 0; i < hookMethods.Length; i++)
        {
            MethodInfo mi = type.GetMethod(hookMethods[i].method, BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
            // If the method is declared somewhere other than PassiveSkill, the skill overrides it.
            if (mi != null && mi.DeclaringType != typeof(PassiveSkill))
            {
                mask |= hookMethods[i].flag;
            }
        }
        hookCache[type] = mask;
        return mask;
    }

    /// <summary>
    /// 默认 false：本被动在 OnBeforeTakeDamage 里对治疗（DMG &lt; 0）的改写会被分发器丢弃，
    /// 但依旧会被正常调用，所以状态追踪型被动不受影响。确实需要改写治疗量的被动（如 Metal）才置 true。
    /// </summary>
    public virtual bool CanModifyHealing => false;

    public virtual void OnStartingGame() { }
    public virtual void OnDeployUnit(Character character) { }
    public virtual void OnAddingAbility(Character character) { }
    public virtual void OnBeforeTakeDamage(Character character, ref float DMG, List<AttackType> atkTypes) { }
    public virtual void OnMatchedTraits(Character character, List<AttackType> atkTypes) { }
    public virtual void OnAfterTakeDamage(Character character) { }
    public virtual void OnStartAttack(Character character) { }
    public virtual void OnAttacking(Character character, ref float dmg, ref List<AttackType> types) { }
    public virtual void OnAfterAttack(Character character, float dmg, List<CharacterEffect> ces, List<AttackType> types) { }
    public virtual void OnFinishAttack(Character character) { }
    public virtual void OnAfterSwitchingAnim(Character character, ref int index) { }
    public virtual void OnBeforeKB(Character character) { }
    public virtual void OnAfterKB(Character character) { }
    public virtual void OnDead(Character character) { }
    public void SetPassiveValues(object n, int p, int d, int i) { name = n; probability = p; duration = d; intensity = i; }
    public bool Triggered() { return UnityEngine.Random.Range(0, 100) < probability; }
    protected virtual void SummonPassiveEffect() { }
}

/// <summary>
/// 会在角色身上挂 PSStatusBar 的被动。子类提供图标名、颜色和 0~1 进度即可。
/// 进度变化时调用 RefreshStatusBar；条会在更新后淡出。
/// </summary>
public abstract class StatusBarPassive : PassiveSkill
{
    private const string PrefabResourcePath = "Effects/PS_statebar/StatusBar";

    private PSStatusBar statusBar;

    /// <summary>
    /// 状态条图标直接取能力自己的图标（Resources/EAIcons/a-{枚举值}），不另外维护一套图。
    /// 返回 null 表示本被动不挂状态条——这同时是 UsesStatusBar 的开关。
    /// 用枚举而不是字符串，填错就是编译错误，不会变成运行时的一张空图。
    /// </summary>
    protected virtual AbilityName? StatusBarAbility => null;
    protected virtual Color StatusBarColor => Color.white;
    protected virtual bool UsesStatusBar => StatusBarAbility.HasValue;

    public override void OnAddingAbility(Character character)
    {
        EnsureStatusBar(character);
        RefreshStatusBar(character);
    }

    public override void OnDeployUnit(Character character)
    {
        EnsureStatusBar(character);
        RefreshStatusBar(character);
    }

    public override void OnDead(Character character)
    {
        DestroyStatusBar();
    }

    protected virtual float GetStatusBarProgress(Character character) => 0f;

    protected void RefreshStatusBar(Character character)
    {
        if (!UsesStatusBar) return;
        EnsureStatusBar(character);
        if (statusBar == null) return;
        statusBar.SetPSValue(GetStatusBarProgress(character));
    }

    private void EnsureStatusBar(Character character)
    {
        if (statusBar != null || character == null || !UsesStatusBar) return;
        GameObject prefab = Resources.Load<GameObject>(PrefabResourcePath);
        if (prefab == null)
        {
            Debug.LogWarning($"[StatusBarPassive] Missing prefab Resources/{PrefabResourcePath}");
            return;
        }

        GameObject go = UnityEngine.Object.Instantiate(prefab, character.transform);
        go.transform.localPosition = Vector3.zero;
        statusBar = go.GetComponent<PSStatusBar>();
        if (statusBar == null) statusBar = go.GetComponentInChildren<PSStatusBar>(true);
        if (statusBar == null)
        {
            UnityEngine.Object.Destroy(go);
            return;
        }

        // 上面已经 !UsesStatusBar 就返回了，走到这里 StatusBarAbility 必有值。
        statusBar.SetupPS("a-" + (int)StatusBarAbility.Value, StatusBarColor);
        statusBar.ReverseDirection(character.IsCat());
    }

    private void DestroyStatusBar()
    {
        if (statusBar == null) return;
        if (statusBar.gameObject != null) UnityEngine.Object.Destroy(statusBar.gameObject);
        statusBar = null;
    }
}

/// <summary>
/// 驱动型被动技能的基类：技能通过协程完全接管角色的动画/移动/攻击（如遁地 ZombieDive、practician 特殊攻击）。
/// 取代旧的、散落在各处的 BlockAnimationSwitch 开关写法：
///  - BeginDrive/EndDrive 管理 externalAnimControl（暂停角色自身行为，但不阻断 SwitchAnimation）。
///  - 当驱动中时，OnAfterSwitchingAnim 把角色试图切换的动画号钉在当前阶段动画上（KB/死亡动画放行）。
///  - RunDrive 用 try/finally 包裹协程，保证无论如何退出（KB、角色销毁、异常）都会释放控制权，
///    从根本上消除"卡在自定义动画里出不来"的问题。
/// </summary>
public abstract class AnimationDrivingPassive : StatusBarPassive
{
    protected bool driving;
    private int pinnedAnim = -1;

    // KB 与死亡动画永远放行，其它动画号在驱动期间被钉住。
    private const int KBAnim = 3;

    /// <summary>进入驱动模式：暂停角色自身 UpdateAnimation，并把动画钉在 phaseAnim。</summary>
    protected void BeginDrive(Character c, int phaseAnim)
    {
        if (c == null) return;
        driving = true;
        pinnedAnim = phaseAnim;
        c.SetExternalAnimControl(true);
        c.SwitchAnimation(phaseAnim);
    }

    /// <summary>切换到驱动期间的另一个阶段动画（如 in -> dive -> out）。</summary>
    protected void SetPhaseAnim(Character c, int phaseAnim)
    {
        pinnedAnim = phaseAnim;
        if (c != null) c.SwitchAnimation(phaseAnim);
    }

    /// <summary>退出驱动模式，把控制权还给角色。可安全重复调用。</summary>
    protected void EndDrive(Character c)
    {
        driving = false;
        pinnedAnim = -1;
        if (c != null) c.SetExternalAnimControl(false);
    }

    public override void OnAfterSwitchingAnim(Character character, ref int index)
    {
        if (!driving) return;
        if (index == KBAnim) return; // KB 动画放行
        index = pinnedAnim;          // 其它一律钉在当前阶段动画
    }

    /// <summary>
    /// 用 try/finally 包裹实际驱动协程，保证结束时一定 EndDrive。
    /// 子类实现 DriveRoutine，正常按帧 yield 即可，无需手动清理控制标志。
    /// </summary>
    protected IEnumerator RunDrive(Character character, int enterAnim)
    {
        BeginDrive(character, enterAnim);
        try
        {
            IEnumerator inner = DriveRoutine(character);
            while (true)
            {
                // 角色被销毁或已被 KB 打断则提前结束。
                if (character == null || character.IsOnKB()) break;
                bool moveNext;
                try { moveNext = inner.MoveNext(); }
                catch (System.Exception e) { Debug.LogError($"[AnimationDrivingPassive] drive error: {e}"); break; }
                if (!moveNext) break;
                yield return inner.Current;
            }
        }
        finally
        {
            EndDrive(character);
            OnDriveEnd(character);
        }
    }

    /// <summary>子类的实际驱动逻辑；期间可用 SetPhaseAnim 切换阶段动画。</summary>
    protected abstract IEnumerator DriveRoutine(Character character);

    /// <summary>驱动结束时（无论正常完成、被 KB 打断还是异常）保证执行的清理。可用 character.IsOnKB() 区分退出原因。</summary>
    protected virtual void OnDriveEnd(Character character) { }
}

/// <summary>
/// 会生成新单位的被动基类（炮弹、未来的 Summoner 等）。
/// 安装时自动异步预热子类声明的资源，避免 WebGL 上首次生成命中 PREWARM MISS。
/// 子类实现 <see cref="CollectSpawnAddresses"/>；若还要预热整套角色资源（data / 贴图 / 动画），再覆盖 <see cref="CollectSpawnAssets"/>。
/// 覆盖 <see cref="OnAddingAbility"/> 时必须调用 base。
/// </summary>
public abstract class UnitSpawningPassive : PassiveSkill
{
    private static readonly Dictionary<string, GameObject> PrefabCache = new Dictionary<string, GameObject>();
    private static readonly HashSet<string> PrewarmInFlight = new HashSet<string>();

    public override void OnAddingAbility(Character character)
    {
        RequestPrewarm(character);
    }

    /// <summary>本能力会 Instantiate 的单位 prefab 地址（无扩展名的 Resources 风格）。</summary>
    public abstract void CollectSpawnAddresses(IList<string> addresses);

    /// <summary>
    /// 把本能力需要的全部 Addressables 加入预热列表。
    /// 默认把 <see cref="CollectSpawnAddresses"/> 的 prefab 入列；Summoner 可在此调用 <see cref="BattlePrewarm.AddUnit"/>。
    /// </summary>
    public virtual void CollectSpawnAssets(BundledAddressables.PrewarmList list)
    {
        if (list == null) return;
        var addresses = new List<string>(4);
        CollectSpawnAddresses(addresses);
        for (int i = 0; i < addresses.Count; i++) AddPrefabAddress(list, addresses[i]);
    }

    /// <summary>角色生成时：扫描 data.abilities，对所有 UnitSpawningPassive 启动预热。</summary>
    public static void RequestPrewarm(Character host, CharacterData data)
    {
        if (host == null || data?.abilities == null) return;
        for (int i = 0; i < data.abilities.Length; i++)
        {
            if (!(AbilityInstaller.Create(data.abilities[i]) is UnitSpawningPassive spawner)) continue;
            spawner.RequestPrewarm(host);
        }
    }

    /// <summary>战前预热：把 data 上所有生成型能力的资源加入 list。</summary>
    public static void CollectSpawnAssetsFromData(CharacterData data, BundledAddressables.PrewarmList list)
    {
        if (data?.abilities == null || list == null) return;
        for (int i = 0; i < data.abilities.Length; i++)
        {
            if (!(AbilityInstaller.Create(data.abilities[i]) is UnitSpawningPassive spawner)) continue;
            spawner.CollectSpawnAssets(list);
        }
    }

    public void RequestPrewarm(Character host)
    {
        if (host == null) return;

        string gate = PrewarmGateKey();
        if (!PrewarmInFlight.Add(gate)) return;

        var list = new BundledAddressables.PrewarmList();
        CollectSpawnAssets(list);
        if (list.Count == 0)
        {
            PrewarmInFlight.Remove(gate);
            return;
        }

        host.StartCoroutine(PrewarmRoutine(list, gate));
    }

    protected virtual string PrewarmGateKey()
        => GetType().FullName + "|" + probability + "|" + duration + "|" + intensity;

    protected static void AddPrefabAddress(BundledAddressables.PrewarmList list, string address)
    {
        if (list == null || string.IsNullOrEmpty(address)) return;
        if (BundledAddressables.Exists(address, typeof(GameObject))) list.Add<GameObject>(address);
    }

    protected static GameObject TryLoadPrefab(string address)
    {
        if (string.IsNullOrEmpty(address)) return null;
        if (PrefabCache.TryGetValue(address, out GameObject cached) && cached != null) return cached;

        GameObject prefab = BundledAddressables.LoadSync<GameObject>(address);
        if (prefab != null) PrefabCache[address] = prefab;
        return prefab;
    }

    private static IEnumerator PrewarmRoutine(BundledAddressables.PrewarmList list, string gate)
    {
        yield return BundledAddressables.PrewarmRoutine(list);
        PrewarmInFlight.Remove(gate);
    }
}

public abstract class SelfPermanentDebuffBase : PassiveSkill
{
    // Keep "permanent" long enough for battles while avoiding float->int overflow/precision edge cases
    private const int PermanentDuration = 1000000;
    private bool applied;

    public override void OnDeployUnit(Character character)
    {
        if (applied || character == null) return;
        applied = true;
        ApplyDebuff(character, PermanentDuration);
    }

    protected abstract void ApplyDebuff(Character character, int durationFrames);
}
