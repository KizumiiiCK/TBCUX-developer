using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

public class BontiqueCanvas : UICanvasMain
{
    [Header("UI Refs")]
    [SerializeField] private Transform categoryButtonsRoot;
    [SerializeField] private GameObject categoryButtonPrefab; // expects a Button
    [SerializeField] private RectTransform itemsContent;
    [SerializeField] private GameObject itemPrefab; // BontiqueItems prefab

    private List<BontiqueShopItem> shopItems = new List<BontiqueShopItem>();
    private readonly Dictionary<BontiqueType, List<BontiqueShopItem>> groupedByCategory = new Dictionary<BontiqueType, List<BontiqueShopItem>>();
    private readonly List<BontiqueShopItem> filteredBuffer = new List<BontiqueShopItem>();
    private readonly Dictionary<string, BontiquePurchaseEntry> purchaseByBid = new Dictionary<string, BontiquePurchaseEntry>();
    private readonly List<GameObject> spawnedItemCards = new List<GameObject>();
    private static Dictionary<int, RewardName> rewardNameByOrder;
    private BontiqueType currentCategory = BontiqueType.Dayly;
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
        LoadShopFromStaticCatalog();
        RebuildPurchaseCache();
        CleanupExpiredPurchaseRecords(currentTime);
        RefreshCurrentCategory();
    }

    private void LoadShopFromStaticCatalog()
    {
        shopItems.Clear();
        groupedByCategory.Clear();
        List<BontiqueShopItem> templates = BontiqueStaticCatalog.GetTemplateItems();
        for (int i = 0; i < templates.Count; i++)
        {
            BontiqueShopItem item = templates[i];
            if (item == null) continue;
            shopItems.Add(item);
            if (!groupedByCategory.TryGetValue(item.Category, out List<BontiqueShopItem> list))
            {
                list = new List<BontiqueShopItem>();
                groupedByCategory[item.Category] = list;
            }
            list.Add(item);
        }
    }

    private void BindItem(GameObject go, int _, BontiqueShopItem item)
    {
        if (go == null) return;
        var controller = go.GetComponent<BontiqueItems>();
        if (controller == null) controller = go.AddComponent<BontiqueItems>();
        EvaluateItemState(item, currentTime, out int remaining, out bool interactable);
        controller.Configure(item, remaining, interactable, OnRedeemRequested);
    }

    private void ShowCategory(BontiqueType t)
    {
        ClearSpawnedItemCards();
        filteredBuffer.Clear();
        if (groupedByCategory.TryGetValue(t, out List<BontiqueShopItem> list) && list != null)
        {
            for (int i = 0; i < list.Count; i++)
            {
                BontiqueShopItem item = list[i];
                if (ShouldDisplayItem(item, currentTime)) filteredBuffer.Add(item);
            }
        }
        SpawnItems(filteredBuffer);
    }

    private void RefreshCurrentCategory()
    {
        ShowCategory(currentCategory);
    }

    private void SpawnItems(List<BontiqueShopItem> items)
    {
        if (itemsContent == null || itemPrefab == null || items == null) return;
        for (int i = 0; i < items.Count; i++)
        {
            BontiqueShopItem item = items[i];
            GameObject go = Instantiate(itemPrefab, itemsContent);
            spawnedItemCards.Add(go);
            BindItem(go, i, item);
        }
    }

    private void ClearSpawnedItemCards()
    {
        for (int i = spawnedItemCards.Count - 1; i >= 0; i--)
        {
            GameObject card = spawnedItemCards[i];
            if (card != null) Destroy(card);
        }
        spawnedItemCards.Clear();
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

        if (item.RewardKind == RewardType.character)
        {
            CharacterUpgradeSave.UnlockCharacterTire(item.gainId.ToString("0000"), 0);
        }
        else
        {
            RewardingSystem.GainRewardByOrder(item.gainId, item.ObtainAmount);
        }
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

    private bool ShouldDisplayItem(BontiqueShopItem item, DateTime now)
    {
        if (item == null) return false;
        if (!item.IsInActiveWindow(now)) return false;
        if (item.Limit != LimitType.OnlyOnce) return true;
        if (string.IsNullOrEmpty(item.bid)) return true;
        if (!purchaseByBid.TryGetValue(item.bid, out BontiquePurchaseEntry record) || record == null) return true;
        return Mathf.Max(0, record.purchaseCount) <= 0;
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
