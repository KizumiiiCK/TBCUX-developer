using System.Collections;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class SectionCanvas : UICanvasMain
{
    private const string DailyRemarkPrefabPath = "UI/FunctionalPanels/sectionDailyRemark";
    private const string DailyRemarkNodeName = "__SectionDailyRemark";
    private const string DailyTextId = "id:daily";
    private const string DailyClearedTextId = "id:daily_cleared";

    [Header("Section UI")]
    [SerializeField] private RectTransform sectionRoot;
    [SerializeField] private RectTransform sectionContent;
    [SerializeField] private ScrollRect sectionScrollRect;
    [SerializeField] private GameObject sectionItemPrefab;
    [SerializeField] private TMP_Text chapterName;
    [SerializeField] private float transitionDuration = 0.5f;
    [Header("Section List Layout")]
    [SerializeField] private float sectionItemXOffset = -1100f;
    [SerializeField] private float sectionItemSpacing = 15f;
    [SerializeField] private float sectionItemCellHeight = 30f;
    [SerializeField] private int sectionPreloadRows = 10;

    // Key = chapter folder name (PlayerPrefs ChapterName). Value = sectionName load/display order.
    private static readonly Dictionary<string, string[]> SectionLoadOrder = new Dictionary<string, string[]>
    {
        ["World_I"] = new[]
        {
            "0_worldi",
        },
        ["World_II"] = new[]
        {
            "0_worldii",
        },
        ["World_III"] = new[]
        {
            "0_worldiii",
            "1_iii_mouse",
        },
        ["Future_I"] = new[]
        {
            "0_futurei",
            "1_f1_m3",
            "2_f1_cm",
        },
        ["LEGEND"] = new[]
        {
            "0_L",
            "1_L",
            "2_L",
            "3_L",
            "4_L",
            "5_L",
            "6_L",
            "7_L",
            "8_L",
            "9_L",
            "10_L",
            "11_L",
        },
        ["Dream_Pre"] = new[]
        {
            "0_d00",
            "0_d01",
            "0_d02",
        },
        ["Dungeon"] = new[]
        {
            "0_greeting",
            "0_gret1",
            "1_a0_xpm",
            "1_c0_red",
            "1_c1_purple",
            "1_c2_green",
            "1_c3_yellow",
            "1_c4_blue",
            "1_m0_mnk",
            "1_m1_mnk",
            "3_b1_forest",
            "3_b2_desert",
            "3_b3_volcano",
        },
        ["Challenge"] = new[]
        {
            "0_boss0",
        }
    };

    private readonly List<SectionEntry> sectionEntries = new List<SectionEntry>();
    private Vector2 canvasSize;
    private VirtualizedScrollGrid<SectionEntry> sectionGrid;

    public override void Initialize(FrameUIDisplayer frameUI)
    {
        base.Initialize(frameUI);
        ResolveReferences();
        InitializeVirtualizedSectionList();
        InitializeSections();
        PrepareOffscreenPosition();
    }

    public override IEnumerator OnEnter()
    {
        if (sectionRoot == null) yield break;

        ResolveCanvasSize();
        float swapDistance = canvasSize.x / 2f;
        float t = 0f;
        sectionRoot.gameObject.SetActive(true);
        sectionRoot.anchoredPosition = new Vector2(-canvasSize.x, 0f);

        while (t < transitionDuration)
        {
            t += Time.deltaTime;
            sectionRoot.anchoredPosition = new Vector2(
                -swapDistance * (transitionDuration - t) * (transitionDuration - t) / (transitionDuration * transitionDuration) - canvasSize.x / 2f,
                0f
            );
            yield return new WaitForFixedUpdate();
        }

        sectionRoot.anchoredPosition = new Vector2(-canvasSize.x / 2f, 0f);
    }

    public override IEnumerator OnExit()
    {
        if (sectionRoot == null) yield break;

        ResolveCanvasSize();
        float swapDistance = canvasSize.x / 2f;
        float t = 0f;

        while (t < transitionDuration)
        {
            t += Time.deltaTime;
            sectionRoot.anchoredPosition = new Vector2(
                swapDistance * (transitionDuration - t) * (transitionDuration - t) / (transitionDuration * transitionDuration) - canvasSize.x,
                0f
            );
            yield return new WaitForFixedUpdate();
        }

        sectionRoot.anchoredPosition = new Vector2(-canvasSize.x, 0f);
        sectionRoot.gameObject.SetActive(false);
    }

    private void ResolveReferences()
    {
        if (sectionRoot == null)
        {
            Transform leftPinned = transform.Find("LeftPinned");
            if (leftPinned != null) sectionRoot = leftPinned as RectTransform;
            if (sectionRoot == null) sectionRoot = GetComponent<RectTransform>();
        }
        if (sectionContent == null && sectionRoot != null)
        {
            if (sectionScrollRect == null) sectionScrollRect = sectionRoot.GetComponentInChildren<ScrollRect>(true);
            if (sectionScrollRect != null) sectionContent = sectionScrollRect.content;
        }
        if (sectionScrollRect == null && sectionContent != null)
            sectionScrollRect = sectionContent.GetComponentInParent<ScrollRect>();
        if (chapterName == null && sectionRoot != null)
        {
            chapterName = sectionRoot.GetComponentInChildren<TMP_Text>(true);
        }
        if (sectionItemPrefab == null)
        {
            sectionItemPrefab = Resources.Load<GameObject>("UI/Buttons/Section");
        }

        ResolveCanvasSize();
    }

    private void ResolveCanvasSize()
    {
        RectTransform canvasRect = GetComponent<RectTransform>();
        if (canvasRect != null && canvasRect.sizeDelta.x > 0f)
        {
            canvasSize = canvasRect.sizeDelta;
        }
        else if (sectionRoot != null && sectionRoot.sizeDelta.x > 0f)
        {
            canvasSize = sectionRoot.sizeDelta;
        }
        else
        {
            canvasSize = new Vector2(Screen.width, Screen.height);
        }
    }

    private void PrepareOffscreenPosition()
    {
        if (sectionRoot == null) return;
        sectionRoot.anchoredPosition = new Vector2(-canvasSize.x, 0f);
    }

    private void InitializeSections()
    {
        if (sectionContent == null || sectionItemPrefab == null)
        {
            Debug.LogWarning("[SectionCanvas] Missing sectionContent or sectionItemPrefab.");
            return;
        }

        sectionEntries.Clear();

        string worldName = PlayerPrefs.GetString(UXPref.ChapterName);
        if (chapterName != null)
        {
            LocalizationHelper.GetLocalizedText(UXPref.Localized_CS, worldName,
                localizedText => chapterName.text = localizedText ?? worldName);
        }

        if (!SectionLoadOrder.TryGetValue(worldName, out string[] order) || order == null || order.Length == 0)
        {
            Debug.LogWarning($"[SectionCanvas] No section load order for chapter '{worldName}'.");
            sectionGrid?.SetData(sectionEntries, true);
            return;
        }

        string worldPath = $"LevelData/Chapters/{worldName}";
        for (int i = 0; i < order.Length; i++)
        {
            string sectionName = order[i];
            if (string.IsNullOrEmpty(sectionName)) continue;

            MapInfo mapInfo = Resources.Load<MapInfo>($"{worldPath}/{sectionName}");
            if (mapInfo == null)
            {
                Debug.LogWarning($"[SectionCanvas] Missing MapInfo '{worldPath}/{sectionName}'.");
                continue;
            }

            sectionEntries.Add(new SectionEntry { mapInfo = mapInfo, sectionOrder = sectionEntries.Count });
        }

        sectionGrid?.SetData(sectionEntries, true);
    }

    private void InitializeVirtualizedSectionList()
    {
        if (sectionContent == null || sectionItemPrefab == null) return;
        if (sectionScrollRect == null) sectionScrollRect = sectionContent.GetComponentInParent<ScrollRect>();
        if (sectionScrollRect == null) return;

        float baseItemHeight = sectionItemCellHeight;
        RectTransform prefabRect = sectionItemPrefab.GetComponent<RectTransform>();
        if (prefabRect != null && prefabRect.sizeDelta.y > 1f)
        {
            baseItemHeight = prefabRect.sizeDelta.y;
        }
        float itemHeight = baseItemHeight + Mathf.Max(0f, sectionItemSpacing);

        sectionGrid = new VirtualizedScrollGrid<SectionEntry>(
            new VirtualizedScrollGrid<SectionEntry>.Settings
            {
                Content = sectionContent,
                ScrollRect = sectionScrollRect,
                ItemPrefab = sectionItemPrefab,
                Columns = 1,
                CellWidth = 450,
                CellHeight = Mathf.Max(1f, itemHeight),
                PreloadRows = Mathf.Max(0, sectionPreloadRows),
                DisableAutoLayout = true
            },
            BindSectionItem
        );
        sectionGrid.Initialize();
    }

    private void BindSectionItem(GameObject itemGO, int _, SectionEntry entry)
    {
        if (itemGO == null || entry.mapInfo == null) return;

        RectTransform itemRect = itemGO.GetComponent<RectTransform>();
        if (itemRect != null)
        {
            itemRect.anchoredPosition = new Vector2(-350, itemRect.anchoredPosition.y);
        }

        var sectionButton = itemGO.GetComponent<SectionButton>();
        if (sectionButton != null) sectionButton.Configure(entry.mapInfo, entry.sectionOrder);
        RefreshDailyRemark(itemGO.transform, entry.mapInfo);

        LocalizationHelper.GetLocalizedText(UXPref.Localized_CS, entry.mapInfo.sectionName,
            localizedText => itemGO.name = localizedText ?? entry.mapInfo.sectionName);
    }

    private void RefreshDailyRemark(Transform sectionItemRoot, MapInfo mapInfo)
    {
        if (sectionItemRoot == null) return;

        Transform existing = sectionItemRoot.Find(DailyRemarkNodeName);
        if (mapInfo == null || !mapInfo.oncePerDay)
        {
            if (existing != null) existing.gameObject.SetActive(false);
            return;
        }

        GameObject remarkGO = existing != null ? existing.gameObject : CreateDailyRemark(sectionItemRoot);
        if (remarkGO == null) return;
        remarkGO.SetActive(true);

        Transform textRoot = remarkGO.transform.childCount > 0 ? remarkGO.transform.GetChild(0) : null;
        TMP_Text remarkText = textRoot != null ? textRoot.GetComponent<TMP_Text>() : null;
        if (remarkText == null) return;

        bool clearedToday = DailyMapChallengeSave.HasSectionClearRecordToday(
            CheckInSystem.GetCachedWorldDateToken(),
            mapInfo.sectionName);
        string textId = clearedToday ? DailyClearedTextId : DailyTextId;
        LocalizationHelper.GetLocalizedText(UXPref.Localized_UI, textId,
            localizedText => remarkText.text = localizedText ?? textId);
    }

    private GameObject CreateDailyRemark(Transform parent)
    {
        GameObject prefab = Resources.Load<GameObject>(DailyRemarkPrefabPath);
        if (prefab == null) return null;

        GameObject instance = Instantiate(prefab, parent);
        instance.name = DailyRemarkNodeName;
        RectTransform rect = instance.GetComponent<RectTransform>();
        if (rect != null)
        {
            rect.localScale = Vector3.one;
            rect.anchoredPosition3D = Vector3.zero;
        }
        return instance;
    }

    private void OnDestroy()
    {
        if (sectionGrid != null)
        {
            sectionGrid.Dispose();
            sectionGrid = null;
        }
    }

    private struct SectionEntry
    {
        public MapInfo mapInfo;
        public int sectionOrder;
    }
}
