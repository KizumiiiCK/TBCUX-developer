using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Xml.Linq;
using UnityEngine;

public class WaveUnit : Character
{
    protected override TargetRegistrationKind RegistrationKind => TargetRegistrationKind.Projectile;
    private readonly HashSet<Character> attackedTargets = new HashSet<Character>();

    public int wave_level = 1;
    bool Mini=false;
    private GameObject W;
    private AnimDecryptPack adp;
    private GameObject currentWave;

    public void BeginWaveAttack(int level, bool mini, float DMG, Traits _traits, SubTraits _subtraits, AgainstCareer opponentCE, DamageRelatedEffect dre, List<CharacterEffect> enemyEffect, List<AttackType> atkTypes)
    {
        CharacterTargetManager.Instance.RegisterProjectile(this);
        attackedTargets.Clear();
        wave_level = level;
        Mini = mini;
        float scaledDamage = Mini ? DMG * 0.2f : DMG;
        realDamage = new int[1] { Mathf.Max(0, Mathf.RoundToInt(scaledDamage)) };
        traits = _traits;
        subtraits = _subtraits;
        againstCareer = opponentCE;
        DRE = dre;
        //
        characterEffects = enemyEffect.Select(e => JsonUtility.FromJson<CharacterEffect>(JsonUtility.ToJson(e))).ToArray();
        foreach (var ef in characterEffects) ef.probability = 100;
        ATKTypes = new List<AttackType> { AttackType.wave };
        for (int i = 0; i < atkTypes.Count; i++) ATKTypes.Add(atkTypes[i]);
        InitializeWave();
    }

    public override float GetFactor() { return 1; }
    //public override void ReceiveAttack(float DMG, Traits enemyTraits, SubTraits opponentSubtraits, AgainstCareer opponentCE, DamageRelatedEffect dre, List<CharacterEffect> enemyEffect, List<AttackType> atkTypes) { }
    public override void InitializeCharacter() { StartCoroutine(SummonWave()); }
    public override void SetAttackRange(float near, float far)
    {
        CharacterTargetManager.Instance.SetCharacterAttackRange(this, near, far);
    }
    public override void UpdateAnimation() { }
    private void InitializeWave()
    {
        string e = Mini ? "miniwave" : "wave";
        e += IsCat() ? string.Empty : "_e";
        //W = Resources.Load<GameObject>($"Effects/{e}");
        //adp = IsCat() ? EM.GetCatWave() : EM.GetEnemyWave();
    }
    private IEnumerator SummonWave()
    {
        Vector3 basePos = transform.position;
        if (basePos.y < -900) basePos = basePos + new Vector3(0, 1000, 0);
        int dis = 3;
        int sign=IsCat() ? -1 : 1;
        int delayer = Mini ? 2 : 5;
        for(int i = 0; i < wave_level; i++)
        {
            //AnimationDisplayer adw=Instantiate(W, transform.position + new Vector3((dis + i * 2) * sign,0,0), Quaternion.identity).GetComponent<AnimationDisplayer>();
            float offsetX = (dis + i * 2) * sign;
            float waveX = basePos.x + offsetX;
            transform.position = new Vector3(waveX, basePos.y, basePos.z);
            SEnums waveEffect = IsCat()
                ? (Mini ? SEnums.miniwave : SEnums.wave)
                : (Mini ? SEnums.miniwave_e : SEnums.wave_e);
            currentWave = EM.InstantiateBattleObject(waveEffect, waveX, basePos.y).gameObject;

            // 单帧判定：范围(-100,100)，立刻攻击一次
            int minRange = -300;
            int maxRange = 100;
            SetAttackRange(minRange, maxRange);
            if (ApplyProjectileAttack(1f)) yield break;
            for (int j=0; j<delayer;j++) yield return new WaitForFixedUpdate();
        }
        Destroy(gameObject);
    }
    public override void ReceiveAttack(float DMG, Traits enemyTraits, SubTraits opponentSubtraits, AgainstCareer opponentAC, DamageRelatedEffect dre, List<CharacterEffect> enemyEffect, List<AttackType> atkTypes)
    {

    }

    /// <returns>true if wave was stopped and this unit was destroyed</returns>
    private bool ApplyProjectileAttack(float damageScale)
    {
        CharacterTargetManager.Instance.RefreshTargetsForProjectile(this);

        // 群体伤害前：区间内若有 WaveStop 对手，直接移除自身，避免同框友军仍被打到。
        for (int i = 0; i < Targets.Count; i++)
        {
            if (Targets[i] == null) continue;
            Character probe = Targets[i].GetComponent<Character>();
            if (probe == null || probe.GetHealth() <= 0) continue;
            if (!probe.HasAbility(AbilityName.wave_stop)) continue;
            EM?.InstantiateBattleObject(SEnums.wave_stop, probe.transform.position.x, probe.transform.position.y);
            Destroy(currentWave);
            Destroy(gameObject);
            return true;
        }

        float dmg = realDamage[0] * damageScale;
        for (int i = Targets.Count - 1; i >= 0; i--)
        {
            if (Targets[i] == null) continue;
            Character target = Targets[i].GetComponent<Character>();
            if (target == null) continue;
            if (attackedTargets.Contains(target)) continue;
            if (target.GetHealth() <= 0) continue;
            target.ReceiveAttack(dmg, traits, subtraits, againstCareer, DRE, characterEffects.ToList(), ATKTypes);
            attackedTargets.Add(target);
        }
        return false;
    }

    protected override void OnDestroy() => base.OnDestroy();
}
