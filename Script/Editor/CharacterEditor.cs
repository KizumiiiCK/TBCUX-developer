using UnityEditor;
using UnityEngine;
using UnityEngine.UIElements;

[CustomEditor(typeof(Character),true)]
public class CharacterEditor : Editor
{
    //private SerializedProperty weakenProperty;
    //private SerializedProperty stopProperty;
    //private SerializedProperty slowProperty;
    //private SerializedProperty knockbackProperty;
    //private SerializedProperty wrapProperty;
    //private SerializedProperty curseProperty;
    //private SerializedProperty dodgeProperty;


    //private void OnEnable()
    //{
    //    // 获取所有需要的SerializedProperty
    //    weakenProperty = serializedObject.FindProperty("effects.weaken");
    //    stopProperty = serializedObject.FindProperty("effects.stop");
    //    slowProperty = serializedObject.FindProperty("effects.slow");
    //    knockbackProperty = serializedObject.FindProperty("effects.knockback");
    //    wrapProperty = serializedObject.FindProperty("effects.wrap");
    //    curseProperty = serializedObject.FindProperty("effects.curse");
    //    dodgeProperty = serializedObject.FindProperty("effects.dodge");
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
    //    EditorGUILayout.PropertyField(serializedObject.FindProperty("isEliteUnit"), new GUIContent("Is Elite Unit"));
    //    EditorGUILayout.PropertyField(serializedObject.FindProperty("affectByStrategy"), new GUIContent("Affect by Strategy"));
    //    // ATK
    //    SerializedProperty atkProperty = serializedObject.FindProperty("ATK");
    //    SerializedProperty atkRangeProperty = serializedObject.FindProperty("ATKRange");
    //    SerializedProperty ATProperty = serializedObject.FindProperty("DoNotTriggerEffects");
    //    if (atkProperty.arraySize == 0)
    //    {
    //        atkProperty.InsertArrayElementAtIndex(0);
    //        atkRangeProperty.InsertArrayElementAtIndex(0);
    //        ATProperty.InsertArrayElementAtIndex(0);
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
    //        EditorGUILayout.PropertyField(ATProperty.GetArrayElementAtIndex(i), new GUIContent($"Do NOT tirgger effects"));
    //        EditorGUILayout.PropertyField(atkRangeProperty.GetArrayElementAtIndex(i), new GUIContent($"Range [{i}]"));
    //        GUILayout.EndVertical();
    //    }
    //    if (GUILayout.Button("Add ATK"))
    //    {
    //        AddElement(ref atkProperty);
    //        AddElement(ref atkRangeProperty);
    //        AddElement(ref ATProperty);
    //    }

    //    void RemoveElement(ref SerializedProperty property, int index)
    //    {
    //        property.DeleteArrayElementAtIndex(index);
    //    }
    //    void AddElement(ref SerializedProperty property)
    //    {
    //        property.InsertArrayElementAtIndex(property.arraySize);
    //    }

    //    EditorGUILayout.PropertyField(serializedObject.FindProperty("areaATK"), new GUIContent("Area ATK"));
    //    EditorGUILayout.PropertyField(serializedObject.FindProperty("one_off"), new GUIContent("One Off"));
    //    EditorGUILayout.PropertyField(serializedObject.FindProperty("Health"), new GUIContent("Health"));
    //    EditorGUILayout.PropertyField(serializedObject.FindProperty("KB"), new GUIContent("Knockback"));
    //    EditorGUILayout.PropertyField(serializedObject.FindProperty("Speed"), new GUIContent("Speed"));
    //    EditorGUILayout.PropertyField(serializedObject.FindProperty("Reload"), new GUIContent("Reload"));
    //    EditorGUILayout.PropertyField(serializedObject.FindProperty("DetectionRange"), new GUIContent("DetectionRange"));
    //    EditorGUILayout.PropertyField(serializedObject.FindProperty("Cost"), new GUIContent("Cost"));
    //    EditorGUILayout.PropertyField(serializedObject.FindProperty("Cooldown"), new GUIContent("Cooldown"));
    //    GUILayout.EndVertical();

    //    // 属性 Character Traits
    //    GUILayout.BeginVertical("box");
    //    GUILayout.Label("属性 Character Traits", customStyle);
    //    EditorGUILayout.PropertyField(serializedObject.FindProperty("traits.Red"), new GUIContent("红 Red"));
    //    EditorGUILayout.PropertyField(serializedObject.FindProperty("traits.Flt"), new GUIContent("浮 Flt"));
    //    EditorGUILayout.PropertyField(serializedObject.FindProperty("traits.Blk"), new GUIContent("黑 Blk"));
    //    EditorGUILayout.PropertyField(serializedObject.FindProperty("traits.Mtl"), new GUIContent("钢 Mtl"));
    //    EditorGUILayout.PropertyField(serializedObject.FindProperty("traits.Ang"), new GUIContent("天 Ang"));
    //    EditorGUILayout.PropertyField(serializedObject.FindProperty("traits.Aln"), new GUIContent("星 Aln"));
    //    EditorGUILayout.PropertyField(serializedObject.FindProperty("traits.Z"), new GUIContent("死 Z"));
    //    EditorGUILayout.PropertyField(serializedObject.FindProperty("traits.Re"), new GUIContent("古 Re"));
    //    EditorGUILayout.PropertyField(serializedObject.FindProperty("traits.Aku"), new GUIContent("恶 Aku"));
    //    EditorGUILayout.PropertyField(serializedObject.FindProperty("traits.None"), new GUIContent("无 None"));
    //    GUILayout.EndVertical();

    //    // 副属性 Character SubTraits
    //    GUILayout.BeginVertical("box");
    //    GUILayout.Label("副属性 Character SubTraits", customStyle);
    //    EditorGUILayout.PropertyField(serializedObject.FindProperty("subtraits.Starred"), new GUIContent("异星星标 Starred"));
    //    EditorGUILayout.PropertyField(serializedObject.FindProperty("subtraits.Colossus"), new GUIContent("超生命体 Colossus"));
    //    EditorGUILayout.PropertyField(serializedObject.FindProperty("subtraits.Behemoth"), new GUIContent("超兽 Behemoth"));
    //    EditorGUILayout.PropertyField(serializedObject.FindProperty("subtraits.Sage"), new GUIContent("贤者 Sage"));
    //    GUILayout.EndVertical();

    //    // 职业 Career
    //    GUILayout.BeginVertical("box");
    //    GUILayout.Label("职业 Career", customStyle);
    //    EditorGUILayout.PropertyField(serializedObject.FindProperty("career.Warrior"), new GUIContent("战士 Warrior"));
    //    EditorGUILayout.PropertyField(serializedObject.FindProperty("career.Deffender"), new GUIContent("防御 Defender"));
    //    EditorGUILayout.PropertyField(serializedObject.FindProperty("career.Magician"), new GUIContent("法师 Magician"));
    //    EditorGUILayout.PropertyField(serializedObject.FindProperty("career.Supporter"), new GUIContent("辅助 Supporter"));
    //    EditorGUILayout.PropertyField(serializedObject.FindProperty("career.Practician"), new GUIContent("技巧 Practician"));
    //    GUILayout.EndVertical();

    //    // 职业针对 Career Effects
    //    GUILayout.BeginVertical("box");
    //    GUILayout.Label("职业针对 Career Effects", customStyle);
    //    EditorGUILayout.PropertyField(serializedObject.FindProperty("careerEffects.AggainstWarrior"), new GUIContent("Against Warrior"));
    //    EditorGUILayout.PropertyField(serializedObject.FindProperty("careerEffects.AggainstMagician"), new GUIContent("Against Magician"));
    //    EditorGUILayout.PropertyField(serializedObject.FindProperty("careerEffects.AggainstDeffender"), new GUIContent("Against Defender"));
    //    EditorGUILayout.PropertyField(serializedObject.FindProperty("careerEffects.AggainstSuppoter"), new GUIContent("Against Supporter"));
    //    EditorGUILayout.PropertyField(serializedObject.FindProperty("careerEffects.AggainstPractician"), new GUIContent("Against Practician"));
    //    GUILayout.EndVertical();

    //    // 能力 Effects
    //    GUILayout.BeginVertical("box");
    //    GUILayout.Label("能力 Effects", customStyle);

    //    // Weaken Effect
    //    DrawEffect(weakenProperty);

    //    // Stop Effect
    //    DrawEffect(stopProperty);

    //    // Slow Effect
    //    DrawEffect(slowProperty);

    //    // Knockback Effect
    //    DrawEffect(knockbackProperty);

    //    // Wrap Effect
    //    DrawEffect(wrapProperty);

    //    // Curse Effect
    //    DrawEffect(curseProperty);

    //    // Dodge Effect
    //    DrawEffect(dodgeProperty);

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

    //private void AddElement(ref SerializedProperty arrayProperty)
    //{
    //    arrayProperty.InsertArrayElementAtIndex(arrayProperty.arraySize);
    //    arrayProperty.GetArrayElementAtIndex(arrayProperty.arraySize - 1).intValue = 0; // 默认值
    //}

    //private void RemoveElement(ref SerializedProperty arrayProperty, int index)
    //{
    //    if (arrayProperty.arraySize <= 1) return; // 至少保留一个元素
    //    arrayProperty.DeleteArrayElementAtIndex(index);
    //}
}

