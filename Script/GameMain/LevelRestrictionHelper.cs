using System;
using System.Collections.Generic;
using UnityEngine;

public static class LevelRestrictionHelper
{
    public delegate void RestrictionParser(RestrictionRules rules, string value);

    private const int NoLimit = -1;
    private static readonly Dictionary<string, RestrictionParser> ParserMap =
        new Dictionary<string, RestrictionParser>(StringComparer.Ordinal)
        {
            { "R+", ParseAllowRarity },
            { "R-", ParseDenyRarity },
            { "U+", ParseRequiredUnit },
            { "U-", ParseDenyUnit },
            { "CC", ParseMaxCatCount },
            { "LC", ParseMaxCatLevel },
            { "P+", ParseRestrictionValue },
            { "P-", ParseRestrictionValue },
            { "D+", ParseRestrictionValue },
            { "D-", ParseRestrictionValue },
            { "ES", ParseRestrictionValue },
            { "IV", ParseRestrictionValue },
            { "S+", ParseSurgeRestrictionValue },
            { "S-", ParseSurgeRestrictionValue },
            { "s+", ParseSurgeRestrictionValue },
            { "s-", ParseSurgeRestrictionValue }
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
        public int maxCatCount = NoLimit;
        public int maxCatLevel = NoLimit;
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
            if (!TrySplitRule(restrictions[i], out string key, out string value)) continue;

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
        if (rules == null || string.IsNullOrEmpty(code) || code.Length < 4) return true;
        string code4 = code.Substring(0, 4);
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
            if (string.IsNullOrEmpty(code) || code.Length < 4) continue;
            selectedUnits.Add(code.Substring(0, 4));
        }

        foreach (string requiredUnit in rules.requiredUnits)
        {
            if (!selectedUnits.Contains(requiredUnit)) return false;
        }

        return true;
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
                case "ES":
                    if (!isCatTeam) ApplyEnemyStrengthenRestriction(data, entry.Value);
                    break;
                case "S-":
                    if (!isCatTeam) ApplySurgeRestriction(data, entry.Value, AbilityName.surge);
                    break;
                case "s-":
                    if (!isCatTeam) ApplySurgeRestriction(data, entry.Value, AbilityName.miniSurge);
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

    private static bool TrySplitRule(string raw, out string key, out string value)
    {
        key = string.Empty;
        value = string.Empty;
        if (string.IsNullOrWhiteSpace(raw)) return false;

        string rule = raw.Trim();
        string[] parts = rule.Split(':');
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
        if (string.IsNullOrEmpty(code) || code.Length < 5) return null;
        if (!char.IsDigit(code[0]) || !char.IsDigit(code[4])) return null;
        if (!char.IsDigit(code[1]) || !char.IsDigit(code[2]) || !char.IsDigit(code[3])) return null;
        return Resources.Load<CharacterData>($"Units/Cat Units/{code[0]}/{code.Substring(1, 3)}/{code[4]}/data");
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

    private static string NormalizeRestrictionKey(string rawKey)
    {
        if (string.IsNullOrWhiteSpace(rawKey)) return string.Empty;
        string trimmed = rawKey.Trim();
        if (trimmed == "s+" || trimmed == "s-") return trimmed;
        return trimmed.ToUpperInvariant();
    }
}
