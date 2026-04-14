using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEngine;
using static UnityEngine.RuleTile.TilingRuleOutput;

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
        { AbilityName.savage,     typeof(Savage)},
        { AbilityName.wave,       typeof(Wave)},
        { AbilityName.miniWave,   typeof(MiniWave)},
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
    };
    public static void Install(Character C, CharacterAbility ca)
    {
        if (!skillMap.TryGetValue(ca.name, out var skillType)) return;
        var passive = (PassiveSkill)Activator.CreateInstance(skillType);
        passive.SetPassiveValues(ca.name, ca.probability, ca.duration, ca.intensity);
        C.AddPassiveEffect(passive);
        passive.OnAddingAbility(C);
    }
}
public interface PassiveNode
{
    void OnStartingGame();
    void OnDeployUnit(Character character);
    void OnAddingAbility(Character character);
    void OnBeforeTakeDamage(Character character, ref float DMG, List<AttackType> atkTypes);
    void OnAfterTakeDamage(Character character);
    void OnStartAttack(Character character);
    void OnAttacking(Character character, ref float dmg, ref List<AttackType> types);
    void OnAfterAttack(Character character, float dmg, List<CharacterEffect> ces, List<AttackType> types);
    void OnFinishAttack(Character character);
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
    public virtual void OnStartingGame() { }
    public virtual void OnDeployUnit(Character character) { }
    public virtual void OnAddingAbility(Character character) { }
    public virtual void OnBeforeTakeDamage(Character character, ref float DMG, List<AttackType> atkTypes) { }
    public virtual void OnAfterTakeDamage(Character character) { }
    public virtual void OnStartAttack(Character character) { }
    public virtual void OnAttacking(Character character, ref float dmg, ref List<AttackType> types) { }
    public virtual void OnAfterAttack(Character character, float dmg, List<CharacterEffect> ces, List<AttackType> types) { }
    public virtual void OnFinishAttack(Character character) { }
    public virtual void OnBeforeKB(Character character) { }
    public virtual void OnAfterKB(Character character) { }
    public virtual void OnDead(Character character) { }
    public void SetPassiveValues(object n, int p, int d, int i) { name = n; probability = p; duration = d; intensity = i; }
    public bool Triggered() { return UnityEngine.Random.Range(0, 100) < probability; }
    protected virtual void SummonPassiveEffect() { }
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
public class Practician : PassiveSkill
{
    private float atk = 1;
    private int stack = 0;
    private static int maxStack = 6;
    private static float atkMuitipiler = 0.2f;
    public override void OnAfterAttack(Character character, float dmg, List<CharacterEffect> ces, List<AttackType> types)
    {
        if (stack > maxStack) return;
        stack++;
        Debug.Log($"Stack = {stack}");
        atk += Mathf.Abs(dmg);
    }
    public override void OnFinishAttack(Character character)
    {
        if (stack >= maxStack) SpecialAttack(character,false);
    }
    public override void OnAfterKB(Character character)
    {
        SpecialAttack(character,true);
    }
    private void SpecialAttack(Character c, bool extend)
    {
        c.SwitchAnimation(4);
        c.BlockAnimationSwitch = true;
        c.StartCoroutine(SpecialProcess(c,extend));
    }
    private IEnumerator SpecialProcess(Character c, bool extend)
    {
        int t = 0;
        c.Supporter_Target_Switch(true);
        c.SetAttackRange(0, extend?Mathf.Abs(intensity):c.DetectionRange);
        while (t < duration && !c.IsOnKB())
        {
            t++;
            if (t == probability)
            {
                c.Attack(atk * atkMuitipiler, true, intensity<0);
            }
            yield return new WaitForFixedUpdate();
        }
        c.BlockAnimationSwitch = false;
        c.ExitAttack();
        c.Supporter_Target_Switch(true);
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
    //ATK Type+
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
            int surge_distance = UnityEngine.Random.Range(0, intensity);
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
            int surge_distance = UnityEngine.Random.Range(0, intensity);
            character.Surge_Attack(duration, true, surge_distance, dmg, ces, types);
        }
    }
}
public class WaveStop : PassiveSkill
{
    public override void OnBeforeTakeDamage(Character character, ref float DMG, List<AttackType> atkTypes)
    {
        bool wave = false;
        foreach (var at in atkTypes) { if (at == AttackType.wave) wave = true; break; }
        if (!wave) return;
        else
        {
            //?????????????????
        }
    }
}
public class MaxShield : PassiveSkill
{
    private float damageLimit=int.MaxValue;
    public override void OnDeployUnit(Character character)
    {
        damageLimit = character.GetMaxHealth() * probability / 100f;
    }
    public override void OnBeforeTakeDamage(Character character, ref float DMG, List<AttackType> atkTypes)
    {
        if (DMG > damageLimit) DMG = damageLimit;
    }
}
public class ATK_Buffer : PassiveSkill
{
    public override void OnAfterAttack(Character character, float dmg, List<CharacterEffect> ces, List<AttackType> types)
    {
        Debug.Log($"Buff count: {character.Targets.Count}");
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
    public override void OnAttacking(Character character, ref float dmg, ref List<AttackType> types)
    {
        if (character == null) return;

        int step = character.GetAnimationStep();
        if (character.atkInfos == null || step < 0 || step >= character.atkInfos.Length) return;
        ATKInfo atk = character.atkInfos[step];
        bool triggerEffect = !atk.DoNotTriggerEffects;

        GameObject prefab = Resources.Load<GameObject>($"Units/Projectiles/p{probability.ToString("000")}/projunit");
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
            effectPayload = character.characterEffects
                .Select(e => JsonUtility.FromJson<CharacterEffect>(JsonUtility.ToJson(e)))
                .ToList();
            for (int i = 0; i < effectPayload.Count; i++) effectPayload[i].probability = 100;
        }
        float atkDamage = dmg;
        pu.BeginProjectileAttack(character, atkDamage, Mathf.Max(1, duration), effectPayload, types, triggerEffect);

        // Block this attack's native hit while keeping attack animation/state flow intact.
        character.RemoveAllTarget();
    }
}

public class ZombieDiveAddon : PassiveSkill
{
    private int remainingDiveTimes;
    private bool initialized;
    private bool diving;

    public override void OnAddingAbility(Character character)
    {
        if (initialized) return;
        initialized = true;
        remainingDiveTimes = probability;
    }

    public override void OnStartAttack(Character character)
    {
        if (character == null || diving) return;
        if (remainingDiveTimes == 0) return;
        if (CanAttackBaseNow(character)) return;

        if (remainingDiveTimes > 0) remainingDiveTimes--;
        character.RequestCancelAttackStart();
        character.StartCoroutine(DiveRoutine(character));
    }

    public override void OnAfterKB(Character character)
    {
        if (remainingDiveTimes == 0)
        {
            CharacterTargetManager.Instance.SetCharacterUndetectable(character, false);
            character.RemovePassiveEffect(this);
        }
    }

    private IEnumerator DiveRoutine(Character character)
    {
        if (character == null) yield break;
        diving = true;
        // In
        int speedBeforeIn = ResolveTransitExitSpeed(character);
        character.SwitchAnimation(4);
        character.BlockAnimationSwitch = true;
        character.ChangeSpeed(0);

        int transitionFrames = 30;

        int t = 0;
        while (t < transitionFrames && !CanAttackBaseNow(character))
        {
            t += character.GetFrameStep();
            if (character == null) yield break;
            yield return new WaitForFixedUpdate();
        }
        // Diving
        character.ChangeSpeed(speedBeforeIn);
        CharacterTargetManager.Instance.SetCharacterUndetectable(character, true);
        int moveDir = character.IsCat() ? -1 : 1;
        int moveFrames = Mathf.Max(0, duration);
        character.BlockAnimationSwitch = false;
        character.SwitchAnimation(5);
        character.BlockAnimationSwitch = true;
        for (int i = 0; i < moveFrames; i++)
        {
            if (character == null) yield break;
            if (CanAttackBaseNow(character)) break;
            int currentSpeed = Mathf.Max(0, character.GetRealSpeed());
            character.transform.Translate(new Vector2(character.TBCspeedTranslator(currentSpeed) * moveDir * Time.deltaTime, 0));
            yield return new WaitForFixedUpdate();
        }
        // Out
        int speedBeforeOut = ResolveTransitExitSpeed(character);
        CharacterTargetManager.Instance.SetCharacterUndetectable(character, false);
        character.ChangeSpeed(0);
        character.BlockAnimationSwitch = false;
        character.SwitchAnimation(6);
        character.BlockAnimationSwitch = true;
        t = 0;
        while (t < transitionFrames)
        {
            t += character.GetFrameStep();
            if (character == null) yield break;
            yield return new WaitForFixedUpdate();
        }

        character.BlockAnimationSwitch = false;
        character.SwitchAnimation(0);
        character.ChangeSpeed(ResolveTransitExitSpeed(character, speedBeforeOut));
        diving = false;

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
