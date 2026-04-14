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
    private readonly Dictionary<BontiqueType, List<BontiqueShopItem>> groupedByCategory = new Dictionary<BontiqueType, List<BontiqueShopItem>>();
    private readonly List<BontiqueShopItem> filteredBuffer = new List<BontiqueShopItem>();
    private readonly Dictionary<string, BontiquePurchaseEntry> purchaseByBid = new Dictionary<string, BontiquePurchaseEntry>();
    private static Dictionary<int, RewardName> rewardNameByOrder;
    private VirtualizedScrollGrid<BontiqueShopItem> grid;
    private BontiqueType currentCategory = BontiqueType.Type0;
    private DateTime currentTime;
    private LoadingPage loadingPage;

    private const string LoadingPagePath = "UI/Pages/loading";

    private void Start()
    {
        StartCoroutine(BootstrapWithWorldTime());
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
        RefreshCurrentCategory();
    }

    private IEnumerator BootstrapWithWorldTime()
    {
        bool done = false;
        bool success = false;
        DateTime fetchedTime = DateTime.MinValue;

        if (!StartLoading(
                new List<LoadingTask> { new LoadingTask("Fetching world time...", task => ExecuteFetchTimeTask(task, t => fetchedTime = t)) },
                ok => { success = ok; done = true; }))
        {
            if (FrameUI != null) FrameUI.ReturnToPrevious();
            yield break;
        }

        while (!done) yield return null;
        CleanupLoading();

        if (!success)
        {
            if (FrameUI != null) FrameUI.ReturnToPrevious();
            yield break;
        }

        if (fetchedTime == DateTime.MinValue)
        {
            if (FrameUI != null) FrameUI.ReturnToPrevious();
            yield break;
        }

        currentTime = fetchedTime;
        InitializeShopAfterTimeReady();
    }

    private void InitializeShopAfterTimeReady()
    {
        InitializeCategories();
        LoadShopFromCsv("Shop/boutique");
        RebuildPurchaseCache();
        CleanupExpiredPurchaseRecords(currentTime);
        InitializeGrid();
        RefreshCurrentCategory();
    }

    private void LoadShopFromCsv(string resourcePath)
    {
        shopItems.Clear();
        groupedByCategory.Clear();
        var ta = Resources.Load<TextAsset>(resourcePath);
        if (ta == null)
        {
            Debug.LogError($"BontiqueCanvas: missing csv at Resources/{resourcePath}");
            return;
        }
        using (StringReader sr = new StringReader(ta.text))
        {
            string line;
            bool firstLine = true;
            while ((line = sr.ReadLine()) != null)
            {
                if (string.IsNullOrWhiteSpace(line)) continue;

                // optional header skip: if first non-empty line starts with non-digit at category column
                if (firstLine)
                {
                    firstLine = false;
                    string[] headerProbe = line.Split(',');
                    if (headerProbe.Length > 2 && !int.TryParse(headerProbe[2], out _)) continue;
                }

                var cols = line.Split(',');
                var item = BontiqueShopItem.FromCsvRow(cols);
                shopItems.Add(item);
                if (!groupedByCategory.TryGetValue(item.Category, out List<BontiqueShopItem> list))
                {
                    list = new List<BontiqueShopItem>();
                    groupedByCategory[item.Category] = list;
                }
                list.Add(item);
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
        EvaluateItemState(item, currentTime, out int remaining, out bool interactable);
        controller.Configure(item, remaining, interactable, OnRedeemRequested);
    }

    private void ShowCategory(BontiqueType t, bool forceReset)
    {
        if (grid == null) InitializeGrid();
        filteredBuffer.Clear();
        if (groupedByCategory.TryGetValue(t, out List<BontiqueShopItem> list) && list != null)
        {
            filteredBuffer.AddRange(list);
        }
        grid?.SetData(filteredBuffer, forceReset);
    }

    private void RefreshCurrentCategory()
    {
        ShowCategory(currentCategory, true);
    }

    private void RebuildPurchaseCache()
    {
        purchaseByBid.Clear();
        List<BontiquePurchaseEntry> entries = BontiquePurchaseSave.GetAll();
        for (int i = 0; i < entries.Count; i++)
        {
            BontiquePurchaseEntry e = entries[i];
            if (e == null || string.IsNullOrEmpty(e.bid)) continue;
            purchaseByBid[e.bid] = e;
        }
    }

    private void CleanupExpiredPurchaseRecords(DateTime now)
    {
        HashSet<string> toRemove = new HashSet<string>();
        for (int i = 0; i < shopItems.Count; i++)
        {
            BontiqueShopItem item = shopItems[i];
            if (item == null || string.IsNullOrEmpty(item.bid)) continue;
            if (!purchaseByBid.TryGetValue(item.bid, out BontiquePurchaseEntry record) || record == null) continue;

            bool periodic = item.Limit == LimitType.Day || item.Limit == LimitType.Week || item.Limit == LimitType.Month || item.Limit == LimitType.Year;
            if (!periodic) continue;
            if (item.IsPeriodExpired(record.firstPurchaseDate, now))
            {
                toRemove.Add(item.bid);
            }
        }

        if (toRemove.Count <= 0) return;
        BontiquePurchaseSave.RemoveBids(toRemove);
        RebuildPurchaseCache();
    }

    private void OnRedeemRequested(BontiqueShopItem item)
    {
        if (item == null) return;

        CleanupExpiredPurchaseRecords(currentTime);
        EvaluateItemState(item, currentTime, out int remaining, out bool interactable);
        if (!interactable)
        {
            Debug.Log($"Bontique: item not purchasable now. bid={item.bid}, remaining={remaining}");
            RefreshCurrentCategory();
            return;
        }

        int have = RewardingSystem.GetAmount(item.CurrencyId);
        if (have < item.CurrencyAmount)
        {
            Debug.Log("Not enough currency");
            RefreshCurrentCategory();
            return;
        }

        if (!TryConsumeByOrder(item.CurrencyId, item.CurrencyAmount))
        {
            Debug.LogWarning($"Bontique: failed to consume currency order={item.CurrencyId}, amount={item.CurrencyAmount}");
            RefreshCurrentCategory();
            return;
        }

        RewardingSystem.GainRewardByOrder(item.gainId, item.ObtainAmount);
        BontiquePurchaseSave.AddPurchase(item.bid, currentTime);
        RebuildPurchaseCache();
        RefreshCurrentCategory();
    }

    private void EvaluateItemState(BontiqueShopItem item, DateTime now, out int remaining, out bool interactable)
    {
        remaining = -1;
        interactable = false;
        if (item == null) return;

        if (!item.IsInActiveWindow(now))
        {
            remaining = 0;
            interactable = false;
            return;
        }

        int purchased = 0;
        if (!string.IsNullOrEmpty(item.bid) && purchaseByBid.TryGetValue(item.bid, out BontiquePurchaseEntry record) && record != null)
        {
            purchased = Mathf.Max(0, record.purchaseCount);
            if (item.IsPeriodExpired(record.firstPurchaseDate, now))
            {
                purchased = 0;
            }
        }

        if (item.Limit == LimitType.None)
        {
            remaining = -1;
            interactable = true;
            return;
        }

        if (item.Limit == LimitType.OnlyOnce)
        {
            remaining = purchased > 0 ? 0 : 1;
            interactable = purchased <= 0;
            return;
        }

        int limitCount = Mathf.Max(0, item.LimitCount);
        if (limitCount <= 0)
        {
            // Fallback for malformed configs: treat as unbuyable finite item.
            remaining = 0;
            interactable = false;
            return;
        }

        remaining = Mathf.Max(0, limitCount - purchased);
        interactable = remaining > 0;
    }

    private static bool TryConsumeByOrder(int rewardOrder, int amount)
    {
        if (amount <= 0) return true;
        if (!TryGetRewardNameByOrder(rewardOrder, out RewardName rewardName)) return false;
        return RewardingSystem.ConsumeItem(rewardName, amount);
    }

    private static bool TryGetRewardNameByOrder(int rewardOrder, out RewardName rewardName)
    {
        if (rewardNameByOrder == null)
        {
            rewardNameByOrder = new Dictionary<int, RewardName>(RewardingSystem.RewardNumMap.Count);
            foreach (KeyValuePair<RewardName, int> kv in RewardingSystem.RewardNumMap)
                rewardNameByOrder[kv.Value] = kv.Key;
        }
        return rewardNameByOrder.TryGetValue(rewardOrder, out rewardName);
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

    private bool StartLoading(List<LoadingTask> tasks, Action<bool> onComplete)
    {
        GameObject prefab = Resources.Load<GameObject>(LoadingPagePath);
        if (prefab == null)
        {
            Debug.LogError($"BontiqueCanvas: missing loading prefab at {LoadingPagePath}");
            onComplete?.Invoke(false);
            return false;
        }

        GameObject obj = Instantiate(prefab);
        loadingPage = obj.GetComponent<LoadingPage>();
        if (loadingPage == null)
        {
            Destroy(obj);
            onComplete?.Invoke(false);
            return false;
        }

        loadingPage.Initialize(tasks, onComplete);
        return true;
    }

    private void CleanupLoading()
    {
        if (loadingPage == null) return;
        Destroy(loadingPage.gameObject);
        loadingPage = null;
    }

    private IEnumerator ExecuteFetchTimeTask(LoadingTask task, Action<DateTime> setResult)
    {
        if (loadingPage != null) loadingPage.SetDetail("Fetching world time (UTC+8)...");

        DateTime? serverDate = null;
        yield return WorldTimeService.FetchUtc8DateTime(
            value => serverDate = value,
            detail =>
            {
                if (loadingPage != null) loadingPage.SetDetail(detail);
            });

        if (serverDate == null)
        {
            task.Success = false;
            task.Result = null;
            if (loadingPage != null) loadingPage.SetDetail("Failed to fetch world time.");
            yield break;
        }

        DateTime valueDate = serverDate.Value.Date;
        setResult?.Invoke(valueDate);
        task.Success = true;
        task.Result = valueDate;
        if (loadingPage != null) loadingPage.SetDetail($"World time OK: {valueDate:yyyy-MM-dd}");
    }

}
