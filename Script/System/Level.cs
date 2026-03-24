using System;
using System.Collections;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class Level : MonoBehaviour
{
    private RectTransform selfTransform;
    private Image selfImage;
    private float cx;
    [SerializeField] private Sprite[] levelImage;
    private TMP_Text levelName;
    [SerializeField] private TMP_Text clearedTimes;
    [SerializeField] private TMP_Text highestScore;
    [SerializeField] private Button ShowDetailBtn;
    [SerializeField] private Image MarkImg;
    public LevelTiler LT;
    // Start is called before the first frame update
    void Start()
    {
        selfTransform = GetComponent<RectTransform>();
        selfImage = GetComponent<Image>();
        levelName = transform.GetChild(0).GetComponent<TMP_Text>();
    }

    // Update is called once per frame
    void FixedUpdate()
    {
        cx = Mathf.Clamp(selfTransform.anchoredPosition.x+selfTransform.parent.GetComponent<RectTransform>().anchoredPosition.x, -375, 375);
        selfTransform.localScale = Vector3.one * (1.75f - 0.5f * (Mathf.Abs(cx) / 375));
        selfImage.color = new Color(1, 1, 1, 1 - 0.5f * (Mathf.Abs(cx) / 375));
    }
    public void SetLevelInfo(LevelTileInfo Li)
    {
        LocalizationHelper.GetLocalizedText(UXPref.Localized_LevelNames, Li.levelNameID,
                localizedText => levelName.text = localizedText ?? Li.levelNameID);
        SetLevelImage(Li.tileType);
    }
    public void SetClearedInfo(int ct, int hs)
    {
        clearedTimes.text=ct.ToString();
        highestScore.text=hs.ToString();
    }
    public void SetLevelImage(LevelTileType ltt)
    {
        Image lvlimg=GetComponent<Image>();
        lvlimg.sprite = levelImage[Array.IndexOf(Enum.GetValues(typeof(LevelTileType)), ltt)];
    }
    public void SetLT(LevelTiler lt)
    {
        LT = lt;
        ShowDetailBtn.GetComponent<Button>().onClick.AddListener(delegate { LT.ShowSEB(); });
    }
    public void SetMark(int num=60)
    {
        MarkImg.gameObject.SetActive(true);
        MarkImg.sprite = Resources.Load<Sprite>($"Reward/{num}");
    }
}
