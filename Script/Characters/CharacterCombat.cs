using System.Collections;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

public abstract partial class Character
{
    protected string compareTagName = "";
    [SerializeField] public List<GameObject> Targets = new List<GameObject>();

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
            if(ce.name==EffectName.dodge) continue;
            if (Random.Range(0, 100) < ce.probability * sf)
                list.Add(ce);
        }
        return list;
    }

    /* ����ս��������ReceiveAttack / PerformKB / �˺����� ������ԭʵ�� */
    #region Attack Functions
    public void Attack(float dmg, bool areaAttack, bool doNotTrigger, GameObject specific = null)
    {
        Character GetTarget<T>(GameObject go) where T : Character
        {
            if (go == null) return null;
            if (go.CompareTag("Cat"))
                return go.GetComponent<CatCharacter>() as T;
            else
                return go.GetComponent<EnemyCharacter>() as T;
        }
        Targets.RemoveAll(go => go == null);
        if (Targets.Count != 0 || specific != null)
        {
            List<CharacterEffect> decisionEff = new List<CharacterEffect>();
            List<AttackType> types = new List<AttackType>();
            foreach (var at in ATKTypes) types.Add(at);
            //float dmg = realDamage[animateStep] * ATK_muiltipier;
            dmg *= ATK_muiltipier;
            Passive_OnAttacking(ref dmg, ref types);
            if (specific != null)
            {
                decisionEff = DetermineSelfATKEffects();
                Character Target = GetTarget<Character>(specific);
                Target?.ReceiveAttack(dmg, traits, subtraits, againstCareer, DRE, decisionEff, types);
                if (IsCat()) levelController.RecordProficency_DamageDealt(NameCode, (int)dmg);
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
                    Target?.ReceiveAttack(dmg, traits, subtraits, againstCareer, DRE, decisionEff, types);
                    if (IsCat()) levelController.RecordProficency_DamageDealt(NameCode, (int)dmg);
                }
                else
                {
                    if (IsCat()) levelController.RecordProficency_DamageDealt(NameCode, (int)dmg * Targets.Count);//simple count for area atk
                    for (int i = Targets.Count - 1; i >= 0; i--)
                    {
                        Character ec = GetTarget<Character>(Targets[i]);
                        ec?.ReceiveAttack(dmg, traits, subtraits, againstCareer, DRE, decisionEff, types);
                    }
                }
                //if (!atkInfos[animateStep].DoNotTriggerEffects)
                if (!doNotTrigger)
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
        SetAttackRange(0, DetectionRange); // 重置攻击范围为检测范围
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
    protected bool AreCorrespondingTraits(Traits targetTrait)
    {
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
    public void SetNewTarget(GameObject newTarget, bool quickTrigger) { if (quickTrigger) Attack(realDamage[animateStep],false,false,newTarget); else Targets.Add(newTarget); }
    public void RemoveTarget(GameObject newTarget) { Targets.Remove(newTarget); }
    public void RemoveAllTarget() { Targets = new List<GameObject>(); }
    public GameObject FindNearest()
    {
        if (Targets.Count == 0) return null;
        //bool positive = compareTagName == "Enemy";
        GameObject target = Targets[0];
        //int mindis = int.MaxValue;
        foreach (var go in Targets)
        {
            if (target.transform.position.x < go.transform.position.x == IsCat()) target = go;
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
    protected void TakeEffects(List<CharacterEffect> enemyEffect, bool sageBuff = false)
    {
        if (enemyEffect == null) return;
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
            if (IsCat()) levelController.RecordProficency_DebuffSuffered(NameCode, (int)real_duration);
        }
    }
    public abstract void ReceiveAttack(float DMG, Traits enemyTraits, SubTraits opponentSubtraits, AgainstCareer opponentCE, DamageRelatedEffect dre, List<CharacterEffect> enemyEffect, List<AttackType> atkTypes);
    protected void DMG_CarrerEffects(ref float DMG, AgainstCareer opponentAC)
    {
        if (opponentAC == null) return;
        if (career.Warrior && opponentAC.AggainstWarrior ||
            career.Deffender && opponentAC.AggainstDeffender ||
            career.Magician && opponentAC.AggainstMagician ||
            career.Supporter && opponentAC.AggainstSuppoter ||
            career.Practician && opponentAC.AggainstPractician)
            DMG *= 4;
    }
    protected void TakeDMG(float DMG)
    {
        float afterhealth = realHealth - DMG;
        Debug.Log($"{NameCode}: {realHealth} - {DMG} / {maxHealth}");
        //if (DMG < 0) EffectInstaller.Inflict(gameObject, AttackType.heal, 1, 1);
        if (DMG < 0) EM.InstantiateBattleObject(IsCat()?SEnums.heal:SEnums.heal_e, transform.position.x, transform.position.y);
        else if (afterhealth < maxHealth - (realKBtimes + 1) * hardness)
        {
            realKBtimes = (int)((maxHealth - afterhealth) / hardness);
            if (coroutineKB == null) coroutineKB = StartCoroutine(PerformKB());
        }
        realHealth = afterhealth;
        if (realHealth > maxHealth) realHealth = maxHealth;
        if (IsCat()) levelController.RecordProficency_DamageTaken(NameCode, (int)Mathf.Abs(DMG));
    }
    protected float CounterT(int duration) { return (100 - duration) / 100f; }
    public virtual void StartKBCoroutine(KB_Type kbt = KB_Type.none, float DX = 350) { BlockAnimationSwitch = false; if (coroutineKB == null) coroutineKB = StartCoroutine(PerformKB(kbt, DX)); }
    protected IEnumerator PerformKB(KB_Type kbt = KB_Type.none, float DX = 350)
    {
        int duration = 24;
        float speedY = 9;
        switch (kbt)
        {
            case KB_Type.none: duration = 24; DX = 350; speedY = 9; break;
            case KB_Type.knockBack: duration = 15; speedY = 0; break;
            case KB_Type.pushBack: duration = 12; DX = 60; speedY = 4; break;
            case KB_Type.bossShock: duration = Speed == 0 ? 0 : 45; DX = Speed == 0 ? 0 : 700; speedY = Speed == 0 ? 0 : 9; break;
            default: break;
        }
        float speedX = DX / duration * 0.3f;
        int d1 = duration * 2 / 3;
        int d2 = duration - d1;
        BlockAnimationSwitch = false;
        // 不再需要禁用碰撞箱
        onKB = true; onATK = false;
        SetAttackRange(0, DetectionRange);
        SwitchAnimation(3);
        //if(animator!=null)animator.SetInteger("state", 2);//
        //if (animatorDisplayer != null) animatorDisplayer.SetMaanimPointer(3);

        Passive_OnBeforeKB();

        int sign = gameObject.CompareTag("Cat") ? 1 : -1;
        for (int i = 0; i < duration; i++)
        {
            if (i <= d1) { this.transform.Translate(new Vector2(sign * speedX, speedY - (speedY * i / (d1 / 2))) * Time.deltaTime); SwitchAnimation(3); }
            else this.transform.Translate(new Vector2(sign * speedX, speedY - (speedY * (i - d1) / (d2 / 2))) * Time.deltaTime);
            if (i == duration - 1)
            {
                if (realHealth < 0)
                {
                    Passive_OnDead();
                    Dead();
                }// check death
                transform.position = new Vector2(transform.position.x, startingY);
                SwitchAnimation(0);
                // 不再需要启用碰撞箱
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
        if (types==null||types.Count == 0) EM.InstantiateBattleObject(SEnums.bite, transform.position.x, transform.position.y);
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
                    case AttackType.zombieKiller: if (traits.Z && realHealth < 0) EM.InstantiateBattleObject(SEnums.soulStrike, transform.position.x, transform.position.y); break;
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
    public void SetDodgeStatus(bool dodge)=>onDodge = dodge;
    public float GetHealth() => realHealth;
    public float GetMaxHealth()=>maxHealth;
    public void SetHealth(int rh)=>realHealth = rh;
    public void ResetKBtimes()=>realKBtimes--;
    public int GetRealSpeed()=>realSpeed;
    public int GetAnimationStep()=>animateStep;
    public bool IsOnKB() => onKB;
    #endregion


}