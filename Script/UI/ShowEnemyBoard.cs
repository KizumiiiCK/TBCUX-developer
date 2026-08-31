using System.Collections;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class ShowEnemyBoard : MonoBehaviour
{
    private const string EnemyHatenaSpritePath = "Units/Enemy Units/icon_hatena";

    [SerializeField] private GameObject EnemyIcon_prefab;
    [SerializeField] private Transform EnemyList;
    [SerializeField] private KiPanel panel;
    private readonly List<GameObject> enemyIconPool = new List<GameObject>();

    /// <param name="blindAllEnemyIcons">IV 且未通关时：所有槽位使用问号图标，不暴露真实敌人头像。</param>
    public void ShowEnemies(string[] en, int[] enemyMultipliers = null, bool blindAllEnemyIcons = false)
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
            TMP_Text multiText = null;
            if (iconObj.transform.childCount > 0)
                multiText = iconObj.transform.GetChild(0).GetComponent<TMP_Text>();
            if (image != null)
            {
                // 图标按需异步加载；槽位复用时以 iconObj 为 owner，旧请求自动作废。
                // 敌我双边：'-' 前缀仍按 CharacterPlacer 解析到猫/敌资源目录，但预览只读 enemy_icon。
                string iconAddress = ResolveEnemyBoardIconAddress(en[i], blindAllEnemyIcons);
                AsyncIconLoader.Instance.Load(iconObj, iconAddress,
                    sprite => { if (image != null) image.sprite = sprite; });

                if (blindAllEnemyIcons)
                {
                    if (multiText != null) multiText.text = string.Empty;
                    continue;
                }
            }
            if (multiText != null)
            {
                int ratio = (enemyMultipliers != null && i < enemyMultipliers.Length) ? enemyMultipliers[i] : 100;
                multiText.text = $"{ratio}%";
            }
        }
        // if(resize_board)
        // {
        //     panel.SetSize(Mathf.Min(count * 64+160, 800)*2, Mathf.Min(count/10 * 64+200, 330)*2);
        // }
    }

    private static string ResolveEnemyBoardIconAddress(string rawCode, bool blindAllEnemyIcons)
    {
        if (blindAllEnemyIcons) return EnemyHatenaSpritePath;
        if (CharacterPlacer.TryParse(rawCode, false, out UnitIdentity identity) && identity.IsValid)
        {
            return CharacterPlacer.GetLoadPath(identity) + "enemy_icon";
        }
        return $"Units/Enemy Units/{rawCode}/enemy_icon";
    }
}
