using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class Weaken : E
{
    public override void EffectInitializer()
    {
        effectName = EffectName.weaken;
    }
    public override void EffectOperation()
    {
        GetComponent<Character>().SetATKmuiltipier(intensity / 100f);
    }
    public override void RemoveEffect()
    {
        GetComponent<Character>().SetMuiltipierToMAX();
    }
}
