using System.Collections.Generic;
using System.Text;
using UnityEditor;
using UnityEngine;

[CustomEditor(typeof(LevelData))]
public class LevelDataEditor : Editor
{
    private GUIStyle boxStyle;
    private GUIStyle labelBold;
    private GUIStyle labelSmall;

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
        DrawPropertiesExcluding(serializedObject, "m_Script", "rewardlist", "enemySummoners");

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
    }

    private void DrawRewardSection(SerializedProperty rewardListProp)
    {
        if (rewardListProp == null) return;

        EditorGUILayout.LabelField("通关奖励（自定义编辑）", labelBold);
        EditorGUILayout.BeginHorizontal();
        EditorGUILayout.LabelField($"总数: {rewardListProp.arraySize}", labelSmall);
        if (GUILayout.Button("新增奖励", GUILayout.Width(110)))
        {
            AddRewardElement(rewardListProp);
        }
        EditorGUILayout.EndHorizontal();
        EditorGUILayout.Space(4);

        if (rewardListProp.arraySize == 0)
        {
            EditorGUILayout.HelpBox("当前无奖励，点击“新增奖励”创建。", MessageType.Info);
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
        EditorGUILayout.LabelField($"奖励 #{index}", labelBold);
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

        EditorGUILayout.LabelField("敌人出现信息（自定义编辑）", labelBold);
        EditorGUILayout.BeginHorizontal();
        EditorGUILayout.LabelField($"波次组数: {enemySummonersProp.arraySize}", labelSmall);
        if (GUILayout.Button("新增波次组", GUILayout.Width(110)))
        {
            AddEnemySummoner(enemySummonersProp);
        }
        EditorGUILayout.EndHorizontal();
        EditorGUILayout.Space(4);

        if (enemySummonersProp.arraySize == 0)
        {
            EditorGUILayout.HelpBox("当前无敌人波次组，点击“新增波次组”创建。", MessageType.Info);
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
        if (GUILayout.Button("新增敌人条目", GUILayout.Width(110)))
        {
            AddEnemySummonInfo(infosProp);
        }
        EditorGUILayout.EndHorizontal();
        EditorGUILayout.Space(2);

        if (infosProp.arraySize == 0)
        {
            EditorGUILayout.HelpBox("该波次组暂无敌人条目。", MessageType.Info);
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
        EditorGUILayout.LabelField($"敌人 #{infoIndex}", labelBold);
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
        EditorGUILayout.EndHorizontal();

        EditorGUILayout.BeginHorizontal();
        Sprite icon = GetEnemyIcon(enemyIdProp.stringValue);
        if (icon != null)
            GUILayout.Label(icon.texture, GUILayout.Width(64), GUILayout.Height(64));
        else
            GUILayout.Box("(无头像)", GUILayout.Width(64), GUILayout.Height(64));

        EditorGUILayout.BeginVertical();
        EditorGUILayout.PropertyField(enemyIdProp, new GUIContent("敌人 ID"));
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
        backgroundSprite = Resources.Load<Sprite>($"Background/Maps/{levelData.BackgroundID}");
        baseImageSprite = Resources.Load<Sprite>($"Units/DogeBases/{levelData.BaseImageID}");
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

        Sprite sprite = string.IsNullOrEmpty(rewardPath) ? null : Resources.Load<Sprite>(rewardPath);
        rewardSpriteCache[cacheKey] = sprite;
        return sprite;
    }

    private Sprite GetEnemyIcon(string enemyID)
    {
        if (string.IsNullOrWhiteSpace(enemyID)) return null;
        string id = enemyID.Trim();
        if (enemyIconCache.TryGetValue(id, out Sprite cached)) return cached;

        Sprite icon = Resources.Load<Sprite>($"Units/Enemy Units/{id}/enemy_icon");
        enemyIconCache[id] = icon;
        return icon;
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
    }

    private CharacterData GetEnemyData(string enemyID)
    {
        if (string.IsNullOrWhiteSpace(enemyID)) return null;
        string id = enemyID.Trim();
        if (enemyDataCache.TryGetValue(id, out CharacterData cached)) return cached;

        CharacterData data = Resources.Load<CharacterData>($"Units/Enemy Units/{id}/data");
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
