using System;
using System.Collections.Generic;
using System.Globalization;
using UnityEngine;

public static class LevelRestrictionHelper
{
    public delegate void RestrictionParser(RestrictionRules rules, string value);

    private const int NoLimit = -1;
    private static readonly Dictionary<string, RestrictionParser> ParserMap =
        new Dictionary<string, RestrictionParser>(StringComparer.OrdinalIgnoreCase)
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
            { "IV", ParseRestrictionValue }
        };

    public class RestrictionRules
    {
        public readonly HashSet<int> allowRarities = new HashSet<int>();
        public readonly HashSet<int> denyRarities = new HashSet<int>();
        public readonly HashSet<string> requiredUnits = new HashSet<string>();
        public readonly HashSet<string> denyUnits = new HashSet<string>();
        public readonly Dictionary<string, List<string>> rawValuesByKey =
            new Dictionary<string, List<string>>(StringComparer.OrdinalIgnoreCase);

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
        ParserMap[key.Trim().ToUpperInvariant()] = parser;
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

    /// <summary>关卡限制中是否包含 IV（致盲预览等逻辑用）。</summary>
    public static bool HasIvRestriction(string[] restrictions)
    {
        if (restrictions == null) return false;
        return Parse(restrictions).rawValuesByKey.ContainsKey("IV");
    }

    /// <summary>
    /// ???????????????????????? CharacterData ????????????????P+/P-????????????????
    /// </summary>
    public static bool IsUnitAllowed(RestrictionRules rules, string code, CharacterData data = null)
    {
        if (rules == null || string.IsNullOrEmpty(code) || code.Length < 4) return true;
        string code4 = code.Substring(0, 4);
        if (!int.TryParse(code4.Substring(0, 1), out int rarity)) return true;

        if (rules.hasAllowRarity && !rules.allowRarities.Contains(rarity)) return false;
        if (rules.denyRarities.Contains(rarity)) return false;
        if (rules.denyUnits.Contains(code4)) return false;

        // ??????????P+ / P-??????? CharacterData ???? Cost ????????
        if (data != null && !IsUnitCostAllowed(rules, data.Cost)) return false;

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
    /// ????????????????????????????????????????? ApplyCharacterDataRestrictions ???????????
    /// </summary>
    public static void ApplyToDeployer(UnitDeployer deployer, string code, RestrictionRules rules, bool isGuest, bool lockAllUnits)
    {
        if (deployer == null) return;
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

    #region CharacterData Initialization Restrictions - ?????????????????

    /// <summary>
    /// ??????????????????? CharacterData ?????????
    /// ?????????????????????????????????????????
    /// </summary>
    /// <param name="treasureCount">????b?????????? <see cref="Character.LoadCharacterData"/> ?? treasureBonus ????</param>
    /// <param name="deployLevel">???????????? <see cref="CatCharacter.InitializeCharacter"/> ?????????????</param>
    /// <param name="power">???? 1???? InitializeCharacter ? Power ????</param>
    public static void ApplyCharacterDataRestrictions(RestrictionRules rules, CharacterData data, float treasureCount = 0f, int deployLevel = 1, float power = 1f)
    {
        if (rules == null || data == null) return;
        ApplyDamageRestrictions(rules, data, treasureCount, deployLevel, power);
        // TODO: ????????????????? CharacterData ??????????
    }
    /// <summary>
    /// ???????????????????????????????? CharacterData ?????????
    /// ?? LevelEnemySummoner ?????????????????
    /// </summary>
    public static void ApplyEnemyCharacterDataRestrictions(RestrictionRules rules, CharacterData data)
    {
        if (rules == null || data == null) return;
        ApplyInvisibleShowRestriction(rules, data);
        ApplyEnemyStrengthenRestriction(rules, data);
        // TODO: ???????????????????? CharacterData ??????????
    }
    /// <summary>
    /// D- / D+ ?????????????? ATKInfo???????????????????? 0??
    /// D-: ???????????????????????D+: ???????????????????????
    /// </summary>
    private static void ApplyDamageRestrictions(RestrictionRules rules, CharacterData data, float treasureCount, int deployLevel, float power)
    {
        if (data.atkInfos == null) return;

        // ?? rawValuesByKey ??????????????????????????D- ???????D+ ????
        int? damageCap = GetEffectiveDamageCap(rules);
        int? damageMin = GetEffectiveDamageMin(rules);

        if (!damageCap.HasValue && !damageMin.HasValue) return;

        deployLevel = Mathf.Max(1, deployLevel);
        float treasureBonus = 1f + treasureCount / 100f;
        float levelMul = 0.8f + 0.2f * deployLevel;

        for (int i = 0; i < data.atkInfos.Length; i++)
        {
            var atkInfo = data.atkInfos[i];
            if (atkInfo == null) continue;

            // D-: ???????? ?? ????
            int evaluated = EvaluateCatFirstHitDamage(atkInfo.ATK, treasureBonus, levelMul, power);

            if (damageCap.HasValue && evaluated > damageCap.Value)
            {
                atkInfo.ATK = 0;
                continue;
            }

            // D+: ???????? ?? ????
            if (damageMin.HasValue && evaluated < damageMin.Value)
            {
                atkInfo.ATK = 0;
            }
        }
    }

    private static int EvaluateCatFirstHitDamage(float atk, float treasureBonus, float levelMul, float power)
    {
        int afterTreasure = (int)(atk * treasureBonus);
        return (int)(afterTreasure * levelMul * power);
    }
    /// <summary>
    /// IV ??????????????? Aux_InvisibleShow ??????
    /// </summary>
    private static void ApplyInvisibleShowRestriction(RestrictionRules rules, CharacterData data)
    {
        if (!rules.rawValuesByKey.ContainsKey("IV")) return;

        // ?????????????????????????????
        if (HasAbility(data, AbilityName.invisible)) return;

        // ??????????????????? CharacterData
        CharacterAbility ability = new CharacterAbility
        {
            name = AbilityName.invisible,
            probability = 0,
            duration = 0,
            intensity = 0
        };
        AddAbilityToData(data, ability);
    }

    /// <summary>
    /// ES ??????????????? Strengthen ??????probability=50??intensity=?????
    /// ??? ES ???????? intensity ??????????????
    /// </summary>
    private static void ApplyEnemyStrengthenRestriction(RestrictionRules rules, CharacterData data)
    {
        if (!rules.rawValuesByKey.TryGetValue("ES", out List<string> values)) return;

        // ???????? ES ??????? intensity
        int maxIntensity = 0;
        foreach (var val in values)
        {
            if (int.TryParse(val, out int parsed) && parsed > maxIntensity)
                maxIntensity = parsed;
        }

        if (maxIntensity <= 0) return;

        // ??????????????????????????? intensity
        if (TryGetAbility(data, AbilityName.strengthen, out CharacterAbility existing))
        {
            existing.intensity = maxIntensity;
            existing.probability = 50;
            return;
        }

        // ???????????????
        CharacterAbility ability = new CharacterAbility
        {
            name = AbilityName.strengthen,
            probability = 50,
            duration = 0,
            intensity = maxIntensity
        };
        AddAbilityToData(data, ability);
    }

    /// <summary>
    /// ??? CharacterData ?????????????????
    /// </summary>
    private static bool HasAbility(CharacterData data, AbilityName abilityName)
    {
        if (data.abilities == null) return false;
        for (int i = 0; i < data.abilities.Length; i++)
        {
            if (data.abilities[i].name == abilityName) return true;
        }
        return false;
    }

    /// <summary>
    /// ?????? CharacterData ?????????????????
    /// </summary>
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

    /// <summary>
    /// ?? CharacterData ???????????????????????
    /// </summary>
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

        key = parts[0].Trim().ToUpperInvariant();
        value = parts[1].Trim().ToUpperInvariant();
        return !string.IsNullOrEmpty(key) && !string.IsNullOrEmpty(value);
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

    #region Cost Restriction Helpers - ???????????

    /// <summary>
    /// ???????????????????? P+ / P- ?????
    /// ??? P+ ????????????????? P- ???????????????
    /// </summary>
    private static bool IsUnitCostAllowed(RestrictionRules rules, int cost)
    {
        if (rules == null) return true;

        // P+: ??????? minCost
        if (rules.rawValuesByKey.TryGetValue("P+", out List<string> pPlusValues))
        {
            foreach (var val in pPlusValues)
            {
                if (int.TryParse(val, out int minCost) && cost < minCost)
                    return false;
            }
        }

        // P-: ??????? maxCost
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

    #region Damage Restriction Helpers - ??????????

    /// <summary>
    /// ?????????????????D-??????? D- ???????????????
    /// </summary>
    private static int? GetEffectiveDamageCap(RestrictionRules rules)
    {
        if (!rules.rawValuesByKey.TryGetValue("D-", out List<string> values)) return null;

        int? cap = null;
        foreach (var val in values)
        {
            if (int.TryParse(val, out int parsed))
            {
                if (!cap.HasValue || parsed < cap.Value)
                    cap = parsed;
            }
        }
        return cap;
    }

    /// <summary>
    /// ?????????????????D+??????? D+ ??????????????
    /// </summary>
    private static int? GetEffectiveDamageMin(RestrictionRules rules)
    {
        if (!rules.rawValuesByKey.TryGetValue("D+", out List<string> values)) return null;

        int? min = null;
        foreach (var val in values)
        {
            if (int.TryParse(val, out int parsed))
            {
                if (!min.HasValue || parsed > min.Value)
                    min = parsed;
            }
        }
        return min;
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
}