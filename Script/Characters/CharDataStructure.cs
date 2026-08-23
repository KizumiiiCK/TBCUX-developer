using System.Collections;
using System.Collections.Generic;
using System.ComponentModel;
using System;
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
    private const long OverflowBase = (long)int.MaxValue + 1L; // 2,147,483,648
    public int level = 0;
    public int[] pro_stack=new int[maxlvl] { 0,0,0,0 };
    // 本地额外字段：每个进度槽的溢出次数（不改原有 pro_stack 结构）
    public int[] pro_overflow = new int[maxlvl] { 0, 0, 0, 0 };
    private static int[] Dx = new int[maxlvl] { 100, 50000000, 20000000, 300000 };

    public void AddProgress(int slot, int delta)
    {
        if (slot < 0 || slot >= maxlvl) return;
        if (delta <= 0) return;
        NormalizeProgress();

        int before = pro_stack[slot];
        int after = before + delta;
        if (after < 0)
        {
            // 按当前项目约束：单次增量不可能跨越多次 2^31，因此命中负数即记一次溢出。
            if (pro_overflow[slot] < int.MaxValue) pro_overflow[slot]++;
            long corrected = (long)after - int.MinValue; // [-2^31, -1] -> [0, 2^31-1]
            if (corrected < 0L) corrected = 0L;
            if (corrected > int.MaxValue) corrected = int.MaxValue;
            pro_stack[slot] = (int)corrected;
        }
        else
        {
            pro_stack[slot] = after;
        }
    }

    public long GetProgressLong(int slot)
    {
        if (slot < 0 || slot >= maxlvl) return 0L;
        NormalizeProgress();
        long overflow = Math.Max(0, (long)pro_overflow[slot]);
        long low = Math.Max(0, (long)pro_stack[slot]);
        return overflow * OverflowBase + low;
    }

    public long[] ToLongProgressArray()
    {
        NormalizeProgress();
        long[] arr = new long[maxlvl];
        for (int i = 0; i < maxlvl; i++) arr[i] = GetProgressLong(i);
        return arr;
    }

    public void LoadFromLongProgressArray(long[] values)
    {
        EnsureArrays();
        for (int i = 0; i < maxlvl; i++)
        {
            long value = (values != null && i < values.Length) ? values[i] : 0L;
            if (value < 0L) value = 0L;
            SetProgressLong(i, value);
        }
    }

    public bool NormalizeProgress()
    {
        bool changed = EnsureArrays();
        for (int i = 0; i < maxlvl; i++)
        {
            if (pro_overflow[i] < 0)
            {
                pro_overflow[i] = 0;
                changed = true;
            }

            if (pro_stack[i] < 0)
            {
                if (pro_overflow[i] < int.MaxValue) pro_overflow[i]++;
                long corrected = (long)pro_stack[i] - int.MinValue; // 历史负值视作一次溢出后的余数
                if (corrected < 0L) corrected = 0L;
                if (corrected > int.MaxValue) corrected = int.MaxValue;
                pro_stack[i] = (int)corrected;
                changed = true;
            }
        }
        return changed;
    }

    private bool EnsureArrays()
    {
        bool changed = false;
        if (pro_stack == null || pro_stack.Length != maxlvl)
        {
            int[] fixedStack = new int[maxlvl] { 0, 0, 0, 0 };
            if (pro_stack != null)
            {
                int copyLength = Mathf.Min(maxlvl, pro_stack.Length);
                for (int i = 0; i < copyLength; i++)
                {
                    fixedStack[i] = pro_stack[i];
                }
            }
            pro_stack = fixedStack;
            changed = true;
        }

        if (pro_overflow == null || pro_overflow.Length != maxlvl)
        {
            int[] fixedOverflow = new int[maxlvl] { 0, 0, 0, 0 };
            if (pro_overflow != null)
            {
                int copyLength = Mathf.Min(maxlvl, pro_overflow.Length);
                for (int i = 0; i < copyLength; i++)
                {
                    fixedOverflow[i] = pro_overflow[i];
                }
            }
            pro_overflow = fixedOverflow;
            changed = true;
        }
        return changed;
    }

    private void SetProgressLong(int slot, long value)
    {
        if (slot < 0 || slot >= maxlvl) return;
        if (value < 0L) value = 0L;

        long overflow = value / OverflowBase;
        long low = value % OverflowBase;
        if (overflow > int.MaxValue)
        {
            overflow = int.MaxValue;
            low = int.MaxValue;
        }

        pro_overflow[slot] = (int)overflow;
        pro_stack[slot] = (int)low;
    }

    public bool UpdateLevel()
    {
        NormalizeProgress();
        int newlevel = 0;
        for (int i = 0; i < maxlvl; i++)
        {
            if (GetProgressLong(i) < Dx[i]) break;
            newlevel++;
        }
        level = newlevel;
        return true;
        //if(newlevel>level) {level = newlevel;return true; }
        //return false;
    }
    public bool Compare(int lvl)
    {
        if (lvl < 0 || lvl >= maxlvl) return false;
        return GetProgressLong(lvl) >= Dx[lvl];
    }
}
public enum EffectName
{
    none=0, weaken=1, stop=2, slow=3, knockback=4, wrap=5, curse=6, toxic=7, lacerate=9, deathmark=10
}
public enum AttackType
{
    none=0, wave=1, surge=2, explosion=3,
    [EditorBrowsable(EditorBrowsableState.Never)] 
    critical, savage, zombieKiller, barrierBreaker, shieldPiercing, wave_invalid, invalid, effectBlocked, heal, baseCannon, friendly
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
    impatience=50, pressureLearn=51, targetHighestHp=52,
    selfSlow =100, selfWeaken=101, selfLacerate=102, selfDeathmark=103,
    BaseCharacter = 500,
    [EditorBrowsable(EditorBrowsableState.Never)]
    buff_defence = 23, buff_attack = 24, buff_speed = 25, buff_kb = 26, buff_costdown = 27, buff_recover = 28, buff_atkFreq = 29,
    Aux_MaxDMGBlock = 900, Aux_MinDMGBlock = 901, Aux_OneHit = 902, Aux_SelfDamage = 903, Aux_HealDamage = 904,
    invisible =999,
}
public enum KB_Type
{
    none,knockBack,pushBack,bossShock
}
