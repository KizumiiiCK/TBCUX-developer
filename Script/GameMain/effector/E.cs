using System.Collections;
using System.Collections.Generic;
using System.IO;
using UnityEngine;

public abstract class E : MonoBehaviour
{
    public int duration = 0;
    public int intensity = 0;
    protected EffectName effectName = EffectName.none;
    protected Character etarget;
    protected int slotNum=0;
    protected Transform EFF;
    private bool pooledEffectVisual = false;
    private string pooledEffectName = string.Empty;
    private int activeDurationFrames = 0;
    private LevelController cachedLevelController;
    private string cachedNameCode = string.Empty;
    private bool cachedIsCat;
    private void Start()
    {
        etarget=GetComponent<Character>();
        cachedLevelController = etarget != null ? etarget.levelController : null;
        cachedNameCode = etarget != null ? etarget.NameCode : string.Empty;
        cachedIsCat = etarget != null && etarget.IsCat();
        for(int i = 0; i < etarget.effectMarkedSlots.Length; i++)
        {
            if (!etarget.effectMarkedSlots[i])
            {
                slotNum = i;
                etarget.effectMarkedSlots[i] = true;
                break;
            }
        }
        EffectInitializer();
        InstallEffect();
    }
    void FixedUpdate()
    {
        if (duration <= 0)
        {
            Destroy(this);
            return;
        }
        activeDurationFrames++;
        duration--;
    }
    public virtual void EffectInitializer() { }
    public virtual void EffectOperation() { }
    public virtual void RemoveEffect() { }
    public EffectName GetEffectName() => effectName;
    protected void InstallEffect() {
        string en=string.Empty;
        bool enemy;
        switch (effectName)
        {
            case EffectName.weaken: en = "weaken"; break;
            case EffectName.slow: en = "slow"; break;
            case EffectName.stop: en = "stop"; break;
            case EffectName.curse: en = "curse"; break;
            case EffectName.toxic: en = "toxic"; break;
            case EffectName.lacerate: en = "lacerate"; break; 
            default: break;
        }
        if (etarget.CompareTag("Cat")) enemy = false;
        else enemy = true;
        string effectVisualName = $"{en}{(enemy ? "_e" : string.Empty)}";
        Vector3 pos = etarget.transform.position + new Vector3((enemy ? -1 : 1) * slotNum * 1.2f, 0, 0);
        if (effectName != EffectName.lacerate && etarget != null && etarget.EM != null)
        {
            AnimationDisplayer ad = etarget.EM.InstantiateAttachedBattleObject(
                effectVisualName,
                pos,
                etarget.transform,
                worldPositionStays: true,
                playSound: true,
                lifetimeFrames: 0);
            if (ad != null)
            {
                EFF = ad.transform;
                pooledEffectVisual = true;
                pooledEffectName = effectVisualName;
                return;
            }
        }

        //GameObject eff = Resources.Load<GameObject>($"Effects/{effectVisualName}");
        //try
        //{
        //    EFF = Instantiate(eff, pos, Quaternion.identity).transform;
        //    EFF.SetParent(etarget.transform);
        //    pooledEffectVisual = false;
        //}
        //catch
        //{
        //    Debug.LogError($"Error loading eff {effectName}");
        //}
    }
    protected void OnDestroy()
    {
        ReportEffectDuration();
        RemoveEffect();
        if (EFF != null)
        {
            if (pooledEffectVisual && etarget != null && etarget.EM != null)
            {
                AnimationDisplayer ad = EFF.GetComponent<AnimationDisplayer>();
                if (ad != null) etarget.EM.RecycleBattleObject(ad, pooledEffectName);
                else Destroy(EFF.gameObject);
            }
            else
            {
                Destroy(EFF.gameObject);
            }
        }
        if (etarget != null && etarget.effectMarkedSlots != null && slotNum >= 0 && slotNum < etarget.effectMarkedSlots.Length)
        {
            etarget.effectMarkedSlots[slotNum] = false;
        }
    }
    public void SetIntensity(int itst) { intensity = itst; EffectOperation(); }

    protected virtual void ReportEffectDuration()
    {
        if (!cachedIsCat || cachedLevelController == null || activeDurationFrames <= 0) return;
        cachedLevelController.RecordProficency_DebuffSuffered(cachedNameCode, activeDurationFrames);
        //cachedLevelController.RecordProficency_EffectDuration(cachedNameCode, effectName, activeDurationFrames);
    }
}
