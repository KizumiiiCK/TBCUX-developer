using System.Collections;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class LevelRewardBoard : MonoBehaviour
{
    private Reward[] rewardlist;
    [SerializeField] private Transform Board;
    [SerializeField] private RectTransform blackshade;
    [SerializeField] private GameObject rewardUnit;

    private const int gap = 120;

    public void SetRewards(Reward[] R)=>rewardlist = R;
    public void ShowLevelRewards(bool cleared)
    {
        for(int i = Board.childCount - 1; i >= 0; i--) DestroyImmediate(Board.GetChild(i).gameObject);
        blackshade.sizeDelta = new Vector2(600, gap * (1+rewardlist.Length));
        for(int i = 0; i < rewardlist.Length; i++)
        {
            RectTransform rrt= Instantiate(rewardUnit).GetComponent<RectTransform>();
            rrt.SetParent(Board);
            rrt.anchoredPosition = Board.GetComponent<RectTransform>().anchoredPosition + new Vector2(0, -gap * i);
            rrt.localScale = Vector3.one;
            DisplayOneUnit(rrt.gameObject, rewardlist[i], cleared);
        }
    }
    private void DisplayOneUnit(GameObject unit, Reward R, bool cleared)
    {
        Image rewardImg = unit.transform.GetChild(0).GetComponent<Image>();
        TMP_Text droprate = unit.transform.GetChild(1).GetComponent<TMP_Text>();
        TMP_Text count = unit.transform.GetChild(2).GetComponent<TMP_Text>();
        GameObject mark = unit.transform.GetChild(3).gameObject;
        switch (R.type)
        {
            case RewardType.item:
                rewardImg.sprite = StorageImageHelper.GetItemImageByOrder(R.id);
                break;
            case RewardType.character:
                string cid= R.id.ToString("0000");
                rewardImg.GetComponent<RectTransform>().sizeDelta=new Vector2(130, 100);
                rewardImg.sprite = Resources.Load<Sprite>($"Units/Cat Units/{cid[0]}/{cid.Substring(1, 3)}/0/icon_deploy");
                break;
            default: break;
        }
        droprate.text = $"{R.droprate}%";
        count.text = R.drawtimes.ToString();
        mark.SetActive(cleared && R.onlyOnce);
    }
}
