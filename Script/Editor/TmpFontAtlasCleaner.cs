using System.Collections.Generic;
using System.IO;
using System.Text;
using TMPro;
using UnityEditor;
using UnityEngine;
using UnityEngine.Localization.Tables;
using UnityEngine.TextCore.LowLevel;

/// <summary>
/// W03 used to be a 100MB+ static 8192 CJK atlas. Native Win/Android should
/// ship Dynamic TMP + the 3.2MB TTF, and grow extra atlases at runtime.
/// </summary>
public static class TmpFontAtlasCleaner
{
    private const string W03SdfPath = "Assets/Bundled/System/fonts/W03_mianfeiziti.com SDF.asset";
    private const string W03TtfPath = "Assets/Bundled/System/fonts/W03_mianfeiziti.com.ttf";
    private const string WashingtonSdfPath = "Assets/Bundled/System/fonts/WASHINGTONBOLDDYNAMIC SDF.asset";
    private const string WashingtonOtfPath = "Assets/Bundled/System/fonts/WASHINGTONBOLDDYNAMIC.OTF";
    private const string LocalizationRoot = "Assets/Resources/Localization";

    [MenuItem("TBCX/Fonts/Shrink W03 (Dynamic 1024, runtime CJK)")]
    public static void ShrinkW03()
    {
        RecreateDynamicFont(
            W03SdfPath,
            W03TtfPath,
            atlasSize: 1024,
            pointSize: 72,
            padding: 6,
            prewarmAsciiOnly: true);
    }

    [MenuItem("TBCX/Fonts/Shrink WASHINGTON (Dynamic 512)")]
    public static void ShrinkWashington()
    {
        RecreateDynamicFont(
            WashingtonSdfPath,
            WashingtonOtfPath,
            atlasSize: 512,
            pointSize: 72,
            padding: 6,
            prewarmAsciiOnly: true);
    }

    [MenuItem("TBCX/Fonts/Restore W03 Dynamic CJK (prewarm localization)")]
    public static void RestoreW03()
    {
        RestoreFontAtPath(W03SdfPath, prewarmFromLocalization: true);
    }

    [MenuItem("TBCX/Fonts/Restore Selected TMP Font Dynamic CJK")]
    public static void RestoreSelected()
    {
        Object[] selected = Selection.objects;
        if (selected == null || selected.Length == 0)
        {
            Debug.LogError("Select one or more TMP_FontAsset assets first.");
            return;
        }

        int restored = 0;
        for (int i = 0; i < selected.Length; i++)
        {
            string path = AssetDatabase.GetAssetPath(selected[i]);
            if (string.IsNullOrEmpty(path)) continue;
            if (RestoreFontAtPath(path, prewarmFromLocalization: true)) restored++;
        }

        Debug.Log($"Restored Dynamic CJK on {restored} font asset(s).");
    }

    private static void RecreateDynamicFont(
        string sdfPath,
        string sourceFontPath,
        int atlasSize,
        int pointSize,
        int padding,
        bool prewarmAsciiOnly)
    {
        var sourceFont = AssetDatabase.LoadAssetAtPath<Font>(sourceFontPath);
        var existing = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(sdfPath);
        if (sourceFont == null)
        {
            Debug.LogError($"Missing source font: {sourceFontPath}");
            return;
        }
        if (existing == null)
        {
            Debug.LogError($"Missing TMP font: {sdfPath}");
            return;
        }

        long before = new FileInfo(Path.GetFullPath(sdfPath)).Length;
        string displayName = existing.name;
        Material oldMat = existing.material;
        var matFloats = CaptureMaterialFloats(oldMat);
        var matColors = CaptureMaterialColors(oldMat);
        string metaPath = sdfPath + ".meta";
        string metaText = File.Exists(metaPath) ? File.ReadAllText(metaPath) : null;

        TMP_FontAsset created = TMP_FontAsset.CreateFontAsset(
            sourceFont,
            pointSize,
            padding,
            GlyphRenderMode.SDFAA,
            atlasSize,
            atlasSize,
            AtlasPopulationMode.Dynamic,
            enableMultiAtlasSupport: true);
        if (created == null)
        {
            Debug.LogError($"TMP_FontAsset.CreateFontAsset failed for {sourceFontPath}");
            return;
        }

        created.name = displayName;
        created.isMultiAtlasTexturesEnabled = true;

        if (prewarmAsciiOnly)
        {
            created.TryAddCharacters(AsciiAndPunctuationCharset(), out _);
        }

        AssetDatabase.DeleteAsset(sdfPath);
        AssetDatabase.CreateAsset(created, sdfPath);
        if (created.material != null)
            AssetDatabase.AddObjectToAsset(created.material, created);
        if (created.atlasTextures != null)
        {
            for (int i = 0; i < created.atlasTextures.Length; i++)
            {
                if (created.atlasTextures[i] != null)
                    AssetDatabase.AddObjectToAsset(created.atlasTextures[i], created);
            }
        }

        RestoreMaterialLook(created.material, matFloats, matColors);
        if (!string.IsNullOrEmpty(metaText))
            File.WriteAllText(metaPath, metaText);

        EditorUtility.SetDirty(created);
        AssetDatabase.SaveAssets();
        AssetDatabase.ImportAsset(sdfPath, ImportAssetOptions.ForceUpdate);

        long after = new FileInfo(Path.GetFullPath(sdfPath)).Length;
        Debug.Log(
            $"Recreated Dynamic TMP: {sdfPath}\n" +
            $"  {before / (1024f * 1024f):F1}MB -> {after / (1024f * 1024f):F1}MB\n" +
            $"  atlas={atlasSize}  point={pointSize}  padding={padding}  multiAtlas=true\n" +
            "CJK is generated at runtime from the source TTF. Rebuild Addressables Visuals.");
    }

    private static bool RestoreFontAtPath(string assetPath, bool prewarmFromLocalization)
    {
        var font = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(assetPath);
        if (font == null)
        {
            Debug.LogError($"Not a TMP_FontAsset: {assetPath}");
            return false;
        }

        long before = new FileInfo(Path.GetFullPath(assetPath)).Length;

        font.atlasPopulationMode = AtlasPopulationMode.Dynamic;
        font.isMultiAtlasTexturesEnabled = true;
        font.ClearFontAssetData(setAtlasSizeToZero: false);

        string missing = string.Empty;
        int requested = 0;
        if (prewarmFromLocalization)
        {
            string charset = CollectGameCharset();
            requested = charset.Length;
            bool added = font.TryAddCharacters(charset, out missing);
            if (!added)
            {
                Debug.LogWarning(
                    $"{assetPath}: TryAddCharacters did not finish in one pass. " +
                    $"requested={requested} missing={missing?.Length ?? 0}. Multi-atlas should cover the rest at runtime.");
            }
        }

        EditorUtility.SetDirty(font);
        AssetDatabase.SaveAssets();
        AssetDatabase.ImportAsset(assetPath, ImportAssetOptions.ForceUpdate);

        long after = new FileInfo(Path.GetFullPath(assetPath)).Length;
        Debug.Log(
            $"Restored Dynamic CJK: {assetPath}\n" +
            $"  before={before / (1024f * 1024f):F1}MB  after={after / (1024f * 1024f):F1}MB\n" +
            $"  mode={font.atlasPopulationMode}  multiAtlas={font.isMultiAtlasTexturesEnabled}\n" +
            $"  atlas={font.atlasWidth}x{font.atlasHeight}  prewarmRequested={requested}  stillMissing={missing?.Length ?? 0}\n" +
            "Next: rebuild Addressables (Visuals group).");
        return true;
    }

    private static string AsciiAndPunctuationCharset()
    {
        var sb = new StringBuilder(160);
        for (int c = 32; c <= 126; c++) sb.Append((char)c);
        sb.Append("…—·“”‘’《》【】（）！？、。：；，￥★☆→←↑↓●○■□");
        return sb.ToString();
    }

    private static string CollectGameCharset()
    {
        var set = new HashSet<char>();
        string ascii = AsciiAndPunctuationCharset();
        for (int i = 0; i < ascii.Length; i++) set.Add(ascii[i]);

        if (Directory.Exists(LocalizationRoot))
        {
            string[] tablePaths = Directory.GetFiles(LocalizationRoot, "*.asset", SearchOption.AllDirectories);
            for (int i = 0; i < tablePaths.Length; i++)
            {
                string unityPath = tablePaths[i].Replace('\\', '/');
                int assets = unityPath.IndexOf("Assets/", System.StringComparison.Ordinal);
                if (assets >= 0) unityPath = unityPath.Substring(assets);
                var table = AssetDatabase.LoadAssetAtPath<StringTable>(unityPath);
                if (table == null) continue;
                foreach (var entry in table.Values)
                {
                    if (entry == null) continue;
                    string value = entry.LocalizedValue;
                    if (string.IsNullOrEmpty(value)) continue;
                    for (int c = 0; c < value.Length; c++)
                    {
                        char ch = value[c];
                        if (!char.IsControl(ch)) set.Add(ch);
                    }
                }
            }
        }

        var sb = new StringBuilder(set.Count);
        foreach (char ch in set) sb.Append(ch);
        return sb.ToString();
    }

    private static Dictionary<string, float> CaptureMaterialFloats(Material mat)
    {
        var map = new Dictionary<string, float>();
        if (mat == null) return map;
        string[] names =
        {
            "_FaceDilate", "_OutlineWidth", "_OutlineSoftness", "_GradientScale",
            "_ScaleRatioA", "_ScaleRatioB", "_ScaleRatioC", "_WeightNormal", "_WeightBold"
        };
        for (int i = 0; i < names.Length; i++)
        {
            if (mat.HasProperty(names[i])) map[names[i]] = mat.GetFloat(names[i]);
        }
        return map;
    }

    private static Dictionary<string, Color> CaptureMaterialColors(Material mat)
    {
        var map = new Dictionary<string, Color>();
        if (mat == null) return map;
        string[] names = { "_FaceColor", "_OutlineColor" };
        for (int i = 0; i < names.Length; i++)
        {
            if (mat.HasProperty(names[i])) map[names[i]] = mat.GetColor(names[i]);
        }
        return map;
    }

    private static void RestoreMaterialLook(
        Material mat,
        Dictionary<string, float> floats,
        Dictionary<string, Color> colors)
    {
        if (mat == null) return;
        foreach (var kv in floats)
        {
            if (mat.HasProperty(kv.Key)) mat.SetFloat(kv.Key, kv.Value);
        }
        foreach (var kv in colors)
        {
            if (mat.HasProperty(kv.Key)) mat.SetColor(kv.Key, kv.Value);
        }
    }
}
