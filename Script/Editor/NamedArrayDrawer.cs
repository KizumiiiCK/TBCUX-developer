using UnityEditor;
using UnityEngine;

[CustomPropertyDrawer(typeof(NamedArrayAttribute))]
public class NamedArrayDrawer : PropertyDrawer
{
    public override void OnGUI(Rect position, SerializedProperty property, GUIContent label)
    {
        // 确保property指向一个数组
        if (!property.isArray)
        {
            EditorGUI.LabelField(position, "NamedArrayAttribute只能用于数组字段。");
            return;
        }

        // 获取NamedArrayAttribute中的名字数组
        NamedArrayAttribute namedArrayAttribute = (NamedArrayAttribute)attribute;
        string[] names = namedArrayAttribute.names;

        // 绘制每个元素的名字
        for (int i = 0; i < property.arraySize; i++)
        {
            SerializedProperty element = property.GetArrayElementAtIndex(i);
            Rect elementRect = new Rect(position.x, position.y + i * EditorGUIUtility.singleLineHeight, position.width, EditorGUIUtility.singleLineHeight);

            // 使用名字作为标签
            string name = i < names.Length ? names[i] : $"Element {i}";
            EditorGUI.PropertyField(elementRect, element, new GUIContent(name));
        }
    }

    public override float GetPropertyHeight(SerializedProperty property, GUIContent label)
    {
        // 确保property指向一个数组
        if (!property.isArray)
        {
            return EditorGUIUtility.singleLineHeight;
        }

        // 计算总高度
        return property.arraySize * EditorGUIUtility.singleLineHeight;
    }
}
