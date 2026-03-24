using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class EffectInstaller : MonoBehaviour
{
    public static void Inflict(GameObject target, object effectName, float duration, float intensity)
    {
        int _duration = Mathf.FloorToInt(duration);
        int _intensity = Mathf.FloorToInt(intensity);
        if (_duration == 0)
        {
            GameObject blk = Resources.Load<GameObject>($"Effects/effect_blocked");
            Instantiate(blk, target.transform.position, Quaternion.identity).transform.SetParent(target.transform);
            return;
        }
        switch (effectName)
        {
            case EffectName.weaken:
                Weaken wkn;
                if (target.GetComponent<Weaken>() != null) wkn = target.GetComponent<Weaken>();
                else wkn = target.AddComponent<Weaken>();
                wkn.duration = _duration;
                wkn.SetIntensity(_intensity);
                break;
            case EffectName.stop:
                Stop stp;
                if (target.GetComponent<Stop>() != null) stp = target.GetComponent<Stop>();
                else stp = target.AddComponent<Stop>();
                stp.duration = _duration;
                break;
            case EffectName.slow:
                Slow slw;
                if (target.GetComponent<Slow>() != null) slw = target.GetComponent<Slow>();
                else slw = target.AddComponent<Slow>();
                slw.duration = _duration;
                break;
            case EffectName.knockback:
                target.GetComponent<Character>().StartKBCoroutine(KB_Type.knockBack, 165 * duration);
                break;
            case EffectName.wrap:
                Wrap wrp;
                if (target.GetComponent<Wrap>() != null)
                {
                    wrp = target.GetComponent<Wrap>();
                    wrp.duration = _duration;
                    wrp.intensity = _intensity;
                }
                break;
            case EffectName.curse:
                Curse crs;
                if (target.GetComponent<Curse>() != null) crs = target.GetComponent<Curse>();
                else crs = target.AddComponent<Curse>();
                crs.duration = _duration;
                break;
            case EffectName.dodge:
                Dodge dod;
                if (target.GetComponent<Dodge>() != null) dod = target.GetComponent<Dodge>();
                else dod = target.AddComponent<Dodge>();
                dod.duration = _duration;
                break;
            case EffectName.lacerate:
                Lacerate lac;
                if (target.GetComponent<Lacerate>() != null) lac = target.GetComponent<Lacerate>();
                else lac = target.AddComponent<Lacerate>();
                lac.duration = _duration;
                break;
            //
            case AttackType.invalid:
                GameObject ivd = Resources.Load<GameObject>($"Effects/invalid");
                Instantiate(ivd, target.transform.position, Quaternion.identity).transform.SetParent(target.transform);
                break;
            case AttackType.heal:
                string rcv_str = target.GetComponent<Character>().IsCat() ? string.Empty : "_e";
                GameObject rcv = Resources.Load<GameObject>($"Effects/heal{rcv_str}");
                Instantiate(rcv, target.transform.position, Quaternion.identity).transform.SetParent(target.transform);
                break;
            case AbilityName.strengthen:
                string ste_str = target.GetComponent<Character>().IsCat() ? string.Empty : "_e";
                GameObject ste = Resources.Load<GameObject>($"Effects/strengthen{ste_str}");
                Instantiate(ste, target.transform.position, Quaternion.identity).transform.SetParent(target.transform);
                break;
            case AbilityName.survive:
                GameObject sur = Resources.Load<GameObject>("Effects/survive");
                Instantiate(sur, target.transform.position, Quaternion.identity).transform.SetParent(target.transform);
                break;
            default: break;
        }
    }
    //public void DisplayEffect(GameObject target, string effname)
    //{

    //}
}
