using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class EffectInstaller : MonoBehaviour
{
    private const int BlockedEffectLifetime = 30;
    private const int InvalidEffectLifetime = 30;
    private const int HealEffectLifetime = 60;
    private const int StrengthenEffectLifetime = int.MaxValue;
    private const int SurviveEffectLifetime = 30;

    public static void Inflict(GameObject target, object effectName, float duration, float intensity)
    {
        int _duration = Mathf.FloorToInt(duration);
        int _intensity = Mathf.FloorToInt(intensity);
        Character character = target != null ? target.GetComponent<Character>() : null;
        if (_duration == 0)
        {
            SpawnAttachedEffect(character, "effect_blocked", BlockedEffectLifetime);
            return;
        }
        switch (effectName)
        {
            case EffectName.weaken:
                Weaken wkn;
                if (target.GetComponent<Weaken>() != null) wkn = target.GetComponent<Weaken>();
                else wkn = target.AddComponent<Weaken>();
                wkn.duration = _duration;
                wkn.SetIntensity(_intensity);
                break;
            case EffectName.stop:
                Stop stp;
                if (target.GetComponent<Stop>() != null) stp = target.GetComponent<Stop>();
                else stp = target.AddComponent<Stop>();
                stp.duration = _duration;
                break;
            case EffectName.slow:
                Slow slw;
                if (target.GetComponent<Slow>() != null) slw = target.GetComponent<Slow>();
                else slw = target.AddComponent<Slow>();
                slw.duration = _duration;
                break;
            case EffectName.toxic:
                Toxic txc;
                if (target.GetComponent<Toxic>() != null) txc = target.GetComponent<Toxic>();
                else txc = target.AddComponent<Toxic>();
                txc.duration = _duration;
                break;
            case EffectName.knockback:
                target.GetComponent<Character>().StartKBCoroutine(KB_Type.knockBack, 240 * duration);
                break;
            case EffectName.wrap:
                Wrap wrp;
                if (target.GetComponent<Wrap>() != null)
                {
                    wrp = target.GetComponent<Wrap>();
                    wrp.duration = _duration;
                    wrp.intensity = _intensity;
                }
                break;
            case EffectName.curse:
                Curse crs;
                if (target.GetComponent<Curse>() != null) crs = target.GetComponent<Curse>();
                else crs = target.AddComponent<Curse>();
                crs.duration = _duration;
                break;
            case EffectName.lacerate:
                Lacerate lac;
                if (target.GetComponent<Lacerate>() != null) lac = target.GetComponent<Lacerate>();
                else lac = target.AddComponent<Lacerate>();
                lac.duration = _duration;
                break;
            case EffectName.deathmark:
                DeathMark dmk;
                if (target.GetComponent<DeathMark>() != null) dmk = target.GetComponent<DeathMark>();
                else dmk = target.AddComponent<DeathMark>();
                dmk.duration = _duration;
                dmk.intensity = _intensity;
                break;
            //
            case AttackType.invalid:
                SpawnAttachedEffect(character, "invalid", InvalidEffectLifetime);
                break;
            case AttackType.heal:
                string healEffect = character != null && character.IsCat() ? "heal" : "heal_e";
                SpawnAttachedEffect(character, healEffect, HealEffectLifetime);
                break;
            case AbilityName.strengthen:
                string strengthenEffect = character != null && character.IsCat() ? "strengthen" : "strengthen_e";
                SpawnAttachedEffect(character, strengthenEffect, StrengthenEffectLifetime);
                break;
            case AbilityName.survive:
                SpawnAttachedEffect(character, "survive", SurviveEffectLifetime);
                break;
            default: break;
        }
    }

    private static void SpawnAttachedEffect(Character character, string effectName, int lifetimeFrames)
    {
        if (character == null || character.EM == null) return;
        character.EM.InstantiateAttachedBattleObject(
            effectName,
            character.transform.position,
            character.transform,
            worldPositionStays: true,
            playSound: true,
            lifetimeFrames: lifetimeFrames);
    }
    //public void DisplayEffect(GameObject target, string effname)
    //{

    //}
}
