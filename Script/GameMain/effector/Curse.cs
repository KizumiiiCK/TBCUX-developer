using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class Curse : E
{
    public override void EffectInitializer()
    {
        effectName = EffectName.curse;
        GetComponent<Character>().SetCurseStatus(true);
    }
    public override void RemoveEffect()
    {
        GetComponent<Character>().SetCurseStatus(false);
    }
}
