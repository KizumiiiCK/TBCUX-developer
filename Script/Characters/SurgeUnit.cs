using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Xml.Linq;
using UnityEngine;

public class SurgeUnit : Character
{
    protected override TargetRegistrationKind RegistrationKind => TargetRegistrationKind.Projectile;

    public int surge_level = 1;
    public float distance = 1;
    bool Mini=false;
    private GameObject W;
    private AnimDecryptPack adp;

    public void BeginSurgeAttack(int level, bool mini, int dis, float DMG, Traits _traits, SubTraits _subtraits, AgainstCareer opponentCE, DamageRelatedEffect dre, List<CharacterEffect> enemyEffect, List<AttackType> atkTypes)
    {
        surge_level = level;
        Mini = mini;
        distance = dis / 100f;
        realDamage = new int[1] { (int)DMG };
        traits = _traits;
        subtraits = _subtraits;
        againstCareer = opponentCE;
        DRE = dre;
        //
        characterEffects = enemyEffect.Select(e => JsonUtility.FromJson<CharacterEffect>(JsonUtility.ToJson(e))).ToArray();
        foreach (var ef in characterEffects) ef.probability = 100;
        ATKTypes = new List<AttackType> { AttackType.surge };
        for (int i = 0; i < atkTypes.Count; i++) ATKTypes.Add(atkTypes[i]);
        InitializeSurge();
    }

    public override float GetFactor() { return 1; }
    public override void InitializeCharacter() { StartCoroutine(SummonSurge()); }
    public override void SetAttackRange(float near, float far)
    {
        CharacterTargetManager.Instance.SetCharacterAttackRange(this, near, far);
    }
    public override void UpdateAnimation() { }
    private void InitializeSurge()
    {
        string e = Mini ? "minisurge" : "surge";
        e += IsCat() ? string.Empty : "_e";
    }
    private IEnumerator SummonSurge()
    {
        //int dis = 1;
        int sign=IsCat() ? -1 : 1;
        Vector3 basePos = transform.position;
        float surgeX = basePos.x + distance * sign;
        SEnums surgeEffect = IsCat() ? SEnums.surge : SEnums.surge_e;
        AnimationDisplayer ads = EM.InstantiateBattleObject(surgeEffect, surgeX, basePos.y);

        // 固定判定范围(-150,150)
        SetAttackRange(-150, 150);
        transform.position = new Vector3(surgeX, basePos.y, basePos.z);

        for (int j = 0; j < 12; j++) yield return new WaitForFixedUpdate();
        for (int i = 0; i < surge_level; i++)
        {
            ads.SetMaanimPointer(1);
            for (int j = 1; j < 21; j++)
            {
                if (j % 10 == 0)
                {
                    // 每10帧判定一次，伤害为1/2
                    ApplyProjectileAttack(0.5f);
                }
                yield return new WaitForFixedUpdate();
            }
        }
        ads.SetMaanimPointer(2);
        for (int j = 0; j < 30; j++) yield return new WaitForFixedUpdate();
        if (ads != null) EM.RecycleBattleObject(ads, surgeEffect.ToString());
        Destroy(gameObject);
    }
    public override void ReceiveAttack(float DMG, Traits enemyTraits, SubTraits opponentSubtraits, AgainstCareer opponentAC, DamageRelatedEffect dre, List<CharacterEffect> enemyEffect, List<AttackType> atkTypes)
    {

    }

    private void ApplyProjectileAttack(float damageScale)
    {
        CharacterTargetManager.Instance.RefreshTargetsForProjectile(this);

        float dmg = realDamage[0] * damageScale;

        for (int i = Targets.Count - 1; i >= 0; i--)
        {
            if (Targets[i] == null) continue;
            Character target = Targets[i].GetComponent<Character>();
            if (target == null) continue;
            if (target.GetHealth() <= 0) continue;
            target.ReceiveAttack(dmg, traits, subtraits, againstCareer, DRE, characterEffects.ToList(), ATKTypes);
        }
    }

    protected override void OnDestroy() => base.OnDestroy();
}
