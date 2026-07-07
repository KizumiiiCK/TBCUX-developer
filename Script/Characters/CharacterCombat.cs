using System.Collections;
using System.Collections.Generic;
using System.Linq;
using Unity.VisualScripting;
using UnityEngine;

public abstract partial class Character
{
    private const float CharacterTargetVolumeLength = 200;
    private bool incomingTraitCorresponding;
    protected string compareTagName = "";
    [SerializeField] public List<GameObject> Targets = new List<GameObject>();
    [SerializeField] public GameObject BaseTarget;
    private readonly List<Character> lastAttackHitTargets = new List<Character>(8);
    private KB_Type lastTriggeredKBType = KB_Type.none;

    /* ====== ����ս���ӿ� ====== */
    public void SetSearchTagName(string name) { compareTagName = name; }
    public bool IsCat() => gameObject.CompareTag("Cat");

    /* ====== �ڲ�ʵ�� ====== */
    private T GetTarget<T>(GameObject go) where T : Character
    {
        if (go == null) return null;
        return go.CompareTag("Cat") ? go.GetComponent<CatCharacter>() as T
                                     : go.GetComponent<EnemyCharacter>() as T;
    }

    private List<CharacterEffect> DetermineSelfATKEffects()
    {
        var list = new List<CharacterEffect>();
        float sf = GetFactor();
        foreach (var ce in characterEffects)
        {
            if (Random.Range(0, 100) < ce.probability * sf)
                list.Add(ce);
        }
        return list;
    }
    private bool CanHitTargetNow(Character target)
    {
        if (target == null || target.gameObject == null || !target.gameObject.activeInHierarchy) return false;
        return !CharacterTargetManager.Instance.IsCharacterUndetectable(target) || CanTargetUndetectable();
    }

    #region Attack Functions
    public void Attack(float dmg, bool areaAttack, bool doNotTrigger, GameObject specific = null, bool doNotTriggerAbilities = false)
    {
        Character GetTarget<T>(GameObject go) where T : Character
        {
            if (go == null) return null;
            if (go.CompareTag("Cat"))
                return go.GetComponent<CatCharacter>() as T;
            else
                return go.GetComponent<EnemyCharacter>() as T;
        }
        lastAttackHitTargets.Clear();
        Targets.RemoveAll(go => go == null);
        GameObject baseTarget = GetValidBaseTarget();
        if (Targets.Count != 0 || baseTarget != null || specific != null)
        {
            List<CharacterEffect> decisionEff = new List<CharacterEffect>();
            List<AttackType> types = new List<AttackType>();
            foreach (var at in ATKTypes) types.Add(at);
            //float dmg = realDamage[animateStep] * ATK_muiltipier;
            dmg *= ATK_muiltipier;
            if (!doNotTriggerAbilities)
            {
                Passive_OnAttacking(ref dmg, ref types);
            }
            if (specific != null)
            {
                decisionEff = DetermineSelfATKEffects();
                Character Target = GetTarget<Character>(specific);
                if (CanHitTargetNow(Target))
                {
                    if (Target != null && Target.IsCat() == IsCat())
                        types.Add(AttackType.friendly);
                    Target?.ReceiveAttack(dmg, traits, subtraits, againstCareer, DRE, decisionEff, types);
                    TryRecordHitTarget(Target);
                    if (IsCat()) levelController.RecordProficency_DamageDealt(NameCode, (int)dmg);
                }
            }
            else
            {
                //if (!onCurse && !atkInfos[animateStep].DoNotTriggerEffects)
                if (!onCurse && !doNotTrigger)
                {
                    decisionEff = DetermineSelfATKEffects();
                }

                if (!areaAttack)
                {
                    Character Target = GetTarget<Character>(FindNearest());
                    if (CanHitTargetNow(Target))
                    {
                        if (Target != null && Target.IsCat() == IsCat())
                            types.Add(AttackType.friendly);
                        Target?.ReceiveAttack(dmg, traits, subtraits, againstCareer, DRE, decisionEff, types);
                        TryRecordHitTarget(Target);
                        if (IsCat()) levelController.RecordProficency_DamageDealt(NameCode, (int)dmg);
                    }
                }
                else
                {
                    int allTargetCount = Targets.Count + (baseTarget != null && !Targets.Contains(baseTarget) ? 1 : 0);
                    if (IsCat()) levelController.RecordProficency_DamageDealt(NameCode, (int)dmg * allTargetCount);//simple count for area atk
                    for (int i = Targets.Count - 1; i >= 0; i--)
                    {
                        Character ec = GetTarget<Character>(Targets[i]);
                        if (!CanHitTargetNow(ec)) continue;
                        if (ec != null && ec.IsCat() == IsCat())
                            types.Add(AttackType.friendly);
                        ec?.ReceiveAttack(dmg, traits, subtraits, againstCareer, DRE, decisionEff, types);
                        TryRecordHitTarget(ec);
                    }
                    if (baseTarget != null && !Targets.Contains(baseTarget))
                    {
                        Character bt = GetTarget<Character>(baseTarget);
                        if (CanHitTargetNow(bt))
                        {
                            if (bt != null && bt.IsCat() == IsCat())
                                types.Add(AttackType.friendly);
                            bt?.ReceiveAttack(dmg, traits, subtraits, againstCareer, DRE, decisionEff, types);
                            TryRecordHitTarget(bt);
                        }
                    }
                }
                //if (!atkInfos[animateStep].DoNotTriggerEffects)
                if (!doNotTrigger && !doNotTriggerAbilities)
                {
                    Passive_OnAfterAttack(dmg, decisionEff, types);
                }
            }
        }

        animateStep++;
        if (animateStep == realDamage.Length)
        {
            FinishAttack();
        }
        else
        {
            if (atkInfos[animateStep].Friendly) Supporter_Target_Switch();
            else Supporter_Target_Switch(true);
            SetAttackRange(atkInfos[animateStep].ATKRange.x, atkInfos[animateStep].ATKRange.y);
        }
    }
    protected void FinishAttack()
    {
        realReload = 0; 
        animateStep = 0;
        Supporter_Target_Switch(true); // 重置Friendly模式
        SetAttackRange(-CharacterTargetVolumeLength, DetectionRange); // 重置攻击范围为检测范围
        for(int i = Targets.Count - 1; i >= 0; i--)
        {
            if (Targets[i]==null) Targets.RemoveAt(i);
        }
    }
    public void ExitAttack()
    {
        if (one_off) { Destroy(gameObject); }
        onATK = false;
        animateStep = 0;
        animatedframes = 0;
        //if(animator!=null)animator.SetInteger("state", 0);
        //SetAttackRange(0, DetectionRange);
        SwitchAnimation(0);
    }

    public void AbortCurrentAttackForControl()
    {
        if (!onATK) return;
        onATK = false;
        animateStep = 0;
        animatedframes = 0;
        realReload = 0;
        Supporter_Target_Switch(true);
        SetAttackRange(-CharacterTargetVolumeLength, DetectionRange);
    }
    protected bool AreCorrespondingTraits(Traits targetTrait, List<AttackType> atkTypes = null)
    {
        if (atkTypes != null && atkTypes.Contains(AttackType.friendly)) return true;
        if (targetTrait == null) return false;
        return (targetTrait.Red && traits.Red) ||
               (targetTrait.Flt && traits.Flt) ||
               (targetTrait.Blk && traits.Blk) ||
               (targetTrait.Mtl && traits.Mtl) ||
               (targetTrait.Ang && traits.Ang) ||
               (targetTrait.Aln && traits.Aln) ||
               (targetTrait.Z && traits.Z) ||
               (targetTrait.Re && traits.Re) ||
               (targetTrait.Aku && traits.Aku) ||
               (targetTrait.None && traits.None);
    }
    protected void SetIncomingTraitCorresponding(bool matched) => incomingTraitCorresponding = matched;
    public bool HasIncomingTraitCorresponding() => incomingTraitCorresponding;
    public void SetNewTarget(GameObject newTarget, bool quickTrigger) { if (quickTrigger) Attack(realDamage[animateStep],false,false,newTarget); else Targets.Add(newTarget); }
    public void RemoveTarget(GameObject newTarget) { Targets.Remove(newTarget); }
    public void RemoveAllTarget() { Targets = new List<GameObject>(); BaseTarget = null; }
    public GameObject FindNearest()
    {
        GameObject baseTarget = GetValidBaseTarget();
        if (Targets.Count == 0 && baseTarget == null) return null;
        //bool positive = compareTagName == "Enemy";
        GameObject target = Targets.Count > 0 ? Targets[0] : baseTarget;
        //int mindis = int.MaxValue;
        foreach (var go in Targets)
        {
            if (target.transform.position.x < go.transform.position.x == IsCat()) target = go;
        }
        if (baseTarget != null && baseTarget != target)
        {
            if (target.transform.position.x < baseTarget.transform.position.x == IsCat()) target = baseTarget;
        }
        return target;
    }//Find the min-distance inside the targets list
    public void SetATKmuiltipier(float ratio) { ATK_muiltipier = ratio; }
    public void SetMAXmuiltipier(float ratio) { MAX_muiltipier += ratio-1; }
    public void SetMuiltipierToMAX() { ATK_muiltipier = MAX_muiltipier; }

    public void Supporter_Target_Switch(bool switchback = false)
    {
        // 切换Friendly攻击模式（攻击同阵营）
        // switchback=false: 切换到Friendly模式（攻击同阵营）
        // switchback=true: 切换回正常模式（攻击敌对阵营）
        CharacterTargetManager.Instance.SetCharacterFriendlyMode(this, !switchback);
        
        // 清空当前目标列表，让管理器重新计算
        RemoveAllTarget();
    }
    #endregion

    #region Under Attack Functions
    protected void TakeEffects(List<CharacterEffect> enemyEffect, bool sageBuff = false, List<AttackType> atkTypes = null)
    {
        if (enemyEffect == null) return;
        bool friendlyAttack = atkTypes != null && atkTypes.Contains(AttackType.friendly);
        float sagemultiplier = (subtraits.Sage && !sageBuff) ? 0.3f : 1;
        foreach (var ee in enemyEffect)
        {
            int resisted = 0;
            foreach (var myer in effectResistances)
                {
                    if (ee.name == myer.name)
                    {
                        resisted = myer.probability;
                        break;
                    }
                }
            float real_duration = ee.duration * sagemultiplier * CounterT(resisted);
            EffectInstaller.Inflict(gameObject, ee.name, real_duration, ee.intensity);
        }
    }
    public abstract void ReceiveAttack(float DMG, Traits enemyTraits, SubTraits opponentSubtraits, AgainstCareer opponentCE, DamageRelatedEffect dre, List<CharacterEffect> enemyEffect, List<AttackType> atkTypes);
    protected int duration_agnCarrer_stack = 0;
    protected void DMG_CarrerEffects(ref float DMG, AgainstCareer opponentAC)
    {
        if (opponentAC == null) return;
        List<EffectName> matchedEffect = new List<EffectName>();
        int duration_stack = 0;
        if (career.Warrior && opponentAC.AggainstWarrior) { duration_stack += 20; matchedEffect.Add(EffectName.weaken); }
        if (career.Deffender && opponentAC.AggainstDeffender) { duration_stack += 20; matchedEffect.Add(EffectName.stop); }
        if (career.Magician && opponentAC.AggainstMagician) { duration_stack += 20; matchedEffect.Add(EffectName.slow); }
        if (career.Supporter && opponentAC.AggainstSupporter) { duration_stack += 40; matchedEffect.Add(EffectName.deathmark); EffectInstaller.Inflict(gameObject, EffectName.knockback, 1, 1); }
        if (career.Warrior && opponentAC.AggainstWarrior) { duration_stack += 40; matchedEffect.Add(EffectName.lacerate); matchedEffect.Add(EffectName.slow); }

        if (duration_stack > 0)
        {
            DMG *= 1.35f;
            duration_agnCarrer_stack += duration_stack;
            if(duration_agnCarrer_stack>400) duration_agnCarrer_stack = 400;
            for (int i = 0; i < matchedEffect.Count; i++) EffectInstaller.Inflict(gameObject, matchedEffect[i], duration_agnCarrer_stack, 50);
        }
    }
    protected void TakeDMG(float DMG)
    {
        float afterhealth = realHealth - DMG;
        if (DMG < -1) EM.InstantiateBattleObject(IsCat() ? SEnums.heal : SEnums.heal_e, transform.position.x, transform.position.y);
        else if (DMG == 0) EM.InstantiateBattleObject(SEnums.invalid, transform.position.x, transform.position.y);
        else if (afterhealth < maxHealth - (realKBtimes + 1) * hardness)
        {
            realKBtimes = (int)((maxHealth - afterhealth) / hardness);
            if (coroutineKB == null)
            {
                lastTriggeredKBType = KB_Type.none;
                coroutineKB = StartCoroutine(PerformKB());
            }
        }
        realHealth = afterhealth;
        if (realHealth > maxHealth) realHealth = maxHealth;
        if (DMG > 0f && maxHealth > 0f)
        {
            float damageRatio = Mathf.Clamp01(DMG / maxHealth);
            CharacterTargetManager.Instance.NotifyCharacterDamaged(this, damageRatio);
        }
        if (IsCat()) levelController.RecordProficency_DamageTaken(NameCode, (int)Mathf.Max(DMG,0));
    }
    protected float CounterT(int duration) { return (100 - duration) / 100f; }
    public virtual void StartKBCoroutine(KB_Type kbt = KB_Type.none, float DX = 400) 
    {
        if (Speed == 0 && GetHealth() > 1) return;
        BlockAnimationSwitch = false; 
        if (coroutineKB == null)
        {
            lastTriggeredKBType = kbt;
            coroutineKB = StartCoroutine(PerformKB(kbt, DX));
        }
    }
    protected IEnumerator PerformKB(KB_Type kbt = KB_Type.none, float DX = 400)
    {
        int duration = 24;
        float speedY = 9;
        switch (kbt)
        {
            case KB_Type.none: duration = 24; DX = 400; speedY = 9; break;
            case KB_Type.knockBack: duration = 15; speedY = 0; break;
            case KB_Type.pushBack: duration = 12; DX = 60; speedY = 4; break;
            case KB_Type.bossShock: duration = 45; DX = 700; speedY = 9; break;
            default: break;
        }
        int d1 = duration * 2 / 3;
        int d2 = duration - d1;
        BlockAnimationSwitch = false;
        onKB = true; onATK = false;
        // KB can interrupt a Friendly attack step; force search mode back to enemy side.
        CharacterTargetManager.Instance.SetCharacterFriendlyMode(this, false);
        CharacterTargetManager.Instance.NotifyCharacterStatePulse(this, EmotionBattleState.kb);
        SetAttackRange(-CharacterTargetVolumeLength, DetectionRange);
        SwitchAnimation(3);
        Targets.Clear();
        BaseTarget = null;

        Passive_OnBeforeKB();

        int sign = gameObject.CompareTag("Cat") ? 1 : -1;
        float targetX = transform.position.x + sign * (DX / 100f);
        float lerptime = 1 / (float)duration;
        for (int i = 0; i < duration; i++)
        {
            float deltaY = i <= d1
                ? (speedY - (speedY * i / (d1 / 2))) * Time.deltaTime
                : (speedY - (speedY * (i - d1) / (d2 / 2))) * Time.deltaTime;
            float lerpX = Mathf.Lerp(transform.position.x, targetX, lerptime);
            transform.position = new Vector2(lerpX, transform.position.y + deltaY);
            if (i <= d1) SwitchAnimation(3);
            if (i == duration - 1)
            {
                if (realHealth < 0)
                {
                    Passive_OnDead();
                    if (realHealth < 0) Dead();
                }// check death
                transform.position = new Vector2(transform.position.x, startingY);
                Targets.Clear();
                BaseTarget = null;
                SwitchAnimation(0);
                onKB = false;
                coroutineKB = null;
                //GetStrategy();
            }
            yield return new WaitForFixedUpdate();//Operate per frame
        }
        Passive_OnAfterKB();
    }//Play KB animation, with disable everything else
    protected void HitEffect(List<AttackType> types)
    {
        //Under attacked
        //EM.InstantiateEffect(SEnums.bite, transform.position.x);
        if (types == null || types.Count == 0) EM.InstantiateBattleObject(SEnums.bite, transform.position.x, transform.position.y);
        else foreach (var t in types)
        {
            switch (t)
            {
                case AttackType.baseCannon: EM.InstantiateBattleObject(SEnums.bite, transform.position.x, transform.position.y); break;
                case AttackType.none: EM.InstantiateBattleObject(SEnums.bite, transform.position.x, transform.position.y); break;
                case AttackType.wave: EM.InstantiateBattleObject(SEnums.bite, transform.position.x, transform.position.y); break;
                case AttackType.surge: EM.InstantiateBattleObject(SEnums.bite, transform.position.x, transform.position.y); break;
                case AttackType.wave_invalid: EM.InstantiateBattleObject(SEnums.wave_invalid, transform.position.x, transform.position.y); break;
                case AttackType.invalid: EM.InstantiateBattleObject(SEnums.invalid, transform.position.x, transform.position.y); break;
                case AttackType.critical: EM.InstantiateBattleObject(SEnums.critical, transform.position.x, transform.position.y); break;
                case AttackType.savage: EM.InstantiateBattleObject(SEnums.savage, transform.position.x, transform.position.y); break;
                case AttackType.zombieKiller: break;
                default: break;
            }
        }
    }
    public virtual void SetAttackRange(float near, float far) { }
    public virtual void DMG_DREeffects(ref float DMG, DamageRelatedEffect dre) { }
    public virtual void DMG_SubTraitsEffects(ref float DMG, SubTraits opponentSubtraits) { }
    public void Wave_Attack(int level, bool mini, float DMG, List<CharacterEffect> enemyEffect, List<AttackType> atkTypes)
    {
        string e = IsCat() ? "Cat Units" : "Enemy Units";
        GameObject wu = Resources.Load<GameObject>($"Units/{e}/waveunit");
        WaveUnit ww = Instantiate(wu, transform.position, Quaternion.identity).GetComponent<WaveUnit>();
        ww.BeginWaveAttack(level, mini, DMG, traits, subtraits, againstCareer, DRE, enemyEffect, atkTypes);
    }
    public void Surge_Attack(int level, bool mini, int dis, float DMG, List<CharacterEffect> enemyEffect, List<AttackType> atkTypes)
    {
        string e = IsCat() ? "Cat Units" : "Enemy Units";
        GameObject su = Resources.Load<GameObject>($"Units/{e}/surgeunit");
        SurgeUnit ss = Instantiate(su, transform.position, Quaternion.identity).GetComponent<SurgeUnit>();
        ss.BeginSurgeAttack(level, mini, dis, DMG, traits, subtraits, againstCareer, DRE, enemyEffect, atkTypes);
    }
    public virtual void Dead()
    {
        EM.InstantiateBattleObject(SEnums.soul, transform.position.x, transform.position.y);
        Destroy(gameObject);
        if (coroutineKB != null) StopCoroutine(coroutineKB);
    }
    protected void BaseResist()
    {
        var list = atkTypeResis?.ToList() ?? new List<AttackTypeResistance>();
        list.Add(new AttackTypeResistance { type = AttackType.baseCannon, intensity = 100 });
        list.Add(new AttackTypeResistance { type = AttackType.wave, intensity = 100 });
        list.Add(new AttackTypeResistance { type = AttackType.surge, intensity = 100 });
        list.Add(new AttackTypeResistance { type = AttackType.explosion, intensity = 100 });
        atkTypeResis = list.ToArray();
    }
    #endregion

    #region External Modifiers
    public void SetPower(float pw) => Power = pw;
    public void SetSkillFactor(float sf) => skillfactor = sf;
    public float TBCspeedTranslator(int spd) => spd / 10f;
    public void ChangeSpeed(int spd) => realSpeed = spd;
    public void SetCurseStatus(bool curse) => onCurse = curse;
    public float GetHealth() => realHealth;
    public float GetMaxHealth()=>maxHealth;
    public void SetHealth(int rh)=>realHealth = rh;
    public void ResetKBtimes()=>realKBtimes--;
    public void SyncKBStateToHealth()
    {
        if (hardness <= 0)
        {
            realKBtimes = 0;
            return;
        }
        int kbCount = (int)((maxHealth - realHealth) / hardness);
        int maxKbCount = Mathf.Max(0, KB - 1);
        realKBtimes = Mathf.Clamp(kbCount, 0, maxKbCount);
    }
    public int GetRealSpeed()=>realSpeed;
    public int GetAnimationStep()=>animateStep;
    public bool IsOnKB() => onKB;
    public KB_Type GetLastTriggeredKBType() => lastTriggeredKBType;
    public IReadOnlyList<Character> GetLastAttackHitTargets() => lastAttackHitTargets;

    private void TryRecordHitTarget(Character target)
    {
        if (target == null) return;
        if (lastAttackHitTargets.Contains(target)) return;
        lastAttackHitTargets.Add(target);
    }

    private GameObject GetValidBaseTarget()
    {
        if (BaseTarget == null) return null;
        if (!BaseTarget.activeInHierarchy) { BaseTarget = null; return null; }
        return BaseTarget;
    }
    #endregion


}
