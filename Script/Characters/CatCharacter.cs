using System.Collections;
using System.Collections.Generic;
using Unity.VisualScripting;
using UnityEngine;

public class CatCharacter : AnimatorCachedCharacter
{
    public override void InitializeCharacter()
    {
        float real_power=(0.8f + 0.2f * level)*Power;
        maxHealth = Health * real_power;
        realHealth = maxHealth;
        // KB 走 LoadCharacterData 时已归一到 ≥1；预制体里直接填的单位不经过那条路，
        // 所以这里再兜一层，避免 hardness 变成 Infinity。
        hardness = maxHealth / Mathf.Max(1, KB);
        //realDamage = new int[atkInfos.Length];
        for (int i = 0; i < atkInfos.Length; i++)
        {
            realDamage[i] = (int)(realDamage[i] * real_power);
        }
        realSpeed = Speed;
        realKBtimes = 0;
        CacheTopPositionY();
        Debug.Log($"Deployed {NameCode}, lvl:{level}, atk:{realDamage[0]}, hp:{maxHealth}");
    }
    public override void UpdateAnimation()
    {
        //0:idle;   1:atk;    2:kb;   3:wait.
        if (externalAnimControl) return;
        if (onATK)
        {
            animatedframes += frame_step;
            if (ShouldResolveAttackFrame(atkInfos[animateStep].frame))
            {
                Attack(
                    realDamage[animateStep],
                    areaATK,
                    atkInfos[animateStep].DoNotTriggerEffects,
                    doNotTriggerAbilities: atkInfos[animateStep].DoNotTriggerAbilities);
            }
            if (animatedframes == atkDuration)
            {
                ExitAttack();
            }
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
                ResetAttackResolveState();
                CharacterTargetManager.Instance.NotifyCharacterStatePulse(this, EmotionBattleState.attack);
                if (atkInfos[0].Friendly) Supporter_Target_Switch();
                SetAttackRange(atkInfos[0].ATKRange.x, atkInfos[0].ATKRange.y); 
            }
            else SwitchAnimation(1);
        }
        else
        {
            SwitchAnimation(0);
            transform.Translate(new Vector2(TBCspeedTranslator(-realSpeed) * Time.deltaTime, 0));
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
    private const int BehemothSlayerDodgeChance = 5;
    private const float BehemothSlayerDodgeSeconds = 2f;

    public override void DMG_SubTraitsEffects(ref float DMG, SubTraits opponentSubtraits)
    {
        if (opponentSubtraits == null) return;
        if (subtraits.Starred && opponentSubtraits.Starred) DMG *= 0.75f;
        if (subtraits.Colossus && opponentSubtraits.Colossus) DMG *= 0.6f;
        if (subtraits.Behemoth && opponentSubtraits.Behemoth)
        {
            DMG *= 0.4f;
            if (TryProcBehemothSlayerDodge()) DMG = 0f;
        }
    }

    private bool TryProcBehemothSlayerDodge()
    {
        if (UnityEngine.Random.Range(0, 100) >= BehemothSlayerDodgeChance) return false;
        DodgePassive dodge = GetPassive<DodgePassive>();
        if (dodge == null)
        {
            AbilityInstaller.Install(this, new CharacterAbility
            {
                name = AbilityName.dodge,
                probability = 0,
                duration = 0,
                intensity = 0
            });
            dodge = GetPassive<DodgePassive>();
        }
        if (dodge == null) return false;
        int frames = Mathf.Max(1, Mathf.RoundToInt(BehemothSlayerDodgeSeconds / Time.fixedDeltaTime));
        dodge.ActivateInvulnerableWindow(this, frames);
        return true;
    }
    public override void SetAttackRange(float near, float far)
    {
        // 更新统一管理器中的攻击范围
        CharacterTargetManager.Instance.SetCharacterAttackRange(this, near, far);
    }
    protected override void OnDestroy()
    {
        base.OnDestroy();
        if (SkipDestroyCombatAccounting) return;
        if (levelController == null) { Debug.LogError("LC not found."); return; }
        //if (gameObject.CompareTag("Cat")) lc.RemoveACat();
        //else lc.RemoveAnEnemy();
        levelController.RemoveACat();
    }
    public override void ReceiveAttack(float DMG, Traits enemyTraits, SubTraits opponentSubtraits, AgainstCareer opponentAC, DamageRelatedEffect dre, List<CharacterEffect> enemyEffect, List<AttackType> atkTypes)
    {
        if(onKB) return;
        bool matchedTraits = AreCorrespondingTraits(enemyTraits, atkTypes);
        SetIncomingTraitCorresponding(matchedTraits);
        if (matchedTraits) Passive_OnMatchedTraits(atkTypes);
        if(atkTypes!=null)foreach (var ar in atkTypeResis)
        {
            foreach (var at in atkTypes)
            {
                if (ar.type == at)
                {
                    if (ar.intensity > 99) { HitEffect(WaveInvalidHitTypes); return; }
                    else { DMG *= (100 - ar.intensity) / 100f; }
                }
            }
        }
        if (matchedTraits) DMG_DREeffects(ref DMG, dre);
        DMG_SubTraitsEffects(ref DMG, opponentSubtraits);
        DMG_CarrerEffects(ref DMG, opponentAC);
        Passive_OnBeforeTakeDamage(ref DMG, atkTypes);
        // 效果先挂、伤害后落。毒伤是 Toxic 组件挂载时递归调 ReceiveAttack 打出来的，
        // 而 TakeDMG 可能就地把自己打进 KB（PerformKB 里的 onKB=true 是同步生效的），
        // 那之后再进来的毒伤会被本方法开头的 onKB 早退整份吞掉。
        // 挪到 TakeDMG 之前即可，且与 DMG_CarrerEffects 里 knockback 效果的时序一致。
        // 注意不能再往前挪：DodgePassive 要在 Passive_OnBeforeTakeDamage 里读
        // incomingTraitCorresponding，而嵌套进来的毒伤会把这个标志覆写成 false。
        if(DMG>0)TakeEffects(enemyEffect, subtraits.Sage, atkTypes);
        TakeDMG(DMG);
        HitEffect(atkTypes);
        Passive_OnAfterTakeDamage();
    }
}
