using System.Collections;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class LevelRewardBoard : MonoBehaviour
{
    private Reward[] rewardlist;
    [SerializeField] private Transform Board;
    [SerializeField] private GameObject rewardUnit;

    private const int gap = 120;

    public void SetRewards(Reward[] R)=>rewardlist = R;
    public void ShowLevelRewards(bool cleared)
    {
        for(int i = Board.childCount - 1; i >= 0; i--) DestroyImmediate(Board.GetChild(i).gameObject);
        for(int i = 0; i < rewardlist.Length; i++)
        {
            RectTransform rrt= Instantiate(rewardUnit,Board).GetComponent<RectTransform>();
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
                string cid = R.id.ToString("0000");
                rewardImg.GetComponent<RectTransform>().sizeDelta = new Vector2(100, 75);
                rewardImg.sprite = Resources.Load<Sprite>(RewardIconHelper.GetCatDeployIconPath(cid, 0));
                break;
            case RewardType.UnlockTire:
                rewardImg.GetComponent<RectTransform>().sizeDelta = new Vector2(100, 75);
                rewardImg.sprite = Resources.Load<Sprite>(RewardIconHelper.GetUnlockTireIconPath(R.id));
                break;
            default: break;
        }
        droprate.text = $"{R.droprate}%";
        count.text = R.drawtimes.ToString();
        mark.SetActive(cleared && R.onlyOnce);
    }
}
