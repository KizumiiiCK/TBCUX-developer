using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using UnityEngine;
using UnityEngine.UI;

public class BontiqueCanvas : UICanvasMain
{
    [Header("UI Refs")]
    [SerializeField] private Transform categoryButtonsRoot;
    [SerializeField] private GameObject categoryButtonPrefab; // expects a Button
    [SerializeField] private RectTransform itemsContent;
    [SerializeField] private ScrollRect itemsScrollRect;
    [SerializeField] private GameObject itemPrefab; // BontiqueItems prefab

    [Header("Grid Settings")]
    [SerializeField] private int columns = 1;
    [SerializeField] private float cellWidth = 300f;
    [SerializeField] private float cellHeight = 200f;

    private List<BontiqueShopItem> shopItems = new List<BontiqueShopItem>();
    private VirtualizedScrollGrid<BontiqueShopItem> grid;
    private BontiqueType currentCategory = BontiqueType.Type0;

    private void Start()
    {
        InitializeCategories();
        LoadShopFromCsv("Shop/boutique");
        InitializeGrid();
        ShowCategory(currentCategory);
    }

    private void InitializeCategories()
    {
        if (categoryButtonsRoot == null || categoryButtonPrefab == null) return;
        // Clear existing
        for (int i = categoryButtonsRoot.childCount - 1; i >= 0; i--) DestroyImmediate(categoryButtonsRoot.GetChild(i).gameObject);
        // Example: create 4 categories based on enum values (excluding Unknown)
        foreach (BontiqueType t in Enum.GetValues(typeof(BontiqueType)))
        {
            if (t == BontiqueType.Unknown) continue;
            var go = Instantiate(categoryButtonPrefab, categoryButtonsRoot);
            var btn = go.GetComponent<Button>();
            var txt = go.GetComponentInChildren<TMPro.TMP_Text>(true);
            int idx = (int)t;
            if (txt != null) txt.text = t.ToString();
            if (btn != null)
            {
                btn.onClick.RemoveAllListeners();
                btn.onClick.AddListener(() => OnCategoryClicked(idx));
            }
        }
    }

    private void OnCategoryClicked(int idx)
    {
        currentCategory = Enum.IsDefined(typeof(BontiqueType), idx) ? (BontiqueType)idx : BontiqueType.Unknown;
        ShowCategory(currentCategory);
    }

    private void LoadShopFromCsv(string resourcePath)
    {
        shopItems.Clear();
        var ta = Resources.Load<TextAsset>(resourcePath);
        if (ta == null)
        {
            Debug.LogError($"BontiqueCanvas: missing csv at Resources/{resourcePath}");
            return;
        }
        using (StringReader sr = new StringReader(ta.text))
        {
            string line;
            while ((line = sr.ReadLine()) != null)
            {
                if (string.IsNullOrWhiteSpace(line)) continue;
                // split by comma
                var cols = line.Split(',');
                var item = BontiqueShopItem.FromCsvRow(cols);
                shopItems.Add(item);
            }
        }
    }

    private void InitializeGrid()
    {
        if (itemsContent == null || itemPrefab == null || itemsScrollRect == null) return;
        grid = new VirtualizedScrollGrid<BontiqueShopItem>(
            new VirtualizedScrollGrid<BontiqueShopItem>.Settings
            {
                Content = itemsContent,
                ScrollRect = itemsScrollRect,
                ItemPrefab = itemPrefab,
                Columns = Mathf.Max(1, columns),
                CellWidth = Mathf.Max(1f, cellWidth),
                CellHeight = Mathf.Max(1f, cellHeight),
                PreloadRows = 1,
                DisableAutoLayout = true
            },
            BindItem
        );
        grid.Initialize();
    }

    private void BindItem(GameObject go, int _, BontiqueShopItem item)
    {
        if (go == null) return;
        var controller = go.GetComponent<BontiqueItems>();
        if (controller == null) controller = go.AddComponent<BontiqueItems>();
        controller.Configure(item);
    }

    private void ShowCategory(BontiqueType t)
    {
        var filtered = shopItems.FindAll(x => x.Category == t);
        if (grid == null) InitializeGrid();
        grid?.SetData(filtered, true);
    }

    public override IEnumerator OnEnter()
    {
        if (FrameUI != null) FrameUI.OpenDoor();
        // wait door duration if available
        yield return new WaitForSecondsRealtime(FrameUIAnimations.DoorDuration);
    }

    public override IEnumerator OnExit()
    {
        if (FrameUI != null) FrameUI.CloseDoor();
        yield return new WaitForSecondsRealtime(FrameUIAnimations.DoorDuration);
    }
}
