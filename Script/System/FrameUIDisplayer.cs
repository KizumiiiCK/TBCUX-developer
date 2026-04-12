using System.Collections;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class FrameUIDisplayer : MonoBehaviour
{
    public enum DoorAction
    {
        None,
        Open,
        Close
    }

    [Header("Currency Display")]
    [SerializeField] private RectTransform scrollContent;
    [SerializeField] private GameObject currencyItemPrefab;
    [SerializeField] private List<int> baseCurrencyIds = new List<int>();

    [Header("Navigation")]
    [SerializeField] private Button backButton;
    [SerializeField] private GameObject rootPage;
    [SerializeField] private Transform pagesRoot;
    [SerializeField] private FrameUIAnimations doorAnimator;

    private readonly List<GameObject> spawnedItems = new List<GameObject>();
    private readonly List<int> displayedIds = new List<int>();
    private readonly Stack<UICanvasMain> pageStack = new Stack<UICanvasMain>();
    private readonly Stack<List<int>> extraCurrencyStack = new Stack<List<int>>();
    private List<int> currentExtraIds = new List<int>();
    private Coroutine navigationRoutine;
    private BaseCanvas rootCanvas;

    private void Awake()
    {
        SetAppearance();
        if (backButton != null) backButton.onClick.AddListener(ReturnToPrevious);
        if (rootPage == null) rootPage = gameObject;
        rootCanvas = rootPage.GetComponent<BaseCanvas>();
        if (rootCanvas != null)
        {
            rootCanvas.Initialize(this);
            pageStack.Push(rootCanvas);
            currentExtraIds = new List<int>(rootCanvas.ExtraCurrencyIds);
            ApplyPageBgm(rootCanvas);
        }
        if (doorAnimator == null) doorAnimator = GetComponentInChildren<FrameUIAnimations>(true);
    }

    private void Start()
    {
        RefreshCurrencies();
    }
    private void SetAppearance()
    {
        string cptname = PlayerPrefs.GetString(UXPref.ChapterName);
        Sprite[] doorsprites = Resources.LoadAll<Sprite>($"Background/Doors/door_{cptname}");
        if (doorAnimator != null && doorsprites.Length>=2) doorAnimator.SetDoorSprites(doorsprites[0], doorsprites[1]);
    }
    public void SetBaseCurrencies(List<int> ids)
    {
        baseCurrencyIds = ids ?? new List<int>();
        RefreshCurrencies();
    }

    public void SetCurrentExtraCurrencies(List<int> ids)
    {
        currentExtraIds = ids != null ? new List<int>(ids) : new List<int>();
        RefreshCurrencies();
    }

    public void RefreshCurrencies()
    {
        ClearCurrencyItems();
        displayedIds.Clear();

        AppendCurrencyList(baseCurrencyIds);
        AppendCurrencyList(currentExtraIds);
    }

    public void RefreshCurrencyAmounts()
    {
        for (int i = 0; i < spawnedItems.Count; i++)
        {
            if (spawnedItems[i] == null) continue;
            if (i >= displayedIds.Count) break;

            int rewardId = displayedIds[i];
            int amount = RewardingSystem.GetAmount(rewardId);

            var item = spawnedItems[i].GetComponent<FrameCurrencyItem>();
            if (item != null)
            {
                item.SetData(rewardId, amount);
            }
            else
            {
                var text = spawnedItems[i].GetComponentInChildren<TMP_Text>(true);
                if (text != null) text.text = amount.ToString();
            }
        }
    }

    #region Page Navigation

    public void OpenPage(string prefabName)
    {
        OpenPage(prefabName, null, null, DoorAction.None);
    }

    public void OpenPage(string prefabName, System.Action<UICanvasMain> onCreated)
    {
        OpenPage(prefabName, onCreated, null, DoorAction.None);
    }

    public void OpenPage(string prefabName, List<int> extraIds)
    {
        OpenPage(prefabName, null, extraIds, DoorAction.None);
    }

    public void OpenPage(string prefabName, System.Action<UICanvasMain> onCreated, List<int> extraIds)
    {
        OpenPage(prefabName, onCreated, extraIds, DoorAction.None);
    }

    public void OpenPage(string prefabName, System.Action<UICanvasMain> onCreated, List<int> extraIds, DoorAction doorAction)
    {
        if (navigationRoutine != null) return;
        StartNavigation(OpenPageRoutine(prefabName, onCreated, extraIds, doorAction));
    }

    public void ReturnToPrevious()
    {
        ReturnToPrevious(DoorAction.None);
    }

    public void ReturnToPrevious(DoorAction doorAction)
    {
        if (navigationRoutine != null) return;
        if (pageStack.Count <= 1)
        {
            if (rootPage != null) rootPage.SetActive(true);
            var switcher = GetComponent<SceneSwitcher>();
            if (switcher != null) switcher.TagOutTo("MainMenu");
            return;
        }
        StartNavigation(ReturnToPreviousRoutine(doorAction));
    }

    public void ReturnToRoot()
    {
        if (navigationRoutine != null) return;
        StartNavigation(ReturnToRootRoutine());
    }

    private void StartNavigation(IEnumerator routine)
    {
        SetNavigationBusy(true);
        navigationRoutine = StartCoroutine(NavigationRoutineGuard(routine));
    }

    private IEnumerator NavigationRoutineGuard(IEnumerator routine)
    {
        yield return routine;
        navigationRoutine = null;
        SetNavigationBusy(false);
    }

    private void SetNavigationBusy(bool busy)
    {
        if (backButton != null) backButton.interactable = !busy;
    }

    private IEnumerator OpenPageRoutine(string prefabName, System.Action<UICanvasMain> onCreated, List<int> extraIds, DoorAction doorAction)
    {
        if (string.IsNullOrWhiteSpace(prefabName)) yield break;

        if (pageStack.Count > 0)
        {
            var current = pageStack.Peek();
            if (current != null) yield return current.OnExit();
            if (current != null && current != rootCanvas) current.gameObject.SetActive(false);
        }

        string path = $"UI/Pages/{prefabName}";
        var prefab = Resources.Load<GameObject>(path);
        if (prefab == null)
        {
            Debug.LogError($"[FrameUIDisplayer] Missing prefab at {path}");
            yield break;
        }

        var go = pagesRoot != null
            ? Instantiate(prefab, pagesRoot)
            : Instantiate(prefab);
        var page = go.GetComponent<UICanvasMain>();
        if (page == null)
        {
            // Backward compatibility: old SectionCanvas prefab may not yet have the new script attached.
            if (prefabName == "SectionCanvas")
            {
                page = go.GetComponent<SectionCanvas>();
                if (page == null) page = go.AddComponent<SectionCanvas>();
            }
            else
            {
                Debug.LogError($"[FrameUIDisplayer] Prefab {prefabName} has no UICanvasMain");
                Destroy(go);
                yield break;
            }
        }

        page.Initialize(this);
        onCreated?.Invoke(page);
        pageStack.Push(page);

        extraCurrencyStack.Push(currentExtraIds);
        currentExtraIds = extraIds != null
            ? new List<int>(extraIds)
            : new List<int>(page.ExtraCurrencyIds);
        RefreshCurrencies();

        yield return page.OnEnter();
        ApplyPageBgm(page);
        ApplyDoorAction(doorAction);
    }

    private IEnumerator ReturnToPreviousRoutine(DoorAction doorAction)
    {
        var current = pageStack.Pop();
        if (current != null) yield return current.OnExit();
        if (current != null && current != rootCanvas) Destroy(current.gameObject);

        var previous = pageStack.Peek();
        if (previous != null) previous.gameObject.SetActive(true);

        currentExtraIds = extraCurrencyStack.Count > 0 ? extraCurrencyStack.Pop() : new List<int>();
        RefreshCurrencies();

        if (previous != null) yield return previous.OnEnter();
        ApplyPageBgm(previous);
        ApplyDoorAction(doorAction);
    }

    private IEnumerator ReturnToRootRoutine()
    {
        while (pageStack.Count > 1)
        {
            var current = pageStack.Pop();
            if (current != null) yield return current.OnExit();
            if (current != null) Destroy(current.gameObject);
        }

        if (rootPage != null) rootPage.SetActive(true);
        currentExtraIds = rootCanvas != null
            ? new List<int>(rootCanvas.ExtraCurrencyIds)
            : new List<int>();
        extraCurrencyStack.Clear();
        RefreshCurrencies();

        if (rootCanvas != null) yield return rootCanvas.OnEnter();
        ApplyPageBgm(rootCanvas);
    }

    #endregion

    #region BGM

    private void ApplyPageBgm(UICanvasMain page)
    {
        if (page == null) return;
        string bgmName = page.GetPageBgmName();
        if (string.IsNullOrEmpty(bgmName)) return;
        BGMTool.ChangeBGM(bgmName);
    }

    #endregion

    #region Door Animations

    private void ApplyDoorAction(DoorAction action)
    {
        switch (action)
        {
            case DoorAction.Open:
                OpenDoor();
                break;
            case DoorAction.Close:
                CloseDoor();
                break;
        }
    }

    public void OpenDoor()
    {
        if (doorAnimator != null) doorAnimator.OpenDoor();
    }

    public void CloseDoor()
    {
        if (doorAnimator != null) doorAnimator.CloseDoor();
    }

    #endregion

    #region Currency Items Internal

    private void AppendCurrencyList(List<int> ids)
    {
        if (ids == null || ids.Count == 0) return;
        if (scrollContent == null || currencyItemPrefab == null) return;

        for (int i = 0; i < ids.Count; i++)
        {
            int rewardId = ids[i];
            int amount = RewardingSystem.GetAmount(rewardId);

            var go = Instantiate(currencyItemPrefab, scrollContent);
            spawnedItems.Add(go);
            displayedIds.Add(rewardId);

            var item = go.GetComponent<FrameCurrencyItem>();
            if (item != null)
            {
                item.SetData(rewardId, amount);
            }
            else
            {
                ApplyFallbackDisplay(go.transform, rewardId, amount);
            }
        }
    }

    private void ClearCurrencyItems()
    {
        for (int i = 0; i < spawnedItems.Count; i++)
        {
            if (spawnedItems[i] != null) Destroy(spawnedItems[i]);
        }
        spawnedItems.Clear();
    }

    private void ApplyFallbackDisplay(Transform root, int rewardId, int amount)
    {
        if (root == null) return;

        var icon = root.GetComponentInChildren<Image>(true);
        if (icon != null) icon.sprite = StorageImageHelper.GetItemImageByOrder(rewardId);

        var text = root.GetComponentInChildren<TMP_Text>(true);
        if (text != null) text.text = amount.ToString();
    }

    #endregion
}
