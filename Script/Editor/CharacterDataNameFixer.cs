#if UNITY_EDITOR
using System.Collections.Generic;
using System.IO;
using System.Text;
using System.Text.RegularExpressions;
using UnityEditor;
using UnityEngine;

/// <summary>
/// Temporary utility: fix CharacterData.Name to match folder path codes.
/// Cat Units: {rarity}/{unitId}/{form}/data.asset -> Name = rarity + unitId(3 digits) + form[0]
/// Enemy Units: {folder}/data.asset -> Name = folder name
/// </summary>
public static class CharacterDataNameFixer
{
    private const string CatUnitsRoot = "Assets/Bundled/Units/Cat Units";
    private const string EnemyUnitsRoot = "Assets/Bundled/Units/Enemy Units";

    [MenuItem("TBCX/Temporary/Fix CharacterData Names From Folder Paths")]
    public static void FixAllFromMenu()
    {
        List<string> report = FixAll();
        if (report.Count == 0)
        {
            Debug.Log("[CharacterDataNameFixer] No mismatches found.");
            return;
        }

        var sb = new StringBuilder();
        sb.AppendLine($"[CharacterDataNameFixer] Fixed {report.Count} entries:");
        for (int i = 0; i < report.Count; i++)
            sb.AppendLine(report[i]);

        Debug.Log(sb.ToString());
        EditorUtility.DisplayDialog(
            "CharacterData Name Fixer",
            $"Fixed {report.Count} entries.\nSee Console for details.",
            "OK");
    }

    public static List<string> FixAll()
    {
        var report = new List<string>();
        report.AddRange(FixCatUnits());
        report.AddRange(FixEnemyUnits());

        if (report.Count > 0)
        {
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
        }

        return report;
    }

    private static List<string> FixCatUnits()
    {
        var report = new List<string>();
        string[] guids = AssetDatabase.FindAssets("data t:CharacterData", new[] { CatUnitsRoot });
        for (int i = 0; i < guids.Length; i++)
        {
            string assetPath = AssetDatabase.GUIDToAssetPath(guids[i]);
            if (!assetPath.EndsWith("/data.asset", System.StringComparison.OrdinalIgnoreCase))
                continue;

            string relative = assetPath.Substring(CatUnitsRoot.Length + 1).Replace("\\", "/");
            string[] parts = relative.Split('/');
            if (parts.Length != 4)
                continue;

            string rarity = parts[0];
            string unitFolder = parts[1];
            string formFolder = parts[2];
            if (!TryBuildCatName(rarity, unitFolder, formFolder, out string expectedName))
                continue;

            var data = AssetDatabase.LoadAssetAtPath<CharacterData>(assetPath);
            if (data == null)
                continue;

            if (data.Name == expectedName)
                continue;

            string oldName = data.Name;
            data.Name = expectedName;
            EditorUtility.SetDirty(data);
            report.Add($"[Cat] {rarity}/{unitFolder}/{formFolder}: {oldName} -> {expectedName}");
        }

        return report;
    }

    private static List<string> FixEnemyUnits()
    {
        var report = new List<string>();
        string[] guids = AssetDatabase.FindAssets("data t:CharacterData", new[] { EnemyUnitsRoot });
        for (int i = 0; i < guids.Length; i++)
        {
            string assetPath = AssetDatabase.GUIDToAssetPath(guids[i]);
            if (!assetPath.EndsWith("/data.asset", System.StringComparison.OrdinalIgnoreCase))
                continue;

            string folderName = Path.GetFileName(Path.GetDirectoryName(assetPath));
            if (string.IsNullOrEmpty(folderName))
                continue;

            var data = AssetDatabase.LoadAssetAtPath<CharacterData>(assetPath);
            if (data == null)
                continue;

            if (data.Name == folderName)
                continue;

            string oldName = data.Name;
            data.Name = folderName;
            EditorUtility.SetDirty(data);
            report.Add($"[Enemy] {folderName}: {oldName} -> {folderName}");
        }

        return report;
    }

    private static bool TryBuildCatName(string rarity, string unitFolder, string formFolder, out string name)
    {
        name = null;
        if (string.IsNullOrEmpty(rarity) || string.IsNullOrEmpty(unitFolder) || string.IsNullOrEmpty(formFolder))
            return false;

        string unitDigits = Regex.Replace(unitFolder, @"\D", string.Empty);
        if (unitDigits.Length == 0 || !int.TryParse(unitDigits, out int unitNumber))
            return false;

        name = $"{rarity}{unitNumber:D3}{formFolder[0]}";
        return true;
    }
}
#endif
