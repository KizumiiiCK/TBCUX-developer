using System;
using System.Collections.Generic;
using TMPro;
using UnityEngine;

/// <summary>
/// 关卡限制条件展示：每条限制实例化一行预制体，使用第 0 个子物体的 TMP_Text 显示全文。
/// 运行时复用对象池中的行物体，避免频繁 Instantiate/Destroy。
/// </summary>
public class LevelRestrictionBoard : MonoBehaviour
{
    [SerializeField] private Transform board;
    [SerializeField] private GameObject restrictionLinePrefab;

    private readonly Stack<GameObject> pooledLines = new Stack<GameObject>();
    private readonly List<GameObject> activeLines = new List<GameObject>();
    private int contentVersion;

    #region Strategy Pattern - 显示策略

    /// <summary>
    /// 显示策略委托。isStillValid 用于在异步回调中检查当前行是否仍有效（未被对象池回收）。
    /// </summary>
    private delegate void DisplayStrategy(string value, string format, TMP_Text targetText, Func<bool> isStillValid);

    /// <summary>
    /// Key -> 显示策略映射（不区分大小写）。
    /// 仅注册需要特殊处理的 Key；其余 Key 自动使用默认策略 HandleDirectValueDisplay。
    /// </summary>
    private static readonly Dictionary<string, DisplayStrategy> StrategyMap =
        new Dictionary<string, DisplayStrategy>(StringComparer.OrdinalIgnoreCase)
        {
            { "R+", HandleRarityDisplay },
            { "R-", HandleRarityDisplay },
            { "U+", HandleUnitDisplay },
            { "U-", HandleUnitDisplay },
            { "IV", HandleNoneValueDisplay },
        };

    #endregion

    #region Object Pool - 对象池

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

    #region Main Flow - 主流程

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
                row.SetActive(false);
                pooledLines.Push(row);
                activeLines.RemoveAt(activeLines.Count - 1);
                continue;
            }

            string trimmed = raw.Trim();
            string[] colonParts = trimmed.Split(new[] { ':' }, 2);
            if (colonParts.Length != 2)
            {
                lineText.text = trimmed;
                continue;
            }

            string keyRaw = colonParts[0].Trim();
            string value = colonParts[1].Trim();
            string keyUpper = keyRaw.ToUpperInvariant();

            TMP_Text capturedText = lineText;
            LocalizationHelper.GetLocalizedText(UXPref.Localized_Descriptions, keyRaw, formatLoc =>
            {
                if (version != contentVersion || capturedText == null) return;
                string format = string.IsNullOrEmpty(formatLoc) ? "{0}" : formatLoc;

                Func<bool> isStillValid = () => version == contentVersion;

                if (!StrategyMap.TryGetValue(keyUpper, out var strategy))
                {
                    strategy = HandleDirectValueDisplay;
                }
                strategy(value, format, capturedText, isStillValid);
            });
        }
    }

    #endregion

    #region Display Strategies - 各策略具体实现

    /// <summary>
    /// R+ / R-：稀有度限制。查询本地化表将数字转换为稀有度名称后嵌入。
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
    /// U+ / U-：单位限制。查询本地化表将单位代码转换为角色名后嵌入。
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
    /// IV：隐身能力限制。不需要 value 支撑，直接显示格式文本本身。
    /// </summary>
    private static void HandleNoneValueDisplay(string value, string format, TMP_Text text, Func<bool> isStillValid)
    {
        if (!isStillValid() || text == null) return;
        // 若格式文本意外包含 {0}，安全移除避免显示异常
        text.text = format.Contains("{0}") ? format.Replace("{0}", "").Trim() : format;
    }

    /// <summary>
    /// 默认策略：P+/P-/D+/D-/CC/LC/ES 等数值型限制。直接显示 value，无需二次本地化查询。
    /// </summary>
    private static void HandleDirectValueDisplay(string value, string format, TMP_Text text, Func<bool> isStillValid)
    {
        if (!isStillValid() || text == null) return;
        ApplyFormatSafe(text, format, value);
    }

    #endregion

    #region Utility Methods - 工具方法

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
        catch (System.FormatException)
        {
            lineText.text = format + " " + arg0;
        }
    }

    #endregion
}