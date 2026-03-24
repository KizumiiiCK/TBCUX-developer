using System.Collections;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class DifficultyBoard : MonoBehaviour
{
    BaseCanvas basecanv;
    [SerializeField] private TMP_Text sectionName;
    [SerializeField] private Button EnterBtn;
    [SerializeField] private Button BackBtn;
    [SerializeField] private Button leftBtn;
    [SerializeField] private Button rightBtn;
    [SerializeField]private RectTransform[] crowns = new RectTransform[5];
    private MapInfo mapInfo = null;
    private int current_diff = 0;
    private void Start()
    {
        basecanv=GameObject.Find("BaseCanvas").GetComponent<BaseCanvas>();
        leftBtn.onClick.AddListener(LowerDiff);
        rightBtn.onClick.AddListener(AddDiff);
        EnterBtn.onClick.AddListener(delegate { 
            PlayerPrefs.SetInt(UXPref.Difficulty, current_diff); 
            basecanv.LoadMap(mapInfo); 
            Destroy(gameObject); }
        );
        BackBtn.onClick.AddListener(delegate { Destroy(gameObject); });
        current_diff = 0;
        string sn = PlayerPrefs.GetString(UXPref.SectionName);
        LocalizationHelper.GetLocalizedText(UXPref.Localized_CS, sn,
                localizedText => sectionName.text = localizedText ?? sn);
        SwitchDifficulty();
    }
    public void SetMapInfo(MapInfo mi) { mapInfo = mi; }
    public void SwitchDifficulty()
    {
        if(current_diff==0)leftBtn.interactable = false; else leftBtn.interactable = true;
        if(current_diff==mapInfo.hardness-1)rightBtn.interactable = false; else rightBtn.interactable = true;
        SetupCrowns();
    }
    private void SetupCrowns()
    {
        for(int i = 0; i < 5; i++)
        {
            if (i <= current_diff)
            {
                crowns[i].gameObject.SetActive(true);
                crowns[i].anchoredPosition = new Vector2(-30 * current_diff + i * 60, 0);
            }
            else crowns[i].gameObject.SetActive(false);
        }
    }
    private void AddDiff() { current_diff++; SwitchDifficulty(); }
    private void LowerDiff() { current_diff--; SwitchDifficulty(); }
}
