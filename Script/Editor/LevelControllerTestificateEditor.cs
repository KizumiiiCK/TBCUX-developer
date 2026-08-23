using System.Collections.Generic;
using System.Text;
using UnityEditor;
using UnityEngine;

[CustomEditor(typeof(LevelController_Testificate))]
public class LevelControllerTestificateEditor : Editor
{
    private const int TeamSize = 13;
    private const int MainTeamSize = 10;
    private const string CatBundledRoot = "Assets/Bundled/Units/Cat Units";

    private static readonly string[] HiddenUntilSceneSection =
    {
        "m_Script",
        "levelData",
        "treasureCount_test",
        "MaxMoney",
        "cats",
        "catLevels",
        "maxEnemyDeploy",
        "maxCatDeploy",
        "LD"
    };

    private GUIStyle boxStyle;
    private GUIStyle labelBold;
    private GUIStyle labelSmall;
    private bool sceneConfigFoldout;

    private readonly Dictionary<string, Sprite> catIconCache = new Dictionary<string, Sprite>();
    private readonly Dictionary<string, CharacterData> catDataCache = new Dictionary<string, CharacterData>();

    public override void OnInspectorGUI()
    {
        serializedObject.Update();
        InitStyles();
        EnsureTeamArrays();

        DrawBasicSettings();
        EditorGUILayout.Space(8);
        DrawTeamSection();
        EditorGUILayout.Space(8);
        DrawSceneConfigSection();

        serializedObject.ApplyModifiedProperties();
    }

    private void DrawBasicSettings()
    {
        EditorGUILayout.LabelField("关卡基本设置", labelBold);
        EditorGUILayout.Space(4);
        EditorGUILayout.BeginVertical(boxStyle);
        EditorGUILayout.PropertyField(serializedObject.FindProperty("levelData"), new GUIContent("测试关卡数据"));
        EditorGUILayout.PropertyField(serializedObject.FindProperty("treasureCount_test"), new GUIContent("宝物资讯覆盖"));
        EditorGUILayout.PropertyField(serializedObject.FindProperty("MaxMoney"), new GUIContent("满钱包开局"));
        EditorGUILayout.PropertyField(serializedObject.FindProperty("maxCatDeploy"), new GUIContent("猫咪部署上限"));
        EditorGUILayout.PropertyField(serializedObject.FindProperty("maxEnemyDeploy"), new GUIContent("敌人部署上限"));
        EditorGUILayout.EndVertical();
    }

    private void DrawTeamSection()
    {
        SerializedProperty catsProp = serializedObject.FindProperty("cats");
        SerializedProperty levelsProp = serializedObject.FindProperty("catLevels");
        int treasure = serializedObject.FindProperty("treasureCount_test").intValue;

        EditorGUILayout.LabelField("测试队伍（13 格）", labelBold);
        EditorGUILayout.LabelField("编号为 5 位：稀有度 + 三位编号 + 阶段。嘉宾位可留空。", labelSmall);
        EditorGUILayout.Space(4);

        EditorGUILayout.LabelField("出战角色", labelBold);
        for (int i = 0; i < MainTeamSize; i++)
        {
            DrawCatSlot(catsProp, levelsProp, i, $"出战 #{i + 1}", treasure);
        }

        EditorGUILayout.Space(6);
        EditorGUILayout.LabelField("嘉宾角色", labelBold);
        for (int i = MainTeamSize; i < TeamSize; i++)
        {
            DrawCatSlot(catsProp, levelsProp, i, $"嘉宾 #{i - MainTeamSize + 1}", treasure);
        }
    }

    private void DrawCatSlot(
        SerializedProperty catsProp,
        SerializedProperty levelsProp,
        int index,
        string title,
        int treasureCount)
    {
        SerializedProperty codeProp = catsProp.GetArrayElementAtIndex(index);
        SerializedProperty levelProp = levelsProp.GetArrayElementAtIndex(index);
        string code = codeProp.stringValue;
        int level = Mathf.Max(1, levelProp.intValue);
        CharacterData data = GetCatData(code);
        Sprite icon = GetCatIcon(code);

        EditorGUILayout.BeginVertical(boxStyle);
        EditorGUILayout.LabelField(title, labelBold);
        EditorGUILayout.BeginHorizontal();

        if (icon != null)
            GUILayout.Label(icon.texture, GUILayout.Width(64), GUILayout.Height(64));
        else
            GUILayout.Box(string.IsNullOrWhiteSpace(code) ? "(空)" : "(无头像)", GUILayout.Width(64), GUILayout.Height(64));

        EditorGUILayout.BeginVertical();
        EditorGUILayout.PropertyField(codeProp, new GUIContent("角色编号"));
        EditorGUILayout.PropertyField(levelProp, new GUIContent("等级"));
        if (levelProp.intValue < 1) levelProp.intValue = 1;

        if (data != null)
        {
            float bonus = GetCombatBonus(treasureCount, level);
            int hp = Mathf.RoundToInt(data.Health * bonus);
            EditorGUILayout.LabelField($"HP: {hp}", labelSmall);
            EditorGUILayout.LabelField($"ATK: {BuildScaledAtkText(data, bonus)}", labelSmall);
        }
        else if (!string.IsNullOrWhiteSpace(code))
        {
            EditorGUILayout.LabelField("未找到角色数据", labelSmall);
        }

        EditorGUILayout.EndVertical();
        EditorGUILayout.EndHorizontal();
        EditorGUILayout.EndVertical();
    }

    private void DrawSceneConfigSection()
    {
        sceneConfigFoldout = EditorGUILayout.Foldout(sceneConfigFoldout, "场景页面配置", true);
        if (!sceneConfigFoldout) return;

        EditorGUILayout.Space(4);
        EditorGUILayout.HelpBox("以下为关卡场景里的 UI / 部署器引用，一般不用改。", MessageType.None);
        DrawPropertiesExcluding(serializedObject, HiddenUntilSceneSection);
    }

    private void EnsureTeamArrays()
    {
        EnsureArraySize(serializedObject.FindProperty("cats"), TeamSize, "00000");
        EnsureArraySize(serializedObject.FindProperty("catLevels"), TeamSize, 1);
    }

    private static void EnsureArraySize(SerializedProperty arrayProp, int size, object defaultValue)
    {
        if (arrayProp == null || !arrayProp.isArray) return;
        while (arrayProp.arraySize < size)
        {
            int index = arrayProp.arraySize;
            arrayProp.InsertArrayElementAtIndex(index);
            SerializedProperty element = arrayProp.GetArrayElementAtIndex(index);
            if (element.propertyType == SerializedPropertyType.String)
                element.stringValue = defaultValue as string ?? string.Empty;
            else if (element.propertyType == SerializedPropertyType.Integer)
                element.intValue = defaultValue is int intValue ? intValue : 1;
        }
        if (arrayProp.arraySize > size) arrayProp.arraySize = size;
    }

    private Sprite GetCatIcon(string code)
    {
        if (!TryParseCatCode(code, out string rarity, out string unit, out string tire)) return null;
        string cacheKey = $"{rarity}{unit}{tire}";
        if (catIconCache.TryGetValue(cacheKey, out Sprite cached)) return cached;

        string folder = $"{CatBundledRoot}/{rarity}/{unit}/{tire}";
        Sprite icon = AssetDatabase.LoadAssetAtPath<Sprite>($"{folder}/icon_deploy.png");
        if (icon == null) icon = AssetDatabase.LoadAssetAtPath<Sprite>($"{folder}/icon_deploy.PNG");
        catIconCache[cacheKey] = icon;
        return icon;
    }

    private CharacterData GetCatData(string code)
    {
        if (!TryParseCatCode(code, out string rarity, out string unit, out string tire)) return null;
        string cacheKey = $"{rarity}{unit}{tire}";
        if (catDataCache.TryGetValue(cacheKey, out CharacterData cached)) return cached;

        CharacterData data = AssetDatabase.LoadAssetAtPath<CharacterData>(
            $"{CatBundledRoot}/{rarity}/{unit}/{tire}/data.asset");
        catDataCache[cacheKey] = data;
        return data;
    }

    private static bool TryParseCatCode(string code, out string rarity, out string unit, out string tire)
    {
        rarity = unit = tire = null;
        if (string.IsNullOrWhiteSpace(code) || code.Length < 5) return false;
        rarity = code[0].ToString();
        unit = code.Substring(1, 3);
        tire = code[4].ToString();
        return true;
    }

    private static float GetCombatBonus(int treasureCount, int level)
    {
        float treasureBonus = 1f + treasureCount / 100f;
        float levelBonus = 0.8f + 0.2f * Mathf.Max(1, level);
        return treasureBonus * levelBonus;
    }

    private static string BuildScaledAtkText(CharacterData data, float bonus)
    {
        if (data == null || data.atkInfos == null || data.atkInfos.Length == 0) return "-";
        StringBuilder sb = new StringBuilder();
        for (int i = 0; i < data.atkInfos.Length; i++)
        {
            if (i > 0) sb.Append(" / ");
            sb.Append(Mathf.RoundToInt(data.atkInfos[i].ATK * bonus));
        }
        return sb.ToString();
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
}
