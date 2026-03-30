using System.Collections.Generic;
using UnityEngine;

public class EnemyCharacter : AnimatorCachedCharacter
{
    public float strengthenRate = 1;
    public override void InitializeCharacter()
    {
        maxHealth = Health * strengthenRate * Power;
        realHealth = maxHealth;
        hardness = maxHealth / KB;
        realDamage = new int[atkInfos.Length];
        for (int i = 0; i < atkInfos.Length; i++)
        {
            realDamage[i] = (int)(atkInfos[i].ATK * strengthenRate *Power);
        }
        realSpeed = Speed;
        realKBtimes = 0;
        Debug.Log($"Enemy:{NameCode}, atk:{realDamage[0]}, hp:{maxHealth}");
    }
    public override void UpdateAnimation()
    {
        if (BlockAnimationSwitch) return;
        if (onATK)
        {
            animatedframes += frame_step;
            if (animatedframes == atkInfos[animateStep].frame) Attack(realDamage[animateStep], areaATK, atkInfos[animateStep].DoNotTriggerEffects);
            if (animatedframes == atkDuration) { Passive_OnFinishAttack(); ExitAttack(); }
            return;
        }
        if (onKB) return;
        if (Targets.Count > 0)
        {
            if (realReload >= Reload) {
                SwitchAnimation(2);
                Passive_OnStartAttack();
                onATK = true; 
                animateStep = 0;
                animatedframes = 0;
                if (atkInfos[0].Friendly) Supporter_Target_Switch();
                SetAttackRange(atkInfos[0].ATKRange.x, atkInfos[0].ATKRange.y); 
            }
            else SwitchAnimation(1);
        }
        else 
        {
            SwitchAnimation(0);
            this.transform.Translate(new Vector2(TBCspeedTranslator(realSpeed) * Time.deltaTime, 0));
        }
    }//Rectify the cat's movement and animation

    public override float GetFactor()
    {
        return 1;
    }

    public override void DMG_DREeffects(ref float DMG, DamageRelatedEffect dre) 
    {
        if (dre == null) return;
        if (dre.massiveDamage) DMG = DMG * 4;
        if (dre.insaneDamage) DMG = DMG * 6;
        if (dre.strongAgainst) DMG = DMG * 1.5f;
    }
    public override void DMG_SubTraitsEffects(ref float DMG, SubTraits opponentSubtraits) 
    {
        if (opponentSubtraits == null) return;
        if (subtraits.Starred && opponentSubtraits.Starred) DMG *= 1.2f;
        if (subtraits.Colossus && opponentSubtraits.Colossus) DMG *= 1.6f;
        if (subtraits.Behemoth && opponentSubtraits.Behemoth) DMG *= 2.5f;
    }
    //public override void GetStrategy() { SetAttackRange(0,DetectionRange); }
    public override void SetAttackRange(float near,float far)
    {
        // 更新统一管理器中的攻击范围
        CharacterTargetManager.Instance.SetCharacterAttackRange(this, near, far);
    }
    protected override void OnDestroy()
    {
        base.OnDestroy();
        LevelController lc = GameObject.Find("Level Initializer").GetComponent<LevelController>();
        if (lc == null) { Debug.LogError("LC not found."); return; }
        levelController.AddMoney(Cost);
        lc.RemoveAnEnemy();
    }
    public override void ReceiveAttack(float DMG, Traits enemyTraits, SubTraits opponentSubtraits, AgainstCareer opponentAC, DamageRelatedEffect dre, List<CharacterEffect> enemyEffect, List<AttackType> atkTypes)
    {
        if(onKB) return;
        if (onDodge) return;
        Passive_OnBeforeTakeDamage(ref DMG, atkTypes);
        if (atkTypes != null) foreach (var ar in atkTypeResis)
        {
            foreach (var at in atkTypes)
            {
                if (ar.type == at)
                {
                    if (ar.intensity > 99) { HitEffect(new List<AttackType> { AttackType.wave_invalid }); return; }
                    else { DMG *= (100 - ar.intensity) / 100f; }
                }
            }
        }
        bool matchedTraits = AreCorrespondingTraits(enemyTraits);
        if (matchedTraits) DMG_DREeffects(ref DMG, dre);
        DMG_SubTraitsEffects(ref DMG, opponentSubtraits);
        DMG_CarrerEffects(ref DMG, opponentAC);
        TakeDMG(DMG);
        if (matchedTraits) TakeEffects(enemyEffect, subtraits.Sage);
        HitEffect(atkTypes);
        Passive_OnAfterTakeDamage();
    }
}
