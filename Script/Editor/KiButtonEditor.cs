using UnityEditor;
using UnityEditor.UI;

[CustomEditor(typeof(KiButton), true)]
[CanEditMultipleObjects]
public class KiButtonEditor : ButtonEditor
{
    private SerializedProperty frameRoot;
    private SerializedProperty label;
    private SerializedProperty cover;
    private SerializedProperty initialOutfit;
    private SerializedProperty initialType;
    private SerializedProperty initialColor;
    private SerializedProperty initialSize;
    private SerializedProperty rotateToRhombus;

    protected override void OnEnable()
    {
        base.OnEnable();
        frameRoot = serializedObject.FindProperty("frameRoot");
        label = serializedObject.FindProperty("label");
        cover = serializedObject.FindProperty("cover");
        initialOutfit = serializedObject.FindProperty("initialOutfit");
        initialType = serializedObject.FindProperty("initialType");
        initialColor = serializedObject.FindProperty("initialColor");
        initialSize = serializedObject.FindProperty("initialSize");
        rotateToRhombus = serializedObject.FindProperty("rotateToRhombus");
    }

    public override void OnInspectorGUI()
    {
        serializedObject.Update();

        base.OnInspectorGUI();

        EditorGUILayout.Space();
        EditorGUILayout.LabelField("KiButton", EditorStyles.boldLabel);
        EditorGUILayout.PropertyField(frameRoot);
        EditorGUILayout.PropertyField(label);
        EditorGUILayout.PropertyField(cover);
        EditorGUILayout.PropertyField(initialOutfit);
        EditorGUILayout.PropertyField(initialType);
        EditorGUILayout.PropertyField(initialColor);
        EditorGUILayout.PropertyField(initialSize);
        EditorGUILayout.PropertyField(rotateToRhombus);

        serializedObject.ApplyModifiedProperties();
    }
}