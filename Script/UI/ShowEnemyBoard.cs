using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

public class ShowEnemyBoard : MonoBehaviour
{
    [SerializeField] private GameObject EnemyIcon_prefab;
    [SerializeField] private Transform EnemyList;
    [SerializeField] private KiPanel panel;

    public void ShowEnemies(string[] en)
    {
        int count = en.Length;
        if (EnemyList == null || EnemyIcon_prefab == null) return;

        for (int i = EnemyList.childCount - 1; i >= 0; i--)
        {
            DestroyImmediate(EnemyList.GetChild(i).gameObject);
        }

        if (en == null || count == 0) return;

        for (int i = 0; i < count; i++)
        {
            var iconObj = Instantiate(EnemyIcon_prefab, EnemyList);
            iconObj.transform.localScale = Vector3.one;

            var image = iconObj.GetComponent<Image>();
            if (image != null)
            {
                image.sprite = Resources.Load<Sprite>($"Units/Enemy Units/{en[i]}/enemy_icon");
            }
        }
        // if(resize_board)
        // {
        //     panel.SetSize(Mathf.Min(count * 64+160, 800)*2, Mathf.Min(count/10 * 64+200, 330)*2);
        // }
    }
}
