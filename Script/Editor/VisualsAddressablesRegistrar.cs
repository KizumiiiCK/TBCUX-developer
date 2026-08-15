using System;
using System.Collections.Generic;
using UnityEditor;
using UnityEditor.AddressableAssets;
using UnityEditor.AddressableAssets.Settings;
using UnityEngine;

/// <summary>
/// Rebuilds Background / DialogueImage / Fonts Addressable entries with Resources-style
/// addresses (no Assets/Bundled prefix, no file extension), while keeping CG/video entries.
/// </summary>
public static class VisualsAddressablesRegistrar
{
    private const string GroupName = "Visuals";

    private static readonly FolderMapping[] Folders =
    {
        new FolderMapping("Assets/Bundled/Background/Maps", "Background/Maps"),
        new FolderMapping("Assets/Bundled/Background/Doors", "Background/Doors"),
        new FolderMapping("Assets/Bundled/Background/CombatEffects", "Background/CombatEffects"),
        new FolderMapping("Assets/Bundled/DialogueImage", "DialogueImage"),
        new FolderMapping("Assets/Bundled/System/fonts", "System/fonts"),
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

    [MenuItem("TBCX/Addressables/Rebuild Visuals Bundled Entries (Background+Dialogue+Fonts)")]
    public static void RebuildVisualsBundledEntries()
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

        // Remove previous folder / rebuilt entries for these roots only (keep CG + video).
        List<AddressableAssetEntry> existing = new List<AddressableAssetEntry>(group.entries);
        for (int i = 0; i < existing.Count; i++)
        {
            AddressableAssetEntry entry = existing[i];
            if (entry == null) continue;
            if (ShouldReplaceEntry(entry))
                settings.RemoveAssetEntry(entry.guid, false);
        }

        int registered = 0;
        for (int f = 0; f < Folders.Length; f++)
            registered += RegisterFolder(settings, group, Folders[f]);

        settings.SetDirty(AddressableAssetSettings.ModificationEvent.EntryMoved, null, true, true);
        AssetDatabase.SaveAssets();
        Debug.Log(
            $"Rebuilt Visuals bundled entries: {registered} " +
            "(Background Maps/Doors/CombatEffects + DialogueImage + System/fonts). " +
            "CG/video entries preserved. Addresses match old Resources paths.");
    }

    private static bool ShouldReplaceEntry(AddressableAssetEntry entry)
    {
        string address = entry.address ?? string.Empty;
        string path = AssetDatabase.GUIDToAssetPath(entry.guid) ?? string.Empty;

        for (int i = 0; i < Folders.Length; i++)
        {
            FolderMapping folder = Folders[i];
            string folderGuid = AssetDatabase.AssetPathToGUID(folder.BundledRoot);
            if (!string.IsNullOrEmpty(folderGuid) && entry.guid == folderGuid)
                return true;

            if (address == folder.AddressRoot
                || address.StartsWith(folder.AddressRoot + "/")
                || address == folder.BundledRoot
                || address.StartsWith(folder.BundledRoot + "/")
                || address == "Fonts"
                || address.StartsWith("Assets/Bundled/Background/")
                || address.StartsWith("Assets/Bundled/DialogueImage")
                || address.StartsWith("Assets/Bundled/System/fonts"))
                return true;

            if (path == folder.BundledRoot || path.StartsWith(folder.BundledRoot + "/"))
                return true;
        }

        return false;
    }

    private static int RegisterFolder(
        AddressableAssetSettings settings,
        AddressableAssetGroup group,
        FolderMapping folder)
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
                continue;

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
        Type mainType = AssetDatabase.GetMainAssetTypeAtPath(assetPath);
        return mainType == null || mainType == typeof(DefaultAsset);
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
