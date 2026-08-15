using System;
using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEngine;

[CustomEditor(typeof(GamePlot))]
public class GamePlotEditor : Editor
{
    private const float PortraitSize = 96f;
    private const string DialogueImageFolder = "Assets/Bundled/DialogueImage";
    private const string CgFolder = "Assets/Bundled/CG";

    private static readonly Dictionary<string, Sprite> DialogueImageCache = new Dictionary<string, Sprite>();
    private static readonly Dictionary<string, bool> CgExistCache = new Dictionary<string, bool>();
    private GUIStyle cgMarkStyle;
    private GUIStyle invalidCgMarkStyle;

    public override void OnInspectorGUI()
    {
        serializedObject.Update();

        SerializedProperty contentIdProp = serializedObject.FindProperty("contentID");
        SerializedProperty dialoguesProp = serializedObject.FindProperty("dialogues");

        EditorGUILayout.PropertyField(contentIdProp);
        EditorGUILayout.Space(6f);
        DrawDialogueList(dialoguesProp);

        serializedObject.ApplyModifiedProperties();
    }

    private void DrawDialogueList(SerializedProperty dialoguesProp)
    {
        if (dialoguesProp == null) return;

        EditorGUILayout.LabelField("Dialogues", EditorStyles.boldLabel);
        EditorGUILayout.BeginHorizontal();
        EditorGUILayout.LabelField($"Count: {dialoguesProp.arraySize}");
        if (GUILayout.Button("新增", GUILayout.Width(70f)))
        {
            AddDialogue(dialoguesProp);
        }
        EditorGUILayout.EndHorizontal();
        EditorGUILayout.Space(4f);

        if (dialoguesProp.arraySize == 0)
        {
            EditorGUILayout.HelpBox("当前无 Dialogue 条目。", MessageType.Info);
            return;
        }

        for (int i = 0; i < dialoguesProp.arraySize; i++)
        {
            if (DrawDialogueItem(dialoguesProp, i)) break;
        }
    }

    private bool DrawDialogueItem(SerializedProperty dialoguesProp, int index)
    {
        SerializedProperty item = dialoguesProp.GetArrayElementAtIndex(index);
        SerializedProperty nameProp = item.FindPropertyRelative("DialoguerName");
        SerializedProperty imageProp = item.FindPropertyRelative("DialoguerImage");
        SerializedProperty faceToRightProp = item.FindPropertyRelative("faceToRight");
        SerializedProperty clearImageProp = item.FindPropertyRelative("clearImage");
        SerializedProperty cgProp = item.FindPropertyRelative("cg");

        EditorGUILayout.BeginVertical("box");
        EditorGUILayout.BeginHorizontal();
        EditorGUILayout.LabelField($"Dialogue #{index}", EditorStyles.boldLabel);
        GUILayout.FlexibleSpace();
        if (GUILayout.Button("复制", GUILayout.Width(70f)))
        {
            DuplicateDialogue(dialoguesProp, index);
            EditorGUILayout.EndHorizontal();
            EditorGUILayout.EndVertical();
            return true;
        }
        if (GUILayout.Button("删除", GUILayout.Width(70f)))
        {
            dialoguesProp.DeleteArrayElementAtIndex(index);
            EditorGUILayout.EndHorizontal();
            EditorGUILayout.EndVertical();
            return true;
        }
        EditorGUILayout.EndHorizontal();

        bool faceToRight = faceToRightProp.boolValue;

        EditorGUILayout.BeginHorizontal();
        DrawDialoguePreview(imageProp.stringValue, faceToRight, cgProp.stringValue);

        EditorGUILayout.BeginVertical();
        EditorGUILayout.PropertyField(nameProp, new GUIContent("Dialoguer Name"));
        EditorGUILayout.PropertyField(imageProp, new GUIContent("Dialogue Image"));

        bool newFaceToRight = EditorGUILayout.Toggle(new GUIContent("Face To Right"), faceToRight);
        if (newFaceToRight != faceToRight) faceToRightProp.boolValue = newFaceToRight;

        EditorGUILayout.PropertyField(clearImageProp, new GUIContent("Clear Image"));
        EditorGUILayout.PropertyField(cgProp, new GUIContent("CG"));
        EditorGUILayout.EndVertical();
        EditorGUILayout.EndHorizontal();
        EditorGUILayout.EndVertical();

        return false;
    }

    private void DrawDialoguePreview(string dialoguerImage, bool flipX, string cgName)
    {
        EditorGUILayout.BeginVertical(GUILayout.Width(PortraitSize + 8f));
        Rect rect = GUILayoutUtility.GetRect(PortraitSize, PortraitSize, GUILayout.Width(PortraitSize), GUILayout.Height(PortraitSize));
        Sprite portrait = LoadDialogueSprite(dialoguerImage);
        if (portrait == null || portrait.texture == null)
        {
            EditorGUI.HelpBox(rect, "无头像", MessageType.None);
        }
        else
        {
            DrawSpriteWithFlip(rect, portrait, flipX);
        }

        string trimmedCgName = (cgName ?? string.Empty).Trim();
        if (HasCgAsset(trimmedCgName))
        {
            EnsureStyles();
            GUILayout.Label("[含CG]", cgMarkStyle, GUILayout.Width(PortraitSize));
        }
        else if (!string.IsNullOrEmpty(trimmedCgName))
        {
            EnsureStyles();
            GUILayout.Label("[无效CG]", invalidCgMarkStyle, GUILayout.Width(PortraitSize));
        }
        EditorGUILayout.EndVertical();
    }

    private static void AddDialogue(SerializedProperty dialoguesProp)
    {
        int idx = dialoguesProp.arraySize;
        dialoguesProp.InsertArrayElementAtIndex(idx);
        SerializedProperty item = dialoguesProp.GetArrayElementAtIndex(idx);
        item.FindPropertyRelative("DialoguerName").stringValue = string.Empty;
        item.FindPropertyRelative("DialoguerImage").stringValue = string.Empty;
        item.FindPropertyRelative("faceToRight").boolValue = false;
        item.FindPropertyRelative("clearImage").boolValue = false;
        item.FindPropertyRelative("cg").stringValue = string.Empty;
    }

    private static void DuplicateDialogue(SerializedProperty dialoguesProp, int sourceIndex)
    {
        if (sourceIndex < 0 || sourceIndex >= dialoguesProp.arraySize) return;
        SerializedProperty source = dialoguesProp.GetArrayElementAtIndex(sourceIndex);
        int newIndex = sourceIndex + 1;
        dialoguesProp.InsertArrayElementAtIndex(newIndex);
        SerializedProperty copy = dialoguesProp.GetArrayElementAtIndex(newIndex);
        copy.FindPropertyRelative("DialoguerName").stringValue = source.FindPropertyRelative("DialoguerName").stringValue;
        copy.FindPropertyRelative("DialoguerImage").stringValue = source.FindPropertyRelative("DialoguerImage").stringValue;
        copy.FindPropertyRelative("faceToRight").boolValue = source.FindPropertyRelative("faceToRight").boolValue;
        copy.FindPropertyRelative("clearImage").boolValue = source.FindPropertyRelative("clearImage").boolValue;
        copy.FindPropertyRelative("cg").stringValue = source.FindPropertyRelative("cg").stringValue;
    }

    private static void DrawSpriteWithFlip(Rect rect, Sprite sprite, bool flipX)
    {
        Rect uv = new Rect(
            sprite.rect.x / sprite.texture.width,
            sprite.rect.y / sprite.texture.height,
            sprite.rect.width / sprite.texture.width,
            sprite.rect.height / sprite.texture.height);
        if (flipX)
        {
            uv = new Rect(uv.x + uv.width, uv.y, -uv.width, uv.height);
        }
        GUI.DrawTextureWithTexCoords(rect, sprite.texture, uv, true);
    }

    private static Sprite LoadDialogueSprite(string imageName)
    {
        string key = (imageName ?? string.Empty).Trim();
        if (key.Length == 0) return null;
        if (DialogueImageCache.TryGetValue(key, out Sprite cached)) return cached;

        Sprite sprite = AssetDatabase.LoadAssetAtPath<Sprite>($"{DialogueImageFolder}/{key}.png");
        if (sprite == null)
            sprite = AssetDatabase.LoadAssetAtPath<Sprite>($"{DialogueImageFolder}/{key}.PNG");
        if (sprite == null)
            sprite = BundledAddressables.LoadSync<Sprite>($"DialogueImage/{key}");
        if (sprite == null)
        {
            string[] guids = AssetDatabase.FindAssets($"{key} t:Sprite", new[] { DialogueImageFolder });
            for (int i = 0; i < guids.Length; i++)
            {
                string path = AssetDatabase.GUIDToAssetPath(guids[i]);
                string fileName = Path.GetFileNameWithoutExtension(path);
                if (!string.Equals(fileName, key, StringComparison.OrdinalIgnoreCase)) continue;
                sprite = AssetDatabase.LoadAssetAtPath<Sprite>(path);
                if (sprite != null) break;
            }
        }

        DialogueImageCache[key] = sprite;
        return sprite;
    }

    private static bool HasCgAsset(string cgName)
    {
        string key = (cgName ?? string.Empty).Trim();
        if (key.Length == 0) return false;
        if (CgExistCache.TryGetValue(key, out bool cached)) return cached;

        bool exists = false;
        string[] guids = AssetDatabase.FindAssets(key, new[] { CgFolder });
        for (int i = 0; i < guids.Length; i++)
        {
            string path = AssetDatabase.GUIDToAssetPath(guids[i]);
            string fileName = Path.GetFileNameWithoutExtension(path);
            if (!string.Equals(fileName, key, StringComparison.OrdinalIgnoreCase)) continue;
            exists = true;
            break;
        }

        CgExistCache[key] = exists;
        return exists;
    }

    private void EnsureStyles()
    {
        if (cgMarkStyle != null && invalidCgMarkStyle != null) return;
        cgMarkStyle = new GUIStyle(EditorStyles.centeredGreyMiniLabel)
        {
            normal = { textColor = new Color(0.25f, 0.7f, 0.25f, 1f) },
            alignment = TextAnchor.MiddleCenter
        };
        invalidCgMarkStyle = new GUIStyle(EditorStyles.centeredGreyMiniLabel)
        {
            normal = { textColor = new Color(0.85f, 0.2f, 0.2f, 1f) },
            alignment = TextAnchor.MiddleCenter
        };
    }
}
