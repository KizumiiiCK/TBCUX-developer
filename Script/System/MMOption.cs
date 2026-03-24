using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Audio;
using UnityEngine.Localization.Settings;
using UnityEngine.UI;

public class MMOption : MonoBehaviour
{
    [SerializeField] private Button[] languageToggles;
    [SerializeField] private AudioMixer mixer;
    [SerializeField] private Slider bgmSlider;
    [SerializeField] private Slider seSlider;

    private int lang = 0;
    // Start is called before the first frame update
    void Start()
    {
        bgmSlider.onValueChanged.AddListener(SetBGMVolume);
        seSlider.onValueChanged.AddListener(SetSEVolume);
    }

    private void OnEnable()
    {
        lang = PlayerPrefs.GetInt(UXPref.LANG, 0);
        ResetLanguage(lang);
        RefreshTable();
    }

    public void SetBGMVolume(float linear)
    {
        float dB = linear <= 0 ? -80f : 20f * Mathf.Log10(linear);
        Debug.Log(dB);
        mixer.SetFloat(UXPref.BGM_PARAM, dB);
        PlayerPrefs.SetFloat(UXPref.BGM_PARAM, linear);
    }

    public void SetSEVolume(float linear)
    {
        float dB = linear <= 0 ? -80f : 20f * Mathf.Log10(linear);
        Debug.Log(dB);
        mixer.SetFloat(UXPref.SE_PARAM, dB);
        PlayerPrefs.SetFloat(UXPref.SE_PARAM, linear);
    }
    private void RefreshTable()
    {
        bgmSlider.value = PlayerPrefs.GetFloat(UXPref.BGM_PARAM, 1);
        seSlider.value = PlayerPrefs.GetFloat(UXPref.SE_PARAM, 1);
        SetBGMVolume(bgmSlider.value);
        SetSEVolume(seSlider.value);
    }
    public void ResetLanguage(int L)
    {
        lang = L;
        PlayerPrefs.SetInt(UXPref.LANG, lang);
        LocalizationSettings.SelectedLocale = LocalizationSettings.AvailableLocales.Locales[L];
    }
}
