using System.Collections;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

public class CannonUnit : Character
{
    protected override TargetRegistrationKind RegistrationKind => TargetRegistrationKind.Projectile;
    private readonly HashSet<Character> attackedTargets = new HashSet<Character>();

    // Including NULL steps of a cannon effect animation
    [System.Serializable]
    public class CannonAttackPattern
    {
        public int eff_flow_num;
        public int eff_flow_time;
        public Vector2 eff_flow_pos;
        public Vector2 eff_dmg_range;
    }

    [Header("Cannon Attack Patterns")]
    public int cannon_type = 0;
    [SerializeField] private int max_times=-1;
    [SerializeField] private float damage_multiplier=1f;
    // [SerializeField] private bool DO_NOT_summon_while_no_target = false;
    [SerializeField] private CannonAttackPattern[] cannon_patterns;
    
    //
    private GameObject[] cannon_effects;
    private CatBase catBase;

    public override void InitializeCharacter()
    {
        attackedTargets.Clear();
        catBase = GameObject.Find("CatBase").GetComponent<CatBase>();
        transform.position = catBase.transform.position;
        realDamage = new int[1];
        realDamage[0] = (int)(catBase.Health * 0.005f);
        //ATKTypes = new List<AttackType> { AttackType.baseCannon };
        cannon_effects = Resources.LoadAll<GameObject>($"Units/CatBases/effectUnits/{cannon_type}/eff");
        StartCoroutine(SummonCannon());
    }

    public override void UpdateAnimation() { }
    public override float GetFactor() { return 1f; }
    public override void ReceiveAttack(float DMG, Traits enemyTraits, SubTraits opponentSubtraits, AgainstCareer opponentCE, DamageRelatedEffect dre, List<CharacterEffect> enemyEffect, List<AttackType> atkTypes) { }

    public override void SetAttackRange(float near, float far)
    {
        CharacterTargetManager.Instance.SetCharacterAttackRange(this, near, far);
    }

    private IEnumerator SummonCannon()
    {
        int count = cannon_patterns.Length;
        if (count == 0)
        {
            Destroy(gameObject);
            yield break;
        }
        for (int i = 0; i < count; i++)
        {
            CannonAttackPattern pattern = GetPatternStep(i);
            int effIndex = pattern.eff_flow_num;
            if (cannon_effects != null && effIndex >= 0 && effIndex < cannon_effects.Length)
            {
                Instantiate(cannon_effects[effIndex], transform.position 
                    + new Vector3(-pattern.eff_flow_pos.x/100f, pattern.eff_flow_pos.y / 100f, 0), Quaternion.identity);
            }

            Vector2 range = pattern.eff_dmg_range;
            SetAttackRange(range.x, range.y);
            ApplyCannonAttack();
            int waitFrames = Mathf.Max(0, pattern.eff_flow_time);
            for (int f = 0; f < waitFrames; f++)
            {
                yield return new WaitForFixedUpdate();
            }
        }
    }

    private CannonAttackPattern GetPatternStep(int step)
    {
        if (cannon_patterns == null || cannon_patterns.Length == 0) return null;
        return cannon_patterns[step];
    }

    private void ApplyCannonAttack()
    {
        CharacterTargetManager.Instance.RefreshTargetsForProjectile(this);

        float dmg = realDamage[0] * damage_multiplier;
        int hitCount = 0;

        for (int i = Targets.Count - 1; i >= 0; i--)
        {
            if (Targets[i] == null) continue;
            Character target = Targets[i].GetComponent<Character>();
            if (target == null) continue;
            if (attackedTargets.Contains(target)) continue;
            float finalDamage = dmg;
            if (cannon_type == 4 && target.traits != null && target.traits.Mtl)
            {
                finalDamage = target.GetMaxHealth() * 0.7f;
            }
            if (cannon_type == 5 && target.traits != null && target.traits.Z)
            {
                finalDamage = target.GetMaxHealth() * 0.15f;
            }
            target.ReceiveAttack(finalDamage, traits, subtraits, againstCareer, DRE, characterEffects.ToList(), ATKTypes);
            attackedTargets.Add(target);
            hitCount++;
            if (cannon_type == 0) target.StartKBCoroutine(KB_Type.pushBack);
        }

        if (cannon_type == 5)
        {
            List<Character> undetectables = CharacterTargetManager.Instance.GetUndetectableUnits();
            for (int i = 0; i < undetectables.Count; i++)
            {
                Character target = undetectables[i];
                if (target == null) continue;
                if (attackedTargets.Contains(target)) continue;
                if (target.IsCat() == IsCat()) continue;
                if (target.traits == null || !target.traits.Z) continue;
                if (!CharacterTargetManager.Instance.IsTargetInCurrentRange(this, target)) continue;

                float damageToUndetectableZombie = target.GetMaxHealth() * 0.30f;
                target.ReceiveAttack(damageToUndetectableZombie, traits, subtraits, againstCareer, DRE, characterEffects.ToList(), ATKTypes);
                attackedTargets.Add(target);
                hitCount++;
            }
        }

        if (hitCount < 1) return;
        max_times--;
        if (max_times == 0)
        {
            Destroy(gameObject);
        }
    }
}
