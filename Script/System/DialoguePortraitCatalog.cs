using System.Collections;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// The set of dialogue portraits the player can pick as their base character.
///
/// The folder is small and shared across many screens, so it is prewarmed once
/// (<see cref="EnsureLoadedRoutine"/>) rather than fetched per use. <see cref="GetVisiblePortraits"/>
/// stays synchronous for existing callers and simply reads whatever has been loaded.
/// </summary>
public static class DialoguePortraitCatalog
{
    private const string PortraitFolder = "DialogueImage";
    private const string SettingsPath = "DialogueImage/DialoguePortraitSettings";

    private static readonly List<Sprite> VisiblePortraits = new List<Sprite>(64);
    private static bool loaded;

    /// <summary>True once the portrait folder has been prewarmed.</summary>
    public static bool IsLoaded => loaded;

    /// <summary>
    /// Downloads the portrait folder and the hidden-list settings. Yield this before any UI that
    /// shows portraits (base character picker, battle entry banner).
    /// </summary>
    public static IEnumerator EnsureLoadedRoutine()
    {
        if (loaded) yield break;

        var list = new BundledAddressables.PrewarmList();
        list.AddFolder<Sprite>(PortraitFolder);
        list.Add<DialoguePortraitSettings>(SettingsPath);
        yield return BundledAddressables.PrewarmRoutine(list);

        loaded = true;
    }

    /// <summary>
    /// Visible portraits, in catalog order. Returns an empty list if
    /// <see cref="EnsureLoadedRoutine"/> has not run yet.
    /// </summary>
    public static IReadOnlyList<Sprite> GetVisiblePortraits()
    {
        VisiblePortraits.Clear();
        Sprite[] all = BundledAddressables.LoadAllInFolderSync<Sprite>(PortraitFolder);
        DialoguePortraitSettings settings = BundledAddressables.LoadSync<DialoguePortraitSettings>(SettingsPath);
        for (int i = 0; i < all.Length; i++)
        {
            Sprite sprite = all[i];
            if (sprite == null) continue;
            if (settings != null && settings.IsHidden(sprite)) continue;
            VisiblePortraits.Add(sprite);
        }
        return VisiblePortraits;
    }
}
