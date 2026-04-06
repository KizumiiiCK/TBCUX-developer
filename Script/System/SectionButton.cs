using System.Collections;
using System.Collections.Generic;
using System.Threading.Tasks;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
using static Unity.Collections.AllocatorManager;

public class SectionButton : MonoBehaviour
{
    public MapInfo mapinfo;
    public int section_order = 0;
    [SerializeField] private GameObject DB;
    [SerializeField] private Transform HeaderName;
    [SerializeField] private Transform Stars;
    [SerializeField] private Transform Crowns;
    [SerializeField] private Transform LockStatement;
    [SerializeField] private Transform StateBox;
    [SerializeField] private Sprite marked_star;
    [SerializeField] private Sprite marked_crown;
    [SerializeField] private Button detailsBtn;
    [SerializeField] private TMP_Text statement;
    private readonly List<Sprite> defaultStarSprites = new List<Sprite>();
    private readonly List<Sprite> defaultCrownSprites = new List<Sprite>();
    private readonly List<Color> defaultCrownColors = new List<Color>();
    private Color defaultHeaderColor = Color.white;
    private bool defaultsCached;
    private void Start()
    {
        if (mapinfo != null) Configure(mapinfo, section_order);
    }
    public void Configure(MapInfo mi, int order)
    {
        mapinfo = mi;
        section_order = order;
        InitializeSection(mapinfo);
    }
    public void CallDB() { 
        PlayerPrefs.SetString(UXPref.SectionName, mapinfo.sectionName); 
        PlayerPrefs.SetInt(UXPref.SectionNum, section_order); 
        Instantiate(DB).GetComponent<DifficultyBoard>().SetMapInfo(mapinfo); 
    }

    private void InitializeSection(MapInfo mi)
    {
        if (mi == null) return;
        CacheDefaultsIfNeeded();
        ResetVisualsToDefault();

        Image s0 = GetComponent<Image>();
        TMP_Text lvname = HeaderName.GetComponent<TMP_Text>();
        s0.color = TitleColorMap.sectionMapping[mi.titleColor];
        lvname.text = mi.sectionName;
        for (int i = 0; i < 12; i++) if (mi.difficulty[i]) { Image star = Stars.GetChild(i).GetComponent<Image>(); star.sprite = marked_star; }
        for (int i = 0; i < 5; i++) if (mi.hardness < i + 1) { Image crown = Crowns.GetChild(i).GetComponent<Image>(); crown.color = new Color(0, 0, 0, 0); }

        //Check if unlocked
        if (mi != null && !string.IsNullOrEmpty(mi.unlockRestriction))
        {
            string[] s = mi.unlockRestriction.Split('+');
            try
            {
                string cpt = s[0];
                string sec = s[1];
                GameProgressSave.ChapterClearList CCL = GameProgressSave.LoadChapterProgress(cpt);
                bool unlock = false;
                foreach (var sections in CCL.SectionList)
                {
                    if (sections.SectionName == sec)
                        if (sections.cleared) { unlock = true; break; }
                }
                GetComponent<Button>().interactable = unlock;
                LockStatement.gameObject.SetActive(!unlock);
                if (!unlock)
                {
                    GetRestrictionText(mapinfo.sectionName, cpt, sec);
                }
            }
            catch
            {
                GetComponent<Button>().interactable = false;
                LockStatement.gameObject.SetActive(true);
                statement.text = "???";
            }
        }
        else { GetComponent<Button>().interactable = true; LockStatement.gameObject.SetActive(false); }
        StateBox.gameObject.SetActive(false);

        //Check cleared
        GameProgressSave.SectionClearList SCL = GameProgressSave.LoadSectionProgress(PlayerPrefs.GetString(UXPref.ChapterName), mapinfo.sectionName);
        for (int i = 0; i < mi.hardness; i++)
        {
            if (SCL.clear_times[i, SCL.clear_times.GetLength(1) - 1] > 0)
            {
                Image crown = Crowns.GetChild(i).GetComponent<Image>();
                crown.sprite = marked_crown;
            }
        }

        //set name
        TMP_Text sectionName = HeaderName.GetComponent<TMP_Text>();
        LocalizationHelper.GetLocalizedText(UXPref.Localized_CS, mi.sectionName,
                localizedText => sectionName.text = localizedText ?? mi.sectionName);

    }
    private void CacheDefaultsIfNeeded()
    {
        if (defaultsCached) return;
        defaultsCached = true;

        if (HeaderName != null)
        {
            TMP_Text sectionName = HeaderName.GetComponent<TMP_Text>();
            if (sectionName != null) defaultHeaderColor = sectionName.color;
        }

        defaultStarSprites.Clear();
        if (Stars != null)
        {
            for (int i = 0; i < Stars.childCount; i++)
            {
                Image star = Stars.GetChild(i).GetComponent<Image>();
                defaultStarSprites.Add(star != null ? star.sprite : null);
            }
        }

        defaultCrownSprites.Clear();
        defaultCrownColors.Clear();
        if (Crowns != null)
        {
            for (int i = 0; i < Crowns.childCount; i++)
            {
                Image crown = Crowns.GetChild(i).GetComponent<Image>();
                defaultCrownSprites.Add(crown != null ? crown.sprite : null);
                defaultCrownColors.Add(crown != null ? crown.color : Color.white);
            }
        }
    }

    private void ResetVisualsToDefault()
    {
        if (HeaderName != null)
        {
            TMP_Text sectionName = HeaderName.GetComponent<TMP_Text>();
            if (sectionName != null) sectionName.color = defaultHeaderColor;
        }

        if (Stars != null)
        {
            for (int i = 0; i < Stars.childCount; i++)
            {
                Image star = Stars.GetChild(i).GetComponent<Image>();
                if (star != null && i < defaultStarSprites.Count) star.sprite = defaultStarSprites[i];
            }
        }

        if (Crowns != null)
        {
            for (int i = 0; i < Crowns.childCount; i++)
            {
                Image crown = Crowns.GetChild(i).GetComponent<Image>();
                if (crown == null) continue;
                if (i < defaultCrownSprites.Count) crown.sprite = defaultCrownSprites[i];
                if (i < defaultCrownColors.Count) crown.color = defaultCrownColors[i];
            }
        }
        if (statement != null) statement.text = string.Empty;
    }
    public void ShowRestrictionDetail()
    {
        StateBox.gameObject.SetActive(!StateBox.gameObject.activeSelf);
    }
    async void GetRestrictionText(string thisstName, string cptName, string stName)
    {
        string mainText = await GetLocalizedText(UXPref.Localized_UI, "id:cplockstate");
        string param1 = await GetLocalizedText(UXPref.Localized_CS, thisstName);
        string param2 = await GetLocalizedText(UXPref.Localized_CS, cptName);
        string param3 = await GetLocalizedText(UXPref.Localized_CS, stName);

        string formattedText = string.Format(mainText, param1, param2, param3);
        statement.text = formattedText;
    }
    public async Task<string> GetLocalizedText(string tableName, string id)
    {
        var tcs = new TaskCompletionSource<string>();
        LocalizationHelper.GetLocalizedText(tableName, id, tcs.SetResult);
        return await tcs.Task;
    }
}
