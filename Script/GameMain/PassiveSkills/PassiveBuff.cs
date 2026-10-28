using UnityEngine;

/// <summary>
/// 本能常驻数值。加减打在 CharacterData 上，图鉴 / 部署花费 / 出场 LoadCharacterData 共用同一份。
/// OnAddingAbility 不再乘一次，否则会和 TalentData.ApplyTo 叠两遍。
/// 描述里的 {0} 走 ability.probability。
/// </summary>
public abstract class TalentStatBuff : PassiveSkill
{
    public static void ApplyAll(CharacterData data, CharacterAbility[] abilities)
    {
        if (data == null || abilities == null) return;
        for (int i = 0; i < abilities.Length; i++)
        {
            CharacterAbility a = abilities[i];
            if (a == null) continue;
            ApplyOne(data, a.name, a.probability);
        }
    }

    public static void ApplyOne(CharacterData data, AbilityName name, int amount)
    {
        switch (name)
        {
            case AbilityName.buff_defence: BuffDefence.Apply(data, amount); break;
            case AbilityName.buff_attack: BuffAttack.Apply(data, amount); break;
            case AbilityName.buff_speed: BuffSpeed.Apply(data, amount); break;
            case AbilityName.buff_kb: BuffKb.Apply(data, amount); break;
            case AbilityName.buff_costdown: BuffCostDown.Apply(data, amount); break;
            case AbilityName.buff_recover: BuffRecover.Apply(data, amount); break;
            case AbilityName.buff_atkFreq: BuffAtkFreq.Apply(data, amount); break;
        }
    }

    protected static int ScaleUp(int value, int percent) =>
        Mathf.Max(1, Mathf.RoundToInt(value * (100 + percent) / 100f));

    protected static float ScaleUp(float value, int percent) =>
        value * (100 + percent) / 100f;

    protected static int ScaleDown(int value, int percent) =>
        Mathf.Max(1, Mathf.RoundToInt(value * Mathf.Max(0, 100 - percent) / 100f));
}

public class BuffDefence : TalentStatBuff
{
    public static void Apply(CharacterData data, int amount)
    {
        if (data == null || amount == 0) return;
        data.Health = ScaleUp(data.Health, amount);
    }
}

public class BuffAttack : TalentStatBuff
{
    public static void Apply(CharacterData data, int amount)
    {
        if (data == null || amount == 0 || data.atkInfos == null) return;
        for (int i = 0; i < data.atkInfos.Length; i++)
        {
            if (data.atkInfos[i] == null) continue;
            data.atkInfos[i].ATK = ScaleUp(data.atkInfos[i].ATK, amount);
        }
    }
}

public class BuffSpeed : TalentStatBuff
{
    public static void Apply(CharacterData data, int amount)
    {
        if (data == null || amount == 0) return;
        data.Speed = ScaleUp(data.Speed, amount);
    }
}

public class BuffKb : TalentStatBuff
{
    public static void Apply(CharacterData data, int amount)
    {
        if (data == null || amount == 0) return;
        data.KB = Mathf.Max(1, data.KB + amount);
    }
}

public class BuffCostDown : TalentStatBuff
{
    public static void Apply(CharacterData data, int amount)
    {
        if (data == null || amount == 0) return;
        data.Cost = ScaleDown(data.Cost, amount);
    }
}

public class BuffRecover : TalentStatBuff
{
    public static void Apply(CharacterData data, int amount)
    {
        if (data == null || amount == 0) return;
        data.Cooldown = ScaleDown(data.Cooldown, amount);
    }
}

public class BuffAtkFreq : TalentStatBuff
{
    public static void Apply(CharacterData data, int amount)
    {
        if (data == null || amount == 0) return;
        data.Reload = ScaleDown(data.Reload, amount);
    }
}
