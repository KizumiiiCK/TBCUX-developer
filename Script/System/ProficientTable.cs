using System.Collections;
using System.Collections.Generic;
using System;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class ProficientTable : MonoBehaviour
{
    [SerializeField] private Image medalImg;
    [SerializeField] private TMP_Text title_txt;
    [SerializeField] private TMP_Text description_txt;
    [SerializeField] private Button left_btn;
    [SerializeField] private Button right_btn;
    [SerializeField] private Button back_btn;
    public int current_max_level = 0;
    private int show_prof_level = 0;
    private CharacterProficiency CP;
    private void Start()
    {
        InitialzeButtons();
    }
    public void ShowProf(int L)
    {
        left_btn.interactable = L != 0;
        right_btn.interactable = L < current_max_level;
        GetDescriptionText(L);
    }
    public void ShowProf(bool forward)
    {
        show_prof_level += forward ? 1 : -1;
        left_btn.interactable = show_prof_level != 0;
        right_btn.interactable = show_prof_level < current_max_level;
        GetDescriptionText(show_prof_level);
    }
    public void Initialize(CharacterProficiency cp)
    {
        CP = cp;
        int L = cp.level;
        GetTitleText(L);
        //if (L == 4) L = 3;
        current_max_level = Mathf.Min(L, 3);
        //current_max_level = 3;
        show_prof_level = current_max_level;
        ShowProf(current_max_level);
    }
    private void GetTitleText(int L)
    {
        if (L == 4)
        {
            LocalizationHelper.GetLocalizedText(UXPref.Localized_Descriptions, $"D:p:4",
                LocalizedText => title_txt.text = LocalizedText ?? "NULL");
        }
        else
        {
            LocalizationHelper.GetLocalizedText(UXPref.Localized_Descriptions, $"N:p",
                LocalizedText => title_txt.text = string.Format(LocalizedText,L) ?? "NULL");
        } 
    }
    private void GetDescriptionText(int L)
    {
        string tcolor = "<color=#" + (CP.Compare(L) ? "00FF00" : "FF6060") + ">";
        long progressValue = CP != null ? CP.GetProgressLong(L) : 0L;
        string formated = tcolor + FormatProgressValue(progressValue) + "</color>";
        LocalizationHelper.GetLocalizedText(UXPref.Localized_Descriptions, $"D:p:{L}",
                LocalizedText => description_txt.text = string.Format(LocalizedText, formated) ?? "NULL");
        medalImg.sprite = StorageImageHelper.GetItemImageByOrder(100 + L);
        medalImg.color = L < CP.level ? Color.white : new Color(0.5f, 0.5f, 0.5f);
    }

    private static string FormatProgressValue(long value)
    {
        if (value > 1_000_000_000L)
        {
            int exponent = (int)Math.Floor(Math.Log10(value));
            double mantissa = value / Math.Pow(10d, exponent);
            return $"{mantissa:0.0000}e+{exponent}";
        }
        return value.ToString();
    }
    private void InitialzeButtons()
    {
        left_btn.onClick.AddListener(delegate { ShowProf(false); });
        right_btn.onClick.AddListener(delegate { ShowProf(true); });
        back_btn.onClick.AddListener(delegate { Destroy(gameObject); });
    }
}
