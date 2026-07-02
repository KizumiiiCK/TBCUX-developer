using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

public sealed class VirtualizedScrollGrid<TData>
{
    public sealed class Settings
    {
        public RectTransform Content;
        public ScrollRect ScrollRect;
        public GameObject ItemPrefab;
        public int Columns = 1;
        public float CellWidth = 200f;
        public float CellHeight = 200f;
        public int PreloadRows = 2;
        public bool DisableAutoLayout = true;
    }

    private readonly Settings settings;
    private readonly Action<GameObject, int, TData> bindItem;
    private readonly List<TData> items = new List<TData>();
    private readonly Dictionary<int, GameObject> activeItems = new Dictionary<int, GameObject>();
    private readonly Stack<GameObject> pooledItems = new Stack<GameObject>();

    private RectTransform viewport;
    private int lastStartIndex = -1;
    private int lastEndIndex = -1;
    private bool initialized;

    public VirtualizedScrollGrid(Settings settings, Action<GameObject, int, TData> bindItem)
    {
        this.settings = settings;
        this.bindItem = bindItem;
    }

    public void Initialize()
    {
        if (initialized) return;
        if (settings.Content == null || settings.ScrollRect == null || settings.ItemPrefab == null) return;

        // Ensure ScrollRect drives the same content used by this virtualized grid.
        if (settings.ScrollRect.content != settings.Content)
        {
            settings.ScrollRect.content = settings.Content;
        }

        viewport = settings.ScrollRect.viewport != null
            ? settings.ScrollRect.viewport
            : settings.ScrollRect.GetComponent<RectTransform>();

        if (settings.DisableAutoLayout)
        {
            var grid = settings.Content.GetComponent<GridLayoutGroup>();
            if (grid != null) grid.enabled = false;
            var vertical = settings.Content.GetComponent<VerticalLayoutGroup>();
            if (vertical != null) vertical.enabled = false;
            var horizontal = settings.Content.GetComponent<HorizontalLayoutGroup>();
            if (horizontal != null) horizontal.enabled = false;
            var fitter = settings.Content.GetComponent<ContentSizeFitter>();
            if (fitter != null) fitter.enabled = false;
        }

        settings.Content.anchorMin = new Vector2(0.5f, 1f);
        settings.Content.anchorMax = new Vector2(0.5f, 1f);
        settings.Content.pivot = new Vector2(0.5f, 1f);

        settings.ScrollRect.onValueChanged.AddListener(OnScrollChanged);
        initialized = true;
    }

    public void SetData(IList<TData> data, bool resetScrollPosition = true)
    {
        if (!initialized) Initialize();
        if (!initialized) return;

        items.Clear();
        if (data != null)
        {
            for (int i = 0; i < data.Count; i++) items.Add(data[i]);
        }

        lastStartIndex = -1;
        lastEndIndex = -1;
        RecycleAllActive();
        RefreshContentSize();

        if (resetScrollPosition)
        {
            settings.Content.anchoredPosition = new Vector2(settings.Content.anchoredPosition.x, 0f);
            Vector2 np = settings.ScrollRect.normalizedPosition;
            settings.ScrollRect.normalizedPosition = new Vector2(np.x, 1f);
        }

        RefreshVisible(true);
    }

    public void RefreshVisible(bool forceRefresh = false)
    {
        if (!initialized) return;
        if (items.Count == 0)
        {
            RecycleAllActive();
            return;
        }

        int columns = Mathf.Max(1, settings.Columns);
        float cellHeight = Mathf.Max(1f, settings.CellHeight);
        float viewportHeight = viewport != null ? viewport.rect.height : 0f;
        float viewHeight = Mathf.Max(viewportHeight, cellHeight);
        int rows = Mathf.CeilToInt(items.Count / (float)columns);
        float totalHeight = rows * cellHeight;
        float normalized = settings.ScrollRect != null ? settings.ScrollRect.verticalNormalizedPosition : 1f;
        if (float.IsNaN(normalized) || float.IsInfinity(normalized)) normalized = 1f;
        float scrollRatio = 1f - Mathf.Clamp01(normalized); // 1 at top, 0 at bottom
        float scrollY = scrollRatio * Mathf.Max(0f, totalHeight - viewHeight);

        int startRow = Mathf.FloorToInt(scrollY / cellHeight) - Mathf.Max(0, settings.PreloadRows);
        startRow = Mathf.Max(0, startRow);
        int visibleRows = Mathf.CeilToInt(viewHeight / cellHeight) + Mathf.Max(0, settings.PreloadRows) * 2 + 1;

        int startIndex = startRow * columns;
        int endIndex = Mathf.Min(items.Count - 1, (startRow + visibleRows) * columns - 1);

        if (!forceRefresh && startIndex == lastStartIndex && endIndex == lastEndIndex) return;
        lastStartIndex = startIndex;
        lastEndIndex = endIndex;

        var recycle = new List<int>();
        foreach (var pair in activeItems)
        {
            if (pair.Key < startIndex || pair.Key > endIndex) recycle.Add(pair.Key);
        }
        for (int i = 0; i < recycle.Count; i++)
        {
            int index = recycle[i];
            if (!activeItems.TryGetValue(index, out var go)) continue;
            activeItems.Remove(index);
            RecycleItem(go);
        }

        for (int i = startIndex; i <= endIndex; i++)
        {
            if (i < 0 || i >= items.Count) continue;
            if (activeItems.ContainsKey(i)) continue;
            var go = GetOrCreateItem();
            PositionItem(go, i);
            bindItem?.Invoke(go, i, items[i]);
            activeItems[i] = go;
        }
    }

    public void RebindVisible()
    {
        if (!initialized) return;
        foreach (var pair in activeItems)
        {
            int idx = pair.Key;
            if (idx < 0 || idx >= items.Count) continue;
            bindItem?.Invoke(pair.Value, idx, items[idx]);
        }
    }

    public void Dispose()
    {
        if (initialized && settings.ScrollRect != null)
        {
            settings.ScrollRect.onValueChanged.RemoveListener(OnScrollChanged);
        }
        initialized = false;

        foreach (var pair in activeItems)
        {
            if (pair.Value != null) UnityEngine.Object.Destroy(pair.Value);
        }
        activeItems.Clear();

        while (pooledItems.Count > 0)
        {
            var go = pooledItems.Pop();
            if (go != null) UnityEngine.Object.Destroy(go);
        }
    }

    private void OnScrollChanged(Vector2 _)
    {
        RefreshVisible(false);
    }

    private void RefreshContentSize()
    {
        int columns = Mathf.Max(1, settings.Columns);
        int rows = Mathf.CeilToInt(items.Count / (float)columns);
        float height = Mathf.Max(settings.CellHeight, rows * Mathf.Max(1f, settings.CellHeight));
        if (viewport != null) height = Mathf.Max(height, viewport.rect.height);

        Vector2 size = settings.Content.sizeDelta;
        settings.Content.sizeDelta = new Vector2(size.x, height);
    }

    private GameObject GetOrCreateItem()
    {
        if (pooledItems.Count > 0)
        {
            var go = pooledItems.Pop();
            go.SetActive(true);
            return go;
        }

        var created = UnityEngine.Object.Instantiate(settings.ItemPrefab, settings.Content);
        created.transform.localScale = Vector3.one;
        return created;
    }

    private void RecycleItem(GameObject go)
    {
        if (go == null) return;
        go.SetActive(false);
        pooledItems.Push(go);
    }

    private void RecycleAllActive()
    {
        foreach (var pair in activeItems)
        {
            RecycleItem(pair.Value);
        }
        activeItems.Clear();
    }

    private void PositionItem(GameObject item, int index)
    {
        var rt = item.GetComponent<RectTransform>();
        if (rt == null) return;

        int columns = Mathf.Max(1, settings.Columns);
        int row = index / columns;
        int col = index % columns;

        float x = (col - (columns - 1) * 0.5f) * settings.CellWidth;
        float y = -(row * settings.CellHeight + settings.CellHeight * 0.5f);

        rt.SetParent(settings.Content, false);
        rt.localScale = Vector3.one;
        rt.anchorMin = new Vector2(0.5f, 1f);
        rt.anchorMax = new Vector2(0.5f, 1f);
        rt.pivot = new Vector2(0.5f, 0.5f);
        rt.anchoredPosition = new Vector2(x, y);
    }
}
