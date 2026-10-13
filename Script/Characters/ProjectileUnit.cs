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
        levelController = source.levelController;
        NameCode = source.NameCode;
        SetOppositeUnit(source.IsOppositeUnit);

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
        // 取 Effective 版本：发射者处于诅咒状态时，弹幕也不该带上它的职业克制和 DRE。
        // 诅咒解除后发射的弹幕会重新拿到完整值（这里是发射瞬间拷的快照）。
        againstCareer = JsonUtility.FromJson<AgainstCareer>(JsonUtility.ToJson(source.EffectiveAgainstCareer));
        DRE = JsonUtility.FromJson<DamageRelatedEffect>(JsonUtility.ToJson(source.EffectiveDre));

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

        // Keep the prefab hitbox. Rewriting ATKInfo without ATKRange zeros it to (0,0);
        // attack then SetAttackRange(0,0) and the target list is wiped before damage resolves.
        Vector2 hitRange = new Vector2(-200f, 100f);
        if (atkInfos != null && atkInfos.Length > 0 && atkInfos[0] != null)
        {
            hitRange = atkInfos[0].ATKRange;
            if (Mathf.Approximately(hitRange.x, 0f)) hitRange.x = -200f;
            if (Mathf.Approximately(hitRange.y, 0f)) hitRange.y = 100f;
        }
        if (atkInfos == null || atkInfos.Length == 0) atkInfos = new ATKInfo[1];
        atkInfos[0] = new ATKInfo
        {
            ATK = damage,
            frame = 1,
            DoNotTriggerEffects = !triggerEffectThisAttack,
            DoNotTriggerAbilities = !triggerEffectThisAttack,
            Friendly = false,
            ATKRange = hitRange,
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
            {
                CharacterTargetManager.Instance.RefreshTargetsForProjectile(this);
                if (TryReflectByMirror()) return;
                Attack(realDamage[animateStep], areaATK, atkInfos[animateStep].DoNotTriggerEffects, atkInfos[animateStep].DoNotTriggerAbilities);
            }
            if (animatedframes >= atkDuration)
            {
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

    /// <summary>
    /// 结算前若目标带 Mirror 且判定成功：本发不打出，就地改成镜子阵营并掉头。
    /// 必须 return 出 UpdateAnimation，不能走 ExitAttack，否则 oneoff 会把弹毁掉。
    /// </summary>
    private bool TryReflectByMirror()
    {
        if (Targets == null) return false;
        for (int i = 0; i < Targets.Count; i++)
        {
            GameObject go = Targets[i];
            if (go == null) continue;
            Character target = go.GetComponent<Character>();
            if (target == null || target == this) continue;
            Mirror mirror = target.GetPassive<Mirror>();
            if (mirror == null) continue;
            if (!mirror.RollReflect()) continue;
            ApplyMirrorReflect(target, mirror);
            return true;
        }
        return false;
    }

    private void ApplyMirrorReflect(Character mirrorOwner, Mirror mirror)
    {
        if (mirrorOwner == null) return;

        mirror.PlayReflect(mirrorOwner);

        CharacterTargetManager mgr = CharacterTargetManager.Instance;
        if (mgr != null) mgr.UnregisterProjectile(this);

        CharacterPlacer.FlipScaleX(gameObject);

        float incomingDamage = (realDamage != null && realDamage.Length > 0) ? realDamage[0] : 1f;
        float reflectedDamage = incomingDamage * Mathf.Max(0, mirror.DamagePercent) / 100f;
        List<CharacterEffect> effects = CloneEffects(mirrorOwner);
        List<AttackType> types = mirrorOwner.ATKTypes;
        BeginProjectileAttack(mirrorOwner, reflectedDamage, projectileSpeed, effects, types, effects != null && effects.Count > 0);
        realSpeed = projectileSpeed;

        AbortCurrentAttackForControl();
        RemoveAllTarget();
        SwitchAnimation(0);
        if (mgr != null) mgr.RegisterProjectile(this);
    }

    private static List<CharacterEffect> CloneEffects(Character source)
    {
        if (source == null || source.characterEffects == null || source.characterEffects.Length == 0)
            return null;
        List<CharacterEffect> list = new List<CharacterEffect>(source.characterEffects.Length);
        for (int i = 0; i < source.characterEffects.Length; i++)
        {
            CharacterEffect e = source.characterEffects[i];
            if (e == null) continue;
            list.Add(JsonUtility.FromJson<CharacterEffect>(JsonUtility.ToJson(e)));
        }
        return list.Count > 0 ? list : null;
    }

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
