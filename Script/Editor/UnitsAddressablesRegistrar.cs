using System;
using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEditor.AddressableAssets;
using UnityEditor.AddressableAssets.Settings;
using UnityEngine;

/// <summary>
/// Folder Addressable entries keep file extensions in child keys.
/// Gameplay still uses Resources-style keys without extensions.
/// This menu rebuilds explicit entries without extensions.
/// </summary>
public static class UnitsAddressablesRegistrar
{
    private const string GroupName = "Units";

    /// <summary>
    /// Raw/dev extensions Unity cannot import as real assets (break Addressables content build).
    /// </summary>
    private static readonly HashSet<string> ExcludedExtensions = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
    {
        ".maanim",
    };

    private static readonly FolderMapping[] Folders =
    {
        new FolderMapping("Assets/Bundled/Units/Enemy Units", "Units/Enemy Units"),
        new FolderMapping("Assets/Bundled/Units/Cat Units", "Units/Cat Units"),
        new FolderMapping("Assets/Bundled/Units/CatBases", "Units/CatBases"),
        new FolderMapping("Assets/Bundled/Units/DogeBases", "Units/DogeBases"),
        new FolderMapping("Assets/Bundled/Units/Projectiles", "Units/Projectiles"),
    };

    private struct FolderMapping
    {
        public readonly string BundledRoot;
        public readonly string AddressRoot;

        public FolderMapping(string bundledRoot, string addressRoot)
        {
            BundledRoot = bundledRoot;
            AddressRoot = addressRoot;
        }
    }

    [MenuItem("TBCX/Addressables/Rebuild Units Entries (all Bundled Units, no extension)")]
    public static void RebuildAllUnitEntries()
    {
        AddressableAssetSettings settings = AddressableAssetSettingsDefaultObject.Settings;
        if (settings == null)
        {
            Debug.LogError("AddressableAssetSettings not found.");
            return;
        }

        AddressableAssetGroup group = settings.FindGroup(GroupName);
        if (group == null)
        {
            Debug.LogError($"Addressables group '{GroupName}' not found.");
            return;
        }

        List<AddressableAssetEntry> existing = new List<AddressableAssetEntry>(group.entries);
        for (int i = 0; i < existing.Count; i++)
            settings.RemoveAssetEntry(existing[i].guid, false);

        int registered = 0;
        int skipped = 0;
        for (int f = 0; f < Folders.Length; f++)
            registered += RegisterFolder(settings, group, Folders[f], ref skipped);

        settings.SetDirty(AddressableAssetSettings.ModificationEvent.EntryMoved, null, true, true);
        AssetDatabase.SaveAssets();
        Debug.Log(
            $"Rebuilt Units Addressables: {registered} entries (skipped {skipped} unsupported). " +
            "(Enemy/Cat Units + CatBases/DogeBases/Projectiles). " +
            "Addresses match Resources paths without extensions.");
    }

    [MenuItem("TBCX/Addressables/Remove Unsupported Unit Entries (.maanim etc.)")]
    public static void RemoveUnsupportedUnitEntries()
    {
        AddressableAssetSettings settings = AddressableAssetSettingsDefaultObject.Settings;
        if (settings == null)
        {
            Debug.LogError("AddressableAssetSettings not found.");
            return;
        }

        AddressableAssetGroup group = settings.FindGroup(GroupName);
        if (group == null)
        {
            Debug.LogError($"Addressables group '{GroupName}' not found.");
            return;
        }

        List<AddressableAssetEntry> existing = new List<AddressableAssetEntry>(group.entries);
        int removed = 0;
        for (int i = 0; i < existing.Count; i++)
        {
            AddressableAssetEntry entry = existing[i];
            if (entry == null) continue;
            string path = AssetDatabase.GUIDToAssetPath(entry.guid);
            if (string.IsNullOrEmpty(path) || ShouldSkipAsset(path))
            {
                settings.RemoveAssetEntry(entry.guid, false);
                removed++;
            }
        }

        settings.SetDirty(AddressableAssetSettings.ModificationEvent.EntryRemoved, null, true, true);
        AssetDatabase.SaveAssets();
        Debug.Log($"Removed {removed} unsupported Addressable entries from '{GroupName}' (.maanim / DefaultAsset).");
    }

    [MenuItem("TBCX/Addressables/Rebuild Enemy Units Entries (no extension)")]
    public static void RebuildEnemyUnitsEntries() => RebuildOne(Folders[0]);

    [MenuItem("TBCX/Addressables/Rebuild Cat Units Entries (no extension)")]
    public static void RebuildCatUnitsEntries() => RebuildOne(Folders[1]);

    [MenuItem("TBCX/Addressables/Rebuild CatBases Entries (no extension)")]
    public static void RebuildCatBasesEntries() => RebuildOne(Folders[2]);

    [MenuItem("TBCX/Addressables/Rebuild DogeBases Entries (no extension)")]
    public static void RebuildDogeBasesEntries() => RebuildOne(Folders[3]);

    [MenuItem("TBCX/Addressables/Rebuild Projectiles Entries (no extension)")]
    public static void RebuildProjectilesEntries() => RebuildOne(Folders[4]);

    private static void RebuildOne(FolderMapping folder)
    {
        AddressableAssetSettings settings = AddressableAssetSettingsDefaultObject.Settings;
        if (settings == null)
        {
            Debug.LogError("AddressableAssetSettings not found.");
            return;
        }

        AddressableAssetGroup group = settings.FindGroup(GroupName);
        if (group == null)
        {
            Debug.LogError($"Addressables group '{GroupName}' not found.");
            return;
        }

        List<AddressableAssetEntry> existing = new List<AddressableAssetEntry>(group.entries);
        string folderGuid = AssetDatabase.AssetPathToGUID(folder.BundledRoot);
        for (int i = 0; i < existing.Count; i++)
        {
            AddressableAssetEntry entry = existing[i];
            if (entry == null) continue;
            bool isFolderRoot = entry.guid == folderGuid;
            bool underAddress = !string.IsNullOrEmpty(entry.address)
                && (entry.address == folder.AddressRoot
                    || entry.address.StartsWith(folder.AddressRoot + "/"));
            string path = AssetDatabase.GUIDToAssetPath(entry.guid);
            bool underDisk = !string.IsNullOrEmpty(path)
                && (path == folder.BundledRoot || path.StartsWith(folder.BundledRoot + "/"));
            if (isFolderRoot || underAddress || underDisk)
                settings.RemoveAssetEntry(entry.guid, false);
        }

        int skipped = 0;
        int registered = RegisterFolder(settings, group, folder, ref skipped);
        settings.SetDirty(AddressableAssetSettings.ModificationEvent.EntryMoved, null, true, true);
        AssetDatabase.SaveAssets();
        Debug.Log($"Rebuilt {folder.AddressRoot}: {registered} entries (skipped {skipped} unsupported).");
    }

    private static int RegisterFolder(
        AddressableAssetSettings settings,
        AddressableAssetGroup group,
        FolderMapping folder,
        ref int skipped)
    {
        if (!AssetDatabase.IsValidFolder(folder.BundledRoot))
        {
            Debug.LogError($"Folder not found: {folder.BundledRoot}");
            return 0;
        }

        string[] guids = AssetDatabase.FindAssets(string.Empty, new[] { folder.BundledRoot });
        int registered = 0;
        for (int i = 0; i < guids.Length; i++)
        {
            string guid = guids[i];
            string assetPath = AssetDatabase.GUIDToAssetPath(guid);
            if (string.IsNullOrEmpty(assetPath) || AssetDatabase.IsValidFolder(assetPath))
                continue;
            if (!assetPath.StartsWith(folder.BundledRoot + "/"))
                continue;
            if (ShouldSkipAsset(assetPath))
            {
                skipped++;
                continue;
            }

            string relative = assetPath.Substring(folder.BundledRoot.Length + 1).Replace('\\', '/');
            string address = folder.AddressRoot + "/" + StripExtension(relative);

            AddressableAssetEntry entry = settings.CreateOrMoveEntry(guid, group, false, false);
            if (entry == null) continue;
            entry.SetAddress(address, false);
            registered++;
        }

        return registered;
    }

    private static bool ShouldSkipAsset(string assetPath)
    {
        string ext = Path.GetExtension(assetPath);
        if (!string.IsNullOrEmpty(ext) && ExcludedExtensions.Contains(ext))
            return true;

        // Unimported / unsupported files show up as DefaultAsset and break content build.
        Type mainType = AssetDatabase.GetMainAssetTypeAtPath(assetPath);
        if (mainType == null || mainType == typeof(DefaultAsset))
            return true;

        return false;
    }

    private static string StripExtension(string relativePath)
    {
        int slash = relativePath.LastIndexOf('/');
        int dot = relativePath.LastIndexOf('.');
        if (dot > slash)
            return relativePath.Substring(0, dot);
        return relativePath;
    }
}
