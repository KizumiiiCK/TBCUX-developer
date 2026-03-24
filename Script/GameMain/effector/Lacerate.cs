using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class Lacerate : E
{
    private PassiveSkill passive=null;
    public override void EffectInitializer()
    {
        effectName = EffectName.lacerate;
        passive = (PassiveSkill)Activator.CreateInstance(typeof(LaceratedEffect));
        passive.SetPassiveValues(EffectName.lacerate, 100, duration, 1);
        etarget.AddPassiveEffect(passive);
    }
    public override void RemoveEffect()
    {
        etarget.RemovePassiveEffect(passive);
    }
}
