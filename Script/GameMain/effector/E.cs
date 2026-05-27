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
    private AnimationDisplayer pooledEffectDisplay;
    private bool pooledEffectVisual = false;
    private string pooledEffectName = string.Empty;
    private EffectManager cachedEffectManager;
    private bool effectVisualReleased = false;
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
        cachedEffectManager = etarget != null ? etarget.EM : null;
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
            case EffectName.deathmark: en = "death_mark"; break;
            default: break;
        }
        if (etarget.IsCat()) enemy = false;
        else enemy = true;
        string effectVisualName = $"{en}{(enemy ? "_e" : string.Empty)}";
        Vector3 pos = etarget.transform.position + new Vector3((enemy ? -1 : 1) * slotNum * 1.2f, 0, 0);
        EffectManager effectManager = cachedEffectManager != null ? cachedEffectManager : (etarget != null ? etarget.EM : null);
        if (!string.IsNullOrEmpty(en) && etarget != null && effectManager != null)
        {
            AnimationDisplayer ad = effectManager.InstantiateAttachedBattleObject(
                effectVisualName,
                pos,
                etarget.transform,
                worldPositionStays: true,
                playSound: true,
                lifetimeFrames: 0);
            if (ad != null)
            {
                pooledEffectDisplay = ad;
                EFF = ad.transform;
                pooledEffectVisual = true;
                pooledEffectName = effectVisualName;
                return;
            }
        }
    }

    private void OnDisable()
    {
        ReleaseEffectVisual();
    }

    protected void OnDestroy()
    {
        ReportEffectDuration();
        RemoveEffect();
        ReleaseEffectVisual();
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

    private void ReleaseEffectVisual()
    {
        if (effectVisualReleased) return;
        effectVisualReleased = true;

        AnimationDisplayer ad = pooledEffectDisplay;
        if (ad == null && EFF != null)
        {
            ad = EFF.GetComponent<AnimationDisplayer>();
        }

        if (ad != null)
        {
            EffectManager effectManager = cachedEffectManager != null ? cachedEffectManager : (etarget != null ? etarget.EM : null);
            if (pooledEffectVisual && effectManager != null)
            {
                effectManager.RecycleBattleObject(ad, pooledEffectName);
            }
            else if (ad.gameObject != null)
            {
                Destroy(ad.gameObject);
            }
        }
        else if (EFF != null && EFF.gameObject != null)
        {
            Destroy(EFF.gameObject);
        }

        pooledEffectDisplay = null;
        EFF = null;
    }
}
