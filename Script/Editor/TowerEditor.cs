using UnityEditor;
using UnityEngine;

[CustomEditor(typeof(CatBase))]
public class TowerEditor : Editor
{
    //private SerializedProperty weakenProperty;
    //private SerializedProperty stopProperty;
    //private SerializedProperty slowProperty;
    //private SerializedProperty knockbackProperty;
    //private SerializedProperty curseProperty;

    //private void OnEnable()
    //{
    //    weakenProperty = serializedObject.FindProperty("effects.weaken");
    //    stopProperty = serializedObject.FindProperty("effects.stop");
    //    slowProperty = serializedObject.FindProperty("effects.slow");
    //    knockbackProperty = serializedObject.FindProperty("effects.knockback");
    //    curseProperty = serializedObject.FindProperty("effects.curse");
    //}

    //public override void OnInspectorGUI()
    //{
    //    serializedObject.Update(); // 更新序列化对象

    //    GUIStyle customStyle = new GUIStyle(EditorStyles.boldLabel);
    //    customStyle.fontSize = 20;

    //    // 基本 Basic Elements
    //    GUILayout.BeginVertical("box");
    //    GUILayout.Label("基本 Basic Elements", customStyle);
    //    EditorGUILayout.PropertyField(serializedObject.FindProperty("Name"), new GUIContent("Name"));
    //    // ATK
    //    SerializedProperty atkProperty = serializedObject.FindProperty("ATK");
    //    SerializedProperty atkRangeProperty = serializedObject.FindProperty("ATKRange");
    //    if (atkProperty.arraySize == 0)
    //    {
    //        atkProperty.InsertArrayElementAtIndex(0);
    //        atkRangeProperty.InsertArrayElementAtIndex(0);
    //    }
    //    for (int i = 0; i < atkProperty.arraySize; i++)
    //    {
    //        GUILayout.BeginVertical();
    //        GUILayout.BeginHorizontal();
    //        EditorGUILayout.PropertyField(atkProperty.GetArrayElementAtIndex(i), new GUIContent($"ATK [{i}]"));

    //        if (GUILayout.Button("Remove", GUILayout.Width(60)))
    //        {
    //            RemoveElement(ref atkProperty, i);
    //            RemoveElement(ref atkRangeProperty, i);
    //            i--;
    //        }

    //        GUILayout.EndHorizontal();
    //        EditorGUILayout.PropertyField(atkRangeProperty.GetArrayElementAtIndex(i), new GUIContent($"Range [{i}]"));
    //        GUILayout.EndVertical();
    //    }
    //    if (GUILayout.Button("Add ATK"))
    //    {
    //        AddElement(ref atkProperty);
    //        AddElement(ref atkRangeProperty);
    //    }

    //    void RemoveElement(ref SerializedProperty property, int index)
    //    {
    //        property.DeleteArrayElementAtIndex(index);
    //    }
    //    void AddElement(ref SerializedProperty property)
    //    {
    //        property.InsertArrayElementAtIndex(property.arraySize);
    //    }
    //    //ATK Range

    //    //if (atkRangeProperty.arraySize == 0)
    //    //{
    //    //    atkRangeProperty.InsertArrayElementAtIndex(0);
    //    //}

    //    //for (int i = 0; i < atkRangeProperty.arraySize; i++)
    //    //{
    //    //    GUILayout.BeginHorizontal();
    //    //    EditorGUILayout.PropertyField(atkRangeProperty.GetArrayElementAtIndex(i), new GUIContent($"ATK Range [{i}]"));
    //    //    if (GUILayout.Button("Remove", GUILayout.Width(60)))
    //    //    {
    //    //        RemoveElement(ref atkRangeProperty, i);
    //    //    }
    //    //    GUILayout.EndHorizontal();
    //    //}
    //    //if (GUILayout.Button("Add Range"))
    //    //{
    //    //    AddElement(ref atkRangeProperty);
    //    //}
    //    // 其他基本属性
    //    EditorGUILayout.PropertyField(serializedObject.FindProperty("areaATK"), new GUIContent("Area ATK"));
    //    EditorGUILayout.PropertyField(serializedObject.FindProperty("Health"), new GUIContent("Health"));
    //    EditorGUILayout.PropertyField(serializedObject.FindProperty("Reload"), new GUIContent("Reload"));
    //    GUILayout.EndVertical();

    //    serializedObject.ApplyModifiedProperties();
    //}

    //private void DrawEffect(SerializedProperty effectProperty)
    //{
    //    GUILayout.BeginVertical("box");
    //    SerializedProperty effectiveProperty = effectProperty.FindPropertyRelative("effective");
    //    effectiveProperty.boolValue = EditorGUILayout.Toggle(effectProperty.displayName, effectiveProperty.boolValue);

    //    if (effectiveProperty.boolValue)
    //    {
    //        SerializedProperty descriptionProperty = effectProperty.FindPropertyRelative("description");
    //        SerializedProperty probabilityProperty = effectProperty.FindPropertyRelative("probability");
    //        EditorGUILayout.LabelField(descriptionProperty.stringValue, EditorStyles.boldLabel);
    //        probabilityProperty.intValue = (int)EditorGUILayout.Slider("Probability", probabilityProperty.intValue, 0, 100);
    //        EditorGUILayout.PropertyField(effectProperty.FindPropertyRelative("duration"), new GUIContent("Duration"));
    //        EditorGUILayout.PropertyField(effectProperty.FindPropertyRelative("intensity"), new GUIContent("Intensity"));
    //    }

    //    GUILayout.EndVertical();
    //}
}
