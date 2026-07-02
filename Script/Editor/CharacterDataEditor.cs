using System;
using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEngine;
using UnityEngine.Localization;
using UnityEngine.Localization.Settings;
using UnityEngine.Localization.Tables;
using UnityEngine.ResourceManagement.AsyncOperations;

[CustomEditor(typeof(CharacterData))]
public class CharacterDataEditor : Editor
{
    private const string DescTableName = UXPref.Localized_Descriptions;
    private const float PortraitEnemySize = 96f;
    private const float PortraitCatWidth = 110f;
    private const float PortraitCatHeight = 85f;
    private const float SmallIconSize = 24f;
    private const float LargeIconSize = 32f;
    private const float StatKeyWidth = 72f;
    private const float StatValueInnerGap = 6f;
    private const float StatGroupGap = 18f;
    private const float AtkColIndexWidth = 50f;
    private const float AtkColAtkWidth = 120f;
    private const float AtkColFrameWidth = 50f;
    private const float AtkColRangeWidth = 120f;
    private const float AtkColFlagsWidth = 120f;
    private static readonly Color DisabledIconColor = new Color(0.4f, 0.4f, 0.4f, 1f);
    private static readonly Dictionary<string, string> LocalizedCache = new Dictionary<string, string>();
    private static readonly Dictionary<string, PortraitLookup> PortraitCache = new Dictionary<string, PortraitLookup>();
    private static readonly Dictionary<string, StringTable> EditorTableCache = new Dictionary<string, StringTable>();

    private GUIStyle titleStyle;
    private GUIStyle sectionTitleStyle;
    private GUIStyle subtitleStyle;
    private GUIStyle bodyStyle;
    private GUIStyle descriptionStyle;
    private GUIStyle statKeyStyle;
    private GUIStyle statValueStyle;
    private GUIStyle atkTableHeaderStyle;
    private GUIStyle atkTableCellStyle;
    private bool stylesInitialized;
    private bool isCat;

    private void OnEnable()
    {
        stylesInitialized = false;
    }

    public override void OnInspectorGUI()
    {
        EnsureStyles();
        serializedObject.Update();

        if (targets.Length > 1)
        {
            EditorGUILayout.HelpBox("暂不支持多选预览，请单选 CharacterData 查看增强面板。", MessageType.Info);
            DrawDefaultInspector();
            serializedObject.ApplyModifiedProperties();
            return;
        }

        CharacterData data = target as CharacterData;
        if (data == null)
        {
            DrawDefaultInspector();
            serializedObject.ApplyModifiedProperties();
            return;
        }

        DrawSummaryPanels(data);
        EditorGUILayout.Space(10f);
        EditorGUILayout.LabelField("Raw Editable Data", sectionTitleStyle);
        DrawDefaultInspector();
        serializedObject.ApplyModifiedProperties();
    }

    private void DrawSummaryPanels(CharacterData data)
    {
        EditorGUILayout.Space(6f);
        EditorGUILayout.LabelField("Character Data Overview", titleStyle);
        EditorGUILayout.Space(4f);

        DrawBox("Core Stats", () =>
        {
            DrawPortraitAndMainStats(data);
            EditorGUILayout.Space(6f);
            DrawTraitRows(data);
            EditorGUILayout.Space(6f);
            DrawAttackOverview(data);
        });

        DrawBox("Traits & DRE Effects", () =>
        {
            List<IconRowData> rows = new List<IconRowData>();
            if (data.DRE != null)
            {
                if (data.DRE.massiveDamage) rows.Add(NewRow("N:dre:m", "D:dre:m"));
                if (data.DRE.insaneDamage) rows.Add(NewRow("N:dre:i", "D:dre:i"));
                if (data.DRE.tough) rows.Add(NewRow("N:dre:t", "D:dre:t"));
                if (data.DRE.aegis) rows.Add(NewRow("N:dre:a", "D:dre:a"));
                if (data.DRE.strongAgainst) rows.Add(NewRow("N:dre:s", "D:dre:s"));
            }

            if (data.characterEffects != null)
            {
                for (int i = 0; i < data.characterEffects.Length; i++)
                {
                    CharacterEffect ce = data.characterEffects[i];
                    if (ce == null) continue;
                    int id = Convert.ToInt32(ce.name);
                    rows.Add(NewRow($"N:e:{id}", $"D:e:{id}", ce.probability, ce.duration, ce.intensity));
                }
            }

            DrawIconRows(rows, "No DRE or CharacterEffect configured.");
        });

        DrawBox("Abilities", () =>
        {
            List<IconRowData> rows = new List<IconRowData>();
            if (data.abilities != null)
            {
                for (int i = 0; i < data.abilities.Length; i++)
                {
                    CharacterAbility ability = data.abilities[i];
                    if (ability == null) continue;
                    int id = Convert.ToInt32(ability.name);
                    rows.Add(NewRow($"N:a:{id}", $"D:a:{id}", ability.probability, ability.duration, ability.intensity));
                }
            }
            DrawIconRows(rows, "No Ability configured.");
        });

        DrawBox("Attack Type Resistance", () =>
        {
            List<IconRowData> rows = new List<IconRowData>();
            if (data.atkTypeResis != null)
            {
                for (int i = 0; i < data.atkTypeResis.Length; i++)
                {
                    AttackTypeResistance res = data.atkTypeResis[i];
                    if (res == null) continue;
                    int id = Convert.ToInt32(res.type);
                    rows.Add(NewRow($"N:ra:{id}", $"D:ra:{id}", res.intensity, 0, 0));
                }
            }
            DrawIconRows(rows, "No AttackType resistance configured.");
        });

        DrawBox("Effect Resistance", () =>
        {
            List<IconRowData> rows = new List<IconRowData>();
            if (data.effectResistances != null)
            {
                for (int i = 0; i < data.effectResistances.Length; i++)
                {
                    CharacterEffect res = data.effectResistances[i];
                    if (res == null) continue;
                    int id = Convert.ToInt32(res.name);
                    rows.Add(NewRow($"N:re:{id}", $"D:re:{id}", res.probability, 0, 0));
                }
            }
            DrawIconRows(rows, "No Effect resistance configured.");
        });
    }

    private void DrawPortraitAndMainStats(CharacterData data)
    {
        Sprite portrait = GetPortraitSprite(data, out bool portraitIsCat);
        isCat = portraitIsCat;
        float portraitW = isCat ? PortraitCatWidth : PortraitEnemySize;
        float portraitH = isCat ? PortraitCatHeight : PortraitEnemySize;
        string animSource = data.SPINEAnimated ? "Spine 3.8" : (data.UNITYAnimated ? "Unity" : "BCU");

        EditorGUILayout.BeginHorizontal();
        DrawSprite(portrait ?? EAIconResolver.LoadSpriteOrFallback(string.Empty), portraitW, portraitH);
        EditorGUILayout.BeginVertical();
        EditorGUILayout.LabelField($"{data.Name} {(data.isEliteUnit ? "[战术单位]" : string.Empty)}", subtitleStyle);
        DrawMainStatGrid(data, animSource);
        EditorGUILayout.EndVertical();
        EditorGUILayout.EndHorizontal();
    }

    private void DrawMainStatGrid(CharacterData data, string animSource)
    {
        EditorGUILayout.Space(2f);
        DrawMainStatRow("血量", data.Health.ToString(), "KB", data.KB.ToString());
        DrawMainStatRow("速度", data.Speed.ToString(), "攻击恢复", data.Reload.ToString());
        DrawMainStatRow("索敌距离", data.DetectionRange.ToString(), "花费", data.Cost.ToString());
        DrawMainStatRow("冷却", data.Cooldown.ToString(), "范围攻击", data.areaATK ? "是" : "否");
        DrawMainStatRow("攻击时长", data.atkDuration.ToString(), "动画源", animSource);
    }

    private void DrawMainStatRow(string leftKey, string leftValue, string rightKey, string rightValue)
    {
        Rect rowRect = EditorGUILayout.GetControlRect(false, EditorGUIUtility.singleLineHeight + 2f);
        float valueWidth = Mathf.Max(56f, (rowRect.width - StatKeyWidth * 2f - StatValueInnerGap * 2f - StatGroupGap) * 0.5f);

        Rect leftKeyRect = new Rect(rowRect.x, rowRect.y, StatKeyWidth, rowRect.height);
        Rect leftValueRect = new Rect(leftKeyRect.xMax + StatValueInnerGap, rowRect.y, valueWidth, rowRect.height);
        Rect rightKeyRect = new Rect(leftValueRect.xMax + StatGroupGap, rowRect.y, StatKeyWidth, rowRect.height);
        Rect rightValueRect = new Rect(rightKeyRect.xMax + StatValueInnerGap, rowRect.y, valueWidth, rowRect.height);

        GUI.Label(leftKeyRect, leftKey, statKeyStyle);
        GUI.Label(leftValueRect, leftValue, statValueStyle);
        GUI.Label(rightKeyRect, rightKey, statKeyStyle);
        GUI.Label(rightValueRect, rightValue, statValueStyle);
    }

    private void DrawTraitRows(CharacterData data)
    {
        DrawIconBooleanRow("Traits", new[]
        {
            new IconBoolData("EAIcons/traits/t-0", data.traits != null && data.traits.Red),
            new IconBoolData("EAIcons/traits/t-1", data.traits != null && data.traits.Flt),
            new IconBoolData("EAIcons/traits/t-2", data.traits != null && data.traits.Blk),
            new IconBoolData("EAIcons/traits/t-3", data.traits != null && data.traits.Mtl),
            new IconBoolData("EAIcons/traits/t-4", data.traits != null && data.traits.Ang),
            new IconBoolData("EAIcons/traits/t-5", data.traits != null && data.traits.Aln),
            new IconBoolData("EAIcons/traits/t-6", data.traits != null && data.traits.Z),
            new IconBoolData("EAIcons/traits/t-7", data.traits != null && data.traits.Re),
            new IconBoolData("EAIcons/traits/t-8", data.traits != null && data.traits.Aku),
            new IconBoolData("EAIcons/traits/t-9", data.traits != null && data.traits.None),
        });

        string subTraitsPrefix = isCat ? "EAIcons/traits/st-" : "EAIcons/traits/st-e-";
        DrawIconBooleanRow("SubTraits", new[]
        {
            new IconBoolData(subTraitsPrefix + "0", data.subtraits != null && data.subtraits.Starred),
            new IconBoolData(subTraitsPrefix + "1", data.subtraits != null && data.subtraits.Colossus),
            new IconBoolData(subTraitsPrefix + "2", data.subtraits != null && data.subtraits.Behemoth),
            new IconBoolData(subTraitsPrefix + "3", data.subtraits != null && data.subtraits.Sage),
        });

        DrawIconBooleanRow("Career", new[]
        {
            new IconBoolData("EAIcons/traits/c-1", data.career != null && data.career.Warrior),
            new IconBoolData("EAIcons/traits/c-2", data.career != null && data.career.Deffender),
            new IconBoolData("EAIcons/traits/c-3", data.career != null && data.career.Magician),
            new IconBoolData("EAIcons/traits/c-4", data.career != null && data.career.Supporter),
            new IconBoolData("EAIcons/traits/c-5", data.career != null && data.career.Practician),
        });

        DrawIconBooleanRow("Against", new[]
        {
            new IconBoolData("EAIcons/ac-1", data.againstCareer != null && data.againstCareer.AggainstWarrior),
            new IconBoolData("EAIcons/ac-2", data.againstCareer != null && data.againstCareer.AggainstDeffender),
            new IconBoolData("EAIcons/ac-3", data.againstCareer != null && data.againstCareer.AggainstMagician),
            new IconBoolData("EAIcons/ac-4", data.againstCareer != null && data.againstCareer.AggainstSupporter),
            new IconBoolData("EAIcons/ac-5", data.againstCareer != null && data.againstCareer.AggainstPractician),
        });
    }

    private void DrawAttackOverview(CharacterData data)
    {
        EditorGUILayout.LabelField("ATK Infos", subtitleStyle);
        if (data.atkInfos == null || data.atkInfos.Length == 0)
        {
            EditorGUILayout.HelpBox("No atkInfos configured.", MessageType.None);
            return;
        }

        DrawAtkInfoTableHeader();
        for (int i = 0; i < data.atkInfos.Length; i++)
        {
            ATKInfo info = data.atkInfos[i];
            if (info == null) continue;
            DrawAtkInfoTableRow(i, info);
        }
    }

    private void DrawAtkInfoTableHeader()
    {
        EditorGUILayout.BeginHorizontal("box");
        DrawAtkInfoTableCell("#", AtkColIndexWidth, atkTableHeaderStyle);
        DrawAtkInfoTableCell("ATK", AtkColAtkWidth, atkTableHeaderStyle);
        DrawAtkInfoTableCell("Range", AtkColRangeWidth, atkTableHeaderStyle);
        DrawAtkInfoTableCell("Frame", AtkColFrameWidth, atkTableHeaderStyle);
        DrawAtkInfoTableCell("Flags", AtkColFlagsWidth, atkTableHeaderStyle);
        EditorGUILayout.EndHorizontal();
    }

    private void DrawAtkInfoTableRow(int index, ATKInfo info)
    {
        EditorGUILayout.BeginHorizontal("box");
        DrawAtkInfoTableCell(index.ToString(), AtkColIndexWidth, atkTableCellStyle);
        DrawAtkInfoTableCell(info.ATK.ToString("0.##"), AtkColAtkWidth, atkTableCellStyle);
        DrawAtkInfoTableCell($"({info.ATKRange.x}, {info.ATKRange.y})", AtkColRangeWidth, atkTableCellStyle);
        DrawAtkInfoTableCell(info.frame.ToString(), AtkColFrameWidth, atkTableCellStyle);
        DrawAtkInfoTableCell(FormatAtkInfoFlags(info), AtkColFlagsWidth, atkTableCellStyle);
        EditorGUILayout.EndHorizontal();
    }

    private void DrawAtkInfoTableCell(string text, float width, GUIStyle style)
    {
        GUILayout.Label(text, style ?? bodyStyle, GUILayout.Width(width), GUILayout.MinWidth(width), GUILayout.MaxWidth(width));
    }

    private string FormatAtkInfoFlags(ATKInfo info)
    {
        string green = "#45C75A";
        string red = "#D34A4A";

        // E-/A-: false = green, true = red
        string eColor = info.DoNotTriggerEffects ? red : green;
        string aColor = info.DoNotTriggerAbilities ? red : green;
        // F-: false = red, true = green
        string fColor = info.Friendly ? green : red;

        return $"<color={eColor}>E</color>    <color={aColor}>A</color>    <color={fColor}>F</color>";
    }

    private void DrawIconBooleanRow(string label, IconBoolData[] items)
    {
        EditorGUILayout.BeginHorizontal();
        GUILayout.Label(label, GUILayout.Width(70f));
        for (int i = 0; i < items.Length; i++)
        {
            Sprite icon = EAIconResolver.LoadSpriteOrFallback(items[i].resourcePath);
            Color old = GUI.color;
            GUI.color = items[i].enabled ? Color.white : DisabledIconColor;
            DrawSprite(icon, SmallIconSize, SmallIconSize);
            GUI.color = old;
        }
        EditorGUILayout.EndHorizontal();
    }

    private void DrawIconRows(List<IconRowData> rows, string emptyHint)
    {
        if (rows.Count == 0)
        {
            EditorGUILayout.HelpBox(emptyHint, MessageType.None);
            return;
        }

        for (int i = 0; i < rows.Count; i++)
        {
            DrawIconRow(rows[i]);
            if (i < rows.Count - 1) EditorGUILayout.Space(3f);
        }
    }

    private void DrawIconRow(IconRowData row)
    {
        Sprite icon = EAIconResolver.LoadByNameCode(row.nameCode);
        string displayName = GetLocalizedTextCached(DescTableName, row.nameCode);
        string description = BuildLocalizedDescription(row.descCode, row.probability, row.duration, row.intensity);

        EditorGUILayout.BeginHorizontal("box");
        DrawSprite(icon, LargeIconSize, LargeIconSize);
        EditorGUILayout.BeginVertical();
        EditorGUILayout.LabelField(displayName, subtitleStyle);
        DrawRichLabel(description, descriptionStyle);
        EditorGUILayout.EndVertical();
        EditorGUILayout.EndHorizontal();
    }

    private string BuildLocalizedDescription(string descriptionCode, int probability, int duration, int intensity)
    {
        string format = GetLocalizedTextCached(DescTableName, descriptionCode);
        string p = probability > 0 ? $"<color=#FF3030>{probability}</color>" : null;
        string d = duration > 0 ? $"<color=#FF3030>{duration}</color>" : null;
        string i = intensity > 0 ? $"<color=#FF3030>{intensity}</color>" : null;

        try
        {
            if (i != null) return string.Format(format, p, d, i);
            if (d != null) return string.Format(format, p, d);
            if (p != null) return string.Format(format, p);
            return format;
        }
        catch
        {
            return format;
        }
    }

    private string GetLocalizedTextCached(string tableName, string key)
    {
        if (string.IsNullOrEmpty(key)) return string.Empty;
        string localeCode = GetCurrentLocaleCode();
        string cacheKey = tableName + "|" + localeCode + "|" + key;
        if (LocalizedCache.TryGetValue(cacheKey, out string cached))
        {
            return cached;
        }

        string result = TryGetLocalizedTextFromEditorTable(tableName, key);
        if (!string.IsNullOrEmpty(result) && result != key)
        {
            LocalizedCache[cacheKey] = result;
            return result;
        }

        result = key;
        try
        {
            AsyncOperationHandle init = LocalizationSettings.InitializationOperation;
            if (!init.IsDone) init.WaitForCompletion();
            var handle = LocalizationSettings.StringDatabase.GetLocalizedStringAsync(tableName, key);
            if (!handle.IsDone) handle.WaitForCompletion();
            if (handle.Status == AsyncOperationStatus.Succeeded && !string.IsNullOrEmpty(handle.Result))
            {
                result = handle.Result;
            }
        }
        catch
        {
            result = key;
        }

        LocalizedCache[cacheKey] = result;
        return result;
    }

    private string TryGetLocalizedTextFromEditorTable(string tableName, string key)
    {
        if (string.IsNullOrEmpty(tableName) || string.IsNullOrEmpty(key)) return key;
        StringTable table = GetBestEditorStringTable(tableName);
        if (table == null) return key;
        StringTableEntry entry = table.GetEntry(key);
        if (entry == null) return key;
        return string.IsNullOrEmpty(entry.LocalizedValue) ? key : entry.LocalizedValue;
    }

    private StringTable GetBestEditorStringTable(string tableName)
    {
        string localeCode = GetCurrentLocaleCode();
        string cacheKey = tableName + "|" + localeCode;
        if (EditorTableCache.TryGetValue(cacheKey, out StringTable cached) && cached != null)
        {
            return cached;
        }

        string folder = $"Assets/Resources/Localization/{tableName}";
        string[] guids = AssetDatabase.FindAssets("t:StringTable", new[] { folder });
        if (guids == null || guids.Length == 0)
        {
            EditorTableCache[cacheKey] = null;
            return null;
        }

        StringTable localeMatched = null;
        StringTable defaultTable = null;
        StringTable firstTable = null;
        for (int i = 0; i < guids.Length; i++)
        {
            string path = AssetDatabase.GUIDToAssetPath(guids[i]);
            StringTable table = AssetDatabase.LoadAssetAtPath<StringTable>(path);
            if (table == null) continue;
            if (firstTable == null) firstTable = table;

            string tableLocale = table.LocaleIdentifier.Code;
            if (!string.IsNullOrEmpty(localeCode) && tableLocale == localeCode)
            {
                localeMatched = table;
                break;
            }
            if (path.EndsWith("/" + tableName + ".asset", StringComparison.OrdinalIgnoreCase))
            {
                defaultTable = table;
            }
        }

        StringTable selected = localeMatched ?? defaultTable ?? firstTable;
        EditorTableCache[cacheKey] = selected;
        return selected;
    }

    private string GetCurrentLocaleCode()
    {
        try
        {
            Locale locale = LocalizationSettings.SelectedLocale;
            if (locale != null && !string.IsNullOrEmpty(locale.Identifier.Code))
            {
                return locale.Identifier.Code;
            }
        }
        catch { }
        return "en-US";
    }

    private Sprite GetPortraitSprite(CharacterData data, out bool portraitIsCat)
    {
        portraitIsCat = false;
        string dataPath = AssetDatabase.GetAssetPath(data);
        if (string.IsNullOrEmpty(dataPath)) return null;
        if (PortraitCache.TryGetValue(dataPath, out PortraitLookup cached))
        {
            portraitIsCat = cached.isCat;
            return cached.sprite;
        }

        string folder = Path.GetDirectoryName(dataPath)?.Replace("\\", "/");
        if (string.IsNullOrEmpty(folder))
        {
            PortraitCache[dataPath] = new PortraitLookup();
            return null;
        }

        Sprite portrait = FindFirstSpriteByNameInFolder(folder, "icon_deploy");
        portraitIsCat = portrait != null;
        if (portrait == null)
        {
            portrait = FindFirstSpriteByNameInFolder(folder, "enemy_icon");
            portraitIsCat = false;
        }
        PortraitCache[dataPath] = new PortraitLookup
        {
            sprite = portrait,
            isCat = portraitIsCat
        };
        return portrait;
    }

    private Sprite FindFirstSpriteByNameInFolder(string folder, string fileNameNoExt)
    {
        string[] guids = AssetDatabase.FindAssets(fileNameNoExt + " t:Sprite", new[] { folder });
        if (guids == null || guids.Length == 0) return null;
        string assetPath = AssetDatabase.GUIDToAssetPath(guids[0]);
        if (string.IsNullOrEmpty(assetPath)) return null;
        return AssetDatabase.LoadAssetAtPath<Sprite>(assetPath);
    }

    private void DrawSprite(Sprite sprite, float width, float height)
    {
        Rect rect = GUILayoutUtility.GetRect(width, height, GUILayout.Width(width), GUILayout.Height(height));
        if (sprite == null || sprite.texture == null) return;
        Rect uv = new Rect(
            sprite.rect.x / sprite.texture.width,
            sprite.rect.y / sprite.texture.height,
            sprite.rect.width / sprite.texture.width,
            sprite.rect.height / sprite.texture.height);
        GUI.DrawTextureWithTexCoords(rect, sprite.texture, uv, true);
    }

    private void DrawRichLabel(string content, GUIStyle style)
    {
        GUILayout.Label(content, style ?? bodyStyle ?? GUI.skin.label);
    }

    private void EnsureStyles()
    {
        if (stylesInitialized) return;

        titleStyle = CreateStyleSafe(() => EditorStyles.boldLabel, 15, false, false);
        sectionTitleStyle = CreateStyleSafe(() => EditorStyles.boldLabel, 13, false, false);
        subtitleStyle = CreateStyleSafe(() => EditorStyles.boldLabel, 11, false, false);
        bodyStyle = CreateStyleSafe(() => EditorStyles.label, 11, true, false);
        descriptionStyle = CreateStyleSafe(() => EditorStyles.wordWrappedLabel, 11, true, true);
        statKeyStyle = CreateStyleSafe(() => EditorStyles.miniBoldLabel, 11, false, false);
        statKeyStyle.alignment = TextAnchor.MiddleLeft;
        statValueStyle = CreateStyleSafe(() => EditorStyles.label, 11, false, false);
        statValueStyle.alignment = TextAnchor.MiddleRight;
        atkTableHeaderStyle = CreateStyleSafe(() => EditorStyles.miniBoldLabel, 11, false, false);
        atkTableHeaderStyle.alignment = TextAnchor.MiddleCenter;
        atkTableCellStyle = CreateStyleSafe(() => EditorStyles.label, 11, true, false);
        atkTableCellStyle.alignment = TextAnchor.MiddleCenter;
        stylesInitialized = true;
    }

    private GUIStyle CreateStyleSafe(Func<GUIStyle> styleGetter, int fontSize, bool richText, bool wordWrap)
    {
        GUIStyle baseStyle = null;
        try
        {
            baseStyle = styleGetter?.Invoke();
        }
        catch
        {
            // EditorStyles may be unavailable during early init/layout.
        }

        if (baseStyle == null)
        {
            baseStyle = GUI.skin != null ? GUI.skin.label : new GUIStyle();
        }

        GUIStyle style = new GUIStyle(baseStyle)
        {
            fontSize = fontSize,
            richText = richText,
            wordWrap = wordWrap
        };
        return style;
    }

    private string FormatAttackTypes(List<AttackType> atkTypes)
    {
        if (atkTypes == null || atkTypes.Count == 0) return "(none)";
        List<string> names = new List<string>(atkTypes.Count);
        for (int i = 0; i < atkTypes.Count; i++) names.Add(atkTypes[i].ToString());
        return string.Join(", ", names);
    }

    private string FormatAgainstCareers(AgainstCareer ac)
    {
        if (ac == null) return "(none)";
        List<string> list = new List<string>(5);
        if (ac.AggainstWarrior) list.Add("Warrior");
        if (ac.AggainstDeffender) list.Add("Defender");
        if (ac.AggainstMagician) list.Add("Magician");
        if (ac.AggainstSupporter) list.Add("Supporter");
        if (ac.AggainstPractician) list.Add("Practician");
        return list.Count == 0 ? "(none)" : string.Join(", ", list);
    }

    private void DrawBox(string title, Action drawContent)
    {
        EditorGUILayout.BeginVertical("box");
        EditorGUILayout.LabelField(title, sectionTitleStyle);
        EditorGUILayout.Space(2f);
        drawContent?.Invoke();
        EditorGUILayout.EndVertical();
        EditorGUILayout.Space(4f);
    }

    private IconRowData NewRow(string nameCode, string descCode, int p = 0, int d = 0, int i = 0)
    {
        return new IconRowData
        {
            nameCode = nameCode,
            descCode = descCode,
            probability = p,
            duration = d,
            intensity = i
        };
    }

    private struct IconBoolData
    {
        public string resourcePath;
        public bool enabled;

        public IconBoolData(string resourcePath, bool enabled)
        {
            this.resourcePath = resourcePath;
            this.enabled = enabled;
        }
    }

    private struct IconRowData
    {
        public string nameCode;
        public string descCode;
        public int probability;
        public int duration;
        public int intensity;
    }

    private struct PortraitLookup
    {
        public Sprite sprite;
        public bool isCat;
    }
}
