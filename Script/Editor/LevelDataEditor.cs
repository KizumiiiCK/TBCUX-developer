using System;
using System.Collections.Generic;
using System.Text;
using UnityEditor;
using UnityEngine;
using UnityEngine.Localization;
using UnityEngine.Localization.Settings;
using UnityEngine.Localization.Tables;
using UnityEngine.ResourceManagement.AsyncOperations;

[CustomEditor(typeof(LevelData))]
public class LevelDataEditor : Editor
{
    private GUIStyle boxStyle;
    private GUIStyle labelBold;
    private GUIStyle labelSmall;
    private GUIStyle labelHint;

    private const string InvalidRestrictionHint = "无效限制";
    private static readonly HashSet<string> NoneValueRestrictionKeys =
        new HashSet<string>(StringComparer.Ordinal) { "IV", "OH", "oh", "FS", "fs" };
    private static readonly Dictionary<string, string> LocalizedCache = new Dictionary<string, string>();
    private static readonly Dictionary<string, StringTable> EditorTableCache = new Dictionary<string, StringTable>();

    private Sprite backgroundSprite;
    private Sprite baseImageSprite;
    private readonly Dictionary<string, Sprite> rewardSpriteCache = new Dictionary<string, Sprite>();
    private readonly Dictionary<string, Sprite> enemyIconCache = new Dictionary<string, Sprite>();
    private readonly Dictionary<string, CharacterData> enemyDataCache = new Dictionary<string, CharacterData>();

    private void OnEnable()
    {
        rewardSpriteCache.Clear();
        enemyIconCache.Clear();
        enemyDataCache.Clear();
        RefreshLevelPreviewSprites((LevelData)target);
    }

    public override void OnInspectorGUI()
    {
        serializedObject.Update();
        InitStyles();

        LevelData levelData = (LevelData)target;
        RefreshLevelPreviewSprites(levelData);

        // 隐藏原生 rewardlist / enemySummoners，完全改为自定义展示与编辑。
        DrawPropertiesExcluding(serializedObject, "m_Script", "rewardlist", "enemySummoners", "Restriction");

        DrawBasicPreviewSection();
        EditorGUILayout.Space(8);

        DrawRewardSection(serializedObject.FindProperty("rewardlist"));
        EditorGUILayout.Space(8);

        DrawEnemySummonersSection(serializedObject.FindProperty("enemySummoners"));

        serializedObject.ApplyModifiedProperties();
    }

    private void DrawBasicPreviewSection()
    {
        EditorGUILayout.Space();
        EditorGUILayout.LabelField("关卡信息", labelBold);
        EditorGUILayout.Space(4);

        EditorGUILayout.LabelField("背景资料", labelBold);
        EditorGUILayout.Space(4);
        EditorGUILayout.BeginHorizontal();
        if (backgroundSprite != null)
            GUILayout.Label(backgroundSprite.texture, GUILayout.Width(160), GUILayout.Height(90));
        else
            GUILayout.Box("(无背景)", GUILayout.Width(160), GUILayout.Height(90));

        if (baseImageSprite != null)
            GUILayout.Label(baseImageSprite.texture, GUILayout.Width(160), GUILayout.Height(90));
        else
            GUILayout.Box("(无基地图)", GUILayout.Width(160), GUILayout.Height(90));
        EditorGUILayout.EndHorizontal();

        EditorGUILayout.Space(8);
        DrawRestrictionSection(serializedObject.FindProperty("Restriction"));
    }

    private void DrawRestrictionSection(SerializedProperty restrictionProp)
    {
        if (restrictionProp == null) return;

        EditorGUILayout.LabelField("限制条件", labelBold);
        EditorGUILayout.BeginHorizontal();
        EditorGUILayout.LabelField($"总数: {restrictionProp.arraySize}", labelSmall);
        if (GUILayout.Button("新增", GUILayout.Width(110)))
        {
            int index = restrictionProp.arraySize;
            restrictionProp.InsertArrayElementAtIndex(index);
            restrictionProp.GetArrayElementAtIndex(index).stringValue = string.Empty;
        }
        EditorGUILayout.EndHorizontal();
        EditorGUILayout.Space(4);

        if (restrictionProp.arraySize == 0)
        {
            EditorGUILayout.HelpBox("当前无限制条件。", MessageType.Info);
            return;
        }

        for (int i = 0; i < restrictionProp.arraySize; i++)
        {
            if (DrawRestrictionElement(restrictionProp, i)) break;
        }
    }

    private bool DrawRestrictionElement(SerializedProperty restrictionProp, int index)
    {
        SerializedProperty element = restrictionProp.GetArrayElementAtIndex(index);

        EditorGUILayout.BeginVertical(boxStyle);
        EditorGUILayout.BeginHorizontal();
        EditorGUILayout.LabelField($"#{index}", labelBold);
        GUILayout.FlexibleSpace();
        if (GUILayout.Button("删除", GUILayout.Width(70)))
        {
            restrictionProp.DeleteArrayElementAtIndex(index);
            EditorGUILayout.EndHorizontal();
            EditorGUILayout.EndVertical();
            return true;
        }
        GUI.enabled = index > 0;
        if (GUILayout.Button("↑", GUILayout.Width(28)))
        {
            restrictionProp.MoveArrayElement(index, index - 1);
            EditorGUILayout.EndHorizontal();
            EditorGUILayout.EndVertical();
            GUI.enabled = true;
            return true;
        }
        GUI.enabled = index < restrictionProp.arraySize - 1;
        if (GUILayout.Button("↓", GUILayout.Width(28)))
        {
            restrictionProp.MoveArrayElement(index, index + 1);
            EditorGUILayout.EndHorizontal();
            EditorGUILayout.EndVertical();
            GUI.enabled = true;
            return true;
        }
        GUI.enabled = true;
        EditorGUILayout.EndHorizontal();

        EditorGUILayout.PropertyField(element, new GUIContent("限制"));
        EditorGUILayout.LabelField(FormatRestrictionExplanation(element.stringValue), labelHint);
        EditorGUILayout.EndVertical();
        return false;
    }

    private string FormatRestrictionExplanation(string raw)
    {
        if (string.IsNullOrWhiteSpace(raw)) return InvalidRestrictionHint;
        if (!LevelRestrictionHelper.TrySplitRestriction(raw, out string key, out string value))
            return InvalidRestrictionHint;

        string format = GetLocalizedTextCached(UXPref.Localized_Descriptions, key);
        if (!HasLocalizedField(format, key)) return InvalidRestrictionHint;

        if (NoneValueRestrictionKeys.Contains(key))
            return format.Contains("{0}") ? format.Replace("{0}", string.Empty).Trim() : format;

        if (key == "R+" || key == "R-")
        {
            string detail = value;
            if (int.TryParse(value, out int rarity) && rarity >= 0 && rarity <= 9)
            {
                string rarityName = GetLocalizedTextCached(UXPref.Localized_UI, $"id:t{rarity}");
                if (HasLocalizedField(rarityName, $"id:t{rarity}")) detail = rarityName;
            }
            return FormatSafe(format, detail);
        }

        if (key == "U+" || key == "U-")
        {
            string detail = value;
            if (value.Length == 4 && IsAllDigits(value))
            {
                string unitKey = value + "0";
                string unitName = GetLocalizedTextCached(UXPref.Localized_UnitNames, unitKey);
                if (HasLocalizedField(unitName, unitKey)) detail = unitName;
            }
            return FormatSafe(format, detail);
        }

        if (LevelRestrictionHelper.IsSurgeRestrictionKey(key) &&
            LevelRestrictionHelper.TryParseSurgeRestrictionValue(value, out int probability, out int duration))
        {
            return FormatSafe(format, probability, duration);
        }

        if (key == "zr" &&
            LevelRestrictionHelper.TryParseZombieReviveRestrictionValue(value, out int times, out int hpPercent, out int intervalFrames))
        {
            return FormatSafe(format, times, hpPercent, intervalFrames);
        }

        return FormatSafe(format, value);
    }

    private static bool HasLocalizedField(string localized, string key)
    {
        return !string.IsNullOrEmpty(localized) && localized != key;
    }

    private static bool IsAllDigits(string value)
    {
        if (string.IsNullOrEmpty(value)) return false;
        for (int i = 0; i < value.Length; i++)
        {
            if (!char.IsDigit(value[i])) return false;
        }
        return true;
    }

    private static string FormatSafe(string format, object arg0)
    {
        try { return string.Format(format, arg0); }
        catch (FormatException) { return format + " " + arg0; }
    }

    private static string FormatSafe(string format, object arg0, object arg1)
    {
        try { return string.Format(format, arg0, arg1); }
        catch (FormatException) { return format + " " + arg0 + " " + arg1; }
    }

    private static string FormatSafe(string format, object arg0, object arg1, object arg2)
    {
        try { return string.Format(format, arg0, arg1, arg2); }
        catch (FormatException) { return format + " " + arg0 + " " + arg1 + " " + arg2; }
    }

    private string GetLocalizedTextCached(string tableName, string key)
    {
        if (string.IsNullOrEmpty(key)) return string.Empty;
        string localeCode = GetCurrentLocaleCode();
        string cacheKey = tableName + "|" + localeCode + "|" + key;
        if (LocalizedCache.TryGetValue(cacheKey, out string cached)) return cached;

        string result = TryGetLocalizedTextFromEditorTable(tableName, key);
        if (HasLocalizedField(result, key))
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
                result = handle.Result;
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
            return cached;

        string folder = $"Assets/Resources/Localization/{tableName}";
        string[] guids = AssetDatabase.FindAssets("t:StringTable", new[] { folder });
        if (guids == null || guids.Length == 0)
        {
            EditorTableCache[cacheKey] = null;
            return null;
        }

        StringTable localeMatched = null;
        StringTable zhTable = null;
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
            if (tableLocale == "zh-CN") zhTable = table;
            if (path.EndsWith("/" + tableName + ".asset", StringComparison.OrdinalIgnoreCase))
                defaultTable = table;
        }

        StringTable selected = localeMatched ?? zhTable ?? defaultTable ?? firstTable;
        EditorTableCache[cacheKey] = selected;
        return selected;
    }

    private static string GetCurrentLocaleCode()
    {
        try
        {
            Locale locale = LocalizationSettings.SelectedLocale;
            if (locale != null && !string.IsNullOrEmpty(locale.Identifier.Code))
                return locale.Identifier.Code;
        }
        catch { }
        return "zh-CN";
    }

    private void DrawRewardSection(SerializedProperty rewardListProp)
    {
        if (rewardListProp == null) return;

        EditorGUILayout.LabelField("通关奖励（自定义编辑）", labelBold);
        EditorGUILayout.BeginHorizontal();
        EditorGUILayout.LabelField($"总数: {rewardListProp.arraySize}", labelSmall);
        if (GUILayout.Button("新增", GUILayout.Width(110)))
        {
            AddRewardElement(rewardListProp);
        }
        EditorGUILayout.EndHorizontal();
        EditorGUILayout.Space(4);

        if (rewardListProp.arraySize == 0)
        {
            EditorGUILayout.HelpBox("当前无奖励。", MessageType.Info);
            return;
        }

        for (int i = 0; i < rewardListProp.arraySize; i++)
        {
            if (DrawRewardElement(rewardListProp, i)) break;
        }
    }

    private bool DrawRewardElement(SerializedProperty rewardListProp, int index)
    {
        SerializedProperty element = rewardListProp.GetArrayElementAtIndex(index);
        SerializedProperty typeProp = element.FindPropertyRelative("type");
        SerializedProperty idProp = element.FindPropertyRelative("id");
        SerializedProperty drawTimesProp = element.FindPropertyRelative("drawtimes");
        SerializedProperty dropRateProp = element.FindPropertyRelative("droprate");
        SerializedProperty onlyOnceProp = element.FindPropertyRelative("onlyOnce");

        RewardType rewardType = (RewardType)typeProp.enumValueIndex;
        int rewardId = idProp.intValue;
        Sprite icon = GetRewardSprite(rewardType, rewardId);

        EditorGUILayout.BeginVertical(boxStyle);
        EditorGUILayout.BeginHorizontal();
        EditorGUILayout.LabelField($"#{index}", labelBold);
        GUILayout.FlexibleSpace();
        if (GUILayout.Button("删除", GUILayout.Width(70)))
        {
            rewardListProp.DeleteArrayElementAtIndex(index);
            EditorGUILayout.EndHorizontal();
            EditorGUILayout.EndVertical();
            return true;
        }
        EditorGUILayout.EndHorizontal();
        EditorGUILayout.Space(2);

        EditorGUILayout.BeginHorizontal();
        if (icon != null)
            GUILayout.Label(icon.texture, GUILayout.Width(72), GUILayout.Height(72));
        else
            GUILayout.Box("(无图像)", GUILayout.Width(72), GUILayout.Height(72));

        EditorGUILayout.BeginVertical();
        EditorGUILayout.PropertyField(typeProp, new GUIContent("类型"));
        EditorGUILayout.PropertyField(idProp, new GUIContent("ID"));
        EditorGUILayout.PropertyField(drawTimesProp, new GUIContent("抽取次数"));
        EditorGUILayout.PropertyField(dropRateProp, new GUIContent("掉落率(%)"));
        EditorGUILayout.PropertyField(onlyOnceProp, new GUIContent("仅一次"));

        if ((RewardType)typeProp.enumValueIndex == RewardType.UnlockTire)
        {
            RewardIconHelper.ParseUnlockTireId(idProp.intValue, out string characterId, out int tire);
            EditorGUILayout.LabelField($"解锁目标: {characterId} 的阶段 {tire}", labelSmall);
        }

        EditorGUILayout.EndVertical();
        EditorGUILayout.EndHorizontal();

        EditorGUILayout.EndVertical();
        return false;
    }

    private void AddRewardElement(SerializedProperty rewardListProp)
    {
        int newIndex = rewardListProp.arraySize;
        rewardListProp.InsertArrayElementAtIndex(newIndex);
        SerializedProperty newElement = rewardListProp.GetArrayElementAtIndex(newIndex);
        newElement.FindPropertyRelative("type").enumValueIndex = (int)RewardType.item;
        newElement.FindPropertyRelative("id").intValue = 0;
        newElement.FindPropertyRelative("drawtimes").intValue = 1;
        newElement.FindPropertyRelative("droprate").intValue = 100;
        newElement.FindPropertyRelative("onlyOnce").boolValue = false;
    }

    private void DrawEnemySummonersSection(SerializedProperty enemySummonersProp)
    {
        if (enemySummonersProp == null) return;

        EditorGUILayout.LabelField("敌人出现信息", labelBold);
        EditorGUILayout.BeginHorizontal();
        EditorGUILayout.LabelField($"波次组数: {enemySummonersProp.arraySize}", labelSmall);
        if (GUILayout.Button("新增城联动", GUILayout.Width(110)))
        {
            AddEnemySummoner(enemySummonersProp);
        }
        EditorGUILayout.EndHorizontal();
        EditorGUILayout.Space(4);

        if (enemySummonersProp.arraySize == 0)
        {
            EditorGUILayout.HelpBox("当前无敌人和城联动。", MessageType.Info);
            return;
        }

        for (int i = 0; i < enemySummonersProp.arraySize; i++)
        {
            if (DrawEnemySummoner(enemySummonersProp, i)) break;
        }
    }

    private bool DrawEnemySummoner(SerializedProperty enemySummonersProp, int summonerIndex)
    {
        SerializedProperty summonerProp = enemySummonersProp.GetArrayElementAtIndex(summonerIndex);
        SerializedProperty hpBreakProp = summonerProp.FindPropertyRelative("HealthPercentageOnBreak");
        SerializedProperty bgmProp = summonerProp.FindPropertyRelative("bgm");
        SerializedProperty infosProp = summonerProp.FindPropertyRelative("enemySummonInfos");

        EditorGUILayout.BeginVertical(boxStyle);
        EditorGUILayout.BeginHorizontal();
        EditorGUILayout.LabelField($"波次组 #{summonerIndex}", labelBold);
        if (GUILayout.Button("删除组", GUILayout.Width(80)))
        {
            enemySummonersProp.DeleteArrayElementAtIndex(summonerIndex);
            EditorGUILayout.EndHorizontal();
            EditorGUILayout.EndVertical();
            return true;
        }
        EditorGUILayout.EndHorizontal();

        EditorGUILayout.PropertyField(hpBreakProp, new GUIContent("城联动(%)"));
        EditorGUILayout.PropertyField(bgmProp, new GUIContent("背景音乐"));

        EditorGUILayout.BeginHorizontal();
        EditorGUILayout.LabelField($"敌人条目: {infosProp.arraySize}", labelSmall);
        if (GUILayout.Button("新增敌人", GUILayout.Width(110)))
        {
            AddEnemySummonInfo(infosProp);
        }
        EditorGUILayout.EndHorizontal();
        EditorGUILayout.Space(2);

        if (infosProp.arraySize == 0)
        {
            EditorGUILayout.HelpBox("该组暂无敌人条目。", MessageType.Info);
        }
        else
        {
            for (int i = 0; i < infosProp.arraySize; i++)
            {
                if (DrawEnemySummonInfo(infosProp, i)) break;
            }
        }

        EditorGUILayout.EndVertical();
        return false;
    }

    private bool DrawEnemySummonInfo(SerializedProperty infosProp, int infoIndex)
    {
        SerializedProperty infoProp = infosProp.GetArrayElementAtIndex(infoIndex);
        SerializedProperty enemyIdProp = infoProp.FindPropertyRelative("enemyID");
        SerializedProperty ratioProp = infoProp.FindPropertyRelative("ratio");
        SerializedProperty bossShockProp = infoProp.FindPropertyRelative("bossShock");
        SerializedProperty repeatProp = infoProp.FindPropertyRelative("repeat");
        SerializedProperty firstAppearProp = infoProp.FindPropertyRelative("firstAppear");
        SerializedProperty repeatMinProp = infoProp.FindPropertyRelative("repeatMin");
        SerializedProperty repeatMaxProp = infoProp.FindPropertyRelative("repeatMax");
        CharacterData enemyData = GetEnemyData(enemyIdProp.stringValue);
        int ratio = Mathf.Max(0, ratioProp.intValue);
        int scaledHealth = enemyData != null ? Mathf.RoundToInt(enemyData.Health * ratio / 100f) : 0;
        string scaledAtkText = BuildScaledAtkText(enemyData, ratio);

        EditorGUILayout.BeginVertical(boxStyle);
        EditorGUILayout.BeginHorizontal();
        EditorGUILayout.LabelField($"#{infoIndex}", labelBold);
        GUILayout.FlexibleSpace();
        if (GUILayout.Button("复制", GUILayout.Width(70)))
        {
            DuplicateEnemySummonInfo(infosProp, infoIndex);
            EditorGUILayout.EndHorizontal();
            EditorGUILayout.EndVertical();
            return true;
        }
        if (GUILayout.Button("删除", GUILayout.Width(80)))
        {
            infosProp.DeleteArrayElementAtIndex(infoIndex);
            EditorGUILayout.EndHorizontal();
            EditorGUILayout.EndVertical();
            return true;
        }
        GUI.enabled = infoIndex > 0;
        if (GUILayout.Button("↑", GUILayout.Width(28)))
        {
            infosProp.MoveArrayElement(infoIndex, infoIndex - 1);
            EditorGUILayout.EndHorizontal();
            EditorGUILayout.EndVertical();
            GUI.enabled = true;
            return true;
        }
        GUI.enabled = infoIndex < infosProp.arraySize - 1;
        if (GUILayout.Button("↓", GUILayout.Width(28)))
        {
            infosProp.MoveArrayElement(infoIndex, infoIndex + 1);
            EditorGUILayout.EndHorizontal();
            EditorGUILayout.EndVertical();
            GUI.enabled = true;
            return true;
        }
        GUI.enabled = true;
        EditorGUILayout.EndHorizontal();

        EditorGUILayout.BeginHorizontal();
        Sprite icon = GetEnemyIcon(enemyIdProp.stringValue);
        if (icon != null)
            GUILayout.Label(icon.texture, GUILayout.Width(64), GUILayout.Height(64));
        else
            GUILayout.Box("(无头像)", GUILayout.Width(64), GUILayout.Height(64));

        EditorGUILayout.BeginVertical();
        EditorGUILayout.PropertyField(enemyIdProp, new GUIContent("单位 ID"));
        EditorGUILayout.PropertyField(ratioProp, new GUIContent("倍率(%)"));
        EditorGUILayout.LabelField($"HP: {scaledHealth}", labelSmall);
        EditorGUILayout.LabelField($"ATK: {scaledAtkText}", labelSmall);
        EditorGUILayout.Space(2);
        EditorGUILayout.PropertyField(bossShockProp, new GUIContent("Boss 震波"));
        EditorGUILayout.PropertyField(repeatProp, new GUIContent("重复出现"));
        EditorGUILayout.PropertyField(firstAppearProp, new GUIContent("初出现帧"));
        EditorGUILayout.PropertyField(repeatMinProp, new GUIContent("再出现最短帧"));
        EditorGUILayout.PropertyField(repeatMaxProp, new GUIContent("再出现最长帧"));
        EditorGUILayout.EndVertical();
        EditorGUILayout.EndHorizontal();

        EditorGUILayout.EndVertical();
        return false;
    }

    private static void AddEnemySummoner(SerializedProperty enemySummonersProp)
    {
        int index = enemySummonersProp.arraySize;
        enemySummonersProp.InsertArrayElementAtIndex(index);
        SerializedProperty summoner = enemySummonersProp.GetArrayElementAtIndex(index);
        summoner.FindPropertyRelative("HealthPercentageOnBreak").intValue = 100;
        summoner.FindPropertyRelative("bgm").stringValue = "002";

        SerializedProperty infos = summoner.FindPropertyRelative("enemySummonInfos");
        infos.arraySize = 0;
        AddEnemySummonInfo(infos);
    }

    private static void AddEnemySummonInfo(SerializedProperty infosProp)
    {
        int index = infosProp.arraySize;
        infosProp.InsertArrayElementAtIndex(index);
        SerializedProperty info = infosProp.GetArrayElementAtIndex(index);
        info.FindPropertyRelative("enemyID").stringValue = "e000";
        info.FindPropertyRelative("ratio").intValue = 100;
        info.FindPropertyRelative("bossShock").boolValue = false;
        info.FindPropertyRelative("repeat").intValue = 1;
        info.FindPropertyRelative("firstAppear").intValue = 0;
        info.FindPropertyRelative("repeatMin").intValue = 0;
        info.FindPropertyRelative("repeatMax").intValue = 0;
    }

    private static void DuplicateEnemySummonInfo(SerializedProperty infosProp, int sourceIndex)
    {
        if (sourceIndex < 0 || sourceIndex >= infosProp.arraySize) return;
        SerializedProperty source = infosProp.GetArrayElementAtIndex(sourceIndex);
        int newIndex = sourceIndex + 1;
        infosProp.InsertArrayElementAtIndex(newIndex);
        SerializedProperty copy = infosProp.GetArrayElementAtIndex(newIndex);
        copy.FindPropertyRelative("enemyID").stringValue = source.FindPropertyRelative("enemyID").stringValue;
        copy.FindPropertyRelative("ratio").intValue = source.FindPropertyRelative("ratio").intValue;
        copy.FindPropertyRelative("bossShock").boolValue = source.FindPropertyRelative("bossShock").boolValue;
        copy.FindPropertyRelative("repeat").intValue = source.FindPropertyRelative("repeat").intValue;
        copy.FindPropertyRelative("firstAppear").intValue = source.FindPropertyRelative("firstAppear").intValue;
        copy.FindPropertyRelative("repeatMin").intValue = source.FindPropertyRelative("repeatMin").intValue;
        copy.FindPropertyRelative("repeatMax").intValue = source.FindPropertyRelative("repeatMax").intValue;
    }

    private void RefreshLevelPreviewSprites(LevelData levelData)
    {
        if (levelData == null) return;
        backgroundSprite = AssetDatabase.LoadAssetAtPath<Sprite>(
            $"Assets/Bundled/Background/Maps/{levelData.BackgroundID}.png");
        if (backgroundSprite == null)
        {
            backgroundSprite = AssetDatabase.LoadAssetAtPath<Sprite>(
                $"Assets/Bundled/Background/Maps/{levelData.BackgroundID}.PNG");
        }
        baseImageSprite = AssetDatabase.LoadAssetAtPath<Sprite>(
            $"Assets/Bundled/Units/DogeBases/{levelData.BaseImageID}.png");
    }

    private Sprite GetRewardSprite(RewardType type, int rewardId)
    {
        string cacheKey = $"{(int)type}:{rewardId}";
        if (rewardSpriteCache.TryGetValue(cacheKey, out Sprite cached)) return cached;

        string rewardPath = null;
        switch (type)
        {
            case RewardType.item:
                rewardPath = $"Reward/{rewardId}";
                break;
            case RewardType.character:
                rewardPath = RewardIconHelper.GetCatDeployIconPath(rewardId.ToString("0000"), 0);
                break;
            case RewardType.UnlockTire:
                rewardPath = RewardIconHelper.GetUnlockTireIconPath(rewardId);
                break;
        }

        Sprite sprite = null;
        if (!string.IsNullOrEmpty(rewardPath))
        {
            if (rewardPath.StartsWith("Units/Cat Units/"))
                sprite = AssetDatabase.LoadAssetAtPath<Sprite>($"Assets/Bundled/{rewardPath}.png");
            else
                sprite = Resources.Load<Sprite>(rewardPath);
        }
        rewardSpriteCache[cacheKey] = sprite;
        return sprite;
    }

    private Sprite GetEnemyIcon(string enemyID)
    {
        if (string.IsNullOrWhiteSpace(enemyID)) return null;
        string id = enemyID.Trim();
        if (enemyIconCache.TryGetValue(id, out Sprite cached)) return cached;

        Sprite icon = null;
        if (CharacterPlacer.TryParse(id, false, out UnitIdentity identity) && identity.IsValid)
        {
            string folder = CharacterPlacer.GetBundledFolderPath(identity);
            icon = LoadUnitSprite(folder, "enemy_icon");
        }

        enemyIconCache[id] = icon;
        return icon;
    }

    private static Sprite LoadUnitSprite(string folder, string fileName)
    {
        if (string.IsNullOrEmpty(folder) || string.IsNullOrEmpty(fileName)) return null;
        Sprite sprite = AssetDatabase.LoadAssetAtPath<Sprite>($"{folder}/{fileName}.png");
        if (sprite == null) sprite = AssetDatabase.LoadAssetAtPath<Sprite>($"{folder}/{fileName}.PNG");
        return sprite;
    }

    private void InitStyles()
    {
        if (boxStyle == null)
        {
            boxStyle = new GUIStyle(GUI.skin.box)
            {
                padding = new RectOffset(10, 10, 10, 10),
                margin = new RectOffset(0, 0, 5, 5)
            };
        }
        if (labelBold == null)
        {
            labelBold = new GUIStyle(EditorStyles.boldLabel)
            {
                fontSize = 15
            };
        }
        if (labelSmall == null)
        {
            labelSmall = new GUIStyle(EditorStyles.label)
            {
                fontSize = 12,
                richText = true
            };
        }
        if (labelHint == null)
        {
            labelHint = new GUIStyle(EditorStyles.wordWrappedLabel)
            {
                fontSize = 12,
                richText = true,
                fontStyle = FontStyle.Italic
            };
        }
    }

    private CharacterData GetEnemyData(string enemyID)
    {
        if (string.IsNullOrWhiteSpace(enemyID)) return null;
        string id = enemyID.Trim();
        if (enemyDataCache.TryGetValue(id, out CharacterData cached)) return cached;

        CharacterData data = null;
        if (CharacterPlacer.TryParse(id, false, out UnitIdentity identity) && identity.IsValid)
        {
            data = AssetDatabase.LoadAssetAtPath<CharacterData>($"{CharacterPlacer.GetBundledFolderPath(identity)}/data.asset");
        }
        enemyDataCache[id] = data;
        return data;
    }

    private string BuildScaledAtkText(CharacterData data, int ratio)
    {
        if (data == null || data.atkInfos == null || data.atkInfos.Length == 0) return "-";

        StringBuilder sb = new StringBuilder();
        for (int i = 0; i < data.atkInfos.Length; i++)
        {
            if (i > 0) sb.Append(" / ");
            int scaledAtk = Mathf.RoundToInt(data.atkInfos[i].ATK * ratio / 100f);
            sb.Append(scaledAtk);
        }
        return sb.ToString();
    }
}
