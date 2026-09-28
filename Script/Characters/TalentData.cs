using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 本能（Talent）：猫咪达到 45 级后可选的进化方向。
/// 资源放在角色自己的目录里，和 upgrade（TotalUpgradeCost）同级，默认文件名 talent，
/// 即 Units/Cat Units/{rarity}/{code}/talent。
///
/// 这里只描述「在原本角色之上追加了什么」，所以没有血量/攻击/射程这些基础数值，
/// 全部字段都按叠加语义理解：
///   - Traits / SubTraits / Careers / AgainstCareer / DRE 这些 bool 组：勾上的项在原角色基础上「或」进去，
///     不勾的项不代表取消原有的，只代表本能不动它。
///   - characterEffects / abilities / atkTypeResis / effectResistances / ATKType：在原数组后面追加。
/// 具体怎么合并到 CharacterData 上由接入层决定，本文件只负责存数据。
/// </summary>
[CreateAssetMenu(fileName = "talent", menuName = "ScriptableObjects/Character Talent", order = 1)]
[System.Serializable]
public class TalentData : ScriptableObject
{
    // Traits
    public Traits traits = new Traits();
    // Subtraits
    public SubTraits subtraits = new SubTraits();
    // Career
    public Careers career = new Careers();
    // Career Effects
    public AgainstCareer againstCareer = new AgainstCareer();
    // Effects
    public DamageRelatedEffect DRE = new DamageRelatedEffect();
    public List<AttackType> ATKType = new List<AttackType>();
    public CharacterEffect[] characterEffects = new CharacterEffect[0];
    public CharacterAbility[] abilities = new CharacterAbility[0];
    public AttackTypeResistance[] atkTypeResis = new AttackTypeResistance[0];
    public CharacterEffect[] effectResistances = new CharacterEffect[0];

    /// <summary>
    /// 按角色编号取本能资源。本能挂在角色根目录（和 upgrade 同级），
    /// 既不分阶段也不带 -pN 后缀，所以只用编号前 4 位定位，变身前后拿到的是同一份。
    /// 没做本能的角色读不到，返回 null 当作没有。
    /// </summary>
    public static TalentData Load(string characterCode)
    {
        string address = ResolveAssetAddress(characterCode);
        return address == null ? null : BundledAddressables.LoadSync<TalentData>(address);
    }

    private static string ResolveAssetAddress(string characterCode)
    {
        if (string.IsNullOrEmpty(characterCode) || characterCode.Length < 4) return null;
        string id4 = characterCode.Substring(0, 4);
        return $"Units/Cat Units/{id4[0]}/{id4.Substring(1, 3)}/talent";
    }

    /// <summary>只问「这角色做没做本能」，不真的加载。地址解析结果是带缓存的，可以放心在 UI 刷新里调。</summary>
    public static bool Exists(string characterCode)
    {
        string address = ResolveAssetAddress(characterCode);
        return address != null && BundledAddressables.Exists(address, typeof(TalentData));
    }

    /// <summary>和 CharacterData.Clone 一样，走 Json 深拷贝，免得运行时改到资源本体。</summary>
    public TalentData Clone()
    {
        var clone = ScriptableObject.CreateInstance<TalentData>();
        JsonUtility.FromJsonOverwrite(JsonUtility.ToJson(this), clone);
        return clone;
    }

    /// <summary>
    /// 把本能叠加到一份角色数据上，就地修改。
    /// 传进来的必须是角色数据的副本（CharacterVisualLoader.LoadCharacterData 返回的已经是 Clone），
    /// 否则会污染 Addressables 缓存里的资源本体。
    ///
    /// 规则：
    ///  - 属性类（traits/subtraits/career/againstCareer/DRE）逐位取「或」，两边有一个为 true 就是 true。
    ///  - 列表类（effects/abilities/抗性）按 name（抗性按 type）对齐：原本没有的直接加，
    ///    原本就有的用本能这条整体覆盖掉。
    /// 写进去的都是新对象，本能资源自身不会被后续的运行时改动碰到。
    /// </summary>
    public void ApplyTo(CharacterData data)
    {
        if (data == null) return;

        ApplyTraits(data);
        ApplyATKTypes(data);
        data.characterEffects = MergeEffects(data.characterEffects, characterEffects);
        data.abilities = MergeAbilities(data.abilities, abilities);
        data.atkTypeResis = MergeAtkResistances(data.atkTypeResis, atkTypeResis);
        data.effectResistances = MergeEffects(data.effectResistances, effectResistances);
    }

    private void ApplyTraits(CharacterData data)
    {
        if (traits != null)
        {
            if (data.traits == null) data.traits = new Traits();
            data.traits.Red |= traits.Red;
            data.traits.Flt |= traits.Flt;
            data.traits.Blk |= traits.Blk;
            data.traits.Mtl |= traits.Mtl;
            data.traits.Ang |= traits.Ang;
            data.traits.Aln |= traits.Aln;
            data.traits.Z |= traits.Z;
            data.traits.Re |= traits.Re;
            data.traits.Aku |= traits.Aku;
            data.traits.None |= traits.None;
        }

        if (subtraits != null)
        {
            if (data.subtraits == null) data.subtraits = new SubTraits();
            data.subtraits.Starred |= subtraits.Starred;
            data.subtraits.Colossus |= subtraits.Colossus;
            data.subtraits.Behemoth |= subtraits.Behemoth;
            data.subtraits.Sage |= subtraits.Sage;
        }

        if (career != null)
        {
            if (data.career == null) data.career = new Careers();
            data.career.Warrior |= career.Warrior;
            data.career.Deffender |= career.Deffender;
            data.career.Magician |= career.Magician;
            data.career.Supporter |= career.Supporter;
            data.career.Practician |= career.Practician;
        }

        if (againstCareer != null)
        {
            if (data.againstCareer == null) data.againstCareer = new AgainstCareer();
            data.againstCareer.AggainstWarrior |= againstCareer.AggainstWarrior;
            data.againstCareer.AggainstMagician |= againstCareer.AggainstMagician;
            data.againstCareer.AggainstDeffender |= againstCareer.AggainstDeffender;
            data.againstCareer.AggainstSupporter |= againstCareer.AggainstSupporter;
            data.againstCareer.AggainstPractician |= againstCareer.AggainstPractician;
        }

        if (DRE != null)
        {
            if (data.DRE == null) data.DRE = new DamageRelatedEffect();
            data.DRE.massiveDamage |= DRE.massiveDamage;
            data.DRE.insaneDamage |= DRE.insaneDamage;
            data.DRE.tough |= DRE.tough;
            data.DRE.aegis |= DRE.aegis;
            data.DRE.strongAgainst |= DRE.strongAgainst;
        }
    }

    private void ApplyATKTypes(CharacterData data)
    {
        if (ATKType == null || ATKType.Count == 0) return;
        if (data.ATKType == null) data.ATKType = new List<AttackType>();
        for (int i = 0; i < ATKType.Count; i++)
        {
            if (!data.ATKType.Contains(ATKType[i])) data.ATKType.Add(ATKType[i]);
        }
    }

    private static CharacterEffect[] MergeEffects(CharacterEffect[] baseList, CharacterEffect[] extra)
    {
        if (extra == null || extra.Length == 0) return baseList ?? new CharacterEffect[0];

        List<CharacterEffect> result = new List<CharacterEffect>(baseList ?? new CharacterEffect[0]);
        for (int i = 0; i < extra.Length; i++)
        {
            CharacterEffect src = extra[i];
            if (src == null) continue;
            CharacterEffect copy = new CharacterEffect
            {
                name = src.name,
                probability = src.probability,
                duration = src.duration,
                intensity = src.intensity
            };
            int at = result.FindIndex(e => e != null && e.name == src.name);
            if (at < 0) { result.Add(copy); continue; }
            result[at] = copy;
            RemoveDuplicatesAfter(result, at, e => e.name == src.name);
        }
        return result.ToArray();
    }

    private static CharacterAbility[] MergeAbilities(CharacterAbility[] baseList, CharacterAbility[] extra)
    {
        if (extra == null || extra.Length == 0) return baseList ?? new CharacterAbility[0];

        List<CharacterAbility> result = new List<CharacterAbility>(baseList ?? new CharacterAbility[0]);
        for (int i = 0; i < extra.Length; i++)
        {
            CharacterAbility src = extra[i];
            if (src == null) continue;
            CharacterAbility copy = new CharacterAbility
            {
                name = src.name,
                probability = src.probability,
                duration = src.duration,
                intensity = src.intensity
            };
            int at = result.FindIndex(a => a != null && a.name == src.name);
            if (at < 0) { result.Add(copy); continue; }
            result[at] = copy;
            RemoveDuplicatesAfter(result, at, a => a.name == src.name);
        }
        return result.ToArray();
    }

    private static AttackTypeResistance[] MergeAtkResistances(AttackTypeResistance[] baseList, AttackTypeResistance[] extra)
    {
        if (extra == null || extra.Length == 0) return baseList ?? new AttackTypeResistance[0];

        List<AttackTypeResistance> result = new List<AttackTypeResistance>(baseList ?? new AttackTypeResistance[0]);
        for (int i = 0; i < extra.Length; i++)
        {
            AttackTypeResistance src = extra[i];
            if (src == null) continue;
            AttackTypeResistance copy = new AttackTypeResistance { type = src.type, intensity = src.intensity };
            int at = result.FindIndex(r => r != null && r.type == src.type);
            if (at < 0) { result.Add(copy); continue; }
            result[at] = copy;
            RemoveDuplicatesAfter(result, at, r => r.type == src.type);
        }
        return result.ToArray();
    }

    /// <summary>
    /// 覆盖掉第一条同名项之后，把原数据里剩下的同名项一并删掉。
    /// 不删的话，「本能覆盖了 wave」会变成「本能的 wave + 原本第二条 wave」，等于没覆盖干净。
    /// </summary>
    private static void RemoveDuplicatesAfter<T>(List<T> list, int keepIndex, System.Predicate<T> sameKey) where T : class
    {
        for (int i = list.Count - 1; i > keepIndex; i--)
        {
            if (list[i] != null && sameKey(list[i])) list.RemoveAt(i);
        }
    }
}
