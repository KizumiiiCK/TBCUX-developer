using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class Stop : E
{
    public override void EffectInitializer()
    {
        effectName = EffectName.stop;
        etarget.SetAnimationSpeed(0);
        etarget.ChangeSpeed(0);
        etarget.SetFrameStep(0);
    }
    public override void RemoveEffect()
    {
        etarget.SetAnimationSpeed(1);
        etarget.SetFrameStep(1);
        if (etarget.GetComponent<Slow>() == null) etarget.ChangeSpeed(etarget.Speed);
        else etarget.ChangeSpeed(1);
    }
}
