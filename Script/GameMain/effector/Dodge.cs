using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class Dodge : E
{
    public override void EffectInitializer()
    {
        effectName = EffectName.dodge;
        etarget.SetDodgeStatus(true);
    }
    public override void RemoveEffect()
    {
        etarget.SetDodgeStatus(false);
    }
}
