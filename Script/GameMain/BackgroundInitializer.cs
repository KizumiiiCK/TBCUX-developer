using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class BackgroundInitializer : MonoBehaviour
{
    public void UpdateMaterialProperties(int mapNum)
    {
        // 战斗背景由 BattlePrewarm 预热（AddLevelScenery），此处同步读取应命中缓存
        Sprite s = BundledAddressables.LoadSync<Sprite>($"Background/Maps/{mapNum}");
        if (s == null)
        {
            Debug.LogWarning($"[BackgroundInitializer] Background/Maps/{mapNum} not loaded.");
            return;
        }
        GetComponent<SpriteRenderer>().sprite = s;
        transform.Translate(new Vector2(0, (s.rect.height - 512) / 70f));
    }
}
