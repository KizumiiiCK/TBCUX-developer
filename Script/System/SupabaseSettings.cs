using UnityEngine;

#if UNITY_EDITOR
using UnityEditor;
#endif

/// <summary>
/// Local-only cloud credentials. The filled asset is gitignored;
/// collaborators copy the example or use the TBCX menu to create a blank one.
/// </summary>
[CreateAssetMenu(fileName = "SupabaseSettings", menuName = "TBCX/Supabase Settings")]
public class SupabaseSettings : ScriptableObject
{
    public const string ResourcePath = "Private/SupabaseSettings";
    public const string AssetPath = "Assets/Resources/Private/SupabaseSettings.asset";
    public const string MissingConfigHint =
        "Cloud save is not configured on this machine. Copy Resources/Private/SupabaseSettings.example.asset to SupabaseSettings.asset and fill url / anonKey.";

    [SerializeField] private string url = "";
    [SerializeField] private string anonKey = "";

    private static SupabaseSettings cached;
    private static bool loggedMissing;

    public string Url => string.IsNullOrWhiteSpace(url) ? "" : url.Trim();
    public string AnonKey => string.IsNullOrWhiteSpace(anonKey) ? "" : anonKey.Trim();
    public bool HasCredentials => Url.Length > 0 && AnonKey.Length > 0;

    public static bool IsConfigured => Current.HasCredentials;
    public static string UrlValue => Current.Url;
    public static string KeyValue => Current.AnonKey;

    public static SupabaseSettings Current
    {
        get
        {
            if (cached == null)
            {
                cached = Resources.Load<SupabaseSettings>(ResourcePath);
                if (cached == null)
                {
                    cached = CreateInstance<SupabaseSettings>();
                    if (!loggedMissing)
                    {
                        loggedMissing = true;
                        Debug.LogWarning("[SupabaseSettings] " + MissingConfigHint);
                    }
                }
            }
            return cached;
        }
    }

#if UNITY_EDITOR
    [MenuItem("TBCX/Create Local Supabase Settings")]
    private static void CreateLocalSettingsAsset()
    {
        const string resourcesDir = "Assets/Resources";
        const string privateDir = "Assets/Resources/Private";

        if (!AssetDatabase.IsValidFolder(resourcesDir))
            AssetDatabase.CreateFolder("Assets", "Resources");
        if (!AssetDatabase.IsValidFolder(privateDir))
            AssetDatabase.CreateFolder(resourcesDir, "Private");

        var existing = AssetDatabase.LoadAssetAtPath<SupabaseSettings>(AssetPath);
        if (existing != null)
        {
            Selection.activeObject = existing;
            EditorGUIUtility.PingObject(existing);
            return;
        }

        var asset = CreateInstance<SupabaseSettings>();
        AssetDatabase.CreateAsset(asset, AssetPath);
        AssetDatabase.SaveAssets();
        Selection.activeObject = asset;
        EditorGUIUtility.PingObject(asset);
        Debug.Log("[SupabaseSettings] Created local asset at " + AssetPath + ". Fill url and anonKey; do not commit this file.");
    }
#endif
}
