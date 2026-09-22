using System;
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;
using UnityEngine.Localization;
using UnityEngine.Localization.Settings;
using UnityEngine.Localization.Tables;
using UnityEngine.ResourceManagement.AsyncOperations;

/// <summary>
/// CharacterData / TalentData 这类「角色性能资源」共用的 Inspector 绘制件：
/// 属性图标开关行、效果/能力/抗性列表、以及带本地化描述的条目卡片。
///
/// 现阶段只有 TalentDataEditor 在用；CharacterDataEditor 里还留着同名的私有实现，
/// 等这边验证没问题之后可以把它迁过来，避免两份逻辑各自漂移。
/// </summary>
public static class CharacterAssetEditorGUI
{
    private const string DescTableName = UXPref.Localized_Descriptions;
    private const float LargeIconSize = 32f;
    private static readonly Color DisabledIconColor = new Color(0.4f, 0.4f, 0.4f, 1f);
    private static readonly Color WarningTextColor = new Color(1f, 0.6f, 0.2f, 1f);
    private static readonly Dictionary<string, string> LocalizedCache = new Dictionary<string, string>();
    private static readonly Dictionary<string, StringTable> EditorTableCache = new Dictionary<string, StringTable>();

    private static readonly string[] TraitFields =
        { "Red", "Flt", "Blk", "Mtl", "Ang", "Aln", "Z", "Re", "Aku", "None" };
    private static readonly string[] TraitIcons =
    {
        "EAIcons/traits/t-0","EAIcons/traits/t-1","EAIcons/traits/t-2","EAIcons/traits/t-3","EAIcons/traits/t-4",
        "EAIcons/traits/t-5","EAIcons/traits/t-6","EAIcons/traits/t-7","EAIcons/traits/t-8","EAIcons/traits/t-9"
    };
    private static readonly string[] SubTraitFields = { "Starred", "Colossus", "Behemoth", "Sage" };
    private static readonly string[] CareerFields =
        { "Warrior", "Deffender", "Magician", "Supporter", "Practician" };
    private static readonly string[] CareerIcons =
        { "EAIcons/traits/c-1", "EAIcons/traits/c-2", "EAIcons/traits/c-3", "EAIcons/traits/c-4", "EAIcons/traits/c-5" };
    private static readonly string[] AgainstFields =
        { "AggainstWarrior", "AggainstDeffender", "AggainstMagician", "AggainstSupporter", "AggainstPractician" };
    private static readonly string[] AgainstIcons =
        { "EAIcons/ac-1", "EAIcons/ac-2", "EAIcons/ac-3", "EAIcons/ac-4", "EAIcons/ac-5" };
    private static readonly string[] DreFields =
        { "massiveDamage", "insaneDamage", "tough", "aegis", "strongAgainst" };
    private static readonly string[] DreIcons =
        { "EAIcons/dre-m", "EAIcons/dre-i", "EAIcons/dre-t", "EAIcons/dre-a", "EAIcons/dre-s" };

    private static GUIStyle titleStyle;
    private static GUIStyle sectionTitleStyle;
    private static GUIStyle subtitleStyle;
    private static GUIStyle bodyStyle;
    private static GUIStyle descriptionStyle;
    private static GUIStyle warningStyle;
    private static bool stylesInitialized;

    public static GUIStyle TitleStyle { get { EnsureStyles(); return titleStyle; } }
    public static GUIStyle SectionTitleStyle { get { EnsureStyles(); return sectionTitleStyle; } }
    public static GUIStyle SubtitleStyle { get { EnsureStyles(); return subtitleStyle; } }
    public static GUIStyle BodyStyle { get { EnsureStyles(); return bodyStyle; } }
    public static GUIStyle DescriptionStyle { get { EnsureStyles(); return descriptionStyle; } }
    /// <summary>配置不完整之类的行内提醒，橙色，不够严重到用 HelpBox。</summary>
    public static GUIStyle WarningStyle { get { EnsureStyles(); return warningStyle; } }

    /// <summary>编辑器皮肤可能在两次打开之间变过，OnEnable 里叫一声让样式重建。</summary>
    public static void ResetStyles() => stylesInitialized = false;

    #region Layout

    public static void DrawBox(string title, Action drawContent)
    {
        EnsureStyles();
        EditorGUILayout.BeginVertical("box");
        EditorGUILayout.LabelField(title, sectionTitleStyle);
        EditorGUILayout.Space(2f);
        drawContent?.Invoke();
        EditorGUILayout.EndVertical();
        EditorGUILayout.Space(4f);
    }

    public static void DrawSprite(Sprite sprite, float width, float height)
    {
        Rect rect = GUILayoutUtility.GetRect(width, height, GUILayout.Width(width), GUILayout.Height(height));
        DrawSpriteAtRect(sprite, rect);
    }

    public static void DrawSpriteAtRect(Sprite sprite, Rect rect)
    {
        if (sprite == null || sprite.texture == null) return;
        Rect uv = new Rect(
            sprite.rect.x / sprite.texture.width,
            sprite.rect.y / sprite.texture.height,
            sprite.rect.width / sprite.texture.width,
            sprite.rect.height / sprite.texture.height);
        GUI.DrawTextureWithTexCoords(rect, sprite.texture, uv, true);
    }

    public static void DrawRichLabel(string content, GUIStyle style)
    {
        EnsureStyles();
        GUILayout.Label(content, style ?? descriptionStyle);
    }

    #endregion

    #region Bool icon rows

    public static void DrawTraits(SerializedProperty traits) =>
        DrawBoolIconGroup(traits, "Traits", TraitFields, TraitIcons);

    /// <summary>猫和敌人的副属性图标不是同一套，isCat 决定用哪个前缀。</summary>
    public static void DrawSubTraits(SerializedProperty subtraits, bool isCat)
    {
        string prefix = isCat ? "EAIcons/traits/st-" : "EAIcons/traits/st-e-";
        string[] icons = { prefix + "0", prefix + "1", prefix + "2", prefix + "3" };
        DrawBoolIconGroup(subtraits, "SubTraits", SubTraitFields, icons);
    }

    public static void DrawCareer(SerializedProperty career) =>
        DrawBoolIconGroup(career, "Career", CareerFields, CareerIcons);

    public static void DrawAgainstCareer(SerializedProperty against) =>
        DrawBoolIconGroup(against, "AgainstCareer", AgainstFields, AgainstIcons);

    public static void DrawDRE(SerializedProperty dre) =>
        DrawBoolIconGroup(dre, "DRE", DreFields, DreIcons);

    private static void DrawBoolIconGroup(SerializedProperty owner, string label, string[] fields, string[] iconPaths)
    {
        if (owner == null)
        {
            EditorGUILayout.HelpBox($"{label} 数据不存在。", MessageType.Warning);
            return;
        }

        SerializedProperty[] props = new SerializedProperty[fields.Length];
        for (int i = 0; i < fields.Length; i++) props[i] = owner.FindPropertyRelative(fields[i]);
        DrawCenteredIconToggleRow(props, iconPaths);
    }

    public static void DrawCenteredIconToggleRow(SerializedProperty[] boolProps, string[] iconPaths)
    {
        if (boolProps == null || iconPaths == null || boolProps.Length != iconPaths.Length) return;

        EditorGUILayout.BeginHorizontal();
        GUILayout.FlexibleSpace();
        for (int i = 0; i < boolProps.Length; i++)
        {
            SerializedProperty prop = boolProps[i];
            if (prop == null)
            {
                GUILayout.Space(LargeIconSize + 4f);
                continue;
            }

            Rect rect = GUILayoutUtility.GetRect(LargeIconSize, LargeIconSize, GUILayout.Width(LargeIconSize), GUILayout.Height(LargeIconSize));
            if (GUI.Button(rect, GUIContent.none, GUIStyle.none))
            {
                prop.boolValue = !prop.boolValue;
            }

            Color old = GUI.color;
            GUI.color = prop.boolValue ? Color.white : DisabledIconColor;
            DrawSpriteAtRect(EAIconResolver.LoadSpriteOrFallback(iconPaths[i]), rect);
            GUI.color = old;

            GUILayout.Space(4f);
        }
        GUILayout.FlexibleSpace();
        EditorGUILayout.EndHorizontal();
    }

    #endregion

    #region Effect / Ability / Resistance lists

    /// <summary>iconKind 为 "e" 时是普通效果（带 duration/intensity），"re" 时是效果抗性（只有概率）。</summary>
    public static void DrawCharacterEffectList(SerializedProperty arrayProp, string iconKind, string label)
    {
        if (!DrawListHeader(arrayProp, label, AddCharacterEffectElement)) return;
        for (int i = 0; i < arrayProp.arraySize; i++)
        {
            if (DrawCharacterEffectElement(arrayProp, i, iconKind, label)) break;
        }
    }

    public static void DrawAbilityList(SerializedProperty arrayProp)
    {
        if (!DrawListHeader(arrayProp, "Ability", AddAbilityElement)) return;
        for (int i = 0; i < arrayProp.arraySize; i++)
        {
            if (DrawAbilityElement(arrayProp, i)) break;
        }
    }

    public static void DrawAtkResistanceList(SerializedProperty arrayProp)
    {
        if (!DrawListHeader(arrayProp, "AtkTypeResis", AddAtkResElement)) return;
        for (int i = 0; i < arrayProp.arraySize; i++)
        {
            if (DrawAtkResElement(arrayProp, i)) break;
        }
    }

    /// <summary>画「计数 + 新增」那一行。返回 false 表示没有可画的条目（数据缺失或空表）。</summary>
    private static bool DrawListHeader(SerializedProperty arrayProp, string label, Action<SerializedProperty> onAdd)
    {
        EnsureStyles();
        if (arrayProp == null)
        {
            EditorGUILayout.HelpBox($"{label} 数据不存在。", MessageType.Warning);
            return false;
        }

        EditorGUILayout.BeginHorizontal();
        EditorGUILayout.LabelField($"{label} Count: {arrayProp.arraySize}", bodyStyle);
        if (GUILayout.Button("新增", GUILayout.Width(70)))
        {
            onAdd(arrayProp);
        }
        EditorGUILayout.EndHorizontal();

        if (arrayProp.arraySize == 0)
        {
            EditorGUILayout.HelpBox($"No {label} configured.", MessageType.None);
            return false;
        }
        return true;
    }

    /// <summary>返回 true 表示这一帧删掉了元素，调用方要立刻停止遍历。</summary>
    private static bool DrawCharacterEffectElement(SerializedProperty arrayProp, int index, string iconKind, string label)
    {
        SerializedProperty element = arrayProp.GetArrayElementAtIndex(index);
        SerializedProperty nameProp = element.FindPropertyRelative("name");
        SerializedProperty probabilityProp = element.FindPropertyRelative("probability");
        SerializedProperty durationProp = element.FindPropertyRelative("duration");
        SerializedProperty intensityProp = element.FindPropertyRelative("intensity");

        int id = nameProp.intValue;
        string nameCode = $"N:{iconKind}:{id}";
        string descCode = $"D:{iconKind}:{id}";
        string displayName = GetLocalizedTextCached(DescTableName, nameCode);
        string description = BuildLocalizedDescription(descCode, probabilityProp.intValue, durationProp.intValue, intensityProp.intValue);

        EditorGUILayout.BeginVertical("box");
        EditorGUILayout.BeginHorizontal();
        DrawSprite(EAIconResolver.LoadByNameCode(nameCode), LargeIconSize, LargeIconSize);

        EditorGUILayout.BeginVertical();
        EditorGUILayout.BeginHorizontal();
        EditorGUILayout.LabelField($"#{index}  {displayName}", subtitleStyle);
        GUILayout.FlexibleSpace();
        if (GUILayout.Button("删除", GUILayout.Width(70)))
        {
            arrayProp.DeleteArrayElementAtIndex(index);
            EditorGUILayout.EndHorizontal();
            EditorGUILayout.EndVertical();
            EditorGUILayout.EndHorizontal();
            EditorGUILayout.EndVertical();
            return true;
        }
        EditorGUILayout.EndHorizontal();
        EditorGUILayout.PropertyField(nameProp, new GUIContent("Type"));
        EditorGUILayout.PropertyField(probabilityProp, new GUIContent("Probability"));
        if (iconKind == "e")
        {
            EditorGUILayout.PropertyField(durationProp, new GUIContent("Duration"));
            EditorGUILayout.PropertyField(intensityProp, new GUIContent("Intensity"));
        }
        EditorGUILayout.EndVertical();
        EditorGUILayout.EndHorizontal();

        DrawRichLabel(description, descriptionStyle);
        EditorGUILayout.EndVertical();
        return false;
    }

    private static bool DrawAbilityElement(SerializedProperty arrayProp, int index)
    {
        SerializedProperty element = arrayProp.GetArrayElementAtIndex(index);
        SerializedProperty nameProp = element.FindPropertyRelative("name");
        SerializedProperty probabilityProp = element.FindPropertyRelative("probability");
        SerializedProperty durationProp = element.FindPropertyRelative("duration");
        SerializedProperty intensityProp = element.FindPropertyRelative("intensity");

        int id = nameProp.intValue;
        string nameCode = $"N:a:{id}";
        string descCode = $"D:a:{id}";
        string displayName = GetLocalizedTextCached(DescTableName, nameCode);
        string description = BuildLocalizedDescription(descCode, probabilityProp.intValue, durationProp.intValue, intensityProp.intValue);

        EditorGUILayout.BeginVertical("box");
        EditorGUILayout.BeginHorizontal();
        DrawSprite(EAIconResolver.LoadByNameCode(nameCode), LargeIconSize, LargeIconSize);
        EditorGUILayout.BeginVertical();
        EditorGUILayout.BeginHorizontal();
        EditorGUILayout.LabelField($"Ability #{index}  {displayName}", subtitleStyle);
        GUILayout.FlexibleSpace();
        if (GUILayout.Button("删除", GUILayout.Width(70)))
        {
            arrayProp.DeleteArrayElementAtIndex(index);
            EditorGUILayout.EndHorizontal();
            EditorGUILayout.EndVertical();
            EditorGUILayout.EndHorizontal();
            EditorGUILayout.EndVertical();
            return true;
        }
        EditorGUILayout.EndHorizontal();
        EditorGUILayout.PropertyField(nameProp, new GUIContent("Type"));
        EditorGUILayout.PropertyField(probabilityProp, new GUIContent("Probability"));
        EditorGUILayout.PropertyField(durationProp, new GUIContent("Duration"));
        EditorGUILayout.PropertyField(intensityProp, new GUIContent("Intensity"));
        EditorGUILayout.EndVertical();
        EditorGUILayout.EndHorizontal();
        DrawRichLabel(description, descriptionStyle);
        EditorGUILayout.EndVertical();
        return false;
    }

    private static bool DrawAtkResElement(SerializedProperty arrayProp, int index)
    {
        SerializedProperty element = arrayProp.GetArrayElementAtIndex(index);
        SerializedProperty typeProp = element.FindPropertyRelative("type");
        SerializedProperty intensityProp = element.FindPropertyRelative("intensity");

        int id = typeProp.intValue;
        string nameCode = $"N:ra:{id}";
        string descCode = $"D:ra:{id}";
        string displayName = GetLocalizedTextCached(DescTableName, nameCode);
        string description = BuildLocalizedDescription(descCode, intensityProp.intValue, 0, 0);

        EditorGUILayout.BeginVertical("box");
        EditorGUILayout.BeginHorizontal();
        DrawSprite(EAIconResolver.LoadByNameCode(nameCode), LargeIconSize, LargeIconSize);
        EditorGUILayout.BeginVertical();
        EditorGUILayout.BeginHorizontal();
        EditorGUILayout.LabelField($"#{index}  {displayName}", subtitleStyle);
        GUILayout.FlexibleSpace();
        if (GUILayout.Button("删除", GUILayout.Width(70)))
        {
            arrayProp.DeleteArrayElementAtIndex(index);
            EditorGUILayout.EndHorizontal();
            EditorGUILayout.EndVertical();
            EditorGUILayout.EndHorizontal();
            EditorGUILayout.EndVertical();
            return true;
        }
        EditorGUILayout.EndHorizontal();
        EditorGUILayout.PropertyField(typeProp, new GUIContent("Type"));
        EditorGUILayout.PropertyField(intensityProp, new GUIContent("Intensity"));
        EditorGUILayout.EndVertical();
        EditorGUILayout.EndHorizontal();
        DrawRichLabel(description, descriptionStyle);
        EditorGUILayout.EndVertical();
        return false;
    }

    private static void AddCharacterEffectElement(SerializedProperty arrayProp)
    {
        int idx = arrayProp.arraySize;
        arrayProp.InsertArrayElementAtIndex(idx);
        SerializedProperty element = arrayProp.GetArrayElementAtIndex(idx);
        element.FindPropertyRelative("name").intValue = 0;
        element.FindPropertyRelative("probability").intValue = 0;
        element.FindPropertyRelative("duration").intValue = 0;
        element.FindPropertyRelative("intensity").intValue = 0;
    }

    private static void AddAbilityElement(SerializedProperty arrayProp)
    {
        int idx = arrayProp.arraySize;
        arrayProp.InsertArrayElementAtIndex(idx);
        SerializedProperty element = arrayProp.GetArrayElementAtIndex(idx);
        element.FindPropertyRelative("name").intValue = 0;
        element.FindPropertyRelative("probability").intValue = 0;
        element.FindPropertyRelative("duration").intValue = 0;
        element.FindPropertyRelative("intensity").intValue = 0;
    }

    private static void AddAtkResElement(SerializedProperty arrayProp)
    {
        int idx = arrayProp.arraySize;
        arrayProp.InsertArrayElementAtIndex(idx);
        SerializedProperty element = arrayProp.GetArrayElementAtIndex(idx);
        element.FindPropertyRelative("type").intValue = 0;
        element.FindPropertyRelative("intensity").intValue = 0;
    }

    #endregion

    #region Localization

    public static string BuildLocalizedDescription(string descriptionCode, int probability, int duration, int intensity)
    {
        string format = GetLocalizedTextCached(DescTableName, descriptionCode);
        string p = $"<color=#FF3030>{probability}</color>";
        string d = $"<color=#FF3030>{duration}</color>";
        string i = $"<color=#FF3030>{intensity}</color>";

        try
        {
            return string.Format(format, p, d, i);
        }
        catch
        {
            return format;
        }
    }

    public static string GetLocalizedTextCached(string tableName, string key)
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

    private static string TryGetLocalizedTextFromEditorTable(string tableName, string key)
    {
        if (string.IsNullOrEmpty(tableName) || string.IsNullOrEmpty(key)) return key;
        StringTable table = GetBestEditorStringTable(tableName);
        if (table == null) return key;
        StringTableEntry entry = table.GetEntry(key);
        if (entry == null) return key;
        return string.IsNullOrEmpty(entry.LocalizedValue) ? key : entry.LocalizedValue;
    }

    private static StringTable GetBestEditorStringTable(string tableName)
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

    private static string GetCurrentLocaleCode()
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

    #endregion

    #region Styles

    private static void EnsureStyles()
    {
        if (stylesInitialized && titleStyle != null) return;

        titleStyle = CreateStyleSafe(() => EditorStyles.boldLabel, 15, false, false);
        sectionTitleStyle = CreateStyleSafe(() => EditorStyles.boldLabel, 13, false, false);
        subtitleStyle = CreateStyleSafe(() => EditorStyles.boldLabel, 11, false, false);
        bodyStyle = CreateStyleSafe(() => EditorStyles.label, 11, true, false);
        descriptionStyle = CreateStyleSafe(() => EditorStyles.wordWrappedLabel, 11, true, true);
        warningStyle = CreateStyleSafe(() => EditorStyles.miniBoldLabel, 11, false, false);
        warningStyle.normal.textColor = WarningTextColor;
        stylesInitialized = true;
    }

    private static GUIStyle CreateStyleSafe(Func<GUIStyle> styleGetter, int fontSize, bool richText, bool wordWrap)
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

        return new GUIStyle(baseStyle)
        {
            fontSize = fontSize,
            richText = richText,
            wordWrap = wordWrap
        };
    }

    #endregion
}
