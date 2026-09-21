using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class Practician : AnimationDrivingPassive
{
    private float atk = 1;
    private int stack = 0;
    private const int maxStack = 6;
    private const float atkMuitipiler = 0.2f;

    protected override AbilityName? StatusBarAbility => AbilityName.practician;
    protected override Color StatusBarColor => new Color(1.000f, 0.302f, 1.000f, 1.000f);
    protected override float GetStatusBarProgress(Character character) =>
        Mathf.Clamp01(stack / (float)maxStack);

    public override void OnAfterAttack(Character character, float dmg, List<CharacterEffect> ces, List<AttackType> types)
    {
        // 原来写的是 stack > maxStack，会让 stack 涨到 7、atk 多累积一次命中，
        // 状态条的 Clamp01 恰好把这个溢出盖住了。
        if (stack >= maxStack) return;
        stack++;
        atk += Mathf.Abs(dmg);
        RefreshStatusBar(character);
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
        if (c == null || driving || c.IsOnKB()) return;
        int specialAnim = c.GetExtraAnimIndex(CharacterVisualLoader.ExtraAnim.P);
        if (specialAnim < 0) return;
        c.StartCoroutine(RunDrive(c, specialAnim));
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
        atk = 1;
        stack = 0;
        RefreshStatusBar(c);
        if (c == null || c.IsOnKB()) return;
        c.ExitAttack();
        c.Supporter_Target_Switch(true);
        // 驱动期把范围改成了 intensity，结束后必须复位，否则单位一直带着这个窄范围。
        // 同时这也是模式变更的同步点（见 CharacterTargetManager.SetCharacterAttackRange）。
        c.ResetAttackRangeToDetection();
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
    private const string BloodEffectPath = "Effects/lacerate_blood";
    private static GameObject bloodPrefab;

    public override void OnAfterAttack(Character character, float dmg, List<CharacterEffect> ces, List<AttackType> types)
    {
        character.ReceiveAttack(character.GetMaxHealth() * 0.05f, null, null, null, null, null, null);
        // 每次命中都会走到这里，Resources.Load 只做一次。
        if (bloodPrefab == null) bloodPrefab = Resources.Load<GameObject>(BloodEffectPath);
        if (bloodPrefab == null) return;
        GameObject.Instantiate(bloodPrefab, character.transform.position, Quaternion.identity);
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
public class Critical : PassiveSkill
{
    public override void OnAttacking(Character character, ref float dmg, ref List<AttackType> types)
    {
        if (Triggered()) { dmg *= 1.75f; types.Add(AttackType.critical);}
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
public class ShieldPiercing : PassiveSkill
{
    public override void OnAttacking(Character character, ref float dmg, ref List<AttackType> types)
    {
        if (Triggered()) types.Add(AttackType.shieldPiercing);
    }
}
public class Impatience : StatusBarPassive
{
    private int impatience_level = 0;
    private int originalReload = 0;

    protected override AbilityName? StatusBarAbility => AbilityName.impatience;
    protected override Color StatusBarColor => new Color(1.000f, 0.490f, 0.251f, 1.000f);
    // 焦躁的易伤倍率同样作用于治疗（设定如此，故 opt-in）。
    public override bool CanModifyHealing => true;
    protected override float GetStatusBarProgress(Character character)
    {
        if (originalReload <= 0) return 1f;
        int current = character != null ? character.GetReload() : originalReload;
        return Mathf.Clamp01(current / (float)originalReload);
    }

    public override void OnAddingAbility(Character character)
    {
        if (character != null && originalReload <= 0) originalReload = character.GetReload();
        base.OnAddingAbility(character);
    }

    public override void OnBeforeTakeDamage(Character character, ref float DMG, List<AttackType> atkTypes)
    {
        DMG = DMG * (probability + intensity * impatience_level) / 100;
    }
    public override void OnFinishAttack(Character character)
    {
        impatience_level++;
        character.SetReload(character.GetReload() - duration);
        RefreshStatusBar(character);
    }
}
public class PressureLearn : StatusBarPassive
{
    private const int MaxPressure = 1000;
    private const int CriticalReducer = 200;
    private int normal_pressure = 0;

    protected override AbilityName? StatusBarAbility => AbilityName.pressureLearn;
    protected override Color StatusBarColor => new Color(0.125f, 0.678f, 1.000f, 1.000f);
    protected override float GetStatusBarProgress(Character character) =>
        Mathf.Clamp01(GetMaxPressure() / (float)MaxPressure);

    public override void OnAddingAbility(Character character)
    {
        int start = intensity * 10;
        normal_pressure = Mathf.Clamp(start, 0, MaxPressure);
        base.OnAddingAbility(character);
    }

    public override void OnBeforeTakeDamage(Character character, ref float DMG, List<AttackType> atkTypes)
    {
        // 治疗（负伤害）不该积攒压力，也不该被压力缩放。分发器只挡住对 DMG 的改写，
        // 挡不住这里对 normal_pressure 的副作用，所以要显式跳过。
        if (DMG < 0f) return;

        bool hasCritical = false;
        if (atkTypes != null)
        {
            if(atkTypes.Contains(AttackType.critical)) { hasCritical = true; }
        }

        if (hasCritical)
        {
            ReduceAllPressure(CriticalReducer);
            DMG *= 1.2f * (MaxPressure - GetMaxPressure()) / (float)MaxPressure;
            RefreshStatusBar(character);
            return;
        }
        if (GetMaxPressure() >= MaxPressure)
        {
            DMG = 0f;
            return;
        }
        ApplyPressureAndScaleDamage(ref normal_pressure, probability, ref DMG);
        RefreshStatusBar(character);
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
    }

    private int GetMaxPressure()
    {
        return normal_pressure;
    }
}

public class TargetHighestHp : PassiveSkill
{
    public override void OnAttacking(Character character, ref float dmg, ref List<AttackType> types)
    {
        if (character == null) return;
        List<GameObject> targets = character.Targets;
        if (targets == null || targets.Count <= 1) return;

        GameObject best = null;
        float bestHp = float.MinValue;
        for (int i = 0; i < targets.Count; i++)
        {
            GameObject go = targets[i];
            if (go == null) continue;
            Character unit = go.GetComponent<Character>();
            if (unit == null) continue;
            float hp = unit.GetHealth();
            if (best != null && hp <= bestHp) continue;
            best = go;
            bestHp = hp;
        }
        if (best == null) return;

        for (int i = targets.Count - 1; i >= 0; i--)
        {
            if (targets[i] != best) targets.RemoveAt(i);
        }
    }
}

public class Savage : PassiveSkill
{
    public override void OnAttacking(Character character, ref float dmg, ref List<AttackType> types)
    {
        if (Triggered()) { dmg *= 3; types.Add(AttackType.savage);}
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
/// 死亡时在身前放一发 Surge（「亡魂」）。
/// probability = 触发几率(%)，duration = Surge 等级，intensity = 生成距离上限，
/// 单位和 surge / miniSurge 一样是 1/100 世界单位，实际落点在 [intensity/2, intensity) 里随机。
/// 伤害取这个单位所有攻击段强度的总和（见 Character.GetTotalAttackDamage，治疗段按绝对值计，
/// 所以纯奶妈的亡魂也是一发正经的伤害）。
///
/// 这里可以安全地在 OnDead 里生成东西：Surge_Attack 出的 surgeunit 和 deadSoul 特效都是
/// 独立的根物体，不挂在角色下面，所以紧接着执行的 Dead() → Destroy(gameObject) 不会把它们
/// 一起带走；后续的动画和判定各自由 SurgeUnit 的协程和 PooledEffectLifetime 驱动，
/// 不依赖已经死掉的角色。
/// </summary>
public class DeadSoul : PassiveSkill
{
    /// <summary>deadSoul 的 maanim 只有一段、73 帧，多给两帧余量后回池。</summary>
    private const int SoulEffectFrames = 75;

    /// <summary>
    /// 亡魂特效播到第 30 帧才炸出 Surge，让「魂飘出去」和「爆发」接得上。
    /// 这段等待由 surgeunit 自己数（Surge_Attack 的 delayFrames），不是在这里 yield，
    /// 因为本方法返回后角色立刻就被 Dead() 销毁了。
    /// </summary>
    private const int SurgeDelayFrames = 30;

    public override void OnDead(Character character)
    {
        if (character == null) return;
        if (!Triggered()) return;

        int distance = UnityEngine.Random.Range(intensity / 2, intensity);
        // 攻击类型沿用单位自身的 ATKTypes（穿盾、破障之类），SurgeUnit 会在前面再补一个
        // AttackType.surge。拷一份是因为紧接着角色就被 Destroy 了，不留引用更干净。
        List<AttackType> types = character.ATKTypes == null
            ? new List<AttackType>()
            : new List<AttackType>(character.ATKTypes);
        character.Surge_Attack(
            duration,
            false,
            distance,
            character.GetTotalAttackDamage(),
            character.DetermineSelfATKEffects(),
            types,
            SurgeDelayFrames);

        // 特效立刻播，放在尸体位置而不是 Surge 落点：Surge 自己已经有 surge/surge_e 的表现，
        // deadSoul 演的是魂从死者身上飘出去这一下。
        character.EM?.InstantiateBattleObject(
            character.IsCat() ? "deadSoul" : "deadSoul_e",
            character.transform.position.x,
            character.transform.position.y,
            true,
            SoulEffectFrames);
    }
}
public class ATK_Buffer : PassiveSkill
{
    public override void OnAfterAttack(Character character, float dmg, List<CharacterEffect> ces, List<AttackType> types)
    {
        IReadOnlyList<Character> hitTargets = character.GetLastAttackHitTargets();
        int count = character.areaATK ? hitTargets.Count : Mathf.Min(1, hitTargets.Count);
        for (int i = 0; i < count; i++)
        {
            IncreaseATK(hitTargets[i]);
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
        int penalty = PlayerPrefs.GetInt(UXPref.RewardPenalty, 0);
        if (penalty >= int.MaxValue) return;
        PlayerPrefs.SetInt(UXPref.RewardPenalty, penalty + 1);
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
            prefab = BundledAddressables.LoadSync<GameObject>($"Units/Projectiles/p{probability:000}/projunit");
            ProjectilePrefabCache[probability] = prefab;
        }
        if (prefab == null)
        {
            Debug.Log("No such projectile!");
            return;
        }

        GameObject go = GameObject.Instantiate(prefab, character.transform.position+new Vector3(0, intensity/100f,0), Quaternion.identity);
        CharacterVisualLoader.ResetAnimationOrderLayer(go, "Units", 20000);
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
