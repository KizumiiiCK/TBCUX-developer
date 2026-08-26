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
    [Header("Account Actions")]
    [SerializeField] private Button uploadAccountButton;
    [SerializeField] private Button deleteAccountButton;

    private const string UploadAccountPagePath = "UI/Pages/user/TransferAccount";
    private const string DeleteAccountPagePath = "UI/Pages/user/DeleteAccount";

    private int lang = 0;
    // Start is called before the first frame update
    void Start()
    {
        // Platform settings page owns music/SFX mute and language. Hiding the in-game controls
        // avoids a mixer slider that cannot reach host-played BGM, and a language toggle that
        // fights RuntimeLanguage(). Prefab references stay so Editor Play Mode still has them.
#if UNITY_WEBGL && !UNITY_EDITOR
        if (bgmSlider != null) bgmSlider.gameObject.SetActive(false);
        if (seSlider != null) seSlider.gameObject.SetActive(false);
        if (languageToggles != null)
        {
            for (int i = 0; i < languageToggles.Length; i++)
            {
                if (languageToggles[i] != null) languageToggles[i].gameObject.SetActive(false);
            }
        }
#else
        bgmSlider.onValueChanged.AddListener(SetBGMVolume);
        seSlider.onValueChanged.AddListener(SetSEVolume);
#endif
        if (uploadAccountButton != null) uploadAccountButton.gameObject.SetActive(false);
        if (deleteAccountButton != null) deleteAccountButton.gameObject.SetActive(false);
    }

    private void OnEnable()
    {
#if UNITY_WEBGL && !UNITY_EDITOR
        return;
#else
        lang = PlayerPrefs.GetInt(UXPref.LANG, 0);
        ResetLanguage(lang);
        RefreshTable();
#endif
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

    private void OpenUploadAccountPage()
    {
        GameObject prefab = Resources.Load<GameObject>(UploadAccountPagePath);
        if (prefab == null)
        {
            Debug.LogError($"[MMOption] Missing page prefab: {UploadAccountPagePath}");
            return;
        }
        Instantiate(prefab);
    }

    private void OpenDeleteAccountPage()
    {
        GameObject prefab = Resources.Load<GameObject>(DeleteAccountPagePath);
        if (prefab == null)
        {
            Debug.LogError($"[MMOption] Missing page prefab: {DeleteAccountPagePath}");
            return;
        }
        Instantiate(prefab);
    }
}
