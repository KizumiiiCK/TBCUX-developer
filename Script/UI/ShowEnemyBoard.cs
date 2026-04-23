using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

public class ShowEnemyBoard : MonoBehaviour
{
    private const string EnemyHatenaSpritePath = "Units/Enemy Units/icon_hatena";

    [SerializeField] private GameObject EnemyIcon_prefab;
    [SerializeField] private Transform EnemyList;
    [SerializeField] private KiPanel panel;
    private readonly List<GameObject> enemyIconPool = new List<GameObject>();
    private readonly Dictionary<string, Sprite> enemyIconCache = new Dictionary<string, Sprite>();
    private Sprite hatenaSpriteCache;

    /// <param name="blindAllEnemyIcons">IV 且未通关时：所有槽位使用问号图标，不暴露真实敌人头像。</param>
    public void ShowEnemies(string[] en, bool blindAllEnemyIcons = false)
    {
        int count = en != null ? en.Length : 0;
        if (EnemyList == null || EnemyIcon_prefab == null) return;

        for (int i = 0; i < enemyIconPool.Count; i++)
        {
            if (enemyIconPool[i] != null) enemyIconPool[i].SetActive(false);
        }

        if (en == null || count == 0) return;

        if (blindAllEnemyIcons)
        {
            if (hatenaSpriteCache == null)
                hatenaSpriteCache = Resources.Load<Sprite>(EnemyHatenaSpritePath);
        }

        for (int i = 0; i < count; i++)
        {
            GameObject iconObj;
            if (i < enemyIconPool.Count && enemyIconPool[i] != null)
            {
                iconObj = enemyIconPool[i];
                iconObj.SetActive(true);
            }
            else
            {
                iconObj = Instantiate(EnemyIcon_prefab, EnemyList);
                iconObj.transform.localScale = Vector3.one;
                enemyIconPool.Add(iconObj);
            }

            var image = iconObj.GetComponent<Image>();
            if (image != null)
            {
                if (blindAllEnemyIcons && hatenaSpriteCache != null)
                {
                    image.sprite = hatenaSpriteCache;
                    continue;
                }

                if (!enemyIconCache.TryGetValue(en[i], out Sprite icon))
                {
                    icon = Resources.Load<Sprite>($"Units/Enemy Units/{en[i]}/enemy_icon");
                    enemyIconCache[en[i]] = icon;
                }
                image.sprite = icon;
            }
        }
        // if(resize_board)
        // {
        //     panel.SetSize(Mathf.Min(count * 64+160, 800)*2, Mathf.Min(count/10 * 64+200, 330)*2);
        // }
    }
}
