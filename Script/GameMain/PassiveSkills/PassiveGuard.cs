using System.Collections;
using System.Collections.Generic;
using UnityEngine;

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
public class Metal : PassiveSkill
{
    // 铁壁把非会心的伤害压成 1，治疗同样压成 1（保持原有行为，故 opt-in）。
    public override bool CanModifyHealing => true;

    public override void OnBeforeTakeDamage(Character character, ref float DMG, List<AttackType> atkTypes)
    {
        if (DMG == 0) return;
        // 自造伤害（LaceratedEffect / Sacrifice / Aux_SelfDamage 等）会传 null atkTypes。
        // 会心和毒伤穿透铁壁：毒伤按最大生命百分比结算，压成 1 等于让铁壁完全免疫毒。
        bool pierce = atkTypes != null &&
            (atkTypes.Contains(AttackType.critical) || atkTypes.Contains(AttackType.toxic));
        if (!pierce) DMG = DMG > 0 ? 1 : -1;
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

    public bool TryRestore(Character character, int newIntensity)
    {
        if (!broken) return false;
        hardness = Mathf.Max(0f, newIntensity);
        broken = false;
        PlaySpawn(character);
        return true;
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
    }

    public override void OnDead(Character character)
    {
        CleanupShieldAnim(character);
    }

    protected void PlayShieldAnim(Character character, int animIndex)
    {
        if (character == null || character.EM == null) return;
        Vector3 pos = character.transform.position + new Vector3(0f, 0.5f, 0f);
        character.EM.PlayReusableAttachedEffect(
            ref shieldDisplay,
            shieldEffectName,
            character.transform,
            pos,
            animIndex,
            worldPositionStays: true);
        if (shieldDisplay != null)
            CharacterVisualLoader.ResetAnimationOrderLayer(shieldDisplay, 20000);
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

    public void PlaySpawn(Character character) => PlayShieldAnim(character, 0);
}

public class AkuShield : StatusBarPassive
{
    private string shieldEffectName;
    private float maxHardness;
    private float remainingHardness;
    private bool broken;
    private bool oneTime;
    private AnimationDisplayer shieldDisplay;

    protected override AbilityName? StatusBarAbility => AbilityName.akuShield;
    protected override Color StatusBarColor => new Color(0.231f, 0.196f, 0.745f, 1.000f);
    protected override float GetStatusBarProgress(Character character)
    {
        if (maxHardness <= 0f) return 0f;
        return Mathf.Clamp01(remainingHardness / maxHardness);
    }

    public void SetOneTime(bool value) => oneTime = value;

    public override void OnAddingAbility(Character character)
    {
        float multiplier = character != null ? character.GetCombatStatMultiplier() : 1f;
        maxHardness = Mathf.Max(0f, intensity * multiplier);
        remainingHardness = maxHardness;
        Debug.Log($"Akus: maxHardness = {maxHardness}");
        broken = false;
        shieldEffectName = character != null && character.IsCat() ? "akuShield" : "akuShield_e";
        base.OnAddingAbility(character);
    }

    public void Reinforce(Character character, int addIntensity)
    {
        float add = Mathf.Max(0f, addIntensity * (character != null ? character.GetCombatStatMultiplier() : 1f));
        maxHardness += add;
        remainingHardness += add;
        broken = remainingHardness <= 0f;
        PlayShieldAnim(character, 0);
        RefreshStatusBar(character);
    }

    public void PlaySpawn(Character character) => PlayShieldAnim(character, 0);

    public override void OnBeforeTakeDamage(Character character, ref float DMG, List<AttackType> atkTypes)
    {
        if (character == null) return;
        if (DMG < 0f) return;

        bool isShieldPiercing = atkTypes != null && atkTypes.Contains(AttackType.shieldPiercing);
        if (isShieldPiercing)
        {
            PlayShieldAnim(character, 3);
            DMG *= 1.25f;
            remainingHardness = 0f;
            broken = true;
            RefreshStatusBar(character);
            return;
        }

        if (broken || remainingHardness <= 0f) return;

        float incomingDamage = Mathf.Max(0f, DMG);
        if (incomingDamage <= 0f) return;

        remainingHardness -= incomingDamage;
        DMG = 0;
        if (remainingHardness > 0f)
        {
            PlayShieldAnim(character, 1);
        }
        else
        {
            broken = true;
            PlayShieldAnim(character, 2);
        }
        RefreshStatusBar(character);
    }

    /// <summary>
    /// 被 KB_Type.none 击退后按 probability（%）重新恢复盾量：probability=50、intensity=100
    /// 就是破盾后重新拿到 50 点盾（上限仍然是 100，所以状态条显示一半）。
    /// 恢复量按 maxHardness 算而不是按 intensity，这样 ShieldProvider.Reinforce 抬高上限后
    /// 恢复量也跟着涨，和状态条的分母保持一致。probability=0 就是破了不再回来。
    /// </summary>
    public override void OnAfterKB(Character character)
    {
        if (character == null || oneTime) return;
        if (!broken) return;
        if (character.GetLastTriggeredKBType() != KB_Type.none) return;
        float restored = maxHardness * Mathf.Clamp(probability, 0, 100) / 100f;
        if (restored <= 0f) return;
        remainingHardness = restored;
        broken = false;
        PlayShieldAnim(character, 0);
        RefreshStatusBar(character);
    }

    public override void OnDead(Character character)
    {
        CleanupShieldAnim(character);
        base.OnDead(character);
    }

    private void PlayShieldAnim(Character character, int animIndex)
    {
        if (character == null || character.EM == null) return;
        Vector3 pos = character.transform.position + new Vector3(0f, 0.5f, 0f);
        character.EM.PlayReusableAttachedEffect(
            ref shieldDisplay,
            shieldEffectName,
            character.transform,
            pos,
            animIndex,
            worldPositionStays: true);
        if (shieldDisplay != null)
            CharacterVisualLoader.ResetAnimationOrderLayer(shieldDisplay, 20000);
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

public class BarrierProvider : PassiveSkill
{
    public override void OnAfterAttack(Character character, float dmg, List<CharacterEffect> ces, List<AttackType> types)
    {
        if (character == null || !Triggered()) return;
        IReadOnlyList<Character> hitTargets = character.GetLastAttackHitTargets();
        for (int i = 0; i < hitTargets.Count; i++)
        {
            Character target = hitTargets[i];
            if (target == null) continue;
            Barrier existing = target.GetPassive<Barrier>();
            if (existing != null)
            {
                existing.TryRestore(target, intensity);
                continue;
            }
            var barrier = new Barrier();
            barrier.SetPassiveValues(AbilityName.barrier, 100, 0, intensity);
            AbilityInstaller.Install(target, barrier);
            barrier.PlaySpawn(target);
        }
    }
}

public class ShieldProvider : PassiveSkill
{
    public override void OnAfterAttack(Character character, float dmg, List<CharacterEffect> ces, List<AttackType> types)
    {
        if (character == null || !Triggered()) return;
        IReadOnlyList<Character> hitTargets = character.GetLastAttackHitTargets();
        for (int i = 0; i < hitTargets.Count; i++)
        {
            Character target = hitTargets[i];
            if (target == null) continue;
            AkuShield existing = target.GetPassive<AkuShield>();
            if (existing != null)
            {
                existing.Reinforce(target, intensity);
                continue;
            }
            var shield = new AkuShield();
            shield.SetPassiveValues(AbilityName.akuShield, 100, 0, intensity);
            shield.SetOneTime(true);
            AbilityInstaller.Install(target, shield);
            shield.PlaySpawn(target);
        }
    }
}

public class DodgePassive : PassiveSkill
{
    private bool invulnerable;
    private Coroutine invulnRoutine;

    // Cats only *trigger* dodge on trait-matched hits; enemies can trigger on any hit.
    // Once the invuln window is active, all incoming damage is zeroed.
    //
    // 相符判定直接读 Character 每次 ReceiveAttack 都会刷新的 incomingTraitCorresponding，
    // 不自己缓存一份：缓存要求「置位（OnMatchedTraits）」和「消费（本方法）」成对执行，
    // 而完全抗性那条路（ReceiveAttack 里 ar.intensity > 99）会在两者之间提前 return，
    // 把置位漏给下一次任意攻击，导致不该闪避的时候闪避。
    public override void OnBeforeTakeDamage(Character character, ref float DMG, List<AttackType> atkTypes)
    {
        if (character == null) return;
        if (invulnerable)
        {
            DMG = 0f;
            return;
        }
        if (character.IsCat() && !character.HasIncomingTraitCorresponding()) return;
        if (!Triggered()) return;
        DMG = 0f;
        ActivateInvulnerableWindow(character);
    }

    public void ActivateInvulnerableWindow(Character character, int durationFrames = -1)
    {
        if (character == null) return;
        if (invulnRoutine != null)
        {
            character.StopCoroutine(invulnRoutine);
            invulnRoutine = null;
        }
        invulnRoutine = character.StartCoroutine(InvulnerableWindowRoutine(character, durationFrames));
    }

    private IEnumerator InvulnerableWindowRoutine(Character character, int durationFrames)
    {
        invulnerable = true;
        int frames = durationFrames > 0 ? durationFrames : Mathf.Max(1, duration);
        for (int i = 0; i < frames; i++)
        {
            if (character == null) break;
            yield return new WaitForFixedUpdate();
        }
        invulnerable = false;
        invulnRoutine = null;
    }
}
