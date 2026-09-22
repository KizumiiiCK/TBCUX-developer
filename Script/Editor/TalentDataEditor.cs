using System.IO;
using UnityEditor;
using UnityEngine;

/// <summary>
/// TalentData 的 Inspector。布局照着 CharacterDataEditor 走，只是去掉了基础数值和 ATKInfo 表——
/// 本能只填「在原角色之上追加什么」，所以剩下的都是属性开关和效果/能力列表。
/// </summary>
[CustomEditor(typeof(TalentData))]
public class TalentDataEditor : Editor
{
    private const float PortraitWidth = 110f;
    private const float PortraitHeight = 85f;
    private const string ThirdPhaseFolder = "2";
    private const string BasePhaseFolder = "0";

    private void OnEnable()
    {
        CharacterAssetEditorGUI.ResetStyles();
    }

    public override void OnInspectorGUI()
    {
        serializedObject.Update();

        if (targets.Length > 1)
        {
            EditorGUILayout.HelpBox("暂不支持多选预览，请单选 TalentData 查看增强面板。", MessageType.Info);
            DrawDefaultInspector();
            serializedObject.ApplyModifiedProperties();
            return;
        }

        TalentData data = target as TalentData;
        if (data == null)
        {
            DrawDefaultInspector();
            serializedObject.ApplyModifiedProperties();
            return;
        }

        DrawPanels(data);
        serializedObject.ApplyModifiedProperties();
    }

    private void DrawPanels(TalentData data)
    {
        EditorGUILayout.Space(6f);
        EditorGUILayout.LabelField("Talent Overview", CharacterAssetEditorGUI.TitleStyle);
        EditorGUILayout.Space(4f);

        DrawHeader(data);

        CharacterAssetEditorGUI.DrawBox("Traits", () =>
            CharacterAssetEditorGUI.DrawTraits(serializedObject.FindProperty("traits")));

        CharacterAssetEditorGUI.DrawBox("SubTraits", () =>
            CharacterAssetEditorGUI.DrawSubTraits(serializedObject.FindProperty("subtraits"), true));

        CharacterAssetEditorGUI.DrawBox("Career", () =>
            CharacterAssetEditorGUI.DrawCareer(serializedObject.FindProperty("career")));

        CharacterAssetEditorGUI.DrawBox("AgainstCareer", () =>
            CharacterAssetEditorGUI.DrawAgainstCareer(serializedObject.FindProperty("againstCareer")));

        CharacterAssetEditorGUI.DrawBox("DRE", () =>
            CharacterAssetEditorGUI.DrawDRE(serializedObject.FindProperty("DRE")));

        CharacterAssetEditorGUI.DrawBox("ATK Types", () =>
            EditorGUILayout.PropertyField(serializedObject.FindProperty("ATKType"), new GUIContent("ATK Types"), true));

        CharacterAssetEditorGUI.DrawBox("Effects", () =>
            CharacterAssetEditorGUI.DrawCharacterEffectList(serializedObject.FindProperty("characterEffects"), "e", "Effect"));

        CharacterAssetEditorGUI.DrawBox("Abilities", () =>
            CharacterAssetEditorGUI.DrawAbilityList(serializedObject.FindProperty("abilities")));

        CharacterAssetEditorGUI.DrawBox("AtkTypeResis", () =>
            CharacterAssetEditorGUI.DrawAtkResistanceList(serializedObject.FindProperty("atkTypeResis")));

        CharacterAssetEditorGUI.DrawBox("Effect Resistances", () =>
            CharacterAssetEditorGUI.DrawCharacterEffectList(serializedObject.FindProperty("effectResistances"), "re", "Resistance"));
    }

    /// <summary>
    /// 头部只是个定位用的提示：本能资源本身没有名字字段，靠所在目录认人，
    /// 所以这里把角色 id 和出战头像捞出来显示。
    /// </summary>
    private void DrawHeader(TalentData data)
    {
        Sprite portrait = FindOwnerPortrait(data, out bool hasThirdPhase);

        EditorGUILayout.BeginHorizontal("box");
        CharacterAssetEditorGUI.DrawSprite(portrait, PortraitWidth, PortraitHeight);
        EditorGUILayout.BeginVertical();
        EditorGUILayout.LabelField(DescribeOwner(data), CharacterAssetEditorGUI.SubtitleStyle);
        if (!hasThirdPhase)
        {
            EditorGUILayout.LabelField("⚠ 此角色还未开启三阶", CharacterAssetEditorGUI.WarningStyle);
        }
        EditorGUILayout.LabelField("本能：叠加在原角色之上的性能补充", CharacterAssetEditorGUI.BodyStyle);
        EditorGUILayout.EndVertical();
        EditorGUILayout.EndHorizontal();
        EditorGUILayout.Space(4f);
    }

    /// <summary>从资源路径倒推 "稀有度/编号"，认不出来就直接把目录显示出来。</summary>
    private string DescribeOwner(TalentData data)
    {
        string folder = GetAssetFolder(data);
        if (string.IsNullOrEmpty(folder)) return "(未保存的 TalentData)";

        string code = Path.GetFileName(folder);
        string rarity = Path.GetFileName(Path.GetDirectoryName(folder) ?? string.Empty);
        if (int.TryParse(code, out _) && int.TryParse(rarity, out _)) return $"角色 {rarity}{code}";
        return folder;
    }

    /// <summary>
    /// 本能基本只有开了三阶的角色才会有，所以头像优先取 2 号阶段；
    /// 找不到就退回 0 号，并通过 hasThirdPhase 让调用方挂个提示。
    /// </summary>
    private Sprite FindOwnerPortrait(TalentData data, out bool hasThirdPhase)
    {
        hasThirdPhase = false;
        string folder = GetAssetFolder(data);
        if (string.IsNullOrEmpty(folder)) return null;

        Sprite thirdPhase = FindDeployIcon($"{folder}/{ThirdPhaseFolder}");
        if (thirdPhase != null)
        {
            hasThirdPhase = true;
            return thirdPhase;
        }
        return FindDeployIcon($"{folder}/{BasePhaseFolder}");
    }

    private Sprite FindDeployIcon(string phaseFolder)
    {
        // 阶段目录可能压根不存在，FindAssets 碰到无效路径会直接报错，先挡一下。
        if (!AssetDatabase.IsValidFolder(phaseFolder)) return null;

        string[] guids = AssetDatabase.FindAssets("icon_deploy t:Sprite", new[] { phaseFolder });
        if (guids == null || guids.Length == 0) return null;
        string path = AssetDatabase.GUIDToAssetPath(guids[0]);
        return string.IsNullOrEmpty(path) ? null : AssetDatabase.LoadAssetAtPath<Sprite>(path);
    }

    private string GetAssetFolder(TalentData data)
    {
        string assetPath = AssetDatabase.GetAssetPath(data);
        if (string.IsNullOrEmpty(assetPath)) return null;
        return Path.GetDirectoryName(assetPath)?.Replace("\\", "/");
    }
}
