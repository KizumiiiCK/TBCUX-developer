using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class IconImageHelper:MonoBehaviour
{
    public Sprite strategic;
    public Sprite icon_massive;
    public Sprite icon_insane;
    public Sprite icon_tough;
    public Sprite icon_aegis;
    public Sprite icon_strongagainst;
    public Sprite against_W;
    public Sprite against_D;
    public Sprite against_M;
    public Sprite against_S;
    public Sprite against_P;
    public Sprite[] effectSprites;
    public Sprite[] abilitySprites;
    public Sprite[] atkResSprites;
    public Sprite[] effResSprites;
    public Sprite GetIconSprite(EffectName en) => effectSprites[Array.IndexOf(Enum.GetValues(typeof(EffectName)), en)-1];
    public Sprite GetIconSprite(AbilityName an) => abilitySprites[Array.IndexOf(Enum.GetValues(typeof(AbilityName)), an)-1];
    public Sprite GetAtkResSprite(AttackType at) => atkResSprites[Array.IndexOf(Enum.GetValues(typeof(AttackType)), at)-1];
    public Sprite GetEffResSprite(EffectName en) => effResSprites[Array.IndexOf(Enum.GetValues(typeof(EffectName)), en)-1];
}
