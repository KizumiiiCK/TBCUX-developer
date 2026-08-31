using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class Toxic : E
{
    public override void EffectInitializer()
    {
        effectName = EffectName.toxic;
        Character c = GetComponent<Character>();
        if (c == null) return;
        if (c.EM != null)
        {
            c.EM.InstantiateBattleObject(
                c.IsCat() ? SEnums.toxic : SEnums.toxic_e,
                c.transform.position.x,
                c.transform.position.y);
        }
        c.ReceiveAttack(c.GetMaxHealth() * duration / 100, null, null, null, null, null, null);
    }
}
