using System.Collections.Generic;
using UnityEngine;

[CreateAssetMenu(fileName = "data", menuName = "ScriptableObjects/CharacterData", order = 1)]
[System.Serializable]
public class CharacterData : ScriptableObject
{
    public string Name;
    public bool isEliteUnit;
    public List<AttackType> ATKType;
    public ATKInfo[] atkInfos;
    public bool areaATK;
    public int atkDuration;
    //public bool one_off;
    public int Health;
    public int KB; // Knockback
    public int Speed;
    public int Reload;
    public int DetectionRange;
    public int Cost;
    public int Cooldown;
    public bool UNITYAnimated;
    // Traits
    public Traits traits;
    public TraitSpecials traitSpecials;
    // Subtraits
    public SubTraits subtraits;
    // Career
    public Careers career;
    // Career Effects
    public AgainstCareer againstCareer;
    // Effects
    public DamageRelatedEffect DRE;
    public CharacterEffect[] characterEffects;
    public CharacterAbility[] abilities;
    public AttackTypeResistance[] atkTypeResis;
    public CharacterEffect[] effectResistances;

    public CharacterData Clone()
    {
        var clone = ScriptableObject.CreateInstance<CharacterData>();
        JsonUtility.FromJsonOverwrite(JsonUtility.ToJson(this), clone);
        return clone;
    }
}