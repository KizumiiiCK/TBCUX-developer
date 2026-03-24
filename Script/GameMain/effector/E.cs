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
    private void Start()
    {
        etarget=GetComponent<Character>();
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
        if (duration <= 0) Destroy(this);
        duration--;
    }
    public virtual void EffectInitializer() { }
    public virtual void EffectOperation() { }
    public virtual void RemoveEffect() { }
    protected void InstallEffect() {
        string en=string.Empty;
        bool enemy;
        switch (effectName)
        {
            case EffectName.weaken: en = "weaken"; break;
            case EffectName.slow: en = "slow"; break;
            case EffectName.stop: en = "stop"; break;
            case EffectName.curse: en = "curse"; break;
            case EffectName.lacerate: en = "lacerate"; break; 
            default: break;
        }
        if (etarget.CompareTag("Cat")) enemy = false;
        else enemy = true;
        GameObject eff = Resources.Load<GameObject>($"Effects/{en}{(enemy ? "_e" : string.Empty)}");
        Vector3 pos = etarget.transform.position + new Vector3((enemy ? -1 : 1) * slotNum * 1.2f, 0, 0);
        try { EFF = Instantiate(eff, pos, Quaternion.identity).transform; }
        catch { Debug.LogError($"Error loading eff {effectName}"); }
        EFF.SetParent(etarget.transform);
    }
    protected void OnDestroy()
    {
        RemoveEffect();
        if(EFF.gameObject!=null) Destroy(EFF.gameObject);
        etarget.effectMarkedSlots[slotNum] = false;
    }
    public void SetIntensity(int itst) { intensity = itst; EffectOperation(); }
}
