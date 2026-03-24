using System.Collections;
using System.Collections.Generic;
using TMPro;
using UnityEngine;

public class SectionCanvas : UICanvasMain
{
    [Header("Section UI")]
    [SerializeField] private RectTransform sectionRoot;
    [SerializeField] private Transform sectionsContent;
    [SerializeField] private GameObject sectionItemPrefab;
    [SerializeField] private TMP_Text chapterName;
    [SerializeField] private float transitionDuration = 0.5f;
    [SerializeField] private float itemStartX = 350f;
    [SerializeField] private float itemStartY = -125f;
    [SerializeField] private float itemSpacingY = 150f;
    [SerializeField] private float itemScale = 1.5f;

    private readonly List<GameObject> spawnedSectionItems = new List<GameObject>();
    private Vector2 canvasSize;
    private bool initialized;

    public override void Initialize(FrameUIDisplayer frameUI)
    {
        base.Initialize(frameUI);
        ResolveReferences();
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
        if (sectionsContent == null && sectionRoot != null && sectionRoot.childCount > 0)
        {
            Transform scrollRoot = sectionRoot.GetChild(0);
            if (scrollRoot != null && scrollRoot.childCount > 0) sectionsContent = scrollRoot.GetChild(0);
        }
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
        if (initialized) return;
        initialized = true;

        if (sectionsContent == null || sectionItemPrefab == null)
        {
            Debug.LogWarning("[SectionCanvas] Missing sectionsContent or sectionItemPrefab.");
            return;
        }

        for (int i = 0; i < spawnedSectionItems.Count; i++)
        {
            if (spawnedSectionItems[i] != null) Destroy(spawnedSectionItems[i]);
        }
        spawnedSectionItems.Clear();

        string worldName = PlayerPrefs.GetString(UXPref.ChapterName);
        if (chapterName != null)
        {
            LocalizationHelper.GetLocalizedText(UXPref.Localized_CS, worldName,
                localizedText => chapterName.text = localizedText ?? worldName);
        }

        string worldPath = $"LevelData/Chapters/{worldName}";
        MapInfo[] sections = Resources.LoadAll<MapInfo>(worldPath);
        Transform scrollbarHost = sectionsContent.parent != null ? sectionsContent.parent : sectionsContent;
        if (scrollbarHost.parent != null) scrollbarHost = scrollbarHost.parent;
        CustomScrollbar customScrollbar = scrollbarHost.GetComponent<CustomScrollbar>();

        int sectionNum = 0;
        for (int i = 0; i < sections.Length; i++)
        {
            GameObject item = Instantiate(sectionItemPrefab, sectionsContent);
            spawnedSectionItems.Add(item);

            SectionButton sectionButton = item.GetComponent<SectionButton>();
            if (sectionButton != null)
            {
                sectionButton.mapinfo = sections[i];
                sectionButton.section_order = i;
            }

            LocalizationHelper.GetLocalizedText(UXPref.Localized_CS, sections[i].sectionName,
                localizedText => item.name = localizedText ?? sections[i].sectionName);

            item.transform.localScale = Vector3.one * itemScale;
            RectTransform itemRect = item.GetComponent<RectTransform>();
            if (itemRect != null)
            {
                itemRect.anchoredPosition = new Vector2(itemStartX, itemStartY - sectionNum * itemSpacingY);
            }

            sectionNum++;
            if (customScrollbar != null) customScrollbar.SetMaxY(sectionNum, (int)itemSpacingY);
        }
    }
}
