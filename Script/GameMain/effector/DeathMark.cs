using System;
using UnityEngine;

public class DeathMark : E
{
    private PassiveSkill passive;

    public override void EffectInitializer()
    {
        effectName = EffectName.deathmark;
        if (etarget == null) return;

        passive = (PassiveSkill)Activator.CreateInstance(typeof(Aux_DeathMark));
        passive.SetPassiveValues(EffectName.deathmark, 100, duration, intensity);
        etarget.AddPassiveEffect(passive);
        passive.OnAddingAbility(etarget);
        CharacterTargetManager.Instance.RegisterDeathMarkedCharacter(etarget);
    }

    public override void RemoveEffect()
    {
        if (etarget != null)
        {
            CharacterTargetManager.Instance.UnregisterDeathMarkedCharacter(etarget);
            if (passive != null) etarget.RemovePassiveEffect(passive);
        }
        passive = null;
    }
}
