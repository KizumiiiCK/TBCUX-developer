using System.Collections;
using System.Collections.Generic;
using System.Linq;
using Unity.VisualScripting;
using UnityEngine;

public abstract partial class Character
{
    private const float CharacterTargetVolumeLength = 200;
    /// <summary>
    /// 击退位移的距离补偿。下面 PerformKB 的 X 位移用的是「每帧从当前位置、按恒定系数
    /// 1/duration 插值」的写法，属于指数逼近，duration 帧后只能走到 DX 的约 64%
    /// （剩余 (1-1/n)^n → 1/e，帧数越多越接近 63.2%）。而各 KB_Type 的 DX 常数是当年
    /// 按线性插值调出来的，换成 ease-out 时没有同步调整，所以在这里统一乘回去：
    /// 64% × 1.5 ≈ 96%，实际位移重新接近 DX/100 的名义值。
    /// </summary>
    private const float KBDistanceCompensation = 1.5f;
    // 全抗性命中特效类型，只读共享，避免每次完全抵抗命中时分配新List
    protected static readonly List<AttackType> WaveInvalidHitTypes = new List<AttackType> { AttackType.wave_invalid };
    // 当本次攻击禁止触发效果时，职业克制也应失效（全false）。
    private static readonly AgainstCareer EmptyAgainstCareer = new AgainstCareer();
    private bool incomingTraitCorresponding;
    protected string compareTagName = "";
    [SerializeField] public List<GameObject> Targets = new List<GameObject>();
    [SerializeField] public GameObject BaseTarget;
    private readonly List<Character> lastAttackHitTargets = new List<Character>(8);
    private KB_Type lastTriggeredKBType = KB_Type.none;
    private int attackResolvedStep = -1;
    private bool attackResolvedThisStep;

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

    /// <summary>
    /// 按各效果自身的 probability（乘 GetFactor）掷一次骰，返回本次攻击真正要附加的效果。
    /// public 是给不走 Attack() 的被动用的（如 DeadSoul 在死亡时补一发 Surge），
    /// 这样脱离普攻流程产生的攻击也和普攻用同一套附带效果规则。
    /// </summary>
    public List<CharacterEffect> DetermineSelfATKEffects()
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
    protected void ResetAttackResolveState()
    {
        attackResolvedStep = -1;
        attackResolvedThisStep = false;
    }

    protected bool ShouldResolveAttackFrame(int attackFrame)
    {
        if (attackResolvedStep != animateStep)
        {
            attackResolvedStep = animateStep;
            attackResolvedThisStep = false;
        }
        if (attackResolvedThisStep) return false;
        if (animatedframes != attackFrame) return false;
        attackResolvedThisStep = true;
        return true;
    }

    public void Attack(float dmg, bool areaAttack, bool doNotTrigger, bool doNotTriggerAbilities, GameObject specific = null)
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
            AgainstCareer effectiveAgainstCareer = doNotTrigger ? EmptyAgainstCareer : againstCareer;
            foreach (var at in ATKTypes) types.Add(at);
            //float dmg = realDamage[animateStep] * ATK_muiltipier;
            dmg *= ATK_muiltipier;
            if (!doNotTriggerAbilities)
            {
                Passive_OnAttacking(ref dmg, ref types);
                // 被动（如 ProjectileLauncher）可能在这里 RemoveAllTarget() 来接管本次命中。
                // Targets 在下面是实时读的，基地目标却是上面缓存的局部变量，必须重新取一次，
                // 否则群攻分支会用失效的引用再打基地一次（弹幕伤害 + 原生伤害双击）。
                baseTarget = GetValidBaseTarget();
            }
            if (specific != null)
            {
                decisionEff = DetermineSelfATKEffects();
                Character Target = GetTarget<Character>(specific);
                if (CanHitTargetNow(Target))
                {
                    if (Target != null && Target.IsCat() == IsCat())
                        types.Add(AttackType.friendly);
                    Target?.ReceiveAttack(dmg, traits, subtraits, effectiveAgainstCareer, DRE, decisionEff, types);
                    TryRecordHitTarget(Target);
                    if (ShouldRecordProficiency()) levelController.RecordProficency_DamageDealt(NameCode, (int)dmg);
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
                        Target?.ReceiveAttack(dmg, traits, subtraits, effectiveAgainstCareer, DRE, decisionEff, types);
                        TryRecordHitTarget(Target);
                        if (ShouldRecordProficiency()) levelController.RecordProficency_DamageDealt(NameCode, (int)dmg);
                    }
                }
                else
                {
                    int allTargetCount = Targets.Count + (baseTarget != null && !Targets.Contains(baseTarget) ? 1 : 0);
                    if (ShouldRecordProficiency()) levelController.RecordProficency_DamageDealt(NameCode, (int)dmg * allTargetCount);//simple count for area atk
                    for (int i = Targets.Count - 1; i >= 0; i--)
                    {
                        Character ec = GetTarget<Character>(Targets[i]);
                        if (!CanHitTargetNow(ec)) continue;
                        if (ec != null && ec.IsCat() == IsCat())
                            types.Add(AttackType.friendly);
                        ec?.ReceiveAttack(dmg, traits, subtraits, effectiveAgainstCareer, DRE, decisionEff, types);
                        TryRecordHitTarget(ec);
                    }
                    if (baseTarget != null && !Targets.Contains(baseTarget))
                    {
                        Character bt = GetTarget<Character>(baseTarget);
                        if (CanHitTargetNow(bt))
                        {
                            if (bt != null && bt.IsCat() == IsCat())
                                types.Add(AttackType.friendly);
                            bt?.ReceiveAttack(dmg, traits, subtraits, effectiveAgainstCareer, DRE, decisionEff, types);
                            TryRecordHitTarget(bt);
                        }
                    }
                }
                if (!doNotTriggerAbilities)
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
        ResetAttackRangeToDetection();
        for(int i = Targets.Count - 1; i >= 0; i--)
        {
            if (Targets[i]==null) Targets.RemoveAt(i);
        }
    }
    public void ExitAttack()
    {
        onATK = false;
        ResetAttackResolveState();
        animateStep = 0;
        animatedframes = 0;
        SwitchAnimation(0);
        Passive_OnExitAttack();
    }

    public void AbortCurrentAttackForControl()
    {
        if (!onATK) return;
        onATK = false;
        ResetAttackResolveState();
        animateStep = 0;
        animatedframes = 0;
        realReload = 0;
        Supporter_Target_Switch(true);
        ResetAttackRangeToDetection();
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
    public void RemoveAllTarget() { Targets = new List<GameObject>(); BaseTarget = null; }
    public GameObject FindNearest()
    {
        GameObject baseTarget = GetValidBaseTarget();
        GameObject target = null;
        foreach (var go in Targets)
        {
            if (go == null) continue;
            if (target == null)
            {
                target = go;
                continue;
            }
            if (target.transform.position.x < go.transform.position.x == IsCat()) target = go;
        }
        if (target == null) return baseTarget;
        if (baseTarget != null && (target.transform.position.x < baseTarget.transform.position.x == IsCat()))
            return baseTarget;
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
        //
        // 这里只写模式，不清空 Targets。CharacterTargetManager 每轮 tick 都是覆盖式重算，
        // 清空既不会触发重算，又会在下一轮 tick 前留下一段空目标窗口——那段窗口里 Attack()
        // 会被 Targets 为空的入口判定挡掉，整段攻击静默作废（AttackBox 时代靠清空来强制
        // 重新检测，那套前提已经不存在了）。真正的同步点是随后的 SetAttackRange。
        CharacterTargetManager.Instance.SetCharacterFriendlyMode(this, !switchback);
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
        if (career.Practician && opponentAC.AggainstPractician) { duration_stack += 40; matchedEffect.Add(EffectName.lacerate); matchedEffect.Add(EffectName.slow); }

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
        else if (afterhealth <= 0f || afterhealth < maxHealth - (realKBtimes + 1) * hardness)
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
        if (ShouldRecordProficiency()) levelController.RecordProficency_DamageTaken(NameCode, (int)Mathf.Max(DMG,0));
    }
    protected float CounterT(int duration) { return (100 - duration) / 100f; }
    public virtual void StartKBCoroutine(KB_Type kbt = KB_Type.none, float DX = 400)
    {
        if (Speed == 0 && GetHealth() > 1) return;
        externalAnimControl = false;
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
        externalAnimControl = false;
        onKB = true; onATK = false;
        // KB can interrupt a Friendly attack step; force search mode back to enemy side.
        CharacterTargetManager.Instance.SetCharacterFriendlyMode(this, false);
        CharacterTargetManager.Instance.NotifyCharacterStatePulse(this, EmotionBattleState.kb);
        ResetAttackRangeToDetection();
        SwitchAnimation(3);
        Targets.Clear();
        BaseTarget = null;

        Passive_OnBeforeKB();

        int sign = gameObject.CompareTag("Cat") ? 1 : -1;
        float targetX = transform.position.x + sign * (DX * KBDistanceCompensation / 100f);
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
                if (realHealth <= 0)
                {
                    Passive_OnDead();
                    if (SkipDestroyCombatAccounting)
                    {
                        onKB = false;
                        coroutineKB = null;
                        yield break;
                    }
                    if (realHealth <= 0) Dead();
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
    /// <summary>
    /// 把攻击范围复位成出场时的检测范围。攻击结束、被击退、被外部动画（AnimationDrivingPassive）
    /// 接管结束后都该回到这个状态，否则单位会一直带着某个攻击段的窄范围。
    /// detectionScale 用于按比例缩放远端（如策略「防守」只用一半检测范围），
    /// 近端始终是 -CharacterTargetVolumeLength，避免各处手写 0 造成近身范围不一致。
    /// </summary>
    public void ResetAttackRangeToDetection(float detectionScale = 1f) => SetAttackRange(-CharacterTargetVolumeLength, DetectionRange * detectionScale);
    public virtual void DMG_DREeffects(ref float DMG, DamageRelatedEffect dre) { }
    public virtual void DMG_SubTraitsEffects(ref float DMG, SubTraits opponentSubtraits) { }
    public void Wave_Attack(int level, bool mini, float DMG, List<CharacterEffect> enemyEffect, List<AttackType> atkTypes)
    {
        GameObject wuPrefab = BundledAddressables.LoadSync<GameObject>(
            IsCat() ? "Units/Cat Units/waveunit" : "Units/Enemy Units/waveunit");
        if (wuPrefab == null) return;
        WaveUnit ww = Instantiate(wuPrefab, transform.position, Quaternion.identity).GetComponent<WaveUnit>();
        CharacterTargetManager.Instance.RegisterProjectile(ww);
        ww.BeginWaveAttack(level, mini, DMG, traits, subtraits, againstCareer, DRE, enemyEffect, atkTypes);
    }
    /// <summary>
    /// 在身前 dis（1/100 世界单位）处放一发 Surge。
    /// delayFrames 让 surgeunit 生成后先空转若干帧再出现并判定，
    /// 给「死亡后 30 帧才放亡魂」这类有前摇的来源用——等待由 surgeunit 自己数，
    /// 所以发起者当场消失也不影响。
    /// </summary>
    public void Surge_Attack(int level, bool mini, int dis, float DMG, List<CharacterEffect> enemyEffect, List<AttackType> atkTypes, int delayFrames = 0)
    {
        GameObject suPrefab = BundledAddressables.LoadSync<GameObject>(
            IsCat() ? "Units/Cat Units/surgeunit" : "Units/Enemy Units/surgeunit");
        if (suPrefab == null) return;
        SurgeUnit ss = Instantiate(suPrefab, transform.position, Quaternion.identity).GetComponent<SurgeUnit>();
        CharacterTargetManager.Instance.RegisterProjectile(ss);
        ss.BeginSurgeAttack(level, mini, dis, DMG, traits, subtraits, againstCareer, DRE, enemyEffect, atkTypes, delayFrames);
    }
    /// <summary>
    /// 这个单位一整套攻击的总强度：atkInfos 各段攻击力的<b>绝对值</b>之和，乘战斗数值倍率，再乘攻击力增益。
    /// <para>
    /// 取绝对值是因为治疗段的 ATK 存的是负数（Friendly=1 的那一段，如 -128）。按原值相加的话，
    /// 纯奶妈会得到一个负的总和、混合单位的治疗量会把伤害抵掉一部分——但「治疗 128」同样是
    /// 128 点攻击强度，不该因为符号就当它没有。所以这里按量级求和。
    /// </para>
    /// GetCombatStatMultiplier 就是猫咪等级/宝物/Power、敌人关卡强化的那个统一倍率，
    /// realDamage 各段也是这么算出来的；这里不直接求和 realDamage 是因为它是 int[]，
    /// 猫咪那条路（LoadCharacterData 乘宝物取整、InitializeCharacter 再乘等级取整）会截断两次。
    /// ATK_muiltipier 是 ATK_Buffer 之类的临时增益，普攻在 Attack() 里也乘它，跟着乘才对得上实战。
    /// </summary>
    public float GetTotalAttackDamage()
    {
        if (atkInfos == null) return 0f;
        float sum = 0f;
        for (int i = 0; i < atkInfos.Length; i++)
        {
            if (atkInfos[i] == null) continue;
            sum += Mathf.Abs(atkInfos[i].ATK);
        }
        return sum * GetCombatStatMultiplier() * ATK_muiltipier;
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
    public void SetMaxHealth(float mh) => maxHealth = mh;
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
    public int GetReload() => Reload;
    public void SetReload(int reloadT) => Reload = reloadT < 0 ? 0 : reloadT;
    public int GetRealReload() => realReload;
    public void SetRealReload(int reloadT) => realReload = reloadT < 0 ? 0 : reloadT;

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
