using System.Collections;
using System.Collections.Generic;
using UnityEngine;

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
