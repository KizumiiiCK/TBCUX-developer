using System.Collections;
using System.Collections.Generic;
using UnityEngine;

[System.Serializable]
public class Traits
{
    public bool Red;
    public bool Flt;
    public bool Blk;
    public bool Mtl;
    public bool Ang;
    public bool Aln;
    public bool Z;
    public bool Re;
    public bool Aku;
    public bool None;
}
[System.Serializable]
public class TraitSpecials
{
    public int barrierValue = 0;
    public int akuSheildValue = 0;
    public int zombieReviveTimes = 0;
    public int zombieDiveTime = 0;
    public int revivePercentage = 0;
}
[System.Serializable]
public class SubTraits
{
    public bool Starred;
    public bool Colossus;
    public bool Behemoth;
    public bool Sage;
}
[System.Serializable]
public class Careers
{
    public bool Warrior;
    public bool Deffender;
    public bool Magician;
    public bool Supporter;
    public bool Practician;
}
[System.Serializable]
public class ATKInfo
{
    public float ATK;
    public int frame;
    public bool DoNotTriggerEffects=false;
    public bool Friendly=false;
    public Vector2 ATKRange;
}// This class stores all the information of a single attack.
[System.Serializable]
public class DamageRelatedEffect
{
    public bool massiveDamage;
    public bool insaneDamage;
    public bool tough;
    public bool aegis;
    public bool strongAgainst;
}// This class stores the effects that affect the damage dealt.
[System.Serializable]
public class AgainstCareer
{
    public bool AggainstWarrior;
    public bool AggainstMagician;
    public bool AggainstDeffender;
    public bool AggainstSuppoter;
    public bool AggainstPractician;
}
[System.Serializable]
public class CharacterEffect
{
    public EffectName name;
    public int probability = 0;
    public int duration = 0;
    public int intensity = 0;
}// This class stores the effects that can inflict on the characters. These effects are able to be cursed.
[System.Serializable]
public class CharacterAbility
{
    public AbilityName name;
    public int probability = 0;
    public int duration = 0;
    public int intensity = 0;
}// This class stores the abilities that affect the passive skills of the characters. These effects are not going to be cursed.
[System.Serializable]
public class AttackTypeResistance
{
    public AttackType type;
    public int intensity = 0;
}// This class stores the attack types in the game. Mainly used to block certain type of damage.
[System.Serializable]
public class CharacterProficiency
{
    private const int maxlvl = 4;
    public int level = 0;
    public int[] pro_stack=new int[maxlvl] { 0,0,0,0 };
    private static int[] Dx = new int[maxlvl] { 100, 50000000, 20000000, 300000 };
    public bool UpdateLevel()
    {
        int newlevel = 0;
        for (int i = 0; i < maxlvl; i++)
        {
            if (pro_stack[i] < Dx[i]) break;
            newlevel++;
        }
        level = newlevel;
        return true;
        //if(newlevel>level) {level = newlevel;return true; }
        //return false;
    }
    public bool Compare(int lvl) => pro_stack[lvl] >= Dx[lvl];
}
public enum EffectName
{
    none, weaken, stop, slow, knockback, wrap, curse, toxic, dodge, lacerate, deathmark
}
public enum AttackType
{
    none, wave, surge, explosion, critical, savage, zombieKiller, barrierBreaker, sheildPiercing, wave_invalid, invalid, effectBlocked, heal, baseCannon
}
public enum AbilityName
{
    none, support, strategic, strengthen, survive, critical, zombieKiller, soulStrike, barrierBreaker, shieldPiercing, savage, extraMoney, metal, miniWave, wave, wave_stop, miniSurge, surge, counter_surge, explosion, summoner, shieldProvider, maxShield,
    buff_defence, buff_attack, buff_speed, buff_kb, buff_costdown, buff_recover, buff_atkFreq, practician, oneoff, ATK_Buffer, XP_PUNCH, sacrifice, projectile, ZombieDive, BaseHunter
}
public enum KB_Type
{
    none,knockBack,pushBack,bossShock
}
