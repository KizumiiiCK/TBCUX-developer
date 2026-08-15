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
    [Header("Audio")]
    [SerializeField] private AudioSource purchaseAudioSource;
    [SerializeField] private AudioClip purchaseAudioClip;

    private readonly List<BontiqueShopItem> filteredBuffer = new List<BontiqueShopItem>();
    private readonly Dictionary<string, BontiquePurchaseEntry> purchaseByBid = new Dictionary<string, BontiquePurchaseEntry>();
    private readonly List<GameObject> spawnedItemCards = new List<GameObject>();
    private readonly List<BontiqueShopItem> spawnedItemData = new List<BontiqueShopItem>();
    private static Dictionary<int, RewardName> rewardNameByOrder;
    private BontiqueType currentCategory = BontiqueType.Daily;
    private DateTime currentTime;
    private LoadingPage loadingPage;

    private const string LoadingPagePath = "UI/Pages/loading";
    private const string RewardCanvasPath = "UI/Pages/RewardCanvas";

    private void Start()
    {
        Camera.main.backgroundColor = Color.black;
        StartCoroutine(BootstrapWithWorldTime());
    }

    private void InitializeCategories()
    {
        if (categoryButtonsRoot == null || categoryButtonPrefab == null) return;
        // Clear existing
        for (int i = categoryButtonsRoot.childCount - 1; i >= 0; i--) DestroyImmediate(categoryButtonsRoot.GetChild(i).gameObject);
        List<BontiqueType> visibleCategories = GetVisibleCategories(currentTime);
        if (visibleCategories.Count == 0)
        {
            currentCategory = BontiqueType.Unknown;
            return;
        }

        if (!visibleCategories.Contains(currentCategory)) currentCategory = visibleCategories[0];

        foreach (BontiqueType t in visibleCategories)
        {
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
        RebuildPurchaseCache();
        CleanupExpiredPurchaseRecords(currentTime);
        InitializeCategories();
        RefreshCurrentCategory();
    }

    private void BindItem(GameObject go, int _, BontiqueShopItem item)
    {
        if (go == null) return;
        var controller = go.GetComponent<BontiqueItems>();
        if (controller == null) controller = go.AddComponent<BontiqueItems>();
        EvaluateItemState(item, currentTime, out int remaining, out bool interactable);
        controller.Configure(item, remaining, interactable, currentTime, OnRedeemClickedSignal, OnRedeemRequested);
    }

    private void ShowCategory(BontiqueType t)
    {
        ClearSpawnedItemCards();
        filteredBuffer.Clear();
        if (t == BontiqueType.Unknown) return;
        IReadOnlyList<BontiqueShopItem> list = BontiqueStaticCatalog.GetItemsByCategory(t);
        if (list != null)
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
            spawnedItemData.Add(item);
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
        spawnedItemData.Clear();
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
        IReadOnlyList<BontiqueShopItem> allItems = BontiqueStaticCatalog.GetAllItems();
        for (int i = 0; i < allItems.Count; i++)
        {
            BontiqueShopItem item = allItems[i];
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
        if (!string.IsNullOrEmpty(item.bid))
        {
            BontiqueShopItem catalogItem = BontiqueStaticCatalog.GetItemByBid(item.bid);
            if (catalogItem != null) item = catalogItem;
        }

        CleanupExpiredPurchaseRecords(currentTime);
        EvaluateItemState(item, currentTime, out int remaining, out bool interactable);
        if (!interactable)
        {
            Debug.Log($"Bontique: item not purchasable now. bid={item.bid}, remaining={remaining}");
            RefreshSpawnedItemStatesAfterPurchase();
            return;
        }

        int have = RewardingSystem.GetAmount(item.CurrencyId);
        if (have < item.CurrencyAmount)
        {
            Debug.Log("Not enough currency");
            RefreshSpawnedItemStatesAfterPurchase();
            return;
        }

        if (!TryConsumeByOrder(item.CurrencyId, item.CurrencyAmount))
        {
            Debug.LogWarning($"Bontique: failed to consume currency order={item.CurrencyId}, amount={item.CurrencyAmount}");
            RefreshSpawnedItemStatesAfterPurchase();
            return;
        }

        DeliverShopItem(item, recordPurchase: true);
    }

    private void DeliverShopItem(BontiqueShopItem item, bool recordPurchase)
    {
        if (item == null) return;

        if (item.RewardKind == RewardType.character)
        {
            CharacterUpgradeSave.UnlockCharacterTire(item.gainId.ToString("0000"), 0);
        }
        else
        {
            RewardingSystem.GainRewardByOrder(item.gainId, item.ObtainAmount);
        }

        if (recordPurchase) BontiquePurchaseSave.AddPurchase(item.bid, currentTime.Date);
        ShowRewardTransition(item);
        RebuildPurchaseCache();
        RefreshCurrencyDisplaysAfterPurchase();
    }

    private void OnRedeemClickedSignal(BontiqueShopItem item)
    {
        PlayPurchaseSfx();
    }

    private void PlayPurchaseSfx()
    {
        if (purchaseAudioSource != null)
        {
            if (purchaseAudioClip != null) PlatformAudio.PlayOneShot(purchaseAudioSource, purchaseAudioClip);
            else PlatformAudio.PlaySfx(purchaseAudioSource);
            return;
        }
        if (purchaseAudioClip != null) PlatformAudio.PlayClipAtPoint(purchaseAudioClip, Vector3.zero);
    }

    private void RefreshCurrencyDisplaysAfterPurchase()
    {
        RefreshSpawnedItemStatesAfterPurchase();
        if (FrameUI != null) FrameUI.RefreshCurrencyAmounts();
    }

    private void RefreshSpawnedItemStatesAfterPurchase()
    {
        int count = Mathf.Min(spawnedItemCards.Count, spawnedItemData.Count);
        for (int i = 0; i < count; i++)
        {
            GameObject card = spawnedItemCards[i];
            BontiqueShopItem item = spawnedItemData[i];
            if (card == null || item == null) continue;
            BontiqueItems controller = card.GetComponent<BontiqueItems>();
            if (controller == null) continue;

            EvaluateItemState(item, currentTime, out int remaining, out bool interactable);
            controller.RefreshPurchaseState(item, remaining, interactable, currentTime);
        }
    }

    private void ShowRewardTransition(BontiqueShopItem item)
    {
        if (item == null) return;
        GameObject rewardPrefab = Resources.Load<GameObject>(RewardCanvasPath);
        if (rewardPrefab == null)
        {
            Debug.LogWarning($"Bontique: missing reward prefab at Resources/{RewardCanvasPath}");
            return;
        }

        GameObject rewardObj = Instantiate(rewardPrefab);
        RewardCanvas rewardCanvas = rewardObj.GetComponent<RewardCanvas>();
        if (rewardCanvas == null)
        {
            Destroy(rewardObj);
            Debug.LogWarning("Bontique: RewardCanvas component missing on reward prefab.");
            return;
        }
        rewardCanvas.Initialize(item.RewardKind, item.gainId, item.ObtainAmount);
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

        bool requiresHideAfterPurchase = item.Limit == LimitType.OnlyOnce;
        if (!requiresHideAfterPurchase) return true;
        if (string.IsNullOrEmpty(item.bid)) return true;
        if (!purchaseByBid.TryGetValue(item.bid, out BontiquePurchaseEntry record) || record == null) return true;
        return Mathf.Max(0, record.purchaseCount) <= 0;
    }

    private List<BontiqueType> GetVisibleCategories(DateTime now)
    {
        List<BontiqueType> categories = new List<BontiqueType>();
        foreach (BontiqueType t in Enum.GetValues(typeof(BontiqueType)))
        {
            if (t == BontiqueType.Unknown) continue;
            if (HasVisibleItemsInCategory(t, now)) categories.Add(t);
        }
        return categories;
    }

    private bool HasVisibleItemsInCategory(BontiqueType category, DateTime now)
    {
        IReadOnlyList<BontiqueShopItem> list = BontiqueStaticCatalog.GetItemsByCategory(category);
        if (list == null || list.Count == 0) return false;
        for (int i = 0; i < list.Count; i++)
        {
            if (ShouldDisplayItem(list[i], now)) return true;
        }
        return false;
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
        yield return PlatformTimeSystem.FetchUtc8DateTime(
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
