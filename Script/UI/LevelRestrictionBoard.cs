using System;
using System.Collections.Generic;
using TMPro;
using UnityEngine;

/// <summary>
/// Displays localized level restriction rows and reuses row instances through a small pool.
/// </summary>
public class LevelRestrictionBoard : MonoBehaviour
{
    [SerializeField] private Transform board;
    [SerializeField] private GameObject restrictionLinePrefab;

    private readonly Stack<GameObject> pooledLines = new Stack<GameObject>();
    private readonly List<GameObject> activeLines = new List<GameObject>();
    private int contentVersion;

    #region Display Strategies

    /// <summary>
    /// Renders one restriction row after its localized format string is loaded.
    /// </summary>
    private delegate void DisplayStrategy(string value, string format, TMP_Text targetText, Func<bool> isStillValid);

    /// <summary>
    /// Only keys with custom formatting are registered here.
    /// Every other key falls back to direct value display.
    /// </summary>
    private static readonly Dictionary<string, DisplayStrategy> StrategyMap =
        new Dictionary<string, DisplayStrategy>(StringComparer.Ordinal)
        {
            { "R+", HandleRarityDisplay },
            { "R-", HandleRarityDisplay },
            { "U+", HandleUnitDisplay },
            { "U-", HandleUnitDisplay },
            { "IV", HandleNoneValueDisplay },
            { "OH", HandleNoneValueDisplay },
            { "S+", HandleSurgeDisplay },
            { "S-", HandleSurgeDisplay },
            { "s+", HandleSurgeDisplay },
            { "s-", HandleSurgeDisplay },
        };

    #endregion

    #region Object Pool

    private void ReleaseAllLinesToPool()
    {
        for (int i = activeLines.Count - 1; i >= 0; i--)
        {
            GameObject row = activeLines[i];
            if (row == null) continue;
            row.SetActive(false);
            pooledLines.Push(row);
        }
        activeLines.Clear();
    }

    private GameObject RentLine()
    {
        GameObject row = pooledLines.Count > 0 ? pooledLines.Pop() : Instantiate(restrictionLinePrefab, board);
        row.transform.SetParent(board, false);
        row.SetActive(true);
        activeLines.Add(row);
        return row;
    }

    #endregion

    #region Main Flow

    public void ShowRestrictions(string[] restrictions)
    {
        if (board == null || restrictionLinePrefab == null) return;

        contentVersion++;
        int version = contentVersion;
        ReleaseAllLinesToPool();

        if (restrictions == null || restrictions.Length == 0) return;

        for (int i = 0; i < restrictions.Length; i++)
        {
            string raw = restrictions[i];
            if (string.IsNullOrWhiteSpace(raw)) continue;

            GameObject row = RentLine();
            TMP_Text lineText = row.transform.childCount > 0
                ? row.transform.GetChild(0).GetComponent<TMP_Text>()
                : null;

            if (lineText == null)
            {
                Debug.LogWarning("[LevelRestrictionBoard] Line prefab child 0 has no TMP_Text.");
                RecycleLatestRow(row);
                continue;
            }

            if (!TrySplitRestriction(raw, out string key, out string value))
            {
                lineText.text = raw.Trim();
                continue;
            }

            if (LevelRestrictionHelper.IsSurgeRestrictionKey(key) &&
                !LevelRestrictionHelper.TryParseSurgeRestrictionValue(value, out _, out _))
            {
                RecycleLatestRow(row);
                continue;
            }

            TMP_Text capturedText = lineText;
            LocalizationHelper.GetLocalizedText(UXPref.Localized_Descriptions, key, formatLoc =>
            {
                if (version != contentVersion || capturedText == null) return;
                string format = string.IsNullOrEmpty(formatLoc) ? "{0}" : formatLoc;
                Func<bool> isStillValid = () => version == contentVersion;

                if (!StrategyMap.TryGetValue(key, out DisplayStrategy strategy))
                {
                    strategy = HandleDirectValueDisplay;
                }

                strategy(value, format, capturedText, isStillValid);
            });
        }
    }

    private void RecycleLatestRow(GameObject row)
    {
        if (row == null) return;
        row.SetActive(false);
        pooledLines.Push(row);
        if (activeLines.Count > 0 && activeLines[activeLines.Count - 1] == row)
        {
            activeLines.RemoveAt(activeLines.Count - 1);
        }
        else
        {
            activeLines.Remove(row);
        }
    }

    #endregion

    #region Display Strategy Implementations

    /// <summary>
    /// Resolves rarity names through the UI localization table.
    /// </summary>
    private static void HandleRarityDisplay(string value, string format, TMP_Text text, Func<bool> isStillValid)
    {
        if (!int.TryParse(value, out int rarity) || rarity < 0 || rarity > 9)
        {
            ApplyFormatSafe(text, format, value);
            return;
        }

        string rarityUiKey = $"id:t{rarity}";
        LocalizationHelper.GetLocalizedText(UXPref.Localized_UI, rarityUiKey, detailLoc =>
        {
            if (!isStillValid() || text == null) return;
            string detail = string.IsNullOrEmpty(detailLoc) ? value : detailLoc;
            ApplyFormatSafe(text, format, detail);
        });
    }

    /// <summary>
    /// Resolves unit codes into localized unit names.
    /// </summary>
    private static void HandleUnitDisplay(string value, string format, TMP_Text text, Func<bool> isStillValid)
    {
        if (value.Length != 4 || !IsAllDigits(value))
        {
            ApplyFormatSafe(text, format, value);
            return;
        }

        string unitKey = $"{value}0";
        LocalizationHelper.GetLocalizedText(UXPref.Localized_UnitNames, unitKey, nameLoc =>
        {
            if (!isStillValid() || text == null) return;
            string detail = string.IsNullOrEmpty(nameLoc) ? unitKey : nameLoc;
            ApplyFormatSafe(text, format, detail);
        });
    }

    /// <summary>
    /// IV ignores the numeric payload and shows the localized sentence directly.
    /// </summary>
    private static void HandleNoneValueDisplay(string value, string format, TMP_Text text, Func<bool> isStillValid)
    {
        if (!isStillValid() || text == null) return;
        text.text = format.Contains("{0}") ? format.Replace("{0}", string.Empty).Trim() : format;
    }

    /// <summary>
    /// Surge restrictions expand probability and duration into a two-parameter localized sentence.
    /// </summary>
    private static void HandleSurgeDisplay(string value, string format, TMP_Text text, Func<bool> isStillValid)
    {
        if (!isStillValid() || text == null) return;
        if (!LevelRestrictionHelper.TryParseSurgeRestrictionValue(value, out int probability, out int duration))
        {
            ApplyFormatSafe(text, format, value);
            return;
        }

        ApplyFormatSafe(text, format, probability, duration);
    }

    /// <summary>
    /// Default handler for numeric restrictions that only need a single placeholder.
    /// </summary>
    private static void HandleDirectValueDisplay(string value, string format, TMP_Text text, Func<bool> isStillValid)
    {
        if (!isStillValid() || text == null) return;
        ApplyFormatSafe(text, format, value);
    }

    #endregion

    #region Utilities

    private static bool TrySplitRestriction(string raw, out string key, out string value)
    {
        key = string.Empty;
        value = string.Empty;
        if (string.IsNullOrWhiteSpace(raw)) return false;

        string[] parts = raw.Trim().Split(new[] { ':' }, 2);
        if (parts.Length != 2) return false;

        key = NormalizeRestrictionKey(parts[0]);
        value = parts[1].Trim();
        return !string.IsNullOrEmpty(key) && !string.IsNullOrEmpty(value);
    }

    private static string NormalizeRestrictionKey(string rawKey)
    {
        if (string.IsNullOrWhiteSpace(rawKey)) return string.Empty;
        string trimmed = rawKey.Trim();
        if (trimmed == "s+" || trimmed == "s-" || trimmed == "mm") return trimmed;
        return trimmed.ToUpperInvariant();
    }

    private static bool IsAllDigits(string str)
    {
        for (int i = 0; i < str.Length; i++)
        {
            if (!char.IsDigit(str[i])) return false;
        }
        return true;
    }

    private static void ApplyFormatSafe(TMP_Text lineText, string format, object arg0)
    {
        if (lineText == null) return;
        try
        {
            lineText.text = string.Format(format, arg0);
        }
        catch (FormatException)
        {
            lineText.text = format + " " + arg0;
        }
    }

    private static void ApplyFormatSafe(TMP_Text lineText, string format, object arg0, object arg1)
    {
        if (lineText == null) return;
        try
        {
            lineText.text = string.Format(format, arg0, arg1);
        }
        catch (FormatException)
        {
            lineText.text = format + " " + arg0 + " " + arg1;
        }
    }

    #endregion
}
