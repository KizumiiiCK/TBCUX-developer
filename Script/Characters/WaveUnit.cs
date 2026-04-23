using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Xml.Linq;
using UnityEngine;

public class WaveUnit : Character
{
    protected override TargetRegistrationKind RegistrationKind => TargetRegistrationKind.Projectile;

    public int wave_level = 1;
    bool Mini=false;
    private GameObject W;
    private AnimDecryptPack adp;

    public void BeginWaveAttack(int level, bool mini, float DMG, Traits _traits, SubTraits _subtraits, AgainstCareer opponentCE, DamageRelatedEffect dre, List<CharacterEffect> enemyEffect, List<AttackType> atkTypes)
    {
        wave_level = level;
        Mini = mini;
        realDamage = new int[1] { (int)DMG };
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
        int dis = 1;
        int sign=IsCat() ? -1 : 1;
        int delayer = Mini ? 2 : 5;
        for(int i = 0; i < wave_level; i++)
        {
            //AnimationDisplayer adw=Instantiate(W, transform.position + new Vector3((dis + i * 2) * sign,0,0), Quaternion.identity).GetComponent<AnimationDisplayer>();
            float offsetX = (dis + i * 2) * sign;
            float waveX = basePos.x + offsetX;
            transform.position = new Vector3(waveX, basePos.y, basePos.z);
            EM.InstantiateBattleObject(IsCat() ? SEnums.wave : SEnums.wave_e, waveX, basePos.y);

            // 单帧判定：范围(-100,100)，立刻攻击一次
            SetAttackRange(-100, 100);
            ApplyProjectileAttack(1f);
            for (int j=0; j<delayer;j++) yield return new WaitForFixedUpdate();
        }
        Destroy(gameObject);
    }
    public override void ReceiveAttack(float DMG, Traits enemyTraits, SubTraits opponentSubtraits, AgainstCareer opponentAC, DamageRelatedEffect dre, List<CharacterEffect> enemyEffect, List<AttackType> atkTypes)
    {

    }

    private void ApplyProjectileAttack(float damageScale)
    {
        CharacterTargetManager.Instance.RefreshTargetsForProjectile(this);

        float dmg = realDamage[0] * damageScale;
        var effects = characterEffects != null ? new List<CharacterEffect>(characterEffects) : null;

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
