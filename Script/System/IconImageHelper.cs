using System;
using UnityEngine;

[Obsolete("IconImageHelper 已废弃，请改用 EAIconResolver 按名称加载图标。", false)]
public class IconImageHelper : MonoBehaviour
{
    public Sprite strategic => EAIconResolver.LoadByNameCode("N:s");
    public Sprite icon_massive => EAIconResolver.LoadByNameCode("N:dre:m");
    public Sprite icon_insane => EAIconResolver.LoadByNameCode("N:dre:i");
    public Sprite icon_tough => EAIconResolver.LoadByNameCode("N:dre:t");
    public Sprite icon_aegis => EAIconResolver.LoadByNameCode("N:dre:a");
    public Sprite icon_strongagainst => EAIconResolver.LoadByNameCode("N:dre:s");
    public Sprite against_W => EAIconResolver.LoadByNameCode("N:ac:1");
    public Sprite against_D => EAIconResolver.LoadByNameCode("N:ac:2");
    public Sprite against_M => EAIconResolver.LoadByNameCode("N:ac:3");
    public Sprite against_S => EAIconResolver.LoadByNameCode("N:ac:4");
    public Sprite against_P => EAIconResolver.LoadByNameCode("N:ac:5");

    public Sprite GetIconSprite(EffectName en)
    {
        int ibenum = Array.IndexOf(Enum.GetValues(typeof(EffectName)), en);
        return EAIconResolver.LoadByNameCode($"N:e:{ibenum}");
    }

    public Sprite GetIconSprite(AbilityName an)
    {
        int ibenum = Array.IndexOf(Enum.GetValues(typeof(AbilityName)), an);
        return EAIconResolver.LoadByNameCode($"N:a:{ibenum}");
    }

    public Sprite GetAtkResSprite(AttackType at)
    {
        int ibenum = Array.IndexOf(Enum.GetValues(typeof(AttackType)), at);
        return EAIconResolver.LoadByNameCode($"N:ra:{ibenum}");
    }

    public Sprite GetEffResSprite(EffectName en)
    {
        int ibenum = Array.IndexOf(Enum.GetValues(typeof(EffectName)), en);
        return EAIconResolver.LoadByNameCode($"N:re:{ibenum}");
    }
}
