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
    private const float SmallIconSize = 48f;
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
        serializedObject.ApplyModifiedProperties();
    }

    private void DrawSummaryPanels(CharacterData data)
    {
        EditorGUILayout.Space(6f);
        EditorGUILayout.LabelField("Character Data Overview", titleStyle);
        EditorGUILayout.Space(4f);

        SerializedProperty nameProp = serializedObject.FindProperty("Name");
        SerializedProperty eliteProp = serializedObject.FindProperty("isEliteUnit");
        SerializedProperty unityAnimatedProp = serializedObject.FindProperty("UNITYAnimated");
        SerializedProperty spineAnimatedProp = serializedObject.FindProperty("SPINEAnimated");
        SerializedProperty healthProp = serializedObject.FindProperty("Health");
        SerializedProperty kbProp = serializedObject.FindProperty("KB");
        SerializedProperty speedProp = serializedObject.FindProperty("Speed");
        SerializedProperty reloadProp = serializedObject.FindProperty("Reload");
        SerializedProperty detectRangeProp = serializedObject.FindProperty("DetectionRange");
        SerializedProperty costProp = serializedObject.FindProperty("Cost");
        SerializedProperty cooldownProp = serializedObject.FindProperty("Cooldown");
        SerializedProperty areaAtkProp = serializedObject.FindProperty("areaATK");
        SerializedProperty atkDurationProp = serializedObject.FindProperty("atkDuration");
        SerializedProperty baseEmotionProp = serializedObject.FindProperty("baseEmotion");
        SerializedProperty atkTypeProp = serializedObject.FindProperty("ATKType");
        SerializedProperty atkInfosProp = serializedObject.FindProperty("atkInfos");
        SerializedProperty dreProp = serializedObject.FindProperty("DRE");

        DrawBox("Core Stats", () =>
        {
            DrawPortraitAndMainStats(data);
            EditorGUILayout.Space(6f);
            EditorGUILayout.PropertyField(nameProp, new GUIContent("Name"));
            EditorGUILayout.PropertyField(eliteProp, new GUIContent("Elite Unit"));
            EditorGUILayout.PropertyField(unityAnimatedProp, new GUIContent("UNITYAnimated"));
            EditorGUILayout.PropertyField(spineAnimatedProp, new GUIContent("SPINEAnimated"));
            DrawEditableCoreStatGrid(
                healthProp, kbProp,
                speedProp, reloadProp,
                detectRangeProp, costProp,
                cooldownProp, atkDurationProp,
                areaAtkProp);
            EditorGUILayout.PropertyField(baseEmotionProp, new GUIContent("Base Emotion"));
            DrawCatLevel50Preview(data);
            EditorGUILayout.PropertyField(atkTypeProp, new GUIContent("ATK Types"), true);
            DrawAtkInfosTable(atkInfosProp);
        });

        DrawBox("Traits", () =>
        {
            DrawTraitButtons();
        });

        DrawBox("SubTraits", () =>
        {
            DrawSubTraitButtons();
        });

        DrawBox("Career", () =>
        {
            DrawCareerButtons();
        });

        DrawBox("AgainstCareer", () =>
        {
            DrawAgainstCareerButtons();
        });

        DrawBox("DRE", () =>
        {
            DrawDreButtons(dreProp);
        });

        DrawBox("Effects", () =>
        {
            DrawCharacterEffectEditor(serializedObject.FindProperty("characterEffects"), "e", "Effect");
        });

        DrawBox("Abilities", () =>
        {
            DrawAbilityEditor(serializedObject.FindProperty("abilities"));
        });

        DrawBox("AtkTypeResis", () =>
        {
            DrawAtkResistanceEditor(serializedObject.FindProperty("atkTypeResis"));
        });

        DrawBox("Effect Resistances", () =>
        {
            DrawCharacterEffectEditor(serializedObject.FindProperty("effectResistances"), "re", "Resistance");
        });
    }

    private void DrawPortraitAndMainStats(CharacterData data)
    {
        Sprite portrait = GetPortraitSprite(data, out bool portraitIsCat);
        isCat = portraitIsCat;
        float portraitW = isCat ? PortraitCatWidth : PortraitEnemySize;
        float portraitH = isCat ? PortraitCatHeight : PortraitEnemySize;

        EditorGUILayout.BeginHorizontal();
        DrawSprite(portrait ?? EAIconResolver.LoadSpriteOrFallback(string.Empty), portraitW, portraitH);
        EditorGUILayout.BeginVertical();
        EditorGUILayout.LabelField($"{data.Name} {(data.isEliteUnit ? "[战术单位]" : string.Empty)}", subtitleStyle);
        EditorGUILayout.LabelField($"{(isCat ? "50 级满宝数据" : "100% 倍率数据")}", subtitleStyle);
        EditorGUILayout.EndVertical();
        EditorGUILayout.EndHorizontal();
    }

    private void DrawMainStatGrid(CharacterData data, string animSource)
    {
        EditorGUILayout.Space(2f);
        int hp = data.Health * (isCat ? 27 : 1);
        DrawMainStatRow("血量", hp.ToString(), "KB", data.KB.ToString());
        DrawMainStatRow("速度", data.Speed.ToString(), "攻击恢复", data.Reload.ToString());
        DrawMainStatRow("索敌距离", data.DetectionRange.ToString(), "金钱", data.Cost.ToString());
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

    private void DrawEditableCoreStatGrid(
        SerializedProperty healthProp,
        SerializedProperty kbProp,
        SerializedProperty speedProp,
        SerializedProperty reloadProp,
        SerializedProperty detectRangeProp,
        SerializedProperty costProp,
        SerializedProperty cooldownProp,
        SerializedProperty atkDurationProp,
        SerializedProperty areaAtkProp)
    {
        EditorGUILayout.Space(3f);
        DrawCoreStatPair("血量", healthProp, "KB", kbProp);
        DrawCoreStatPair("速度", speedProp, "攻击恢复", reloadProp);
        DrawCoreStatPair("索敌距离", detectRangeProp, "金钱", costProp);
        DrawCoreStatPair("冷却", cooldownProp, "攻击时长", atkDurationProp);
        DrawCoreStatPair("范围攻击", areaAtkProp, null, null);
    }

    private void DrawCoreStatPair(string leftLabel, SerializedProperty leftProp, string rightLabel, SerializedProperty rightProp)
    {
        EditorGUILayout.BeginHorizontal();
        EditorGUILayout.PropertyField(leftProp, new GUIContent(leftLabel));
        if (rightProp != null)
        {
            EditorGUILayout.PropertyField(rightProp, new GUIContent(rightLabel));
        }
        EditorGUILayout.EndHorizontal();
    }

    private void DrawCatLevel50Preview(CharacterData data)
    {
        if (!isCat || data == null) return;

        const int level50FullTreasureMultiplier = 27;
        EditorGUILayout.Space(4f);
        EditorGUILayout.LabelField("猫咪50级预览（满宝）", subtitleStyle);
        EditorGUILayout.LabelField($"50级血量: {data.Health * level50FullTreasureMultiplier}", bodyStyle);

        if (data.atkInfos == null || data.atkInfos.Length == 0)
        {
            EditorGUILayout.LabelField("ATKInfo: (none)", bodyStyle);
            return;
        }

        for (int i = 0; i < data.atkInfos.Length; i++)
        {
            ATKInfo info = data.atkInfos[i];
            if (info == null) continue;
            int scaledAtk = Mathf.RoundToInt(info.ATK * level50FullTreasureMultiplier);
            EditorGUILayout.LabelField(
                $"ATKInfo[{i}] 伤害:{scaledAtk}  帧:{info.frame}  范围:({info.ATKRange.x}, {info.ATKRange.y})",
                bodyStyle);
        }
    }

    private void DrawAtkInfosTable(SerializedProperty atkInfosProp)
    {
        if (atkInfosProp == null)
        {
            EditorGUILayout.HelpBox("ATK Infos 数据不存在。", MessageType.Warning);
            return;
        }

        EditorGUILayout.Space(4f);
        EditorGUILayout.BeginHorizontal();
        EditorGUILayout.LabelField("ATK Infos", subtitleStyle);
        GUILayout.FlexibleSpace();
        if (GUILayout.Button("新增ATK", GUILayout.Width(80)))
        {
            AddAtkInfoElement(atkInfosProp);
        }
        EditorGUILayout.EndHorizontal();

        DrawAtkInfoTableHeader();
        if (atkInfosProp.arraySize == 0)
        {
            EditorGUILayout.HelpBox("No atkInfos configured.", MessageType.None);
            return;
        }

        for (int i = 0; i < atkInfosProp.arraySize; i++)
        {
            if (DrawAtkInfoTableRowEditable(atkInfosProp, i)) break;
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
        GUILayout.Label("Ops", atkTableHeaderStyle, GUILayout.Width(45f));
        EditorGUILayout.EndHorizontal();
    }

    private bool DrawAtkInfoTableRowEditable(SerializedProperty atkInfosProp, int index)
    {
        SerializedProperty infoProp = atkInfosProp.GetArrayElementAtIndex(index);
        SerializedProperty atkProp = infoProp.FindPropertyRelative("ATK");
        SerializedProperty rangeProp = infoProp.FindPropertyRelative("ATKRange");
        SerializedProperty frameProp = infoProp.FindPropertyRelative("frame");
        SerializedProperty noEffectsProp = infoProp.FindPropertyRelative("DoNotTriggerEffects");
        SerializedProperty noAbilitiesProp = infoProp.FindPropertyRelative("DoNotTriggerAbilities");
        SerializedProperty friendlyProp = infoProp.FindPropertyRelative("Friendly");

        EditorGUILayout.BeginHorizontal("box");
        GUILayout.Label(index.ToString(), atkTableCellStyle, GUILayout.Width(AtkColIndexWidth));
        atkProp.floatValue = EditorGUILayout.FloatField(atkProp.floatValue, GUILayout.Width(AtkColAtkWidth));
        EditorGUILayout.PropertyField(rangeProp, GUIContent.none, GUILayout.Width(AtkColRangeWidth));
        frameProp.intValue = EditorGUILayout.IntField(frameProp.intValue, GUILayout.Width(AtkColFrameWidth));

        EditorGUILayout.BeginHorizontal(GUILayout.Width(AtkColFlagsWidth));
        DrawAtkFlagButton("E", noEffectsProp, true);
        GUILayout.Space(4f);
        DrawAtkFlagButton("A", noAbilitiesProp, true);
        GUILayout.Space(4f);
        DrawAtkFlagButton("F", friendlyProp, false);
        EditorGUILayout.EndHorizontal();

        if (GUILayout.Button("删", GUILayout.Width(40f)))
        {
            atkInfosProp.DeleteArrayElementAtIndex(index);
            EditorGUILayout.EndHorizontal();
            return true;
        }

        EditorGUILayout.EndHorizontal();
        return false;
    }

    private void DrawAtkFlagButton(string letter, SerializedProperty boolProp, bool trueIsRed)
    {
        if (boolProp == null)
        {
            GUILayout.Label("-", atkTableCellStyle, GUILayout.Width(20f));
            return;
        }

        bool value = boolProp.boolValue;
        bool redState = trueIsRed ? value : !value;
        Color oldColor = GUI.color;
        GUI.color = redState ? new Color(0.83f, 0.29f, 0.29f, 1f) : new Color(0.27f, 0.78f, 0.35f, 1f);
        if (GUILayout.Button(letter, EditorStyles.miniButton, GUILayout.Width(20f), GUILayout.Height(18f)))
        {
            boolProp.boolValue = !value;
        }
        GUI.color = oldColor;
    }

    private void DrawAtkInfoTableCell(string text, float width, GUIStyle style)
    {
        GUILayout.Label(text, style ?? bodyStyle, GUILayout.Width(width), GUILayout.MinWidth(width), GUILayout.MaxWidth(width));
    }

    private static void AddAtkInfoElement(SerializedProperty atkInfosProp)
    {
        int idx = atkInfosProp.arraySize;
        atkInfosProp.InsertArrayElementAtIndex(idx);
        SerializedProperty element = atkInfosProp.GetArrayElementAtIndex(idx);
        element.FindPropertyRelative("ATK").floatValue = 1f;
        element.FindPropertyRelative("frame").intValue = 1;
        element.FindPropertyRelative("ATKRange").vector2Value = new Vector2(-200f, 200f);
        element.FindPropertyRelative("DoNotTriggerEffects").boolValue = false;
        element.FindPropertyRelative("DoNotTriggerAbilities").boolValue = false;
        element.FindPropertyRelative("Friendly").boolValue = false;
    }

    private void DrawTraitButtons()
    {
        SerializedProperty traits = serializedObject.FindProperty("traits");
        if (traits == null)
        {
            EditorGUILayout.HelpBox("Traits 数据不存在。", MessageType.Warning);
            return;
        }

        SerializedProperty[] props =
        {
            traits.FindPropertyRelative("Red"),
            traits.FindPropertyRelative("Flt"),
            traits.FindPropertyRelative("Blk"),
            traits.FindPropertyRelative("Mtl"),
            traits.FindPropertyRelative("Ang"),
            traits.FindPropertyRelative("Aln"),
            traits.FindPropertyRelative("Z"),
            traits.FindPropertyRelative("Re"),
            traits.FindPropertyRelative("Aku"),
            traits.FindPropertyRelative("None"),
        };
        string[] iconPaths =
        {
            "EAIcons/traits/t-0","EAIcons/traits/t-1","EAIcons/traits/t-2","EAIcons/traits/t-3","EAIcons/traits/t-4",
            "EAIcons/traits/t-5","EAIcons/traits/t-6","EAIcons/traits/t-7","EAIcons/traits/t-8","EAIcons/traits/t-9"
        };
        DrawCenteredIconToggleRow(props, iconPaths);
    }

    private void DrawSubTraitButtons()
    {
        SerializedProperty subTraits = serializedObject.FindProperty("subtraits");
        if (subTraits == null)
        {
            EditorGUILayout.HelpBox("SubTraits 数据不存在。", MessageType.Warning);
            return;
        }

        string prefix = isCat ? "EAIcons/traits/st-" : "EAIcons/traits/st-e-";
        SerializedProperty[] props =
        {
            subTraits.FindPropertyRelative("Starred"),
            subTraits.FindPropertyRelative("Colossus"),
            subTraits.FindPropertyRelative("Behemoth"),
            subTraits.FindPropertyRelative("Sage"),
        };
        string[] iconPaths =
        {
            prefix + "0",
            prefix + "1",
            prefix + "2",
            prefix + "3",
        };
        DrawCenteredIconToggleRow(props, iconPaths);
    }

    private void DrawCareerButtons()
    {
        SerializedProperty career = serializedObject.FindProperty("career");
        if (career == null)
        {
            EditorGUILayout.HelpBox("Career 数据不存在。", MessageType.Warning);
            return;
        }

        SerializedProperty[] props =
        {
            career.FindPropertyRelative("Warrior"),
            career.FindPropertyRelative("Deffender"),
            career.FindPropertyRelative("Magician"),
            career.FindPropertyRelative("Supporter"),
            career.FindPropertyRelative("Practician"),
        };
        string[] iconPaths =
        {
            "EAIcons/traits/c-1","EAIcons/traits/c-2","EAIcons/traits/c-3","EAIcons/traits/c-4","EAIcons/traits/c-5"
        };
        DrawCenteredIconToggleRow(props, iconPaths);
    }

    private void DrawAgainstCareerButtons()
    {
        SerializedProperty against = serializedObject.FindProperty("againstCareer");
        if (against == null)
        {
            EditorGUILayout.HelpBox("AgainstCareer 数据不存在。", MessageType.Warning);
            return;
        }

        SerializedProperty[] props =
        {
            against.FindPropertyRelative("AggainstWarrior"),
            against.FindPropertyRelative("AggainstDeffender"),
            against.FindPropertyRelative("AggainstMagician"),
            against.FindPropertyRelative("AggainstSupporter"),
            against.FindPropertyRelative("AggainstPractician"),
        };
        string[] iconPaths = { "EAIcons/ac-1", "EAIcons/ac-2", "EAIcons/ac-3", "EAIcons/ac-4", "EAIcons/ac-5" };
        DrawCenteredIconToggleRow(props, iconPaths);
    }

    private void DrawDreButtons(SerializedProperty dreProp)
    {
        if (dreProp == null)
        {
            EditorGUILayout.HelpBox("DRE 数据不存在。", MessageType.Warning);
            return;
        }

        SerializedProperty[] props =
        {
            dreProp.FindPropertyRelative("massiveDamage"),
            dreProp.FindPropertyRelative("insaneDamage"),
            dreProp.FindPropertyRelative("tough"),
            dreProp.FindPropertyRelative("aegis"),
            dreProp.FindPropertyRelative("strongAgainst"),
        };
        string[] iconPaths = { "EAIcons/dre-m", "EAIcons/dre-i", "EAIcons/dre-t", "EAIcons/dre-a", "EAIcons/dre-s" };
        DrawCenteredIconToggleRow(props, iconPaths);
    }

    private void DrawCenteredIconToggleRow(SerializedProperty[] boolProps, string[] iconPaths)
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

    private void DrawCharacterEffectEditor(SerializedProperty arrayProp, string iconKind, string label)
    {
        if (arrayProp == null)
        {
            EditorGUILayout.HelpBox($"{label} 数据不存在。", MessageType.Warning);
            return;
        }

        EditorGUILayout.BeginHorizontal();
        EditorGUILayout.LabelField($"{label} Count: {arrayProp.arraySize}", bodyStyle);
        if (GUILayout.Button("新增", GUILayout.Width(70)))
        {
            AddCharacterEffectElement(arrayProp);
        }
        EditorGUILayout.EndHorizontal();
        if (arrayProp.arraySize == 0)
        {
            EditorGUILayout.HelpBox($"No {label} configured.", MessageType.None);
            return;
        }

        for (int i = 0; i < arrayProp.arraySize; i++)
        {
            if (DrawCharacterEffectElement(arrayProp, i, iconKind, label)) break;
        }
    }

    private bool DrawCharacterEffectElement(SerializedProperty arrayProp, int index, string iconKind, string label)
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

    private void DrawAbilityEditor(SerializedProperty arrayProp)
    {
        if (arrayProp == null)
        {
            EditorGUILayout.HelpBox("Ability 数据不存在。", MessageType.Warning);
            return;
        }

        EditorGUILayout.BeginHorizontal();
        EditorGUILayout.LabelField($"Ability Count: {arrayProp.arraySize}", bodyStyle);
        if (GUILayout.Button("新增", GUILayout.Width(70)))
        {
            AddAbilityElement(arrayProp);
        }
        EditorGUILayout.EndHorizontal();

        if (arrayProp.arraySize == 0)
        {
            EditorGUILayout.HelpBox("No Ability configured.", MessageType.None);
            return;
        }

        for (int i = 0; i < arrayProp.arraySize; i++)
        {
            if (DrawAbilityElement(arrayProp, i)) break;
        }
    }

    private bool DrawAbilityElement(SerializedProperty arrayProp, int index)
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

    private void DrawAtkResistanceEditor(SerializedProperty arrayProp)
    {
        if (arrayProp == null)
        {
            EditorGUILayout.HelpBox("AtkTypeResis 数据不存在。", MessageType.Warning);
            return;
        }

        EditorGUILayout.BeginHorizontal();
        EditorGUILayout.LabelField($"AtkTypeResis Count: {arrayProp.arraySize}", bodyStyle);
        if (GUILayout.Button("新增", GUILayout.Width(70)))
        {
            AddAtkResElement(arrayProp);
        }
        EditorGUILayout.EndHorizontal();

        if (arrayProp.arraySize == 0)
        {
            EditorGUILayout.HelpBox("No AttackType resistance configured.", MessageType.None);
            return;
        }

        for (int i = 0; i < arrayProp.arraySize; i++)
        {
            if (DrawAtkResElement(arrayProp, i)) break;
        }
    }

    private bool DrawAtkResElement(SerializedProperty arrayProp, int index)
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

    //private void DrawTraitRows(CharacterData data)
    //{
    //    DrawIconBooleanRow("Traits", new[]
    //    {
    //        new IconBoolData("EAIcons/traits/t-0", data.traits != null && data.traits.Red),
    //        new IconBoolData("EAIcons/traits/t-1", data.traits != null && data.traits.Flt),
    //        new IconBoolData("EAIcons/traits/t-2", data.traits != null && data.traits.Blk),
    //        new IconBoolData("EAIcons/traits/t-3", data.traits != null && data.traits.Mtl),
    //        new IconBoolData("EAIcons/traits/t-4", data.traits != null && data.traits.Ang),
    //        new IconBoolData("EAIcons/traits/t-5", data.traits != null && data.traits.Aln),
    //        new IconBoolData("EAIcons/traits/t-6", data.traits != null && data.traits.Z),
    //        new IconBoolData("EAIcons/traits/t-7", data.traits != null && data.traits.Re),
    //        new IconBoolData("EAIcons/traits/t-8", data.traits != null && data.traits.Aku),
    //        new IconBoolData("EAIcons/traits/t-9", data.traits != null && data.traits.None),
    //    });

    //    string subTraitsPrefix = isCat ? "EAIcons/traits/st-" : "EAIcons/traits/st-e-";
    //    DrawIconBooleanRow("SubTraits", new[]
    //    {
    //        new IconBoolData(subTraitsPrefix + "0", data.subtraits != null && data.subtraits.Starred),
    //        new IconBoolData(subTraitsPrefix + "1", data.subtraits != null && data.subtraits.Colossus),
    //        new IconBoolData(subTraitsPrefix + "2", data.subtraits != null && data.subtraits.Behemoth),
    //        new IconBoolData(subTraitsPrefix + "3", data.subtraits != null && data.subtraits.Sage),
    //    });

    //    DrawIconBooleanRow("Career", new[]
    //    {
    //        new IconBoolData("EAIcons/traits/c-1", data.career != null && data.career.Warrior),
    //        new IconBoolData("EAIcons/traits/c-2", data.career != null && data.career.Deffender),
    //        new IconBoolData("EAIcons/traits/c-3", data.career != null && data.career.Magician),
    //        new IconBoolData("EAIcons/traits/c-4", data.career != null && data.career.Supporter),
    //        new IconBoolData("EAIcons/traits/c-5", data.career != null && data.career.Practician),
    //    });

    //    DrawIconBooleanRow("Against", new[]
    //    {
    //        new IconBoolData("EAIcons/ac-1", data.againstCareer != null && data.againstCareer.AggainstWarrior),
    //        new IconBoolData("EAIcons/ac-2", data.againstCareer != null && data.againstCareer.AggainstDeffender),
    //        new IconBoolData("EAIcons/ac-3", data.againstCareer != null && data.againstCareer.AggainstMagician),
    //        new IconBoolData("EAIcons/ac-4", data.againstCareer != null && data.againstCareer.AggainstSupporter),
    //        new IconBoolData("EAIcons/ac-5", data.againstCareer != null && data.againstCareer.AggainstPractician),
    //    });
    //}

    //private void DrawAttackOverview(CharacterData data)
    //{
    //    EditorGUILayout.LabelField("ATK Infos", subtitleStyle);
    //    if (data.atkInfos == null || data.atkInfos.Length == 0)
    //    {
    //        EditorGUILayout.HelpBox("No atkInfos configured.", MessageType.None);
    //        return;
    //    }

    //    DrawAtkInfoTableHeader();
    //    for (int i = 0; i < data.atkInfos.Length; i++)
    //    {
    //        ATKInfo info = data.atkInfos[i];
    //        if (info == null) continue;
    //        DrawAtkInfoTableRow(i, info);
    //    }
    //}

    //private void DrawAtkInfoTableHeader()
    //{
    //    EditorGUILayout.BeginHorizontal("box");
    //    DrawAtkInfoTableCell("#", AtkColIndexWidth, atkTableHeaderStyle);
    //    DrawAtkInfoTableCell("ATK", AtkColAtkWidth, atkTableHeaderStyle);
    //    DrawAtkInfoTableCell("Range", AtkColRangeWidth, atkTableHeaderStyle);
    //    DrawAtkInfoTableCell("Frame", AtkColFrameWidth, atkTableHeaderStyle);
    //    DrawAtkInfoTableCell("Flags", AtkColFlagsWidth, atkTableHeaderStyle);
    //    EditorGUILayout.EndHorizontal();
    //}

    //private void DrawAtkInfoTableRow(int index, ATKInfo info)
    //{
    //    EditorGUILayout.BeginHorizontal("box");
    //    DrawAtkInfoTableCell(index.ToString(), AtkColIndexWidth, atkTableCellStyle);
    //    int atk = (int)info.ATK * (isCat ? 27 : 1);
    //    DrawAtkInfoTableCell(atk.ToString("0.##"), AtkColAtkWidth, atkTableCellStyle);
    //    DrawAtkInfoTableCell($"({info.ATKRange.x}, {info.ATKRange.y})", AtkColRangeWidth, atkTableCellStyle);
    //    DrawAtkInfoTableCell(info.frame.ToString(), AtkColFrameWidth, atkTableCellStyle);
    //    DrawAtkInfoTableCell(FormatAtkInfoFlags(info), AtkColFlagsWidth, atkTableCellStyle);
    //    EditorGUILayout.EndHorizontal();
    //}

    //private void DrawAtkInfoTableCell(string text, float width, GUIStyle style)
    //{
    //    GUILayout.Label(text, style ?? bodyStyle, GUILayout.Width(width), GUILayout.MinWidth(width), GUILayout.MaxWidth(width));
    //}

    //private string FormatAtkInfoFlags(ATKInfo info)
    //{
    //    string green = "#45C75A";
    //    string red = "#D34A4A";

    //    // E-/A-: false = green, true = red
    //    string eColor = info.DoNotTriggerEffects ? red : green;
    //    string aColor = info.DoNotTriggerAbilities ? red : green;
    //    // F-: false = red, true = green
    //    string fColor = info.Friendly ? green : red;

    //    return $"<color={eColor}>E</color>    <color={aColor}>A</color>    <color={fColor}>F</color>";
    //}

    //private void DrawIconBooleanRow(string label, IconBoolData[] items)
    //{
    //    EditorGUILayout.BeginHorizontal();
    //    GUILayout.Label(label, GUILayout.Width(70f));
    //    for (int i = 0; i < items.Length; i++)
    //    {
    //        Sprite icon = EAIconResolver.LoadSpriteOrFallback(items[i].resourcePath);
    //        Color old = GUI.color;
    //        GUI.color = items[i].enabled ? Color.white : DisabledIconColor;
    //        DrawSprite(icon, SmallIconSize, SmallIconSize);
    //        GUI.color = old;
    //    }
    //    EditorGUILayout.EndHorizontal();
    //}

    //private void DrawIconRows(List<IconRowData> rows, string emptyHint)
    //{
    //    if (rows.Count == 0)
    //    {
    //        EditorGUILayout.HelpBox(emptyHint, MessageType.None);
    //        return;
    //    }

    //    for (int i = 0; i < rows.Count; i++)
    //    {
    //        DrawIconRow(rows[i]);
    //        if (i < rows.Count - 1) EditorGUILayout.Space(3f);
    //    }
    //}

    //private void DrawIconRow(IconRowData row)
    //{
    //    Sprite icon = EAIconResolver.LoadByNameCode(row.nameCode);
    //    string displayName = GetLocalizedTextCached(DescTableName, row.nameCode);
    //    string description = BuildLocalizedDescription(row.descCode, row.probability, row.duration, row.intensity);

    //    EditorGUILayout.BeginHorizontal("box");
    //    DrawSprite(icon, LargeIconSize, LargeIconSize);
    //    EditorGUILayout.BeginVertical();
    //    EditorGUILayout.LabelField(displayName, subtitleStyle);
    //    DrawRichLabel(description, descriptionStyle);
    //    EditorGUILayout.EndVertical();
    //    EditorGUILayout.EndHorizontal();
    //}

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
        DrawSpriteAtRect(sprite, rect);
    }

    private void DrawSpriteAtRect(Sprite sprite, Rect rect)
    {
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
