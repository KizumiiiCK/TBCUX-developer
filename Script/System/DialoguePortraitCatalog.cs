using System.Collections.Generic;
using UnityEngine;

public static class DialoguePortraitCatalog
{
    private const string PortraitFolder = "DialogueImage";
    private const string SettingsPath = "DialogueImage/DialoguePortraitSettings";

    private static readonly List<Sprite> VisiblePortraits = new List<Sprite>(64);

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
