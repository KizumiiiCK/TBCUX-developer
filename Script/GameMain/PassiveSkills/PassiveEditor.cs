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
        { AbilityName.shieldPiercing, typeof(SheildPiercing)},
        { AbilityName.impatience, typeof(Impatience)},
        { AbilityName.pressureLearn, typeof(PressureLearn)},
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
    };
    public static void Install(Character C, CharacterAbility ca)
    {
        if (!skillMap.TryGetValue(ca.name, out var skillType)) return;
        var passive = (PassiveSkill)Activator.CreateInstance(skillType);
        passive.SetPassiveValues(ca.name, ca.probability, ca.duration, ca.intensity);
        C.AddPassiveEffect(passive);
        passive.OnAddingAbility(C);
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
/// 驱动型被动技能的基类：技能通过协程完全接管角色的动画/移动/攻击（如遁地 ZombieDive、practician 特殊攻击）。
/// 取代旧的、散落在各处的 BlockAnimationSwitch 开关写法：
///  - BeginDrive/EndDrive 管理 externalAnimControl（暂停角色自身行为，但不阻断 SwitchAnimation）。
///  - 当驱动中时，OnAfterSwitchingAnim 把角色试图切换的动画号钉在当前阶段动画上（KB/死亡动画放行）。
///  - RunDrive 用 try/finally 包裹协程，保证无论如何退出（KB、角色销毁、异常）都会释放控制权，
///    从根本上消除"卡在自定义动画里出不来"的问题。
/// </summary>
public abstract class AnimationDrivingPassive : PassiveSkill
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

public class AffectByStrategy : PassiveSkill
{
    public override void OnDeployUnit(Character character)
    {
        GetStrategy(character);
    }
    public override void OnFinishAttack(Character character)
    {
        GetStrategy(character);
    }
    public override void OnAfterKB(Character character)
    {
        GetStrategy(character);
    }
    private void GetStrategy(Character character)
    {
        int strategy = PlayerPrefs.GetInt("Strategy", 0);
        switch (strategy)
        {
            case 0:
                character.SetSkillFactor(1);
                character.SetAttackRange(0, character.DetectionRange);
                break;
            case 1:
                character.SetSkillFactor(1.2f);
                character.SetAttackRange(0, character.DetectionRange);
                if (character.Targets.Count > 0) character.StartCoroutine(Retreat(character));
                else strategy = 0;
                break;
            case 2:
                character.SetSkillFactor(0.75f);
                character.SetAttackRange(0, character.DetectionRange / 2);
                break;
            default: break;
        }
    }
    private IEnumerator Retreat(Character character)
    {
        float nearestpoint = character.FindNearest().transform.position.x;
        yield return new WaitForFixedUpdate();
        character.SwitchAnimation(0);

        while ((nearestpoint + character.DetectionRange / 100f * 0.9f) > character.transform.position.x)
        {
            character.transform.Translate(new Vector2(character.TBCspeedTranslator(2 * character.GetRealSpeed()) * Time.deltaTime, 0));
            yield return new WaitForFixedUpdate();
        }
        //strategy = 0;
    }
}
public class Supporter : PassiveSkill
{
}
public class Practician : AnimationDrivingPassive
{
    private const int SpecialAnim = 4;
    private float atk = 1;
    private int stack = 0;
    private static int maxStack = 6;
    private static float atkMuitipiler = 0.2f;

    public override void OnAfterAttack(Character character, float dmg, List<CharacterEffect> ces, List<AttackType> types)
    {
        if (stack > maxStack) return;
        stack++;
        atk += Mathf.Abs(dmg);
    }
    public override void OnFinishAttack(Character character)
    {
        if (stack >= maxStack) SpecialAttack(character);
    }
    public override void OnAfterKB(Character character)
    {
        SpecialAttack(character);
    }
    private void SpecialAttack(Character c)
    {
        if (c == null || driving) return;
        c.StartCoroutine(RunDrive(c, SpecialAnim));
    }
    protected override IEnumerator DriveRoutine(Character c)
    {
        int t = 0;
        c.Supporter_Target_Switch(true);
        c.SetAttackRange(0, Mathf.Abs(intensity));
        while (t < duration && !c.IsOnKB())
        {
            t++;
            if (t == probability)
            {
                c.Attack(atk * atkMuitipiler, true, intensity < 0, false);
            }
            yield return new WaitForFixedUpdate();
        }
    }
    protected override void OnDriveEnd(Character c)
    {
        if (c != null)
        {
            c.ExitAttack();
            c.Supporter_Target_Switch(true);
        }
        atk = 1;
        stack = 0;
    }
}
public class OneOff : PassiveSkill
{
    private bool off = false;
    public override void OnAttacking(Character character, ref float dmg, ref List<AttackType> types) => off = true;
    public override void OnAfterKB(Character character) { if (off) character.Dead(); }
    public override void OnFinishAttack(Character character)=> character.Dead();
}

public class LaceratedEffect : PassiveSkill
{
    public override void OnAfterAttack(Character character, float dmg, List<CharacterEffect> ces, List<AttackType> types)
    {
        character.ReceiveAttack(character.GetMaxHealth() * 0.05f, null, null, null, null, null, null);
        GameObject.Instantiate(Resources.Load<GameObject>("Effects/lacerate_blood"),character.transform.position,Quaternion.identity);
    }
}
public class Strengthen : PassiveSkill
{
    public override void OnAddingAbility(Character character)
    {
        if(probability>99)
        {
            IncreaseATK(character);
        }
    }
    public override void OnAfterTakeDamage(Character character) 
    {
        if (character.GetHealth() / character.GetMaxHealth() * 100 < probability) 
        {
            IncreaseATK(character);
        }
    }
    private void IncreaseATK(Character character)
    {
        EffectInstaller.Inflict(character.gameObject, AbilityName.strengthen, -1, 0);
        character.SetMAXmuiltipier((float)intensity / 100);
        character.SetMuiltipierToMAX();
        Weaken wk = character.GetComponent<Weaken>();
        if (wk != null) { wk.duration = 0; }
        character.RemovePassiveEffect(this);
    }
}
public class Survive : PassiveSkill
{
    public override void OnAfterTakeDamage(Character character)
    {
        if (character.GetHealth() <= 0)
        {
            if (Triggered())
            {
                character.StartKBCoroutine();
                EffectInstaller.Inflict(character.gameObject, AbilityName.survive, 1, 1);
                character.SetHealth(1);
                character.ResetKBtimes();
                character.RemovePassiveEffect(this);
            }

        }
    }
}
public class Critical : PassiveSkill
{
    public override void OnAttacking(Character character, ref float dmg, ref List<AttackType> types)
    {
        if (Triggered()) { dmg *= 1.25f; types.Add(AttackType.critical);}
    }
}
public class ZombieKiller : PassiveSkill
{
    public override void OnAttacking(Character character, ref float dmg, ref List<AttackType> types)
    {
        types.Add(AttackType.zombieKiller);
    }
}
public class SoulStrike : PassiveSkill
{
    public override void OnAddingAbility(Character character)
    {
        if (character == null) return;
        character.SetCanTargetUndetectable(true);
    }
}
public class BarrierBreaker : PassiveSkill
{
    public override void OnAttacking(Character character, ref float dmg, ref List<AttackType> types)
    {
        if (Triggered()) types.Add(AttackType.barrierBreaker);
    }
}
public class SheildPiercing : PassiveSkill
{
    public override void OnAttacking(Character character, ref float dmg, ref List<AttackType> types)
    {
        if (Triggered()) types.Add(AttackType.sheildPiercing);
    }
}
public class Impatience : PassiveSkill
{
    private int impatience_level = 0;
    public override void OnBeforeTakeDamage(Character character, ref float DMG, List<AttackType> atkTypes)
    {
        DMG = DMG * (probability + intensity * impatience_level) / 100;
    }
    public override void OnFinishAttack(Character character)
    {
        impatience_level++;
        character.SetRealReload(character.GetRealReload() - duration);
    }
}
public class PressureLearn : PassiveSkill
{
    private const int MaxPressure = 1000;
    private const int CriticalReducer = 200;
    private const int SpecMultiplier = 10;

    private int normal_pressure = 0;
    private int wave_pressure = 0;
    private int surge_pressure = 0;
    private int explode_pressure = 0;

    public override void OnBeforeTakeDamage(Character character, ref float DMG, List<AttackType> atkTypes)
    {
        bool hasCritical = false;
        bool hasNormal = false;
        bool hasWave = false;
        bool hasSurge = false;
        bool hasExplode = false;

        if (atkTypes != null)
        {
            for (int i = 0; i < atkTypes.Count; i++)
            {
                switch (atkTypes[i])
                {
                    case AttackType.critical:
                        hasCritical = true;
                        break;
                    case AttackType.none:
                        hasNormal = true;
                        break;
                    case AttackType.wave:
                        hasWave = true;
                        break;
                    case AttackType.surge:
                        hasSurge = true;
                        break;
                    case AttackType.explosion:
                        hasExplode = true;
                        break;
                }
            }
        }

        if (hasCritical)
        {
            ReduceAllPressure(CriticalReducer);
            return;
        }

        bool hasRecognizedType = hasNormal || hasWave || hasSurge || hasExplode;
        if (!hasRecognizedType)
        {
            ApplyPressureAndScaleDamage(ref normal_pressure, probability, ref DMG);
        }
        else
        {
            if (hasNormal) ApplyPressureAndScaleDamage(ref normal_pressure, probability, ref DMG);
            if (hasWave) ApplyPressureAndScaleDamage(ref wave_pressure, duration * SpecMultiplier, ref DMG);
            if (hasSurge) ApplyPressureAndScaleDamage(ref surge_pressure, duration * SpecMultiplier, ref DMG);
            if (hasExplode) ApplyPressureAndScaleDamage(ref explode_pressure, duration * SpecMultiplier, ref DMG);
        }

        if (GetMaxPressure() >= MaxPressure) DMG = 0f;
    }

    public override void OnAttacking(Character character, ref float dmg, ref List<AttackType> types)
    {
        if (GetMaxPressure() < MaxPressure) return;

        if (types == null) types = new List<AttackType>();
        if (!types.Contains(AttackType.savage))
        {
            types.Add(AttackType.savage);
        }
    }

    private void ApplyPressureAndScaleDamage(ref int pressure, int gain, ref float dmg)
    {
        pressure = Mathf.Clamp(pressure + Mathf.Max(0, gain), 0, MaxPressure);
        dmg *= (MaxPressure - pressure) / (float)MaxPressure;
    }

    private void ReduceAllPressure(int reducer)
    {
        normal_pressure = Mathf.Max(0, normal_pressure - reducer);
        wave_pressure = Mathf.Max(0, wave_pressure - reducer);
        surge_pressure = Mathf.Max(0, surge_pressure - reducer);
        explode_pressure = Mathf.Max(0, explode_pressure - reducer);
    }

    private int GetMaxPressure()
    {
        return Mathf.Max(normal_pressure, wave_pressure, surge_pressure, explode_pressure);
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

public class SelfSlowDebuff : SelfPermanentDebuffBase
{
    protected override void ApplyDebuff(Character character, int durationFrames)
    {
        EffectInstaller.Inflict(character.gameObject, EffectName.slow, durationFrames, 0);
    }
}

public class SelfWeakenDebuff : SelfPermanentDebuffBase
{
    protected override void ApplyDebuff(Character character, int durationFrames)
    {
        EffectInstaller.Inflict(character.gameObject, EffectName.weaken, durationFrames, intensity);
    }
}

public class SelfLacerateDebuff : SelfPermanentDebuffBase
{
    protected override void ApplyDebuff(Character character, int durationFrames)
    {
        EffectInstaller.Inflict(character.gameObject, EffectName.lacerate, durationFrames, 0);
    }
}

public class SelfDeathmarkDebuff : SelfPermanentDebuffBase
{
    protected override void ApplyDebuff(Character character, int durationFrames)
    {
        EffectInstaller.Inflict(character.gameObject, EffectName.deathmark, durationFrames, 0);
    }
}
public class Savage : PassiveSkill
{
    public override void OnAttacking(Character character, ref float dmg, ref List<AttackType> types)
    {
        if (Triggered()) { dmg *= 3; types.Add(AttackType.savage);}
    }
}
public class ExtraMoney : PassiveSkill
{
    //???????????????????????
}
public class Metal : PassiveSkill
{
    public override void OnBeforeTakeDamage(Character character, ref float DMG, List<AttackType> atkTypes)
    {
        bool crit = false;
        if (atkTypes.Contains(AttackType.critical)) crit = true;
        if (!crit) DMG = 1;
    }
}
public class Wave : PassiveSkill
{
    public override void OnAfterAttack(Character character, float dmg, List<CharacterEffect> ces, List<AttackType> types)
    {
        if (Triggered())
        {
            character.Wave_Attack(duration,false,dmg,ces,types);
        }
    }
}
public class MiniWave : PassiveSkill
{
    public override void OnAfterAttack(Character character, float dmg, List<CharacterEffect> ces, List<AttackType> types)
    {
        if (Triggered())
        {
            character.Wave_Attack(duration, true, dmg, ces, types);
        }
    }
}
public class Surge : PassiveSkill
{
    public override void OnAfterAttack(Character character, float dmg, List<CharacterEffect> ces, List<AttackType> types)
    {
        if (Triggered())
        {
            int surge_distance = UnityEngine.Random.Range(intensity/2, intensity);
            character.Surge_Attack(duration, false, surge_distance, dmg, ces, types);
        }
    }
}
public class MiniSurge : PassiveSkill
{
    public override void OnAfterAttack(Character character, float dmg, List<CharacterEffect> ces, List<AttackType> types)
    {
        if (Triggered())
        {
            int surge_distance = UnityEngine.Random.Range(intensity/2, intensity);
            character.Surge_Attack(duration, true, surge_distance, dmg, ces, types);
        }
    }
}
/// <summary>
/// 波动阻挡：由 WaveUnit 在群体结算前主动检测 HasAbility(wave_stop) 并自毁。
/// 这里仅作兜底：若仍收到 wave 伤害则清零。
/// </summary>
public class WaveStop : PassiveSkill
{
    public override void OnBeforeTakeDamage(Character character, ref float DMG, List<AttackType> atkTypes)
    {
        if (character == null || atkTypes == null || atkTypes.Count == 0) return;
        if (!atkTypes.Contains(AttackType.wave)) return;
        DMG = 0f;
    }
}
public class MaxShield : PassiveSkill
{
    private float damageLimit = int.MaxValue;
    private const string MaxShieldCutEffectName = "dmgcut";
    public override void OnDeployUnit(Character character)
    {
        damageLimit = character.GetMaxHealth() * probability / 100f;
    }
    public override void OnBeforeTakeDamage(Character character, ref float DMG, List<AttackType> atkTypes)
    {
        if (DMG > damageLimit)
        {
            DMG = damageLimit;
            if (character != null && character.EM != null)
            {
                character.EM.InstantiateBattleObject(MaxShieldCutEffectName, character.transform.position.x, character.transform.position.y);
            }
        }
    }
}

public class Barrier : PassiveSkill
{
    private string shieldEffectName;
    private bool broken;
    private float hardness;
    private AnimationDisplayer shieldDisplay;

    public override void OnAddingAbility(Character character)
    {
        hardness = Mathf.Max(0f, intensity);
        broken = false;
        shieldEffectName = character != null && character.IsCat() ? "barrier" : "barrier_e";
    }

    public override void OnBeforeTakeDamage(Character character, ref float DMG, List<AttackType> atkTypes)
    {
        if (character == null || broken) return;
        if(DMG < 0f) return;
        bool isBarrierBreaker = atkTypes != null && atkTypes.Contains(AttackType.barrierBreaker);
        if (isBarrierBreaker)
        {
            PlayShieldAnim(character, 2);
            broken = true;
            DMG *= 1.25f;
            CleanupShieldAnim(character);
            character.RemovePassiveEffect(this);
            return;
        }

        if (DMG < hardness)
        {
            PlayShieldAnim(character, 0);
            DMG = 0f;
            return;
        }

        PlayShieldAnim(character, 1);
        DMG = 0f;
        broken = true;
        CleanupShieldAnim(character);
        character.RemovePassiveEffect(this);
    }

    public override void OnDead(Character character)
    {
        CleanupShieldAnim(character);
    }

    protected void PlayShieldAnim(Character character, int animIndex)
    {
        if (character == null || character.EM == null) return;
        character.EM.PlayReusableAttachedEffect(
            ref shieldDisplay,
            shieldEffectName,
            character.transform,
            character.transform.position,
            animIndex,
            worldPositionStays: true);
    }

    private void CleanupShieldAnim(Character character)
    {
        if (shieldDisplay == null) return;
        if (character != null && character.EM != null)
        {
            character.EM.ReleaseReusableAttachedEffect(ref shieldDisplay, shieldEffectName);
        }
        else
        {
            UnityEngine.Object.Destroy(shieldDisplay.gameObject);
            shieldDisplay = null;
        }
    }
}

public class AkuShield : PassiveSkill
{
    private string shieldEffectName;
    private float maxHardness;
    private float remainingHardness;
    private bool broken;
    private AnimationDisplayer shieldDisplay;

    public override void OnAddingAbility(Character character)
    {
        maxHardness = Mathf.Max(0f, intensity);
        remainingHardness = maxHardness;
        broken = false;
        shieldEffectName = character != null && character.IsCat() ? "akuShield" : "akuShield_e";
    }

    public override void OnBeforeTakeDamage(Character character, ref float DMG, List<AttackType> atkTypes)
    {
        if (character == null) return;
        if (DMG < 0f) return;

        bool isShieldPiercing = atkTypes != null && atkTypes.Contains(AttackType.sheildPiercing);
        if (isShieldPiercing)
        {
            PlayShieldAnim(character, 3);
            DMG *= 1.25f;
            remainingHardness = 0f;
            broken = true;
            return;
        }

        if (broken || remainingHardness <= 0f) return;

        float incomingDamage = Mathf.Max(0f, DMG);
        if (incomingDamage <= 0f) return;

        remainingHardness -= incomingDamage;
        if (remainingHardness > 0f)
        {
            DMG = 0f;
            PlayShieldAnim(character, 1);
            return;
        }

        broken = true;
        PlayShieldAnim(character, 2);
        DMG = Mathf.Max(0f, -remainingHardness);
    }

    public override void OnAfterKB(Character character)
    {
        if (character == null) return;
        if (!broken) return;
        if (character.GetLastTriggeredKBType() != KB_Type.none) return;
        remainingHardness = maxHardness;
        broken = false;
        PlayShieldAnim(character, 0);
    }

    public override void OnDead(Character character)
    {
        CleanupShieldAnim(character);
    }

    private void PlayShieldAnim(Character character, int animIndex)
    {
        if (character == null || character.EM == null) return;
        character.EM.PlayReusableAttachedEffect(
            ref shieldDisplay,
            shieldEffectName,
            character.transform,
            character.transform.position,
            animIndex,
            worldPositionStays: true);
    }

    private void CleanupShieldAnim(Character character)
    {
        if (shieldDisplay == null) return;
        if (character != null && character.EM != null)
        {
            character.EM.ReleaseReusableAttachedEffect(ref shieldDisplay, shieldEffectName);
        }
        else
        {
            UnityEngine.Object.Destroy(shieldDisplay.gameObject);
            shieldDisplay = null;
        }
    }
}

public class Aux_MaxDMGBlock : PassiveSkill
{
    /// <summary>若单次伤害高于 intensity（D-阈值），该次伤害无效。</summary>
    public override void OnBeforeTakeDamage(Character character, ref float DMG, List<AttackType> atkTypes)
    {
        if (DMG > intensity) DMG = 0;
    }
}

public class Aux_MinDMGBlock : PassiveSkill
{
    /// <summary>若单次伤害低于 intensity（D+阈值），该次伤害无效。</summary>
    public override void OnBeforeTakeDamage(Character character, ref float DMG, List<AttackType> atkTypes)
    {
        if (DMG < intensity) DMG = 0;
    }
}

/// <summary>
/// hd / hD 关卡限制的一部分：本单位的普通攻击（伤害>0）仅对自身造成等量的无属性真实伤害，
/// 不再命中其他目标。将 ref dmg 置 0 即可阻断对他人的伤害（无额外分配，性能最优）。
/// </summary>
public class Aux_SelfDamage : PassiveSkill
{
    public override void OnAttacking(Character character, ref float dmg, ref List<AttackType> types)
    {
        if (character == null) return;
        if (dmg <= 0f) return; // 仅拦截正伤害攻击；治愈（负伤害）交给 Aux_HealDamage
        float selfDamage = dmg;
        dmg = 0f; // 该次攻击对其他目标造成 0 伤害
        character.ReceiveAttack(selfDamage, null, null, null, null, null, null);
    }
}

/// <summary>
/// hd / hD 关卡限制的一部分：本单位每施展一次治愈技能（伤害&lt;0），对敌方直接造成 intensity 点伤害。
/// probability &gt; 0 表示对全体敌人（含基地）造成群体伤害；否则只对最前方的敌人造成单体伤害。
/// </summary>
public class Aux_HealDamage : PassiveSkill
{
    private static readonly List<Character> targetBuffer = new List<Character>(32);

    public override void OnAttacking(Character character, ref float dmg, ref List<AttackType> types)
    {
        if (character == null) return;
        if (dmg >= 0f) return; // 仅在治愈（负伤害）时触发
        float damage = intensity;
        if (damage <= 0f) return;

        CharacterTargetManager mgr = CharacterTargetManager.Instance;
        if (probability > 0)
        {
            int count = mgr.FillOpponentsWithBase(character, targetBuffer);
            for (int i = 0; i < count; i++)
            {
                Character t = targetBuffer[i];
                if (t == null) continue;
                t.ReceiveAttack(damage, null, null, null, null, null, null);
            }
            targetBuffer.Clear();
        }
        else
        {
            Character front = mgr.GetFrontmostOpponent(character);
            if (front != null) front.ReceiveAttack(damage, null, null, null, null, null, null);
        }
    }
}
public class ATK_Buffer : PassiveSkill
{
    public override void OnAfterAttack(Character character, float dmg, List<CharacterEffect> ces, List<AttackType> types)
    {
        for (int i = 0; i < character.Targets.Count; i++)
        {
            IncreaseATK(character.Targets[i].GetComponent<Character>());
        }
    }
    private void IncreaseATK(Character character)
    {
        if (character == null) return;
        EffectInstaller.Inflict(character.gameObject, AbilityName.strengthen, -1, 0);
        character.SetMAXmuiltipier((float)intensity / 100);
        character.SetMuiltipierToMAX();
        Weaken wk = character.GetComponent<Weaken>();
        if (wk != null) { wk.duration = 0; }
    }
}

public class XP_PUNCH : PassiveSkill
{
    public override void OnAfterAttack(Character character, float dmg, List<CharacterEffect> ces, List<AttackType> types)
    {
        PlayerPrefs.SetInt(UXPref.RewardPenalty, PlayerPrefs.GetInt(UXPref.RewardPenalty, 0) + 1);
    }
}
public class Sacrifice : PassiveSkill
{
    public override void OnFinishAttack(Character character)
    {
        if(Triggered())character.ReceiveAttack(character.GetMaxHealth() * intensity / 100, null, null, null, null, null, null);
    }
}

public class ProjectileLauncher : PassiveSkill
{
    private static readonly Dictionary<int, GameObject> ProjectilePrefabCache = new Dictionary<int, GameObject>();
    private static readonly List<CharacterEffect> ReusableEffectPayload = new List<CharacterEffect>(8);

    public override void OnAttacking(Character character, ref float dmg, ref List<AttackType> types)
    {
        if (character == null) return;

        int step = character.GetAnimationStep();
        if (character.atkInfos == null || step < 0 || step >= character.atkInfos.Length) return;
        ATKInfo atk = character.atkInfos[step];
        bool triggerEffect = !atk.DoNotTriggerEffects;
        if (!ProjectilePrefabCache.TryGetValue(probability, out GameObject prefab) || prefab == null)
        {
            prefab = Resources.Load<GameObject>($"Units/Projectiles/p{probability:000}/projunit");
            ProjectilePrefabCache[probability] = prefab;
        }
        if (prefab == null)
        {
            Debug.Log("No such projectile!");
            return;
        }

        GameObject go = GameObject.Instantiate(prefab, character.transform.position+new Vector3(0, intensity/100f,0), Quaternion.identity);
        CharacterSummoner.ResetAnimationOrderLayer(go, "Units", 20000);
        ProjectileUnit pu = go.GetComponent<ProjectileUnit>();
        if (pu == null) pu = go.AddComponent<ProjectileUnit>();

        List<CharacterEffect> effectPayload = null;
        if (triggerEffect && character.characterEffects != null && character.characterEffects.Length > 0)
        {
            ReusableEffectPayload.Clear();
            for (int i = 0; i < character.characterEffects.Length; i++)
            {
                CharacterEffect source = character.characterEffects[i];
                if (source == null) continue;
                ReusableEffectPayload.Add(CloneEffectForProjectile(source, character.GetFactor()));
            }
            effectPayload = new List<CharacterEffect>(ReusableEffectPayload);
        }
        float atkDamage = dmg;
        pu.BeginProjectileAttack(character, atkDamage, Mathf.Max(1, duration), effectPayload, types, triggerEffect);

        // Block this attack's native hit while keeping attack animation/state flow intact.
        character.RemoveAllTarget();
    }
    private static CharacterEffect CloneEffectForProjectile(CharacterEffect source, float chanceFactor)
    {
        int adjustedProbability = Mathf.Clamp(Mathf.RoundToInt(source.probability * Mathf.Max(0f, chanceFactor)), 0, 100);
        return new CharacterEffect
        {
            name = source.name,
            probability = adjustedProbability,
            duration = source.duration,
            intensity = source.intensity
        };
    }
}

public class ZombieDiveAddon : AnimationDrivingPassive
{
    private const int InAnim = 4;
    private const int DiveAnim = 5;
    private const int OutAnim = 6;
    private const int TransitionFrames = 30;

    private int remainingDiveTimes;
    private bool initialized;

    public override void OnAddingAbility(Character character)
    {
        if (initialized) return;
        initialized = true;
        remainingDiveTimes = probability;
    }

    public override void OnStartAttack(Character character)
    {
        if (character == null || driving) return;
        if (remainingDiveTimes == 0) return;
        if (CanAttackBaseNow(character)) return;

        if (remainingDiveTimes > 0) remainingDiveTimes--;
        character.RequestCancelAttackStart();
        character.StartCoroutine(RunDrive(character, InAnim));
    }

    public override void OnAfterKB(Character character)
    {
        // 若 KB 期间仍持有控制权，RunDrive 会在下一帧自行释放（finally -> OnDriveEnd）；
        // 这里只需在配额耗尽时移除被动。
        if (remainingDiveTimes == 0 && character != null)
        {
            CharacterTargetManager.Instance.SetCharacterUndetectable(character, false);
            character.RemovePassiveEffect(this);
        }
    }

    protected override IEnumerator DriveRoutine(Character character)
    {
        // In：潜入前摇，原地不动。
        int speedBeforeIn = ResolveTransitExitSpeed(character);
        character.ChangeSpeed(0);
        int t = 0;
        while (t < TransitionFrames && !CanAttackBaseNow(character))
        {
            t += character.GetFrameStep();
            yield return new WaitForFixedUpdate();
        }

        // Diving：潜地推进，不可被检测。
        character.ChangeSpeed(speedBeforeIn);
        CharacterTargetManager.Instance.SetCharacterUndetectable(character, true);
        SetPhaseAnim(character, DiveAnim);
        int moveDir = character.IsCat() ? -1 : 1;
        int moveFrames = Mathf.Max(0, duration) * 2;
        for (int i = 0; i < moveFrames; i++)
        {
            if (CanAttackBaseNow(character)) break;
            int currentSpeed = Mathf.Max(0, character.GetRealSpeed());
            character.transform.Translate(new Vector2(character.TBCspeedTranslator(currentSpeed) * moveDir * Time.deltaTime, 0));
            yield return new WaitForFixedUpdate();
        }

        // Out：钻出后摇，恢复可检测。
        CharacterTargetManager.Instance.SetCharacterUndetectable(character, false);
        character.ChangeSpeed(0);
        SetPhaseAnim(character, OutAnim);
        t = 0;
        while (t < TransitionFrames)
        {
            t += character.GetFrameStep();
            yield return new WaitForFixedUpdate();
        }
    }

    protected override void OnDriveEnd(Character character)
    {
        if (character == null) return;
        // 无论正常结束还是被 KB 打断，都恢复可检测并还原速度。
        CharacterTargetManager.Instance.SetCharacterUndetectable(character, false);
        character.ChangeSpeed(ResolveTransitExitSpeed(character));
        if (remainingDiveTimes == 0)
        {
            character.RemovePassiveEffect(this);
        }
    }

    private bool CanAttackBaseNow(Character character)
    {
        if (character == null) return false;
        GameObject baseTarget = character.BaseTarget;
        return baseTarget != null && baseTarget.activeInHierarchy;
    }

    private int ResolveTransitExitSpeed(Character character, int fallback = -1)
    {
        if (character == null) return 0;
        if (character.GetComponent<Stop>() != null) return 0;
        if (character.GetComponent<Slow>() != null) return 1;

        int current = character.GetRealSpeed();
        if (current > 0) return current;
        if (fallback >= 0) return fallback;
        return Mathf.Max(0, character.Speed);
    }
}

public class ZombieReviveAddon : PassiveSkill
{
    private const int TransitionFrames = 15;
    private const string CorpseEffectName = "corpse";

    private bool initialized;
    private bool reviving;
    private int remainingRevives;
    private bool purified = false;

    public override void OnAddingAbility(Character character)
    {
        if (initialized) return;
        initialized = true;
        remainingRevives = probability;
    }

    public override void OnBeforeTakeDamage(Character character, ref float DMG, List<AttackType> atkTypes)
    {
        if (character == null) return;
        bool hasZombieKiller = atkTypes != null && atkTypes.Contains(AttackType.zombieKiller);
        purified = hasZombieKiller && (character.GetHealth() - DMG < 0);
        if (purified) character.EM?.InstantiateBattleObject(SEnums.zombieKiller, character.transform.position.x, character.transform.position.y);
    }

    public override void OnDead(Character character)
    {
        if (character == null || reviving) return;
        if (purified)
        {
            purified = false;
            return;
        }
        if (remainingRevives == 0) return;

        if (remainingRevives > 0) remainingRevives--;

        int hpPercent = Mathf.Clamp(intensity, 1, 100);
        int revivedHp = Mathf.Max(1, Mathf.RoundToInt(character.GetMaxHealth() * hpPercent / 100f));
        character.SetHealth(revivedHp);
        character.SyncKBStateToHealth();
        character.StartCoroutine(ReviveRoutine(character));
    }

    private IEnumerator ReviveRoutine(Character character)
    {
        if (character == null) yield break;
        reviving = true;

        CharacterTargetManager.Instance.SetCharacterUndetectable(character, true);
        SetCharacterRenderersVisible(character, false);
        character.SetExternalAnimControl(true);
        character.RemoveAllTarget();

        AnimationDisplayer corpse = CreateCorpseOnCharacter(character);
        if (corpse != null)
        {
            corpse.SetMaanimPointer(0);
            yield return WaitFixedFrames(Mathf.Max(0, duration)+ TransitionFrames, character);
            corpse.SetMaanimPointer(1);
            yield return WaitFixedFrames(TransitionFrames, character);
            if (character != null && character.EM != null) character.EM.RecycleBattleObject(corpse, CorpseEffectName);
            else GameObject.Destroy(corpse.gameObject);
        }
        else
        {
            yield return WaitFixedFrames(Mathf.Max(0, duration + TransitionFrames * 2), character);
        }

        if (character == null) yield break;

        SetCharacterRenderersVisible(character, true);
        CharacterTargetManager.Instance.SetCharacterUndetectable(character, false);
        character.SetExternalAnimControl(false);
        character.SwitchAnimation(0);
        reviving = false;
    }

    private IEnumerator WaitFixedFrames(int frameCount, Character character)
    {
        for (int i = 0; i < frameCount; i++)
        {
            if (character == null) yield break;
            yield return new WaitForFixedUpdate();
        }
    }

    private void SetCharacterRenderersVisible(Character character, bool visible)
    {
        if (character == null) return;
        character.transform.GetChild(0).gameObject.SetActive(visible);
    }

    private AnimationDisplayer CreateCorpseOnCharacter(Character character)
    {
        if (character == null || character.EM == null) return null;
        return character.EM.InstantiateAttachedBattleObject(
            CorpseEffectName,
            character.transform.position-new Vector3(0, 0.5f, 0),
            character.transform,
            worldPositionStays: true,
            playSound: false);
    }

}

public class ClearDebuffs : PassiveSkill
{
    public override void OnAfterAttack(Character character, float dmg, List<CharacterEffect> ces, List<AttackType> types)
    {
        if (character == null) return;
        if (!Triggered()) return;

        IReadOnlyList<Character> hitTargets = character.GetLastAttackHitTargets();
        for (int i = 0; i < hitTargets.Count; i++)
        {
            ClearNegativeDebuffs(hitTargets[i]);
        }
    }

    private static void ClearNegativeDebuffs(Character target)
    {
        if (target == null) return;
        E[] effects = target.GetComponents<E>();
        for (int i = 0; i < effects.Length; i++)
        {
            E effect = effects[i];
            if (effect == null) continue;
            if (effect.GetEffectName() == EffectName.deathmark) continue;
            UnityEngine.Object.Destroy(effect);
        }
    }
}

public class DodgePassive : PassiveSkill
{
    private bool invulnerable;
    private bool matchedThisHit;
    private Coroutine invulnRoutine;

    // Cats only dodge trait-matched hits; enemies dodge any hit.
    // OnMatchedTraits fires right before OnBeforeTakeDamage, so we record the match here
    // instead of reaching into the character's shared incoming-trait flag.
    public override void OnMatchedTraits(Character character, List<AttackType> atkTypes)
    {
        matchedThisHit = true;
    }

    public override void OnBeforeTakeDamage(Character character, ref float DMG, List<AttackType> atkTypes)
    {
        bool matched = matchedThisHit;
        matchedThisHit = false;

        if (character == null) return;
        if (character.IsCat() && !matched) return;
        if (invulnerable)
        {
            DMG = 0f;
            return;
        }
        if (!Triggered()) return;
        DMG = 0f;
        if (invulnRoutine != null)
        {
            character.StopCoroutine(invulnRoutine);
            invulnRoutine = null;
        }
        invulnRoutine = character.StartCoroutine(InvulnerableWindowRoutine(character));
    }

    private IEnumerator InvulnerableWindowRoutine(Character character)
    {
        invulnerable = true;
        int frames = Mathf.Max(1, duration);
        for (int i = 0; i < frames; i++)
        {
            if (character == null) break;
            yield return new WaitForFixedUpdate();
        }
        invulnerable = false;
        invulnRoutine = null;
    }
}

public class BaseCharacter : PassiveSkill
{
    private const int IdleAnimIndex = 3;
    private const int FixedSortingOrder = 2000;
    private const float FixedSortingYOffset = FixedSortingOrder / 10000f;
    private Coroutine monitorBaseRoutine;
    private Coroutine breakdownRoutine;
    private bool baseDefeatHandled;

    public override void OnAddingAbility(Character character)
    {
        if (character == null) return;
        if (character.IsCat()) return; // 我方先预留空逻辑
        character.SetSkipTargetRegistration(true);
    }

    public override void OnDeployUnit(Character character)
    {
        if (character == null) return;
        if (character.IsCat()) return; // 我方先预留空逻辑

        character.SetSkipTargetRegistration(true);
        character.Speed = 0;
        character.ChangeSpeed(0);
        character.RemoveAllTarget();
        CharacterTargetManager manager = CharacterTargetManager.Instance;
        manager.UnregisterCharacter(character);

        DogeBase dogeBase = UnityEngine.Object.FindObjectOfType<DogeBase>();
        if (dogeBase == null) return;

        SnapToBaseWithFixedSorting(character, dogeBase.transform.position);
        ApplyFixedSortingLayer(character);
        if (dogeBase.transform.childCount > 0)
        {
            dogeBase.transform.GetChild(0).gameObject.SetActive(false);
        }
        manager.RefreshTargetsForCharacter(character);

        if (monitorBaseRoutine != null) character.StopCoroutine(monitorBaseRoutine);
        monitorBaseRoutine = character.StartCoroutine(MonitorBaseDefeat(character, dogeBase, manager));
    }

    public override void OnDead(Character character)
    {
        if (character == null) return;
        if (monitorBaseRoutine != null)
        {
            character.StopCoroutine(monitorBaseRoutine);
            monitorBaseRoutine = null;
        }
        if (breakdownRoutine != null)
        {
            character.StopCoroutine(breakdownRoutine);
            breakdownRoutine = null;
        }
    }

    private IEnumerator MonitorBaseDefeat(Character character, DogeBase dogeBase, CharacterTargetManager manager)
    {
        while (character != null && dogeBase != null && dogeBase.GetHealthPercentage() > 0f)
        {
            SnapToBaseWithFixedSorting(character, dogeBase.transform.position, refreshStartPos: false);
            manager?.RefreshTargetsForCharacter(character);
            yield return new WaitForFixedUpdate();
        }

        monitorBaseRoutine = null;
        if (character == null || baseDefeatHandled) yield break;
        baseDefeatHandled = true;

        character.SetExternalAnimControl(true);
        character.Speed = 0;
        character.ChangeSpeed(0);
        character.RemoveAllTarget();
        character.SwitchAnimation(IdleAnimIndex);

        if (breakdownRoutine != null) character.StopCoroutine(breakdownRoutine);
        breakdownRoutine = character.StartCoroutine(BreakingDown(character));
    }

    private IEnumerator BreakingDown(Character character)
    {
        const float width = 6f;
        const float height = 6f;
        while (character != null && character.EM != null)
        {
            float dx = -UnityEngine.Random.Range(0f, width);
            float dy = UnityEngine.Random.Range(0f, height);
            character.EM.InstantiateBattleObject(SEnums.bite, character.transform.position.x + dx, character.transform.position.y + dy, false);
            yield return new WaitForFixedUpdate();
        }
    }

    private static void SnapToBaseWithFixedSorting(Character character, Vector3 basePos, bool refreshStartPos = true)
    {
        character.transform.position = new Vector3(basePos.x, basePos.y - FixedSortingYOffset, basePos.z);
        if (refreshStartPos) character.RefreshStartPos();
    }

    private static void ApplyFixedSortingLayer(Character character)
    {
        if (character == null) return;
        if (character.UNITYAnimated)
        {
            if (character.SPINEAnimated)
                CharacterSummoner.ResetSpineOrderLayer(character.gameObject, "Units", FixedSortingOrder);
            else
                CharacterSummoner.ResetAnimationOrderLayer(character.gameObject, "Units", FixedSortingOrder);
            return;
        }

        AnimationDisplayer ad = character.GetComponent<AnimationDisplayer>();
        if (ad != null) CharacterSummoner.ResetAnimationOrderLayer(ad, FixedSortingOrder);
    }
}

public class Aux_DeathMark : PassiveSkill
{
    private static bool isSharingDamage;
    private static readonly List<Character> shareTargetsBuffer = new List<Character>(32);
    private static readonly List<AttackType> shareDamageTypes = new List<AttackType> { AttackType.effectBlocked };

    private float healthBeforeDamage;
    private bool trackDamage;

    public override void OnBeforeTakeDamage(Character character, ref float DMG, List<AttackType> atkTypes)
    {
        if (character == null) return;
        if (isSharingDamage) return;
        if (DMG <= 0f)
        {
            trackDamage = false;
            return;
        }

        healthBeforeDamage = character.GetHealth();
        trackDamage = true;
    }

    public override void OnAfterTakeDamage(Character character)
    {
        if (character == null) return;
        if (isSharingDamage) return;
        if (!trackDamage) return;
        trackDamage = false;

        float damageTaken = Mathf.Max(0f, healthBeforeDamage - character.GetHealth());
        if (damageTaken <= 0f) return;

        int otherCount = CharacterTargetManager.Instance.FillDeathMarkedCharacters(shareTargetsBuffer, character);
        if (otherCount <= 0) return;

        float sharedDamage = damageTaken / otherCount;
        if (sharedDamage <= 0f) return;

        isSharingDamage = true;
        try
        {
            for (int i = 0; i < otherCount; i++)
            {
                Character target = shareTargetsBuffer[i];
                if (target == null) continue;
                // Use a neutral attack type to avoid spawning extra hit VFX/audio for each shared tick.
                target.ReceiveAttack(sharedDamage, null, null, null, null, null, shareDamageTypes);
            }
        }
        finally
        {
            isSharingDamage = false;
            shareTargetsBuffer.Clear();
        }
    }

    public override void OnBeforeKB(Character character)
    {
        if (character == null || character.EM == null) return;
        character.EM.InstantiateBattleObject("doomed", character.transform.position.x, character.transform.position.y);
    }
}
public class Aux_InvisibleShow : PassiveSkill
{
    private Transform ct;
    private float originalPosY;
    private static int invisible_posY = -1000;
    private bool tracked = false;
    public override void OnDeployUnit(Character character)
    {
        ct = character.transform;
        originalPosY = ct.position.y;
        ct.position = new Vector2(ct.position.x, invisible_posY);
    }
    public override void OnAfterTakeDamage(Character character)
    {
        if(!tracked) ct.position = new Vector2(ct.position.x, originalPosY);
        tracked = true;
    }
}
public class Aux_OneHit : PassiveSkill
{
    private bool pendingPositiveDamage = false;
    private bool triggered = false;

    public override void OnBeforeTakeDamage(Character character, ref float DMG, List<AttackType> atkTypes)
    {
        if (triggered)
        {
            if (DMG > 0f) DMG = 0f;
            return;
        }

        pendingPositiveDamage = DMG > 0f;
    }

    public override void OnAfterTakeDamage(Character character)
    {
        if (!pendingPositiveDamage || triggered || character == null) return;

        pendingPositiveDamage = false;
        triggered = true;
        if (character.GetHealth() <= 0)
        {
            character.SetHealth(1);
        }

        character.RemovePassiveEffect(this);
        character.StartCoroutine(OneHitDeathFlight(character));
    }

    private IEnumerator OneHitDeathFlight(Character character)
    {
        if (character == null) yield break;

        character.SetExternalAnimControl(true);
        CharacterTargetManager.Instance.SetCharacterUndetectable(character, true);
        character.RemoveAllTarget();
        character.SwitchAnimation(3);
        FreezeCharacterAnimation(character);

        float durationSeconds = UnityEngine.Random.Range(0.5f, 2f);
        float totalRotation = UnityEngine.Random.Range(360f, 1080f) * (UnityEngine.Random.value > 0.5f ? 1f : -1f);
        //Transform visual = character.transform.childCount > 0 ? character.transform.GetChild(0) : character.transform;
        Transform visual = character.transform;
        Vector3 startPosition = visual.position;
        Vector3 endPosition = GetOffscreenTarget(startPosition);
        Vector3 startScale = visual.localScale;
        Vector3 endScale = GetEndScale(startScale, endPosition.y >= startPosition.y);
        float startRotationZ = visual.eulerAngles.z;

        float elapsed = 0f;
        while (elapsed < durationSeconds && character != null && visual != null)
        {
            elapsed += Time.deltaTime;
            float t = Mathf.Clamp01(elapsed / durationSeconds);
            visual.position = Vector3.Lerp(startPosition, endPosition, t);
            float currentRotationZ = startRotationZ + totalRotation * t;
            visual.rotation = Quaternion.Euler(0f, 0f, currentRotationZ);
            visual.localScale = Vector3.Lerp(startScale, endScale, t);
            yield return null;
        }

        if (character == null || visual == null) yield break;

        visual.position = endPosition;
        visual.rotation = Quaternion.Euler(0f, 0f, startRotationZ + totalRotation);
        visual.localScale = endScale;
        character.transform.position = visual.position;
        CharacterTargetManager.Instance.SetCharacterUndetectable(character, false);
        character.Dead();
    }

    private static void FreezeCharacterAnimation(Character character)
    {
        if (character == null) return;

        Transform visual = character.transform.childCount > 0 ? character.transform.GetChild(0) : null;
        AnimationDisplayer animationDisplayer = visual != null
            ? visual.GetComponent<AnimationDisplayer>()
            : character.GetComponentInChildren<AnimationDisplayer>();
        if (animationDisplayer != null)
        {
            animationDisplayer.SetAnimationSpeed(0);
        }

        Animator animator = character.GetComponentInChildren<Animator>();
        if (animator != null)
        {
            animator.speed = 0f;
        }
    }

    private static Vector3 GetOffscreenTarget(Vector3 startPosition)
    {
        Camera camera = Camera.main;
        if (camera == null)
        {
            Vector2 fallbackDirection = UnityEngine.Random.insideUnitCircle;
            if (fallbackDirection.sqrMagnitude < 0.01f) fallbackDirection = Vector2.up;
            fallbackDirection.Normalize();
            return startPosition + new Vector3(fallbackDirection.x * 18f, fallbackDirection.y * 10f, 0f);
        }

        Vector2 direction = UnityEngine.Random.insideUnitCircle;
        if (direction.sqrMagnitude < 0.01f)
        {
            direction = new Vector2(UnityEngine.Random.value > 0.5f ? 1f : -1f, UnityEngine.Random.value > 0.5f ? 1f : -1f);
        }
        direction.Normalize();

        Vector3 viewportPosition = camera.WorldToViewportPoint(startPosition);
        float viewportX = direction.x >= 0f ? 1.25f : -0.25f;
        float viewportY = direction.y >= 0f ? 1.25f : -0.25f;
        float depth = Mathf.Max(0.01f, viewportPosition.z);
        Vector3 target = camera.ViewportToWorldPoint(new Vector3(viewportX, viewportY, depth));
        target.z = startPosition.z;
        return target;
    }

    private static Vector3 GetEndScale(Vector3 startScale, bool isFlyingUpward)
    {
        if (isFlyingUpward)
        {
            return new Vector3(
                ScaleAxis(startScale.x, 0.33f),
                ScaleAxis(startScale.y, 0.33f),
                ScaleAxis(startScale.z, 1f));
        }

        return new Vector3(
            ScaleAxis(startScale.x, 2f),
            ScaleAxis(startScale.y, 2f),
            ScaleAxis(startScale.z, 1f));
    }

    private static float ScaleAxis(float value, float multiplier)
    {
        float sign = value < 0f ? -1f : 1f;
        return sign * Mathf.Max(0f, Mathf.Abs(value) * multiplier);
    }
}
