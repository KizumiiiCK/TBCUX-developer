using System.Collections;
using System.Collections.Generic;
using System.ComponentModel;
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
    public bool DoNotTriggerAbilities=false;
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
    public bool AggainstSupporter;
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
    none=0, weaken=1, stop=2, slow=3, knockback=4, wrap=5, curse=6, toxic=7, lacerate=9, deathmark=10
}
public enum AttackType
{
    none=0, wave=1, surge=2, explosion=3,
    [EditorBrowsable(EditorBrowsableState.Never)] 
    critical, savage, zombieKiller, barrierBreaker, sheildPiercing, wave_invalid, invalid, effectBlocked, heal, baseCannon, friendly
}
public enum AbilityName
{
    none=0,
    support=1,
    strategic=2,strengthen=3, survive=4, critical=5, zombieKiller=6, soulStrike=7, barrierBreaker=8, shieldPiercing=9, savage=10, 
    extraMoney=11, metal=12, miniWave=13, wave=14, wave_stop=15, miniSurge=16, surge=17, counter_surge=18, explosion=19, summoner=20, 
    shieldProvider=21, maxShield=22,
    practician=30, oneoff=31, ATK_Buffer=32, XP_PUNCH=33, sacrifice=34, projectile=35, ZombieDive=36, ZombieRevive=37, dodge=38, clearDebuffs=39, 
    barrier=40, akuShield=41, barrierProvider = 42,
    selfSlow =100, selfWeaken=101, selfLacerate=102, selfDeathmark=103,
    [EditorBrowsable(EditorBrowsableState.Never)]
    buff_defence = 23, buff_attack = 24, buff_speed = 25, buff_kb = 26, buff_costdown = 27, buff_recover = 28, buff_atkFreq = 29,
    Aux_MaxDMGBlock = 900, Aux_MinDMGBlock = 901, Aux_OneHit = 902,
    invisible =999,
}
public enum KB_Type
{
    none,knockBack,pushBack,bossShock
}
