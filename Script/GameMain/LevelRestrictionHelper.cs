using System.Collections.Generic;

public static class LevelRestrictionHelper
{
    public class RestrictionRules
    {
        public readonly HashSet<int> allowRarities = new HashSet<int>();
        public readonly HashSet<int> denyRarities = new HashSet<int>();
        public readonly HashSet<string> allowUnits = new HashSet<string>();
        public readonly HashSet<string> denyUnits = new HashSet<string>();
        public bool hasAllowRarity;
        public bool hasAllowUnit;
    }

    public static RestrictionRules Parse(string[] restrictions)
    {
        RestrictionRules rules = new RestrictionRules();
        if (restrictions == null) return rules;

        for (int i = 0; i < restrictions.Length; i++)
        {
            string raw = restrictions[i];
            if (string.IsNullOrWhiteSpace(raw)) continue;
            string rule = raw.Trim().ToUpperInvariant();
            string[] parts = rule.Split(':');
            if (parts.Length != 2) continue;
            string key = parts[0];
            string value = parts[1];

            if (key == "R+" || key == "R-")
            {
                if (!int.TryParse(value, out int rarity)) continue;
                if (rarity < 0 || rarity > 9) continue;
                if (key == "R+")
                {
                    rules.allowRarities.Add(rarity);
                    rules.hasAllowRarity = true;
                }
                else
                {
                    rules.denyRarities.Add(rarity);
                }
                continue;
            }

            if (key == "U+" || key == "U-")
            {
                if (value.Length != 4) continue;
                bool allDigits = true;
                for (int c = 0; c < value.Length; c++)
                {
                    if (!char.IsDigit(value[c])) { allDigits = false; break; }
                }
                if (!allDigits) continue;
                if (key == "U+")
                {
                    rules.allowUnits.Add(value);
                    rules.hasAllowUnit = true;
                }
                else
                {
                    rules.denyUnits.Add(value);
                }
            }
        }

        return rules;
    }

    public static bool IsUnitAllowed(RestrictionRules rules, string code)
    {
        if (rules == null || string.IsNullOrEmpty(code) || code.Length < 4) return true;
        string code4 = code.Substring(0, 4);
        if (!int.TryParse(code4.Substring(0, 1), out int rarity)) return true;

        if (rules.hasAllowRarity && !rules.allowRarities.Contains(rarity)) return false;
        if (rules.denyRarities.Contains(rarity)) return false;
        if (rules.hasAllowUnit && !rules.allowUnits.Contains(code4)) return false;
        if (rules.denyUnits.Contains(code4)) return false;
        return true;
    }

    public static void ApplyToDeployer(UnitDeployer deployer, string code, RestrictionRules rules, bool isGuest)
    {
        if (deployer == null) return;
        if (!IsUnitAllowed(rules, code))
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
}
