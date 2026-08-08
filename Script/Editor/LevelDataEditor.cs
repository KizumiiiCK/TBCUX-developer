using System.Collections.Generic;
using System.IO;
using System.Text;
using UnityEditor;
using UnityEngine;
//using static PlasticGui.WorkspaceWindow.Merge.MergeInProgress;

[CustomEditor(typeof(LevelData))]
public class LevelDataEditor : Editor
{
    private GUIStyle boxStyle;
    private GUIStyle labelBold;
    private GUIStyle labelSmall;

    private Sprite backgroundSprite;
    private Sprite baseImageSprite;
    private Dictionary<int, Sprite> rewardSprites = new Dictionary<int, Sprite>();
    private Dictionary<string, CharacterData> enemyDataCache = new Dictionary<string, CharacterData>();
    //private Dictionary<string, AudioClip> bgmCache = new Dictionary<string, AudioClip>();

    void OnEnable()
    {
        LevelData levelData = (LevelData)target;

        // Load background previews
        backgroundSprite = Resources.Load<Sprite>($"Background/Maps/{levelData.BackgroundID}");
        baseImageSprite = Resources.Load<Sprite>($"Units/DogeBases/{levelData.BaseImageID}");

        // Load all enemy previews
        if (levelData.enemySummoners != null)
        {
            foreach (var summoner in levelData.enemySummoners)
            {
                if (summoner.enemySummonInfos == null) continue;

                foreach (var info in summoner.enemySummonInfos)
                {
                    string path = $"Units/Enemy Units/{info.enemyID}/enemy_icon";
                    info.previewSprite = Resources.Load<Sprite>(path);
                }
            }
        }

        // Load reward icons
        rewardSprites.Clear();
        enemyDataCache.Clear();
        if (levelData.rewardlist != null)
        {
            foreach (var reward in levelData.rewardlist)
            {
                string rewardPath = $"Reward/{reward.id}";
                switch (reward.type)
                {
                    case RewardType.item:
                        rewardPath = $"Reward/{reward.id}";
                        break;
                    case RewardType.character:
                        rewardPath = RewardIconHelper.GetCatDeployIconPath(reward.id.ToString("0000"), 0);
                        break;
                    case RewardType.UnlockTire:
                        rewardPath = RewardIconHelper.GetUnlockTireIconPath(reward.id);
                        break;
                    default: break;
                }
                Sprite spr = Resources.Load<Sprite>(rewardPath);
                rewardSprites[reward.id] = spr;
            }
        }
    }

    public override void OnInspectorGUI()
    {
        serializedObject.Update();

        InitStyles();

        LevelData levelData = (LevelData)target;

        DrawPropertiesExcluding(serializedObject, "m_Script");

        // --- Title Section ---
        EditorGUILayout.Space();
        EditorGUILayout.LabelField("关卡信息", labelBold);
        EditorGUILayout.Space();

        // --- Background Section ---
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
        EditorGUILayout.Space(10);

        // --- Reward Section ---
        if (levelData.rewardlist != null && levelData.rewardlist.Length > 0)
        {
            EditorGUILayout.LabelField("通关奖励", labelBold);
            EditorGUILayout.Space(4);

            foreach (var reward in levelData.rewardlist)
            {
                EditorGUILayout.BeginVertical(boxStyle);
                EditorGUILayout.BeginHorizontal();

                // Left — Reward image
                if (rewardSprites.TryGetValue(reward.id, out Sprite spr) && spr != null)
                    GUILayout.Label(spr.texture, GUILayout.Width(64), GUILayout.Height(64));
                else
                    GUILayout.Box("(无图像)", GUILayout.Width(64), GUILayout.Height(64));

                // Right — Reward info
                EditorGUILayout.BeginVertical();
                EditorGUILayout.LabelField($"类型: {reward.type}", labelSmall);
                EditorGUILayout.LabelField($"ID: {reward.id}", labelSmall);
                EditorGUILayout.EndVertical();

                EditorGUILayout.BeginVertical();
                EditorGUILayout.LabelField($"抽取次数: {reward.drawtimes}", labelSmall);
                EditorGUILayout.LabelField($"掉落率: {reward.droprate}%", labelSmall);
                EditorGUILayout.LabelField($"仅一次: {reward.onlyOnce}", labelSmall);
                EditorGUILayout.EndVertical();

                EditorGUILayout.EndHorizontal();
                EditorGUILayout.EndVertical();
            }

            EditorGUILayout.Space(10);
        }

        // --- Enemy Summoners Section ---
        if (levelData.enemySummoners != null)
        {
            EditorGUILayout.LabelField("敌人出现信息", labelBold);
            EditorGUILayout.Space(4);

            foreach (var summoner in levelData.enemySummoners)
            {
                if (summoner.enemySummonInfos == null) continue;
                
                EditorGUILayout.BeginVertical();
                summoner.HealthPercentageOnBreak = EditorGUILayout.IntField("城联动：", summoner.HealthPercentageOnBreak);
                summoner.bgm = EditorGUILayout.TextField("背景音乐：", summoner.bgm);
                EditorGUILayout.EndVertical();
                EditorGUILayout.Space(4);

                foreach (var info in summoner.enemySummonInfos)
                {
                    EditorGUILayout.BeginVertical(boxStyle);

                    // Enemy ID (editable)
                    EditorGUI.BeginChangeCheck();
                    string newID = EditorGUILayout.TextField("敌人 ID", info.enemyID);
                    if (EditorGUI.EndChangeCheck())
                    {
                        info.enemyID = newID;
                        string path = $"Units/Enemy Units/{info.enemyID}/enemy_icon";
                        info.previewSprite = Resources.Load<Sprite>(path);
                    }
                    info.ratio = EditorGUILayout.IntField("倍率(%)", info.ratio);

                    EditorGUILayout.Space(4);

                    // Layout: icon + info
                    EditorGUILayout.BeginHorizontal();

                    // Icon
                    if (info.previewSprite != null)
                        GUILayout.Label(info.previewSprite.texture, GUILayout.Width(64), GUILayout.Height(64));
                    else
                        GUILayout.Box("(No image)", GUILayout.Width(64), GUILayout.Height(64));

                    // Info text
                    EditorGUILayout.BeginVertical();
                    EditorGUILayout.LabelField($"城联动: {summoner.HealthPercentageOnBreak}%", labelSmall);
                    //info.ratio = EditorGUILayout.IntField("倍率(%)：", info.ratio); 
                    //info.bossShock = EditorGUILayout.Toggle("Boss 震波：", info.bossShock); 
                    //info.repeat = EditorGUILayout.IntField("重复出现：", info.repeat); 
                    //info.firstAppear = EditorGUILayout.IntField("初出现f：", info.firstAppear); 
                    //info.repeatMin = EditorGUILayout.IntField("再出现最短f：", info.repeatMin); 
                    //info.repeatMax = EditorGUILayout.IntField("再出现最长f：", info.repeatMax);
                    CharacterData enemyData = GetEnemyData(info.enemyID);
                    int scaledHealth = enemyData != null ? Mathf.RoundToInt(enemyData.Health * info.ratio / 100f) : 0;
                    string scaledAtkText = BuildScaledAtkText(enemyData, info.ratio);
                    EditorGUILayout.LabelField($"HP: {scaledHealth}", labelSmall);
                    EditorGUILayout.LabelField($"ATK: {scaledAtkText}", labelSmall);

                    EditorGUILayout.LabelField($"Boss 震波: {info.bossShock}", labelSmall);
                    EditorGUILayout.LabelField($"重复出现: {info.repeat}", labelSmall);
                    EditorGUILayout.LabelField($"初出现帧: {info.firstAppear}", labelSmall);
                    EditorGUILayout.LabelField($"再出现最短帧: {info.repeatMin}", labelSmall);
                    EditorGUILayout.LabelField($"再出现最长帧: {info.repeatMax}", labelSmall);
                    EditorGUILayout.EndVertical();

                    EditorGUILayout.EndHorizontal();
                    EditorGUILayout.EndVertical();
                }
            }
        }

        serializedObject.ApplyModifiedProperties();
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
