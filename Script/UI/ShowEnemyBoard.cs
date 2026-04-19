using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

public class ShowEnemyBoard : MonoBehaviour
{
    [SerializeField] private GameObject EnemyIcon_prefab;
    [SerializeField] private Transform EnemyList;
    [SerializeField] private KiPanel panel;
    private readonly List<GameObject> enemyIconPool = new List<GameObject>();
    private readonly Dictionary<string, Sprite> enemyIconCache = new Dictionary<string, Sprite>();

    public void ShowEnemies(string[] en)
    {
        int count = en != null ? en.Length : 0;
        if (EnemyList == null || EnemyIcon_prefab == null) return;

        for (int i = 0; i < enemyIconPool.Count; i++)
        {
            if (enemyIconPool[i] != null) enemyIconPool[i].SetActive(false);
        }

        if (en == null || count == 0) return;

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
