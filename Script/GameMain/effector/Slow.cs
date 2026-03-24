using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class Slow : E
{
    public override void EffectInitializer()
    {
        effectName = EffectName.slow;
        if (etarget.GetComponent<Stop>() == null) GetComponent<Character>().ChangeSpeed(1);
    }
    public override void RemoveEffect()
    {
        if(etarget.GetComponent<Stop>()==null)GetComponent<Character>().ChangeSpeed(etarget.Speed);
    }
}
