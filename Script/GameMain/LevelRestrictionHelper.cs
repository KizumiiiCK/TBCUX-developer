using System;
using System.Collections.Generic;
using UnityEngine;

public static class LevelRestrictionHelper
{
    public delegate void RestrictionParser(RestrictionRules rules, string value);

    private const int NoLimit = -1;
    public const int ForcedSlotCount = 13;
    public const int GuestSlotStart = 10;
    public const int GuestSlotCount = 3;
    private static readonly HashSet<string> CaseSensitiveRestrictionKeys =
        new HashSet<string>(StringComparer.Ordinal)
        {
            "s+", "s-", "mm", "oh", "hd", "hD", "ht", "sd", "sD", "zr", "fs"
        };
    private static readonly Dictionary<string, RestrictionParser> ParserMap =
        new Dictionary<string, RestrictionParser>(StringComparer.Ordinal)
        {
            { "R+", ParseAllowRarity },
            { "R-", ParseDenyRarity },
            { "U+", ParseRequiredUnit },
            { "U-", ParseDenyUnit },
            { "CC", ParseMaxCatCount },
            { "LC", ParseMaxCatLevel },
            { "MM", ParseInitialMoneyLevel },
            { "mm", ParseInitialMoneyAmount },
            { "P+", ParseRestrictionValue },
            { "P-", ParseRestrictionValue },
            { "D+", ParseRestrictionValue },
            { "D-", ParseRestrictionValue },
            { "ES", ParseRestrictionValue },
            { "IV", ParseRestrictionValue },
            { "OH", ParseRestrictionValue },
            { "oh", ParseRestrictionValue },
            { "S+", ParseSurgeRestrictionValue },
            { "S-", ParseSurgeRestrictionValue },
            { "s+", ParseSurgeRestrictionValue },
            { "s-", ParseSurgeRestrictionValue },
            { "hd", ParseRestrictionValue },
            { "hD", ParseRestrictionValue },
            { "ht", ParseRestrictionValue },
            { "sd", ParseRestrictionValue },
            { "sD", ParseRestrictionValue },
            { "zr", ParseZombieReviveRestrictionValue },
            { "FS", ParseForcedAllSlots },
            { "fs", ParseForcedGuestSlots }
        };

    public class RestrictionRules
    {
        public readonly HashSet<int> allowRarities = new HashSet<int>();
        public readonly HashSet<int> denyRarities = new HashSet<int>();
        public readonly HashSet<string> requiredUnits = new HashSet<string>();
        public readonly HashSet<string> denyUnits = new HashSet<string>();
        public readonly Dictionary<string, List<string>> rawValuesByKey =
            new Dictionary<string, List<string>>(StringComparer.Ordinal);

        public bool hasAllowRarity;
        public bool hasForcedSlots;
        public readonly bool[] forcedSlotActive = new bool[ForcedSlotCount];
        public readonly string[] forcedSlotCodes = CreateEmptyForcedSlots();
        public readonly int[] forcedSlotLevels = CreateEmptyForcedLevels();
        public int maxCatCount = NoLimit;
        public int maxCatLevel = NoLimit;
        public int initialMoneyLevel = NoLimit;
        public int initialMoneyAmount = NoLimit;
        public float unitCostMultiplier = 1f;

        public void AddRawValue(string key, string value)
        {
            if (!rawValuesByKey.TryGetValue(key, out List<string> values))
            {
                values = new List<string>();
                rawValuesByKey.Add(key, values);
            }
            values.Add(value);
        }
    }

    public static void RegisterParser(string key, RestrictionParser parser)
    {
        if (string.IsNullOrWhiteSpace(key) || parser == null) return;
        ParserMap[NormalizeRestrictionKey(key)] = parser;
    }

    public static RestrictionRules Parse(string[] restrictions)
    {
        RestrictionRules rules = new RestrictionRules();
        if (restrictions == null) return rules;

        for (int i = 0; i < restrictions.Length; i++)
        {
            if (!TrySplitRestriction(restrictions[i], out string key, out string value)) continue;

            rules.AddRawValue(key, value);
            if (ParserMap.TryGetValue(key, out RestrictionParser parser))
            {
                parser(rules, value);
            }
        }

        return rules;
    }

    /// <summary>Returns true when the stage contains an IV restriction.</summary>
    public static bool HasIvRestriction(string[] restrictions)
    {
        if (restrictions == null) return false;
        return Parse(restrictions).rawValuesByKey.ContainsKey("IV");
    }

    /// <summary>
    /// Checks whether a unit can appear in the deploy list.
    /// Cost-based restrictions can optionally be evaluated with the provided CharacterData.
    /// </summary>
    public static bool IsUnitAllowed(RestrictionRules rules, string code, CharacterData data = null)
    {
        if (rules == null || string.IsNullOrEmpty(code)) return true;
        if (IsForcedLineupCode(rules, code)) return true;
        if (!CharacterPlacer.TryParse(code, true, out UnitIdentity identity) || !identity.IsValid) return true;
        if (identity.IsOpposite || !identity.AssetIsCat || identity.CharacterCode.Length < 4) return true;
        string code4 = identity.CharacterCode.Substring(0, 4);
        if (!int.TryParse(code4.Substring(0, 1), out int rarity)) return true;

        if (rules.hasAllowRarity && !rules.allowRarities.Contains(rarity)) return false;
        if (rules.denyRarities.Contains(rarity)) return false;
        if (rules.denyUnits.Contains(code4)) return false;

        // Evaluate P+/P- even when caller does not pass CharacterData.
        CharacterData resolvedData = data ?? TryLoadCharacterDataByCode(code);
        if (resolvedData != null && !IsUnitCostAllowed(rules, resolvedData.Cost)) return false;

        return true;
    }

    public static bool AreRequiredUnitsSelected(RestrictionRules rules, string[] selectedCodes)
    {
        if (rules == null || rules.requiredUnits.Count == 0) return true;
        if (selectedCodes == null || selectedCodes.Length == 0) return false;

        HashSet<string> selectedUnits = new HashSet<string>();
        for (int i = 0; i < selectedCodes.Length; i++)
        {
            string code = selectedCodes[i];
            if (!CharacterPlacer.TryParse(code, true, out UnitIdentity identity) || !identity.IsValid) continue;
            if (identity.IsOpposite || !identity.AssetIsCat || identity.CharacterCode.Length < 4) continue;
            selectedUnits.Add(identity.CharacterCode.Substring(0, 4));
        }

        foreach (string requiredUnit in rules.requiredUnits)
        {
            if (!selectedUnits.Contains(requiredUnit)) return false;
        }

        return true;
    }

    public static bool HasForcedSlots(RestrictionRules rules)
    {
        return rules != null && rules.hasForcedSlots;
    }

    public static bool HasForcedGuestSlots(RestrictionRules rules)
    {
        if (!HasForcedSlots(rules)) return false;
        for (int i = 0; i < GuestSlotCount; i++)
        {
            if (IsSlotForced(rules, GuestSlotStart + i)) return true;
        }
        return false;
    }

    public static bool IsSlotForced(RestrictionRules rules, int slotIndex)
    {
        if (rules == null || rules.forcedSlotActive == null) return false;
        if (slotIndex < 0 || slotIndex >= ForcedSlotCount) return false;
        return rules.forcedSlotActive[slotIndex];
    }

    public static bool TryApplyForcedSlots(RestrictionRules rules, ref string[] selectedCodes)
    {
        if (!HasForcedSlots(rules) || rules.forcedSlotCodes == null) return false;
        selectedCodes = EnsureForcedSlotArray(selectedCodes);
        for (int i = 0; i < ForcedSlotCount; i++)
        {
            if (!IsSlotForced(rules, i)) continue;
            selectedCodes[i] = rules.forcedSlotCodes[i] ?? string.Empty;
        }
        return true;
    }

    public static bool TryAssignMatchingForcedSlots(RestrictionRules rules, string unit4, string[] selectedCodes)
    {
        if (!HasForcedSlots(rules) || selectedCodes == null || string.IsNullOrEmpty(unit4)) return false;
        bool placed = false;
        int limit = selectedCodes.Length < ForcedSlotCount ? selectedCodes.Length : ForcedSlotCount;
        for (int i = 0; i < limit; i++)
        {
            if (!ForcedSlotMatchesUnit(rules, i, unit4)) continue;
            selectedCodes[i] = rules.forcedSlotCodes[i] ?? string.Empty;
            placed = true;
        }
        return placed;
    }

    public static bool AreForcedSlotsSatisfied(RestrictionRules rules, string[] selectedCodes)
    {
        if (!HasForcedSlots(rules)) return true;
        for (int i = 0; i < ForcedSlotCount; i++)
        {
            string selected = selectedCodes != null && i < selectedCodes.Length ? selectedCodes[i] : null;
            if (!IsForcedSlotSatisfied(rules, i, selected)) return false;
        }
        return true;
    }

    public static bool IsForcedSlotSatisfied(RestrictionRules rules, int slotIndex, string selectedCode)
    {
        if (!IsSlotForced(rules, slotIndex)) return true;
        string forced = rules.forcedSlotCodes[slotIndex];
        bool forcedEmpty = string.IsNullOrEmpty(forced);
        bool selectedEmpty = string.IsNullOrEmpty(selectedCode);
        if (forcedEmpty) return selectedEmpty;
        if (selectedEmpty) return false;
        return selectedCode == forced;
    }

    public static bool ForcedSlotMatchesUnit(RestrictionRules rules, int slotIndex, string unit4)
    {
        if (!IsSlotForced(rules, slotIndex) || string.IsNullOrEmpty(unit4) || unit4.Length < 4) return false;
        string forced = rules.forcedSlotCodes[slotIndex];
        if (string.IsNullOrEmpty(forced)) return false;
        if (!CharacterPlacer.TryParse(forced, true, out UnitIdentity identity) || !identity.IsValid) return false;
        if (!identity.AssetIsCat || identity.CharacterCode.Length < 4) return false;
        return identity.CharacterCode.Substring(0, 4) == unit4.Substring(0, 4);
    }

    public static bool TryGetForcedSlotLevel(RestrictionRules rules, int slotIndex, out int level)
    {
        level = 0;
        if (!IsSlotForced(rules, slotIndex) || rules.forcedSlotCodes == null || rules.forcedSlotLevels == null) return false;
        if (string.IsNullOrEmpty(rules.forcedSlotCodes[slotIndex])) return false;
        level = rules.forcedSlotLevels[slotIndex];
        return level >= 1;
    }

    public static int GetMaxCatDeploy(RestrictionRules rules, int fallback)
    {
        if (rules == null || rules.maxCatCount < 0) return fallback;
        return rules.maxCatCount;
    }

    public static int ApplyUnitLevelCap(RestrictionRules rules, int level)
    {
        if (level < 1) level = 1;
        if (rules == null || rules.maxCatLevel < 1) return level;
        return Mathf.Min(level, rules.maxCatLevel);
    }

    public static float GetUnitCostMultiplier(RestrictionRules rules, string code)
    {
        if (rules == null) return 1f;
        return Mathf.Max(0f, rules.unitCostMultiplier);
    }

    public static int GetInitialMoneyLevel(RestrictionRules rules, int fallback)
    {
        if (rules == null || rules.initialMoneyLevel < 1) return fallback;
        return rules.initialMoneyLevel;
    }

    public static int GetInitialMoney(RestrictionRules rules, int fallback)
    {
        if (rules == null || rules.initialMoneyAmount < 0) return fallback;
        return rules.initialMoneyAmount;
    }

    public static int ApplyUnitCostMultiplier(int cost, float multiplier)
    {
        if (cost <= 0) return 0;
        if (multiplier <= 0f) return 0;
        return Mathf.CeilToInt(cost * multiplier);
    }

    /// <summary>
    /// Applies stage restrictions to a deployer and locks it when needed.
    /// </summary>
    public static void ApplyToDeployer(UnitDeployer deployer, string code, RestrictionRules rules, bool isGuest, bool lockAllUnits)
    {
        if (deployer == null) return;
        ApplyCatCharacterDataRestrictions(rules, deployer.GetCharacterData());
        if (lockAllUnits || !IsUnitAllowed(rules, code))
        {
            deployer.LockByRestriction();
            return;
        }

        if (isGuest)
        {
            deployer.ResetCoolDown();
            deployer.GuestMark();
        }
    }

    #region Character Data Restriction Application

    public static void ApplyCatCharacterDataRestrictions(RestrictionRules rules, CharacterData data)
    {
        ApplyCharacterDataRestrictions(rules, data, true);
    }

    /// <summary>
    /// Applies stage-driven combat modifiers to enemy CharacterData before runtime setup.
    /// </summary>
    public static void ApplyEnemyCharacterDataRestrictions(RestrictionRules rules, CharacterData data)
    {
        ApplyCharacterDataRestrictions(rules, data, false);
    }

    private static void ApplyCharacterDataRestrictions(RestrictionRules rules, CharacterData data, bool isCatTeam)
    {
        if (rules == null || data == null || rules.rawValuesByKey.Count == 0) return;

        foreach (KeyValuePair<string, List<string>> entry in rules.rawValuesByKey)
        {
            switch (entry.Key)
            {
                case "S+":
                    if (isCatTeam) ApplySurgeRestriction(data, entry.Value, AbilityName.surge);
                    break;
                case "s+":
                    if (isCatTeam) ApplySurgeRestriction(data, entry.Value, AbilityName.miniSurge);
                    break;
                case "D-":
                    if (!isCatTeam) ApplyDamageBlockRestriction(data, entry.Value, true);
                    break;
                case "D+":
                    if (!isCatTeam) ApplyDamageBlockRestriction(data, entry.Value, false);
                    break;
                case "IV":
                    if (!isCatTeam) ApplyInvisibleShowRestriction(data);
                    break;
                case "OH":
                    if (isCatTeam) ApplyOneHitRestriction(data);
                    break;
                case "oh":
                    if (!isCatTeam) ApplyOneHitRestriction(data);
                    break;
                case "ES":
                    if (!isCatTeam) ApplyEnemyStrengthenRestriction(data, entry.Value);
                    break;
                case "S-":
                    if (!isCatTeam) ApplySurgeRestriction(data, entry.Value, AbilityName.surge);
                    break;
                case "s-":
                    if (!isCatTeam) ApplySurgeRestriction(data, entry.Value, AbilityName.miniSurge);
                    break;
                case "hd":
                    if (isCatTeam) ApplyHealDamageRestriction(data, entry.Value, false);
                    break;
                case "hD":
                    if (isCatTeam) ApplyHealDamageRestriction(data, entry.Value, true);
                    break;
                case "ht":
                    if (isCatTeam) ApplyHealBuffRestriction(data, entry.Value);
                    break;
                case "sd":
                    if (isCatTeam) ApplyEffectDurationRestriction(data, entry.Value);
                    break;
                case "sD":
                    if (!isCatTeam) ApplyEffectDurationRestriction(data, entry.Value);
                    break;
                case "zr":
                    if (!isCatTeam) ApplyZombieReviveRestriction(data, entry.Value);
                    break;
            }
        }
    }

    /// <summary>
    /// Applies D- or D+ as a guaranteed damage clamp ability.
    /// </summary>
    private static void ApplyDamageBlockRestriction(CharacterData data, List<string> values, bool useMinimumValue)
    {
        int? targetValue = useMinimumValue
            ? GetExtremeParsedValue(values, true)
            : GetExtremeParsedValue(values, false);
        if (!targetValue.HasValue) return;

        AbilityName abilityName = useMinimumValue ? AbilityName.Aux_MaxDMGBlock : AbilityName.Aux_MinDMGBlock;
        if (TryGetAbility(data, abilityName, out CharacterAbility ability))
        {
            ability.probability = 100;
            ability.duration = 0;
            ability.intensity = targetValue.Value;
            return;
        }

        AddAbilityToData(data, new CharacterAbility
        {
            name = abilityName,
            probability = 100,
            duration = 0,
            intensity = targetValue.Value
        });
    }

    /// <summary>
    /// Adds a reveal ability when the unit does not already have one.
    /// </summary>
    private static void ApplyInvisibleShowRestriction(CharacterData data)
    {
        if (HasAbility(data, AbilityName.invisible)) return;

        AddAbilityToData(data, new CharacterAbility
        {
            name = AbilityName.invisible,
            probability = 0,
            duration = 0,
            intensity = 0
        });
    }

    /// <summary>
    /// Adds the one-hit gag ability to cat units when OH is active.
    /// </summary>
    private static void ApplyOneHitRestriction(CharacterData data)
    {
        if (HasAbility(data, AbilityName.Aux_OneHit)) return;

        AddAbilityToData(data, new CharacterAbility
        {
            name = AbilityName.Aux_OneHit,
            probability = 100,
            duration = 0,
            intensity = 0
        });
    }

    /// <summary>
    /// hd / hD：本关猫咪攻击仅对自己造成等量伤害；每次治愈（负伤害攻击）对敌方造成直接伤害。
    /// groupWide=true（hD）为全体敌人（含基地）伤害；false（hd）为最前方单体伤害。
    /// 伤害数值取所配置的最大值。
    /// </summary>
    private static void ApplyHealDamageRestriction(CharacterData data, List<string> values, bool groupWide)
    {
        int? damage = GetExtremeParsedValue(values, false); // 取最大值
        if (!damage.HasValue || damage.Value <= 0) return;

        // 自伤能力：普通攻击只对自己造成等量真实伤害。
        if (!HasAbility(data, AbilityName.Aux_SelfDamage))
        {
            AddAbilityToData(data, new CharacterAbility
            {
                name = AbilityName.Aux_SelfDamage,
                probability = 0,
                duration = 0,
                intensity = 0
            });
        }

        // 治愈伤害能力：probability>0 表示群体，intensity 为伤害值。
        AddAbilityToData(data, new CharacterAbility
        {
            name = AbilityName.Aux_HealDamage,
            probability = groupWide ? 1 : 0,
            duration = 0,
            intensity = damage.Value
        });
    }

    /// <summary>
    /// ht：每当猫咪被治愈（拥有负伤害的攻击段）时，永久获得 {value}% 的伤害提升。
    /// 通过给拥有治愈攻击段的角色添加 ATK_Buffer（intensity=100+value），
    /// 并把其它正伤害攻击段设为 DoNotTriggerAbilities，避免普通攻击也触发增益。
    /// </summary>
    private static void ApplyHealBuffRestriction(CharacterData data, List<string> values)
    {
        if (data == null || data.atkInfos == null || data.atkInfos.Length == 0) return;

        int? buff = GetExtremeParsedValue(values, false); // 取最大值
        if (!buff.HasValue || buff.Value <= 0) return;

        bool hasHealAttack = false;
        for (int i = 0; i < data.atkInfos.Length; i++)
        {
            ATKInfo info = data.atkInfos[i];
            if (info == null) continue;
            if (info.ATK < 0f) hasHealAttack = true;      // 治愈攻击段
            else info.DoNotTriggerAbilities = true;        // 正伤害攻击段不触发能力
        }

        if (!hasHealAttack) return; // 没有治愈能力则不生效

        if (TryGetAbility(data, AbilityName.ATK_Buffer, out CharacterAbility existing))
        {
            existing.intensity = 100 + buff.Value;
            return;
        }

        AddAbilityToData(data, new CharacterAbility
        {
            name = AbilityName.ATK_Buffer,
            probability = 0,
            duration = 0,
            intensity = 100 + buff.Value
        });
    }

    /// <summary>
    /// Applies ES by keeping the highest valid strengthen intensity.
    /// </summary>
    private static void ApplyEnemyStrengthenRestriction(CharacterData data, List<string> values)
    {
        int maxIntensity = 0;
        for (int i = 0; i < values.Count; i++)
        {
            if (int.TryParse(values[i], out int parsed) && parsed > maxIntensity)
            {
                maxIntensity = parsed;
            }
        }

        if (maxIntensity <= 0) return;

        if (TryGetAbility(data, AbilityName.strengthen, out CharacterAbility existing))
        {
            existing.intensity = maxIntensity;
            existing.probability = 50;
            existing.duration = 0;
            return;
        }

        AddAbilityToData(data, new CharacterAbility
        {
            name = AbilityName.strengthen,
            probability = 50,
            duration = 0,
            intensity = maxIntensity
        });
    }

    /// <summary>
    /// Adds one independent surge ability for each valid surge rule.
    /// Existing surge abilities are left untouched so effects can stack.
    /// </summary>
    private static void ApplySurgeRestriction(CharacterData data, List<string> values, AbilityName abilityName)
    {
        if (data == null || values == null || values.Count == 0) return;
        for (int i = 0; i < values.Count; i++)
        {
            if (!TryParseSurgeRestrictionValue(values[i], out int probability, out int duration)) continue;
            AddAbilityToData(data, new CharacterAbility
            {
                name = abilityName,
                probability = probability,
                duration = duration,
                intensity = Mathf.Max(1, data.DetectionRange)
            });
        }
    }

    /// <summary>
    /// sd / sD：将该阵营角色 data.characterEffects 的 duration 乘以 n%。击退不改。
    /// </summary>
    private static void ApplyEffectDurationRestriction(CharacterData data, List<string> values)
    {
        int? percent = GetExtremeParsedValue(values, true);
        if (!percent.HasValue || percent.Value < 0) return;
        if (data.characterEffects == null || data.characterEffects.Length == 0) return;

        for (int i = 0; i < data.characterEffects.Length; i++)
        {
            CharacterEffect effect = data.characterEffects[i];
            if (effect == null) continue;
            if (effect.name == EffectName.knockback) continue;
            effect.duration = Mathf.Max(0, Mathf.RoundToInt(effect.duration * percent.Value / 100f));
        }
    }

    /// <summary>
    /// zr：全体敌方获得 ZombieRevive。probability=复活次数，intensity=复活生命%，duration=间隔帧。
    /// </summary>
    private static void ApplyZombieReviveRestriction(CharacterData data, List<string> values)
    {
        if (data == null || values == null) return;

        int times = 0;
        int hpPercent = 0;
        int intervalFrames = 0;
        bool found = false;
        for (int i = 0; i < values.Count; i++)
        {
            if (!TryParseZombieReviveRestrictionValue(values[i], out int parsedTimes, out int parsedHp, out int parsedInterval))
                continue;
            times = parsedTimes;
            hpPercent = parsedHp;
            intervalFrames = parsedInterval;
            found = true;
        }
        if (!found) return;

        if (TryGetAbility(data, AbilityName.ZombieRevive, out CharacterAbility existing))
        {
            existing.probability = times;
            existing.intensity = hpPercent;
            existing.duration = intervalFrames;
            return;
        }

        AddAbilityToData(data, new CharacterAbility
        {
            name = AbilityName.ZombieRevive,
            probability = times,
            duration = intervalFrames,
            intensity = hpPercent
        });
    }

    /// <summary>Returns true when CharacterData already contains the given ability.</summary>
    private static bool HasAbility(CharacterData data, AbilityName abilityName)
    {
        if (data.abilities == null) return false;
        for (int i = 0; i < data.abilities.Length; i++)
        {
            if (data.abilities[i].name == abilityName) return true;
        }
        return false;
    }

    /// <summary>Finds the first matching ability entry on CharacterData.</summary>
    private static bool TryGetAbility(CharacterData data, AbilityName abilityName, out CharacterAbility ability)
    {
        ability = null;
        if (data.abilities == null) return false;
        for (int i = 0; i < data.abilities.Length; i++)
        {
            if (data.abilities[i].name == abilityName)
            {
                ability = data.abilities[i];
                return true;
            }
        }
        return false;
    }

    /// <summary>Appends a new ability entry to CharacterData.</summary>
    private static void AddAbilityToData(CharacterData data, CharacterAbility ability)
    {
        if (data.abilities == null)
        {
            data.abilities = new CharacterAbility[] { ability };
            return;
        }

        int oldLength = data.abilities.Length;
        CharacterAbility[] newAbilities = new CharacterAbility[oldLength + 1];
        for (int i = 0; i < oldLength; i++)
        {
            newAbilities[i] = data.abilities[i];
        }
        newAbilities[oldLength] = ability;
        data.abilities = newAbilities;
    }

    #endregion

    public static bool TrySplitRestriction(string raw, out string key, out string value)
    {
        key = string.Empty;
        value = string.Empty;
        if (string.IsNullOrWhiteSpace(raw)) return false;

        string rule = raw.Trim();
        string[] parts = rule.Split(new[] { ':' }, 2);
        if (parts.Length != 2) return false;

        key = NormalizeRestrictionKey(parts[0]);
        value = parts[1].Trim();
        return !string.IsNullOrEmpty(key) && !string.IsNullOrEmpty(value);
    }

    public static bool IsSurgeRestrictionKey(string key)
    {
        if (string.IsNullOrEmpty(key)) return false;
        return key == "S+" || key == "S-" || key == "s+" || key == "s-";
    }

    public static bool TryParseSurgeRestrictionValue(string value, out int probability, out int duration)
    {
        probability = 0;
        duration = 0;
        if (!int.TryParse(value, out int parsed)) return false;
        if (parsed < 11 || parsed > 1009) return false;
        duration = parsed % 10;
        probability = parsed / 10;
        if (duration == 0) return false;
        if (probability < 1) return false;
        return true;
    }

    private static void ParseAllowRarity(RestrictionRules rules, string value)
    {
        if (!TryParseBoundedInt(value, 0, 9, out int rarity)) return;
        rules.allowRarities.Add(rarity);
        rules.hasAllowRarity = true;
    }

    private static void ParseDenyRarity(RestrictionRules rules, string value)
    {
        if (!TryParseBoundedInt(value, 0, 9, out int rarity)) return;
        rules.denyRarities.Add(rarity);
    }

    private static void ParseRequiredUnit(RestrictionRules rules, string value)
    {
        if (!TryNormalizeUnitCode(value, out string code4)) return;
        rules.requiredUnits.Add(code4);
    }

    private static void ParseDenyUnit(RestrictionRules rules, string value)
    {
        if (!TryNormalizeUnitCode(value, out string code4)) return;
        rules.denyUnits.Add(code4);
    }

    private static void ParseMaxCatCount(RestrictionRules rules, string value)
    {
        if (!TryParseNonNegativeInt(value, out int maxCatCount)) return;
        ApplyMinimumNonNegativeLimit(ref rules.maxCatCount, maxCatCount);
    }

    private static void ParseMaxCatLevel(RestrictionRules rules, string value)
    {
        if (!TryParsePositiveInt(value, out int maxCatLevel)) return;
        ApplyMinimumLimit(ref rules.maxCatLevel, maxCatLevel);
    }

    private static void ParseInitialMoneyLevel(RestrictionRules rules, string value)
    {
        if (!TryParseBoundedInt(value, 1, 8, out int initialMoneyLevel)) return;
        rules.initialMoneyLevel = initialMoneyLevel;
    }

    private static void ParseInitialMoneyAmount(RestrictionRules rules, string value)
    {
        if (!TryParseNonNegativeInt(value, out int initialMoneyAmount)) return;
        rules.initialMoneyAmount = initialMoneyAmount;
    }

    //private static void ParseUnitCostMultiplier(RestrictionRules rules, string value)
    //{
    //    if (!float.TryParse(value, NumberStyles.Float, CultureInfo.InvariantCulture, out float multiplier)) return;
    //    if (multiplier < 0f) return;
    //    rules.unitCostMultiplier *= multiplier;
    //}

    public static void ParseRestrictionValue(RestrictionRules rules, string value)
    {
        if (!TryParseNonNegativeInt(value, out _)) return;
    }

    public static void ParseSurgeRestrictionValue(RestrictionRules rules, string value)
    {
        if (!TryParseSurgeRestrictionValue(value, out _, out _)) return;
    }

    public static void ParseZombieReviveRestrictionValue(RestrictionRules rules, string value)
    {
        if (!TryParseZombieReviveRestrictionValue(value, out _, out _, out _)) return;
    }

    /// <summary>
    /// FS 覆盖全部 13 槽；fs 只覆盖 3 个嘉宾槽。槽位格式均为 角色码/等级，用 + 分隔，非法槽视为空。
    /// 例：FS:00000/25++-e117/40++00031+678
    /// 例：fs:60000/30++-e117/40
    /// </summary>
    private static void ParseForcedAllSlots(RestrictionRules rules, string value)
    {
        ParseForcedSlotRange(rules, value, 0, ForcedSlotCount);
    }

    private static void ParseForcedGuestSlots(RestrictionRules rules, string value)
    {
        ParseForcedSlotRange(rules, value, GuestSlotStart, GuestSlotCount);
    }

    private static void ParseForcedSlotRange(RestrictionRules rules, string value, int startIndex, int count)
    {
        if (rules == null || count < 1) return;
        string[] parts = string.IsNullOrEmpty(value) ? new string[0] : value.Split('+');
        rules.hasForcedSlots = true;
        for (int i = 0; i < count; i++)
        {
            int slot = startIndex + i;
            if (slot < 0 || slot >= ForcedSlotCount) break;
            rules.forcedSlotActive[slot] = true;
            string raw = i < parts.Length ? parts[i].Trim() : string.Empty;
            if (TryParseForcedSlotToken(raw, out string code, out int level))
            {
                rules.forcedSlotCodes[slot] = code;
                rules.forcedSlotLevels[slot] = level;
            }
            else
            {
                rules.forcedSlotCodes[slot] = string.Empty;
                rules.forcedSlotLevels[slot] = 0;
            }
        }
    }

    private static string[] EnsureForcedSlotArray(string[] selectedCodes)
    {
        if (selectedCodes != null && selectedCodes.Length >= ForcedSlotCount) return selectedCodes;

        string[] expanded = new string[ForcedSlotCount];
        int copyCount = selectedCodes == null ? 0 : selectedCodes.Length;
        for (int i = 0; i < ForcedSlotCount; i++)
        {
            expanded[i] = i < copyCount ? (selectedCodes[i] ?? string.Empty) : string.Empty;
        }
        return expanded;
    }

    private static string[] CreateEmptyForcedSlots()
    {
        string[] slots = new string[ForcedSlotCount];
        for (int i = 0; i < slots.Length; i++) slots[i] = string.Empty;
        return slots;
    }

    private static int[] CreateEmptyForcedLevels()
    {
        return new int[ForcedSlotCount];
    }

    private static bool TryParseForcedSlotToken(string raw, out string code, out int level)
    {
        code = string.Empty;
        level = 0;
        if (string.IsNullOrWhiteSpace(raw)) return false;

        int slash = raw.IndexOf('/');
        if (slash <= 0 || slash >= raw.Length - 1) return false;

        string idPart = raw.Substring(0, slash).Trim();
        string levelPart = raw.Substring(slash + 1).Trim();
        if (string.IsNullOrEmpty(idPart) || !int.TryParse(levelPart, out level) || level < 1) return false;
        if (!IsValidForcedCharacterCode(idPart)) return false;

        code = idPart;
        return true;
    }

    private static bool IsForcedLineupCode(RestrictionRules rules, string code)
    {
        if (!HasForcedSlots(rules) || string.IsNullOrEmpty(code)) return false;
        if (!CharacterPlacer.TryParse(code, true, out UnitIdentity selected) || !selected.IsValid) return false;
        if (!selected.AssetIsCat || selected.CharacterCode.Length < 4) return false;
        string unit4 = selected.CharacterCode.Substring(0, 4);
        for (int i = 0; i < ForcedSlotCount; i++)
        {
            if (!IsSlotForced(rules, i)) continue;
            string forced = rules.forcedSlotCodes[i];
            if (string.IsNullOrEmpty(forced)) continue;
            if (!CharacterPlacer.TryParse(forced, true, out UnitIdentity identity) || !identity.IsValid) continue;
            if (!identity.AssetIsCat || identity.CharacterCode.Length < 4) continue;
            if (identity.CharacterCode.Substring(0, 4) == unit4) return true;
        }
        return false;
    }

    private static bool IsValidForcedCharacterCode(string raw)
    {
        if (string.IsNullOrEmpty(raw)) return false;
        if (!CharacterPlacer.TryParse(raw, true, out UnitIdentity identity) || !identity.IsValid) return false;
        return CharacterPlacer.LoadData(identity) != null;
    }

    /// <summary>
    /// zr:{times}{hp3}{interval}。times=1-9，hp 为三位数生命百分比（000 无效），interval 为复活间隔帧。
    /// </summary>
    public static bool TryParseZombieReviveRestrictionValue(string value, out int times, out int hpPercent, out int intervalFrames)
    {
        times = 0;
        hpPercent = 0;
        intervalFrames = 0;
        if (string.IsNullOrEmpty(value) || value.Length < 4) return false;
        if (!char.IsDigit(value[0])) return false;
        times = value[0] - '0';
        if (times < 1 || times > 9) return false;
        if (!int.TryParse(value.Substring(1, 3), out hpPercent)) return false;
        if (hpPercent < 1) return false;
        string rest = value.Length > 4 ? value.Substring(4) : "0";
        if (!int.TryParse(rest, out intervalFrames) || intervalFrames < 0) return false;
        return true;
    }

    #region Cost Restriction Helpers

    /// <summary>
    /// Checks whether a unit cost passes all configured P+ and P- gates.
    /// P+ acts as a minimum cost requirement and P- acts as a maximum cost requirement.
    /// </summary>
    private static bool IsUnitCostAllowed(RestrictionRules rules, int cost)
    {
        if (rules == null) return true;

        // P+ requires the unit cost to be at least the configured value.
        if (rules.rawValuesByKey.TryGetValue("P+", out List<string> pPlusValues))
        {
            foreach (var val in pPlusValues)
            {
                if (int.TryParse(val, out int minCost) && cost < minCost)
                    return false;
            }
        }

        // P- requires the unit cost to stay below the configured value.
        if (rules.rawValuesByKey.TryGetValue("P-", out List<string> pMinusValues))
        {
            foreach (var val in pMinusValues)
            {
                if (int.TryParse(val, out int maxCost) && cost > maxCost)
                    return false;
            }
        }

        return true;
    }

    #endregion

    #region Restriction Value Utilities

    /// <summary>
    /// Returns the smallest or largest valid integer found in the provided list.
    /// </summary>
    private static int? GetExtremeParsedValue(List<string> values, bool useMinimumValue)
    {
        if (values == null || values.Count == 0) return null;

        int? result = null;
        for (int i = 0; i < values.Count; i++)
        {
            if (!int.TryParse(values[i], out int parsed)) continue;
            if (!result.HasValue)
            {
                result = parsed;
                continue;
            }

            if (useMinimumValue)
            {
                if (parsed < result.Value) result = parsed;
            }
            else if (parsed > result.Value)
            {
                result = parsed;
            }
        }

        return result;
    }

    #endregion

    private static bool TryNormalizeUnitCode(string value, out string code4)
    {
        code4 = string.Empty;
        if (value.Length != 4) return false;

        for (int i = 0; i < value.Length; i++)
        {
            if (!char.IsDigit(value[i])) return false;
        }

        code4 = value;
        return true;
    }

    private static CharacterData TryLoadCharacterDataByCode(string code)
    {
        if (string.IsNullOrEmpty(code)) return null;
        if (!CharacterPlacer.TryParse(code, true, out UnitIdentity identity) || !identity.IsValid) return null;
        return CharacterPlacer.LoadData(identity);
    }

    private static bool TryParsePositiveInt(string value, out int result)
    {
        result = 0;
        return int.TryParse(value, out result) && result > 0;
    }

    private static bool TryParseNonNegativeInt(string value, out int result)
    {
        result = 0;
        return int.TryParse(value, out result) && result >= 0;
    }

    private static bool TryParseBoundedInt(string value, int min, int max, out int result)
    {
        result = 0;
        return int.TryParse(value, out result) && result >= min && result <= max;
    }

    private static void ApplyMinimumLimit(ref int currentLimit, int candidate)
    {
        if (candidate < 1) return;
        if (currentLimit < 1 || candidate < currentLimit)
        {
            currentLimit = candidate;
        }
    }

    private static void ApplyMinimumNonNegativeLimit(ref int currentLimit, int candidate)
    {
        if (candidate < 0) return;
        if (currentLimit < 0 || candidate < currentLimit)
        {
            currentLimit = candidate;
        }
    }

    public static string NormalizeRestrictionKey(string rawKey)
    {
        if (string.IsNullOrWhiteSpace(rawKey)) return string.Empty;
        string trimmed = rawKey.Trim();
        if (CaseSensitiveRestrictionKeys.Contains(trimmed)) return trimmed;
        return trimmed.ToUpperInvariant();
    }
}
