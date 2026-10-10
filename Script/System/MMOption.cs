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
    [Header("Navigation")]
    [SerializeField] private Button returnButton;
    [Header("Account Actions")]
    [Tooltip("把「账号操作」标题和 upload / delete 一起装进来的空物体。战斗里整块隐藏用。")]
    [SerializeField] private GameObject accountActionsRoot;
    [SerializeField] private Button uploadAccountButton;
    [SerializeField] private Button deleteAccountButton;
    [Header("Gameplay Options")]
    [SerializeField] private Toggle skipRepeatPlotToggle;
    [SerializeField] private Toggle noBattleEffectsToggle;
    [SerializeField] private Toggle noBattleEmotionToggle;
    [SerializeField] private Toggle noPauseInspectToggle;
    [SerializeField] private Toggle defaultDoubleSpeedToggle;

    private const string UploadAccountPagePath = "UI/Pages/user/TransferAccount";
    private const string LoginCheckPagePath = "UI/Pages/user/UserLoginCheckPage";
    private const string DeleteAccountPagePath = "UI/Pages/user/DeleteAccount";

    private int lang = 0;
    private bool accountActionsAllowed = true;
    // Start is called before the first frame update
    void Start()
    {
        bgmSlider.onValueChanged.AddListener(SetBGMVolume);
        seSlider.onValueChanged.AddListener(SetSEVolume);
        if (returnButton != null)
            returnButton.onClick.AddListener(Close);
        // languageToggles 以前只有声明没人读，预制体里 CHN/ENG 也没连 onClick，
        // 语言切换是断的。这里按数组下标当语种索引补上。
        if (languageToggles != null)
        {
            for (int i = 0; i < languageToggles.Length; i++)
            {
                if (languageToggles[i] == null) continue;
                int index = i;  // 闭包要取副本，否则每个按钮拿到的都是 Length
                languageToggles[i].onClick.AddListener(() => ResetLanguage(index));
            }
        }
        if (uploadAccountButton != null)
            uploadAccountButton.onClick.AddListener(OpenUploadAccountPage);
        if (deleteAccountButton != null)
            deleteAccountButton.onClick.AddListener(OpenDeleteAccountPage);
        ApplyAccountActionsVisibility();
        if (skipRepeatPlotToggle != null)
            skipRepeatPlotToggle.onValueChanged.AddListener(SetSkipRepeatPlot);
        if (noBattleEffectsToggle != null)
            noBattleEffectsToggle.onValueChanged.AddListener(SetNoBattleEffects);
        if (noBattleEmotionToggle != null)
            noBattleEmotionToggle.onValueChanged.AddListener(SetNoBattleEmotion);
        if (noPauseInspectToggle != null)
            noPauseInspectToggle.onValueChanged.AddListener(SetNoPauseInspect);
        if (defaultDoubleSpeedToggle != null)
            defaultDoubleSpeedToggle.onValueChanged.AddListener(SetDefaultDoubleSpeed);
    }

    private void OnEnable()
    {
        lang = PlayerPrefs.GetInt(UXPref.LANG, 0);
        ResetLanguage(lang);
        RefreshTable();
    }

    /// <summary>
    /// 页面现在是生成出来的，关闭就是销毁自己（跟 DifficultyBoard / ProficientTable 一致）。
    /// </summary>
    public void Close() => Destroy(gameObject);

    /// <summary>
    /// 战斗里生成这个页面时用：不给账号操作入口。
    /// 置标志而不是只改按钮——Instantiate 之后 Start() 当帧稍后才跑，
    /// 它会按 accountActionsAllowed 再回填一次，所以两边都要管。
    /// </summary>
    public void DisableAccountActions()
    {
        accountActionsAllowed = false;
        ApplyAccountActionsVisibility();
    }

    /// <summary>
    /// 有 accountActionsRoot 就整块（标题 + 两个按钮）隐藏；
    /// 没连上时退回到只管两个按钮，标题会留着。
    /// </summary>
    private void ApplyAccountActionsVisibility()
    {
        if (accountActionsRoot != null)
        {
            accountActionsRoot.SetActive(accountActionsAllowed);
            return;
        }
        if (uploadAccountButton != null) uploadAccountButton.gameObject.SetActive(accountActionsAllowed);
        if (deleteAccountButton != null) deleteAccountButton.gameObject.SetActive(accountActionsAllowed);
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
    public void SetSkipRepeatPlot(bool on) => GameOptions.SkipRepeatPlot = on;

    public void SetNoBattleEffects(bool on) => GameOptions.NoBattleEffects = on;

    public void SetNoBattleEmotion(bool on) => GameOptions.NoBattleEmotion = on;

    public void SetNoPauseInspect(bool on) => GameOptions.NoPauseInspect = on;

    public void SetDefaultDoubleSpeed(bool on) => GameOptions.DefaultDoubleSpeed = on;

    private void RefreshTable()
    {
        bgmSlider.value = PlayerPrefs.GetFloat(UXPref.BGM_PARAM, 1);
        seSlider.value = PlayerPrefs.GetFloat(UXPref.SE_PARAM, 1);
        SetBGMVolume(bgmSlider.value);
        SetSEVolume(seSlider.value);
        // SetIsOnWithoutNotify：回填勾选状态不该反过来再写一次 PlayerPrefs。
        GameOptions.Refresh();
        if (skipRepeatPlotToggle != null)
            skipRepeatPlotToggle.SetIsOnWithoutNotify(GameOptions.SkipRepeatPlot);
        if (noBattleEffectsToggle != null)
            noBattleEffectsToggle.SetIsOnWithoutNotify(GameOptions.NoBattleEffects);
        if (noBattleEmotionToggle != null)
            noBattleEmotionToggle.SetIsOnWithoutNotify(GameOptions.NoBattleEmotion);
        if (noPauseInspectToggle != null)
            noPauseInspectToggle.SetIsOnWithoutNotify(GameOptions.NoPauseInspect);
        if (defaultDoubleSpeedToggle != null)
            defaultDoubleSpeedToggle.SetIsOnWithoutNotify(GameOptions.DefaultDoubleSpeed);
    }
    public void ResetLanguage(int L)
    {
        var locales = LocalizationSettings.AvailableLocales.Locales;
        // 语种数量比按钮少、或 PlayerPrefs 里存着旧版本的下标时，越界就当没切。
        if (locales == null || L < 0 || L >= locales.Count) return;
        lang = L;
        PlayerPrefs.SetInt(UXPref.LANG, lang);
        LocalizationSettings.SelectedLocale = locales[L];
    }

    private void OpenUploadAccountPage()
    {
        bool hasOnlinePid = UserInfoLocalStore.TryLoad(out UserInfoLocalData user)
            && UserInfoLocalStore.HasOnlinePid(user);
        string path = hasOnlinePid ? UploadAccountPagePath : LoginCheckPagePath;
        GameObject prefab = Resources.Load<GameObject>(path);
        if (prefab == null)
        {
            Debug.LogError($"[MMOption] Missing page prefab: {path}");
            return;
        }

        GameObject obj = Instantiate(prefab);
        if (!hasOnlinePid)
        {
            UserLoginCheckPage page = obj.GetComponent<UserLoginCheckPage>();
            if (page != null) page.StayForAccountChoice();
        }
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
