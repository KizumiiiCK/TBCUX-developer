using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class AffectByStrategy : PassiveSkill
{
    private Coroutine retreatRoutine;

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
                character.ResetAttackRangeToDetection();
                break;
            case 1:
                character.SetSkillFactor(1.2f);
                character.ResetAttackRangeToDetection();
                if (character.IsExternalAnimControlled() || character.IsOnKB()) break;
                if (character.Targets.Count > 0) StartRetreat(character);
                break;
            case 2:
                character.SetSkillFactor(0.75f);
                character.ResetAttackRangeToDetection(0.5f);
                break;
            default: break;
        }
    }
    public override void OnDead(Character character)
    {
        StopRetreat(character);
    }

    /// <summary>
    /// 撤退是「每帧 Translate 一次」的协程，同时跑两个就是双倍速度。
    /// 攻击结束（OnFinishAttack）和挨击退（OnAfterKB）在时间上很容易挨着，
    /// 所以起新的之前先停掉旧的——重启也比忽略合理，新一轮该按当下的目标重新算。
    /// </summary>
    private void StartRetreat(Character character)
    {
        if (character == null) return;
        StopRetreat(character);
        retreatRoutine = character.StartCoroutine(Retreat(character));
    }

    private void StopRetreat(Character character)
    {
        if (retreatRoutine == null) return;
        // 角色已销毁时协程早就随对象停了，此时 StopCoroutine 会抛异常，只清句柄。
        if (character != null) character.StopCoroutine(retreatRoutine);
        retreatRoutine = null;
    }

    private IEnumerator Retreat(Character character)
    {
        // 多个出口，用 finally 统一清句柄（只带 finally 的 try 里允许 yield）。
        try
        {
            if (ShouldAbortRetreat(character)) yield break;
            GameObject nearest = character.FindNearest();
            if (nearest == null) yield break;
            float nearestpoint = nearest.transform.position.x;
            yield return new WaitForFixedUpdate();
            if (ShouldAbortRetreat(character)) yield break;
            character.SwitchAnimation(0);

            while (!ShouldAbortRetreat(character)
                && (nearestpoint + character.DetectionRange / 100f * 0.9f) > character.transform.position.x)
            {
                character.transform.Translate(new Vector2(character.TBCspeedTranslator(2 * character.GetRealSpeed()) * Time.deltaTime, 0));
                yield return new WaitForFixedUpdate();
            }
        }
        finally
        {
            retreatRoutine = null;
        }
    }
    private static bool ShouldAbortRetreat(Character character)
    {
        return character == null || character.IsExternalAnimControlled() || character.IsOnKB();
    }
}
public class Supporter : PassiveSkill
{
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
public class ExtraMoney : PassiveSkill
{
    //???????????????????????
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
