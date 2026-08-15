using System.Collections;
using System.Collections.Generic;
using System.Linq;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class DropRateBoard : MonoBehaviour
{
    [Header("Contents")]
    [SerializeField] private RectTransform Scrollview_Content;
    [SerializeField] private TMP_Text pool_name;
    [SerializeField] private Image poster;
    [SerializeField] private Transform Content;
    [Header("Detail Block Prefab")]
    [SerializeField] private GameObject DetailsBlock;
    //
    private const int blockgap_x = 240;
    private const int blockgap_y = -180;
    private const int tiregap = -75;
    private const int width = 6;
    //
    public void InitializeDropDetails(Pool P)
    {
        pool_name.text = P.pool_name.ToUpper();
        poster.sprite = P.GetPoolPoster();
        Scrollview_Content.anchoredPosition = Vector2.zero;
        int units_stack = 0;
        for (int i = 0; i < 7; i++)
        {
            int tiretile_y = units_stack * blockgap_y/2 + i * tiregap;
            RectTransform TireTile= Content.transform.GetChild(i).GetComponent<RectTransform>();
            TireTile.anchoredPosition = new Vector2(0, tiretile_y);
            int tire_count = 0;
            if (P.dropUnits[i] != null) tire_count = P.dropUnits[i].Length;
            float tire_droprate = P.dropRates[i] / P.dropRates.Sum() *100;
            TireTile.GetChild(1).GetComponent<TMP_Text>().text = tire_droprate.ToString("F2") + " %";
            Transform TireDetailsBox = TireTile.GetChild(2);
            for(int n = TireDetailsBox.childCount - 1; n >= 0; n--) Destroy(TireDetailsBox.GetChild(n).gameObject);
            for (int j = 0; j < tire_count; j++)
            {
                RectTransform details = Instantiate(DetailsBlock).GetComponent<RectTransform>();
                details.SetParent(TireDetailsBox);
                details.localScale = Vector3.one;
                details.anchoredPosition = new Vector2(blockgap_x * (j % width), blockgap_y * (j / width));
                string unitcode = P.dropUnits[i][j].ToString("000");
                Sprite icon = BundledAddressables.LoadSync<Sprite>($"Units/Cat Units/{i}/{unitcode}/0/icon_deploy");
                if (icon != null) details.GetChild(0).GetComponent<Image>().sprite = icon;
                details.GetChild(1).GetComponent<TMP_Text>().text = (tire_droprate / tire_count).ToString("F2") + "%";
            }
            units_stack += tire_count / 6 + (tire_count % 6 == 0 ? 0 : 1) + (tire_count > 0 ? 1 : 0);
        }
    }
    //private void Update()
    //{
    //    if (Scrollview_Content.anchoredPosition.y > min_scroll_y) Scrollview_Content.anchoredPosition = new Vector2(0, min_scroll_y);
    //    if (Scrollview_Content.anchoredPosition.y < max_scroll_y) Scrollview_Content.anchoredPosition = new Vector2(0, max_scroll_y);
    //}
}
