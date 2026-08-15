using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class IndexViewerPause : MonoBehaviour
{
    private const string CatDataPathFormat = "Units/Cat Units/{0}/{1}/{2}/data";
    private const string EnemyDataPathFormat = "Units/Enemy Units/{0}/data";
    private const string CatIconPathFormat = "Units/Cat Units/{0}/{1}/{2}/icon_deploy";
    private const string EnemyIconPathFormat = "Units/Enemy Units/{0}/enemy_icon";

    [Header("Basic Info")]
    [SerializeField] private Image portraitImage;
    [SerializeField] private TMP_Text nameText;
    [SerializeField] private TMP_Text healthText;
    [SerializeField] private Image healthFillImage;

    [Header("KB Split Marks")]
    [SerializeField] private RectTransform kbMarkerRoot;
    [SerializeField] private Image kbMarkerTemplate;
    [SerializeField] private float kbMarkerHeight = 14f;
    [SerializeField] private float kbMarkerWidth = 2f;
    [SerializeField] private Color kbMarkerColor = new Color(1f, 1f, 1f, 0.85f);

    [Header("Embedded Viewer")]
    [SerializeField] private IndexViewer indexViewer;

    private readonly List<Image> kbMarkersPool = new List<Image>();
    private readonly Dictionary<string, CharacterData> characterDataCache = new Dictionary<string, CharacterData>();
    private readonly Dictionary<string, Sprite> portraitCache = new Dictionary<string, Sprite>();
    private Character currentCharacter;
    private CharacterData currentData;
    private int nameRequestToken;

    private void Awake()
    {
        TryAutoAssignReferences();
        InitializeMarkerTemplate();
    }

    private void Update()
    {
        if (currentCharacter == null) return;
        if (currentCharacter.gameObject == null || !currentCharacter.gameObject.activeInHierarchy)
        {
            HidePanel();
            return;
        }

        RefreshHealthOnly();
    }

    public void ShowCharacter(Character character)
    {
        if (character == null)
        {
            HidePanel();
            return;
        }

        currentCharacter = character;
        bool isCat = character is CatCharacter;
        string code = character.NameCode;
        if (string.IsNullOrEmpty(code))
        {
            HidePanel();
            return;
        }

        currentData = LoadCharacterData(code, isCat);
        if (currentData == null)
        {
            HidePanel();
            return;
        }

        gameObject.SetActive(true);
        RefreshPortrait(code, isCat);
        RefreshLocalizedName(code);
        RefreshHealthOnly();
        RefreshKbMarkers(Mathf.Max(0, character.KB));

        if (indexViewer != null)
        {
            indexViewer.ShowCharacterDetails(currentData, false, 1);
        }
    }

    public void HidePanel()
    {
        currentCharacter = null;
        currentData = null;
        HideAllKbMarkers();
        if (gameObject.activeSelf) gameObject.SetActive(false);
    }

    private void RefreshPortrait(string code, bool isCat)
    {
        if (portraitImage == null) return;

        Sprite portrait = LoadPortrait(code, isCat);
        if (portrait != null) portraitImage.sprite = portrait;

        RectTransform rt = portraitImage.rectTransform;
        if (rt != null)
        {
            rt.sizeDelta = isCat ? new Vector2(128f, 100f) : new Vector2(128f, 128f);
        }
    }

    private void RefreshLocalizedName(string code)
    {
        if (nameText == null)
        {
            return;
        }

        nameRequestToken++;
        int token = nameRequestToken;
        nameText.text = code;
        LocalizationHelper.GetLocalizedText(UXPref.Localized_UnitNames, code, localizedText =>
        {
            if (token != nameRequestToken || nameText == null) return;
            nameText.text = string.IsNullOrEmpty(localizedText) ? code : localizedText;
        });
    }

    private void RefreshHealthOnly()
    {
        if (currentCharacter == null) return;

        float hp = Mathf.Max(0f, currentCharacter.GetHealth());
        float maxHp = Mathf.Max(1f, currentCharacter.GetMaxHealth());
        if (healthText != null)
        {
            healthText.text = $"{Mathf.CeilToInt(hp)} / {Mathf.CeilToInt(maxHp)}";
        }

        if (healthFillImage != null)
        {
            healthFillImage.fillAmount = Mathf.Clamp01(hp / maxHp);
        }
    }

    private void RefreshKbMarkers(int kbCount)
    {
        if (kbMarkerRoot == null || kbCount < 2 || kbCount > 25)
        {
            HideAllKbMarkers();
            return;
        }

        int markerCount = kbCount - 1;
        EnsureMarkerPool(markerCount);
        float width = kbMarkerRoot.rect.width;
        if (width <= 0f && healthFillImage != null) width = healthFillImage.rectTransform.rect.width;
        if (width <= 0f) width = 200f;

        for (int i = 0; i < kbMarkersPool.Count; i++)
        {
            Image marker = kbMarkersPool[i];
            if (marker == null) continue;

            bool active = i < markerCount;
            marker.gameObject.SetActive(active);
            if (!active) continue;

            RectTransform rt = marker.rectTransform;
            rt.anchorMin = new Vector2(0f, 0.5f);
            rt.anchorMax = new Vector2(0f, 0.5f);
            rt.pivot = new Vector2(0.5f, 0.5f);
            float x = width * (i + 1) / kbCount;
            rt.anchoredPosition = new Vector2(x, 0f);
            rt.sizeDelta = new Vector2(kbMarkerWidth, kbMarkerHeight);
            marker.color = kbMarkerColor;
        }
    }

    private void HideAllKbMarkers()
    {
        for (int i = 0; i < kbMarkersPool.Count; i++)
        {
            if (kbMarkersPool[i] != null) kbMarkersPool[i].gameObject.SetActive(false);
        }
    }

    private void EnsureMarkerPool(int count)
    {
        InitializeMarkerTemplate();
        if (kbMarkerTemplate == null || kbMarkerRoot == null) return;

        while (kbMarkersPool.Count < count)
        {
            Image marker = Instantiate(kbMarkerTemplate, kbMarkerRoot, false);
            marker.gameObject.SetActive(false);
            kbMarkersPool.Add(marker);
        }
    }

    private void InitializeMarkerTemplate()
    {
        if (kbMarkerRoot == null && healthFillImage != null)
        {
            kbMarkerRoot = healthFillImage.rectTransform;
        }

        if (kbMarkerTemplate != null) return;
        if (kbMarkerRoot == null) return;

        GameObject templateGo = new GameObject("KBMarkerTemplate");
        templateGo.transform.SetParent(kbMarkerRoot, false);
        kbMarkerTemplate = templateGo.AddComponent<Image>();
        kbMarkerTemplate.color = kbMarkerColor;
        kbMarkerTemplate.raycastTarget = false;
        kbMarkerTemplate.gameObject.SetActive(false);
    }

    private CharacterData LoadCharacterData(string code, bool isCat)
    {
        string cacheKey = (isCat ? "cat:" : "enemy:") + code;
        if (characterDataCache.TryGetValue(cacheKey, out CharacterData cachedData) && cachedData != null)
        {
            return cachedData;
        }

        CharacterData data = null;
        if (isCat && code.Length >= 5)
        {
            string path = string.Format(CatDataPathFormat, code[0], code.Substring(1, 3), code[4]);
            data = BundledAddressables.LoadSync<CharacterData>(path);
        }
        else if (!isCat)
        {
            string path = string.Format(EnemyDataPathFormat, code);
            data = BundledAddressables.LoadSync<CharacterData>(path);
        }

        characterDataCache[cacheKey] = data;
        return data;
    }

    private Sprite LoadPortrait(string code, bool isCat)
    {
        string cacheKey = (isCat ? "cat:" : "enemy:") + code;
        if (portraitCache.TryGetValue(cacheKey, out Sprite cached) && cached != null)
        {
            return cached;
        }

        Sprite portrait = null;
        if (isCat && code.Length >= 5)
        {
            string path = string.Format(CatIconPathFormat, code[0], code.Substring(1, 3), code[4]);
            portrait = BundledAddressables.LoadSync<Sprite>(path);
        }
        else if (!isCat)
        {
            string path = string.Format(EnemyIconPathFormat, code);
            portrait = BundledAddressables.LoadSync<Sprite>(path);
        }

        portraitCache[cacheKey] = portrait;
        return portrait;
    }

    private void TryAutoAssignReferences()
    {
        if (indexViewer == null) indexViewer = GetComponentInChildren<IndexViewer>(true);
        if (portraitImage == null || healthFillImage == null)
        {
            Image[] images = GetComponentsInChildren<Image>(true);
            for (int i = 0; i < images.Length; i++)
            {
                Image img = images[i];
                if (img == null) continue;
                string n = img.gameObject.name.ToLowerInvariant();
                if (portraitImage == null && (n.Contains("portrait") || n.Contains("avatar") || n.Contains("head")))
                {
                    portraitImage = img;
                    continue;
                }
                if (healthFillImage == null && n.Contains("fill"))
                {
                    healthFillImage = img;
                }
            }
        }

        if (nameText == null || healthText == null)
        {
            TMP_Text[] texts = GetComponentsInChildren<TMP_Text>(true);
            for (int i = 0; i < texts.Length; i++)
            {
                TMP_Text t = texts[i];
                if (t == null) continue;
                string n = t.gameObject.name.ToLowerInvariant();
                if (nameText == null && n.Contains("name"))
                {
                    nameText = t;
                    continue;
                }
                if (healthText == null && (n.Contains("hp") || n.Contains("health")))
                {
                    healthText = t;
                }
            }
        }

        if (kbMarkerRoot == null && healthFillImage != null)
        {
            kbMarkerRoot = healthFillImage.rectTransform;
        }
    }
}
