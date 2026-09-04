using System;
using System.Collections;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.Audio;
using UnityEngine.Localization.Settings;
using UnityEngine.UI;

public class MainMenu : MonoBehaviour
{
    private static List<string[]> chapterNames = new List<string[]>()
    {
        new string[]{"World_I","World_II","World_III"},
        new string[]{"Future_I"},
        new string[]{},
        new string[]{"LEGEND"},
        new string[]{"Dream_Pre"},
    };
    [Header("Buttons")]
    [SerializeField] private RectTransform TopBtns;
    [SerializeField] private Button StartgameBtn;
    [SerializeField] private Button OptionBtn;
    [SerializeField] private Button DevBtn;
    [SerializeField] private Button OptReturnBtn;
    [SerializeField] private Button DevReturnBtn;
    [SerializeField] private Button FullCptReturnBtn;
    [SerializeField] private Button SubCptReturnBtn;
    [Header("Canvas")]
    [SerializeField] private GameObject optionCanvas;
    [SerializeField] private GameObject devCanvas;
    [SerializeField] private GameObject fullCptCanvas;
    [SerializeField] private GameObject subCptCanvas;
    [SerializeField] private Transform FullChapterContent;
    [SerializeField] private Transform SubChapterContent;
    [SerializeField] private TMP_Text welcomeBackText;
    //[SerializeField] private GameObject ChapterSelectionBtn;
    [Header("Prefab")]
    [SerializeField] private GameObject subChapter;
    [SerializeField] private AudioMixer mixer;

    private bool operating = false;
    private bool tagInShown;
    private readonly List<GameObject> pooledSubChapterButtons = new List<GameObject>();
    private readonly Dictionary<string, Sprite> chapterCoverCache = new Dictionary<string, Sprite>();
    // Start is called before the first frame update
    private void Awake()
    {
        ShowTagInOnce();
        Input.multiTouchEnabled = false;
        StartCoroutine(ApplyInitialLanguageRoutine());
    }

    private void Start()
    {
        ShowTagInOnce();

        Application.targetFrameRate = 30;
        optionCanvas.SetActive(false);
        ButtonInitializer();
#if UNITY_WEBGL && !UNITY_EDITOR
        // Volume is owned by the platform settings page. Keep the start button locked until
        // UserLoginCheckPage finishes boot, otherwise a fast tap can enter a chapter against
        // an empty save cache.
        operating = true;
#else
        SetBGMVolume();
        SetSEVolume();
#endif
        PlayerPrefs.SetString(UXPref.Login_Date, PlatformTimeSystem.Today.ToString());
    }

    /// <summary>
    /// Called once the boot gate has hydrated saves and read <c>whoami</c>. Unlocks the main menu.
    /// </summary>
    public void NotifyPlatformBootComplete()
    {
        operating = false;
    }

    /// <summary>
    /// First thing on entering the game: wait for Unity Localization, then pick locale 0 (zh-CN)
    /// when the Builda host language is Chinese, otherwise locale 1 (en-US).
    /// </summary>
    private IEnumerator ApplyInitialLanguageRoutine()
    {
        yield return LocalizationSettings.InitializationOperation;

        int lang = 0;
#if UNITY_WEBGL && !UNITY_EDITOR
        string host = Builda.BuildaSDK.RuntimeLanguage();
        if (!string.IsNullOrWhiteSpace(host)
            && !host.Trim().StartsWith("zh", StringComparison.OrdinalIgnoreCase))
        {
            lang = 1;
        }
#else
        lang = PlayerPrefs.GetInt(UXPref.LANG, 0);
#endif
        ResetLanguage(lang);
    }

    public void ResetLanguage()
    {
        ResetLanguage(PlayerPrefs.GetInt(UXPref.LANG, 0));
    }

    public void ResetLanguage(int lang)
    {
        var locales = LocalizationSettings.AvailableLocales;
        if (locales == null || locales.Locales == null || locales.Locales.Count == 0) return;

        int index = lang == 1 && locales.Locales.Count > 1 ? 1 : 0;
        if (index >= locales.Locales.Count) return;

        PlayerPrefs.SetInt(UXPref.LANG, index);
        LocalizationSettings.SelectedLocale = locales.Locales[index];
    }

    private void ShowTagInOnce()
    {
        if (tagInShown) return;
        tagInShown = true;

        GameObject prefab = Resources.Load<GameObject>("UI/Tag In");
        if (prefab != null) Instantiate(prefab);
    }
    private void ButtonInitializer()
    {
        TopBtns.localPosition = Vector2.zero;
        OptionBtn.onClick.AddListener(ShowOption);
        OptReturnBtn.onClick.AddListener(CloseOption);
        DevBtn.onClick.AddListener(ShowDeveloper);
        DevReturnBtn.onClick.AddListener(CloseDeveloper);
        StartgameBtn.onClick.AddListener(delegate { if (operating) return; ShowFullCpts(); });
        FullCptReturnBtn.onClick.AddListener(delegate { if (operating) return; StartCoroutine(ReturnToStart()); });
        FullChapterContent.GetChild(0).GetComponent<Button>().onClick.AddListener(delegate { if (operating) return; StartCoroutine(FullToSub(0)); });
        FullChapterContent.GetChild(1).GetComponent<Button>().onClick.AddListener(delegate { if (operating) return; StartCoroutine(FullToSub(1)); });
        FullChapterContent.GetChild(3).GetComponent<Button>().onClick.AddListener(delegate { if (operating) return; StartCoroutine(FullToSub(3)); });
        FullChapterContent.GetChild(4).GetComponent<Button>().onClick.AddListener(delegate { if (operating) return; StartCoroutine(FullToSub(4)); });
        SubCptReturnBtn.onClick.AddListener(delegate { if (operating) return; StartCoroutine(SubToFull()); });
    }
    private void ShowOption()
    {
        optionCanvas.SetActive(true);
    }
    private void CloseOption()
    {
        optionCanvas.SetActive(false);
    }
    private void ShowDeveloper()
    {
        devCanvas.SetActive(true);
    }
    private void CloseDeveloper()
    {
        devCanvas.SetActive(false);
    }
    private void ShowFullCpts()
    {
        TopBtns.localPosition = new Vector2(0, -1000);
        StartCoroutine(FullCptShow(true));
    }
    private IEnumerator FullCptShow(bool show)
    {
        float t = 0;
        float top = -200;
        float bottom = -1000;
        RectTransform rect = fullCptCanvas.transform.GetChild(1).GetComponent<RectTransform>();
        rect.anchoredPosition = new Vector3(0,show ? bottom:top,0);
        Vector3 target = new Vector3(0, show ? top : bottom, 0);

        fullCptCanvas.SetActive(true);
        while (t < 1)
        {
            t += Time.deltaTime;
            rect.anchoredPosition = Vector3.Lerp(rect.anchoredPosition, target, Time.deltaTime * 5);
            yield return new WaitForFixedUpdate();
        }
        rect.anchoredPosition = target;
        fullCptCanvas.SetActive(show);
    }
    private IEnumerator SubCptShow(bool show)
    {
        float t = 0;
        float top = -200;
        float bottom = -1000;
        RectTransform rect = subCptCanvas.transform.GetChild(1).GetComponent<RectTransform>();
        rect.anchoredPosition = new Vector3(0, show ? bottom : top, 0);
        Vector3 target = new Vector3(0, show ? top : bottom, 0);

        subCptCanvas.SetActive(true);
        while (t < 1)
        {
            t += Time.deltaTime;
            rect.anchoredPosition = Vector3.Lerp(rect.anchoredPosition, target, Time.deltaTime * 5);
            yield return new WaitForFixedUpdate();
        }
        rect.anchoredPosition = target;
        subCptCanvas.SetActive(show);
    }
    //Operating related
    private IEnumerator ReturnToStart()
    {
        operating = true;
        StartCoroutine(FullCptShow(false));
        yield return new WaitForSeconds(0.25f);
        TopBtns.localPosition = Vector2.zero;
        operating = false;
    }
    private IEnumerator FullToSub(int fc_num)
    {
        operating = true;
        StartCoroutine(FullCptShow(false));
        yield return new WaitForSeconds(0.25f);
        StartCoroutine(SubCptShow(true));
        for(int i = 1; i < SubChapterContent.childCount; i++)
        {
            Transform child = SubChapterContent.GetChild(i);
            child.gameObject.SetActive(false);
            pooledSubChapterButtons.Add(child.gameObject);
        }
        for(int i = 0;i< chapterNames[fc_num].Length; i++)
        {
            GameObject buttonObj = GetOrCreateSubChapterButton();
            Transform tsc = buttonObj.transform;
            tsc.SetParent(SubChapterContent);
            buttonObj.SetActive(true);
            tsc.localScale = Vector3.one * 0.9f;
            string chapterName = chapterNames[fc_num][i];
            tsc.GetComponent<ChapterButton>().chapterName = chapterName;
            if (!chapterCoverCache.TryGetValue(chapterName, out Sprite cover))
            {
                cover = Resources.Load<Sprite>($"LevelData/Chapters/CPImages/{chapterName}");
                chapterCoverCache[chapterName] = cover;
            }
            tsc.GetComponent<KiButton>().SetCover(cover);
        }
        operating = false;
    }
    private GameObject GetOrCreateSubChapterButton()
    {
        while (pooledSubChapterButtons.Count > 0)
        {
            int last = pooledSubChapterButtons.Count - 1;
            GameObject go = pooledSubChapterButtons[last];
            pooledSubChapterButtons.RemoveAt(last);
            if (go != null) return go;
        }
        return Instantiate(subChapter);
    }
    private IEnumerator SubToFull()
    {
        operating = true;
        StartCoroutine(SubCptShow(false));
        yield return new WaitForSeconds(0.25f);
        StartCoroutine(FullCptShow(true));
        operating = false;
    }
    public void SetBGMVolume()
    {
        float linear= PlayerPrefs.GetFloat(UXPref.BGM_PARAM, 0);
        float dB = linear <= 0 ? -80f : 20f * Mathf.Log10(linear);
        mixer.SetFloat(UXPref.BGM_PARAM, dB);
    }

    public void SetSEVolume()
    {
        float linear = PlayerPrefs.GetFloat(UXPref.SE_PARAM, 0);
        float dB = linear <= 0 ? -80f : 20f * Mathf.Log10(linear);
        mixer.SetFloat(UXPref.SE_PARAM, dB);
    }

    public void SetWelcomeBackMessage(string text)
    {
        if (welcomeBackText != null) welcomeBackText.text = text ?? string.Empty;
    }
}
