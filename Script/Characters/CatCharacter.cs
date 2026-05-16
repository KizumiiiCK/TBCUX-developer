using System.Collections;
using System.Collections.Generic;
using Unity.VisualScripting;
using UnityEngine;

public class CatCharacter : AnimatorCachedCharacter
{
    public override void InitializeCharacter()
    {
        SetPower(1);
        maxHealth = Health * (0.8f + 0.2f * level)*Power;
        realHealth = maxHealth;
        hardness = maxHealth / KB;
        //realDamage = new int[atkInfos.Length];
        for (int i = 0; i < atkInfos.Length; i++)
        {
            realDamage[i] = (int)(realDamage[i] * (0.8f + 0.2f * level) * Power);
        }
        realSpeed = Speed;
        realKBtimes = 0;
        CacheTopPositionY();
        Debug.Log($"Deployed {NameCode}, lvl:{level}, atk:{realDamage[0]}, hp:{maxHealth}");
    }
    public override void UpdateAnimation()
    {
        //0:idle;   1:atk;    2:kb;   3:wait.
        if (BlockAnimationSwitch) return;
        if (onATK)
        {
            animatedframes += frame_step;
            if (animatedframes == atkInfos[animateStep].frame) Attack(realDamage[animateStep], areaATK, atkInfos[animateStep].DoNotTriggerEffects);
            if (animatedframes == atkDuration) { Passive_OnFinishAttack(); ExitAttack(); }
            return;
        }
        if (onKB) return;
        if (Targets.Count > 0 || BaseTarget != null)
        {
            if (realReload >= Reload) {
                SwitchAnimation(2);
                Passive_OnStartAttack(); 
                if (ConsumeAttackStartCancelRequest()) return;
                onATK = true; 
                animateStep = 0;
                animatedframes = 0;
                CharacterTargetManager.Instance.NotifyCharacterStatePulse(this, EmotionBattleState.attack);
                if (atkInfos[0].Friendly) Supporter_Target_Switch();
                SetAttackRange(atkInfos[0].ATKRange.x, atkInfos[0].ATKRange.y); 
            }
            else SwitchAnimation(1);
        }
        else
        {
            SwitchAnimation(0);
            this.transform.Translate(new Vector2(TBCspeedTranslator(-realSpeed) * Time.deltaTime, 0));
        }
    }//Rectify the cat's movement and animation

    public override float GetFactor()
    {
        return skillfactor;
    }
    public override void DMG_DREeffects(ref float DMG, DamageRelatedEffect dre)
    {
        if (dre == null) return;
        if (DRE.tough) DMG = DMG / 4;
        if (DRE.aegis) DMG = DMG / 6;
        if (DRE.strongAgainst) DMG = DMG / 2;
    }
    public override void DMG_SubTraitsEffects(ref float DMG, SubTraits opponentSubtraits)
    {
        if (opponentSubtraits == null) return;
        if (subtraits.Starred && opponentSubtraits.Starred) DMG *= 0.75f;
        if (subtraits.Colossus && opponentSubtraits.Colossus) DMG *= 0.6f;
        if (subtraits.Behemoth && opponentSubtraits.Behemoth) DMG *= 0.7f;
    }
    public override void SetAttackRange(float near, float far)
    {
        // 更新统一管理器中的攻击范围
        CharacterTargetManager.Instance.SetCharacterAttackRange(this, near, far);
    }
    protected override void OnDestroy()
    {
        base.OnDestroy();
        LevelController lc = GameObject.Find("Level Initializer").GetComponent<LevelController>();
        if (lc == null) { Debug.LogError("LC not found."); return; }
        //if (gameObject.CompareTag("Cat")) lc.RemoveACat();
        //else lc.RemoveAnEnemy();
        lc.RemoveACat();
    }
    public override void ReceiveAttack(float DMG, Traits enemyTraits, SubTraits opponentSubtraits, AgainstCareer opponentAC, DamageRelatedEffect dre, List<CharacterEffect> enemyEffect, List<AttackType> atkTypes)
    {
        if(onKB) return;
        Passive_OnBeforeTakeDamage(ref DMG, atkTypes);
        if(atkTypes!=null)foreach (var ar in atkTypeResis)
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
        bool matchedTraits = AreCorrespondingTraits(enemyTraits, atkTypes);
        if (matchedTraits) DMG_DREeffects(ref DMG, dre);
        DMG_SubTraitsEffects(ref DMG, opponentSubtraits);
        DMG_CarrerEffects(ref DMG, opponentAC);
        TakeDMG(DMG);
        if(DMG>0)TakeEffects(enemyEffect, subtraits.Sage, atkTypes);
        HitEffect(atkTypes);
        Passive_OnAfterTakeDamage();
    }
}
