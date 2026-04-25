using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class Toxic : E
{
    public override void EffectInitializer()
    {
        effectName = EffectName.toxic;
        Character c = GetComponent<Character>();
        if (c != null)
        {
            c.ReceiveAttack(c.GetMaxHealth() * duration / 100, null, null, null, null, null, null);
        }
    }
}
