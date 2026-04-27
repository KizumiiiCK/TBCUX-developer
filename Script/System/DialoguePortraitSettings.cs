using System.Collections.Generic;
using UnityEngine;

[CreateAssetMenu(
    fileName = "DialoguePortraitSettings",
    menuName = "TBCX/UI/Dialogue Portrait Settings")]
public class DialoguePortraitSettings : ScriptableObject
{
    [Tooltip("将不想展示在对话头像池中的图片拖进来。")]
    [SerializeField] private List<Sprite> hiddenPortraits = new List<Sprite>();

    private HashSet<string> hiddenNamesCache;

    public bool IsHidden(Sprite sprite)
    {
        if (sprite == null) return true;
        EnsureCache();
        return hiddenNamesCache.Contains(sprite.name);
    }

    private void EnsureCache()
    {
        if (hiddenNamesCache != null) return;
        hiddenNamesCache = new HashSet<string>();
        for (int i = 0; i < hiddenPortraits.Count; i++)
        {
            Sprite s = hiddenPortraits[i];
            if (s == null) continue;
            hiddenNamesCache.Add(s.name);
        }
    }
}
