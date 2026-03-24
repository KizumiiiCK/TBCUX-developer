using UnityEditor;

[CustomEditor(typeof(KiPanel), true)]
[CanEditMultipleObjects]
public class KiPanelEditor : Editor
{
    private SerializedProperty frameRoot;
    private SerializedProperty label;
    private SerializedProperty cover;
    private SerializedProperty initialOutfit;
    private SerializedProperty initialType;
    private SerializedProperty initialColor;
    private SerializedProperty initialSize;
    private SerializedProperty screenSaveScaler;
    private SerializedProperty rotateToRhombus;

    private void OnEnable()
    {
        frameRoot = serializedObject.FindProperty("frameRoot");
        label = serializedObject.FindProperty("label");
        cover = serializedObject.FindProperty("cover");
        initialOutfit = serializedObject.FindProperty("initialOutfit");
        initialType = serializedObject.FindProperty("initialType");
        initialColor = serializedObject.FindProperty("initialColor");
        initialSize = serializedObject.FindProperty("initialSize");
        screenSaveScaler = serializedObject.FindProperty("screenSaveScaler");
        rotateToRhombus = serializedObject.FindProperty("rotateToRhombus");
    }

    public override void OnInspectorGUI()
    {
        serializedObject.Update();

        EditorGUILayout.LabelField("KiPanel", EditorStyles.boldLabel);
        EditorGUILayout.PropertyField(frameRoot);
        EditorGUILayout.PropertyField(label);
        EditorGUILayout.PropertyField(cover);
        EditorGUILayout.PropertyField(initialOutfit);
        EditorGUILayout.PropertyField(initialType);
        EditorGUILayout.PropertyField(initialColor);
        EditorGUILayout.PropertyField(initialSize);
        EditorGUILayout.PropertyField(screenSaveScaler);
        EditorGUILayout.PropertyField(rotateToRhombus);

        serializedObject.ApplyModifiedProperties();
    }
}