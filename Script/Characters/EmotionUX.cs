using UnityEngine;

// Keep this enum synced with Resources/Effects/emo/<emotion_name>.
public enum EmotionUX
{
    none = 0,
    angry,
    call,
    doomed,
    flower1,
    flower2,
    great_shock,
    hurt,
    idea,
    impatient,
    melody1,
    melody2,
    pollen,
    putsu,
    query,
    shock1,
    shock2,
    shy,
    sigh,
    silent,
    sleepy,
    star,
    startled,
    stun
}

public enum EmotionBattleState
{
    walk = 0,
    idle = 1,
    attack = 2,
    kb = 3,
    other = 4
}
