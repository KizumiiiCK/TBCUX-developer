using System.Collections.Generic;
using System.Linq;
using UnityEngine;

public class ProjectileUnit : AnimatorCachedCharacter
{
    protected override TargetRegistrationKind RegistrationKind => TargetRegistrationKind.Projectile;

    private bool configured;
    private int projectileSpeed = 10;
    private bool triggerEffectThisAttack;

    public void BeginProjectileAttack(
        Character source,
        float damage,
        int speed,
        List<CharacterEffect> effects,
        List<AttackType> attackTypes,
        bool notTriggerEffect)
    {
        if (source == null) return;

        configured = true;
        projectileSpeed = Mathf.Max(1, speed);
        triggerEffectThisAttack = notTriggerEffect;

        // IsCat() is CompareTag("Cat"); prefab tag must match source or RegisterProjectile picks wrong pool/base.
        gameObject.tag = source.IsCat() ? "Cat" : "Enemy";

        // Copy key battle properties from source.
        Health = 1;
        KB = 1;
        Speed = projectileSpeed;
        Reload = 0;
        Cost = 0;
        Cooldown = 0;

        traits = JsonUtility.FromJson<Traits>(JsonUtility.ToJson(source.traits));
        subtraits = JsonUtility.FromJson<SubTraits>(JsonUtility.ToJson(source.subtraits));
        career = JsonUtility.FromJson<Careers>(JsonUtility.ToJson(source.career));
        againstCareer = JsonUtility.FromJson<AgainstCareer>(JsonUtility.ToJson(source.againstCareer));
        DRE = JsonUtility.FromJson<DamageRelatedEffect>(JsonUtility.ToJson(source.DRE));

        ATKTypes = new List<AttackType>();
        if (attackTypes != null)
        {
            for (int i = 0; i < attackTypes.Count; i++) ATKTypes.Add(attackTypes[i]);
        }
        if (ATKTypes.Count == 0) ATKTypes.Add(AttackType.none);

        if (effects != null && triggerEffectThisAttack)
        {
            characterEffects = effects.Select(e => JsonUtility.FromJson<CharacterEffect>(JsonUtility.ToJson(e))).ToArray();
        }
        else
        {
            characterEffects = new CharacterEffect[0];
        }

        // Use one attack step from current hit.
        atkInfos[0] = new ATKInfo
        {
            ATK = damage,
            frame = 1,
            DoNotTriggerEffects = !triggerEffectThisAttack,
            DoNotTriggerAbilities = !triggerEffectThisAttack,
            Friendly = false,
        };
        realDamage = new int[1] { Mathf.Max(1, Mathf.RoundToInt(damage)) };

        characterAbilities = new CharacterAbility[]
        {
            new CharacterAbility { name = AbilityName.oneoff, probability = 100, duration = 0, intensity = 0 }
        };
    }

    public override void InitializeCharacter()
    {
        if (!configured)
        {
            // Fallback safety for malformed prefab invocation.
            configured = true;
            atkInfos = new ATKInfo[1] { new ATKInfo { ATK = 1, frame = 1, DoNotTriggerEffects = true, Friendly = false, ATKRange = new Vector2(-330, 15) } };
            realDamage = new int[1] { 1 };
            ATKTypes = new List<AttackType> { AttackType.none };
            characterEffects = new CharacterEffect[0];
        }

        maxHealth = Mathf.Max(1, Health);
        realHealth = maxHealth;
        hardness = 1;
        realSpeed = projectileSpeed;
        realReload = Reload;
        realKBtimes = 0;

        // OneOff ability preinstalled so projectile disappears right after its attack flow.
        AbilityInstaller.Install(this, new CharacterAbility { name = AbilityName.oneoff, probability = 100, duration = 0, intensity = 0 });
    }

    public override void UpdateAnimation()
    {
        if (externalAnimControl) return;
        if (onKB) return;

        if (onATK)
        {
            animatedframes += frame_step;
            if (ShouldResolveAttackFrame(atkInfos[animateStep].frame))
                Attack(realDamage[animateStep], areaATK, atkInfos[animateStep].DoNotTriggerEffects, atkInfos[animateStep].DoNotTriggerAbilities);
            if (animatedframes >= atkDuration)
            {
                Passive_OnFinishAttack();
                ExitAttack();
            }
            return;
        }

        // Projectile can attack either normal targets or enemy base target.
        if ((Targets.Count > 0 || BaseTarget != null) && realReload >= Reload)
        {
            SwitchAnimation(2);
            Passive_OnStartAttack();
            onATK = true;
            animateStep = 0;
            animatedframes = 0;
            ResetAttackResolveState();
            SetAttackRange(atkInfos[0].ATKRange.x, atkInfos[0].ATKRange.y);
            return;
        }

        // Move forward until hit or destroyed by out-of-bounds check.
        SwitchAnimation(0);
        int dir = IsCat() ? -1 : 1;
        transform.Translate(new Vector2(TBCspeedTranslator(realSpeed * dir) * Time.deltaTime, 0f));
        if (Mathf.Abs(transform.position.x) > 50) Destroy(gameObject);
    }

    public override float GetFactor() => 1f;

    public override void SetAttackRange(float near, float far)
    {
        CharacterTargetManager.Instance.SetCharacterAttackRange(this, near, far);
    }

    public override void ReceiveAttack(float DMG, Traits enemyTraits, SubTraits opponentSubtraits, AgainstCareer opponentAC, DamageRelatedEffect dre, List<CharacterEffect> enemyEffect, List<AttackType> atkTypes)
    {
        // Projectile units are not intended to take regular damage.
    }
    public override void Dead()
    {
        Destroy(gameObject);
    }
}
