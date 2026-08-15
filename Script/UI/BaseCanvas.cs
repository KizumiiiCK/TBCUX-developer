using System.Collections;
using UnityEngine;
using UnityEngine.UI;

public class BaseCanvas : UICanvasMain
{
    [Header("Pinned Objects")]
    [SerializeField] private RectTransform leftPinned;
    [SerializeField] private RectTransform rightPinned;
    //[SerializeField] private Transform mainCamera;
    private UICanvasMain currentLevelCanvas;
    private UICanvasMain currentSubCanvas;
    [Header("Initializers")]
    [SerializeField] private FrameUIDisplayer frameUI;
    [SerializeField] private Button StartBtn;
    [SerializeField] private Button UpgradeBtn;
    [SerializeField] private Button EquipBtn;
    [SerializeField] private Button CatBtn;
    [SerializeField] private Button EnemyBtn;
    [SerializeField] private Button BontiqueBtn;
    [SerializeField] private Button MedalBtn;
    [SerializeField] private Button StorageBtn;
    [SerializeField] private Button CatCapsBtn;
    //[SerializeField] private Button EnterMapBtn;

    private Vector2 canvasSize;
    private bool operating = false;
    private bool tagInShown;

    private const string SectionCanvasPrefab = "SectionCanvas";
    private const string LevelCanvasPrefab = "LevelsCanvas";
    private const string UpgradeCanvasPrefab = "UpgradeCanvas";
    private const string EnemyCanvasPrefab = "EnemyIndexCanvas";
    private const string EquipCanvasPrefab = "EquipCanvas";
    private const string CapsuleDrawCanvasPrefab = "DrawCapsuleCanvas";
    private const string StorageCanvasPrefab = "StorageCanvas";
    private const string BontiqueCanvasPrefab = "BontiqueCanvas";

    private void Start()
    {
        ShowTagInOnce();

        Application.targetFrameRate = 30;
        PositionCorners();
        AddButtonListener();
        if (PlayerPrefs.GetInt(UXPref.DirectMark, 0) == 1) DirectToMap();
        //PlayerPrefs.DeleteKey(UXPref.DirectMark);
        Instantiate(Resources.Load<GameObject>("UI/Pages/CheckInCanvas"));
        RewardingSystem.GainReward(RewardName.XP, 0);
        UpdateCurrencies();
    }
    private void Awake()
    {
        ShowTagInOnce();
        Camera.main.backgroundColor = Color.black;
        Input.multiTouchEnabled = false;
    }

    private void ShowTagInOnce()
    {
        if (tagInShown) return;
        tagInShown = true;

        GameObject prefab = Resources.Load<GameObject>("UI/Tag In");
        if (prefab != null) Instantiate(prefab);
    }
    void PositionCorners()
    {
        RectTransform canvasRect = GetComponent<RectTransform>();
        canvasSize = canvasRect.sizeDelta;
        leftPinned.anchoredPosition = new Vector2(-canvasSize.x / 2,0);
        rightPinned.anchoredPosition = new Vector2(canvasSize.x / 2,0);
        leftPinned.gameObject.SetActive(true);
        rightPinned.gameObject.SetActive(true);
    }
    private void AddButtonListener()
    {
        StartBtn.onClick.AddListener(delegate { if (operating) return; ToSectionCanvas(); });
        UpgradeBtn.onClick.AddListener(delegate { if (operating) return; ToUpgradeCanvas(); });
        EquipBtn.onClick.AddListener(delegate { if (operating) return; StartCoroutine(ShowEquipCanvas()); });
        EnemyBtn.onClick.AddListener(delegate { if (operating) return; ToEnemyCanvas(); });
        CatCapsBtn.onClick.AddListener(delegate { if (operating) return; ToCapsuleDrawCanvas(); });
        BontiqueBtn.onClick.AddListener(delegate { if (operating) return; ToBontiqueCanvas(); });
        StorageBtn.onClick.AddListener(delegate { if (operating) return; ToStorageCanvas(); });
    }
    public void UpdateCurrencies()
    {
        if (frameUI != null) frameUI.RefreshCurrencyAmounts();
    }
    public void ToSectionCanvas() { StartCoroutine(ShowSectionPage()); }
    public void LoadMap(MapInfo map)
    {
        if (frameUI != null)
        {
            frameUI.OpenPage(LevelCanvasPrefab, page =>
            {
                currentLevelCanvas = page;
                var tiler = page != null ? page.GetComponent<LevelTiler>() : null;
                if (tiler != null) tiler.SetMapInfo(map);
            });
        }
    }
    public void ReturnFromMap(){StartCoroutine(ReturnSectionsFromMap());}
    private IEnumerator ShowSectionPage()
    {
        operating = true;
        if (frameUI != null) frameUI.OpenPage(SectionCanvasPrefab);
        yield return new WaitForSeconds(FrameUIAnimations.DoorDuration);
        operating = false;
    }
    private IEnumerator ReturnSectionsFromMap()
    {
        CloseDoor();
        yield return new WaitForSeconds(FrameUIAnimations.DoorDuration);
        currentLevelCanvas = null;
        if (frameUI != null) frameUI.ReturnToPrevious();
    }
    public void ToUpgradeCanvas() { StartCoroutine(ShowUpgradeCanvas()); }
    public void SubBacktoBase() { StartCoroutine(ReturnBaseFromSub()); Instantiate(Resources.Load<GameObject>("UI/CheckInCanvas")); }
    public void ToEnemyCanvas() { StartCoroutine(ShowEnemyCanvas()); }
    public void ToCapsuleDrawCanvas() { StartCoroutine(ShowCapsuleCanvas()); }
    public void ToBontiqueCanvas() { StartCoroutine(ShowBontiqueCanvas()); }
    public void ToStorageCanvas() { StartCoroutine(ShowStorageCanvas()); }
    public void MapToEquip(string[] enemies = null, string[] restrictions = null, bool blindEnemyIcons = false)
    {
        StartCoroutine(ShowEquipFromMap(enemies, restrictions, blindEnemyIcons));
    }
    public void EquipBackToMap() { StartCoroutine(ShowMapFromEquip()); }
    private IEnumerator ShowUpgradeCanvas()
    {
        operating = true;
        if (frameUI != null)
        {
            frameUI.OpenPage(
                UpgradeCanvasPrefab,
                page => currentSubCanvas = page,
                null,
                FrameUIDisplayer.DoorAction.None
            );
        }
        yield return new WaitForSeconds(FrameUIAnimations.DoorDuration);
        operating = false;
    }
    
    private IEnumerator ShowEquipCanvas()
    {
        operating = true;
        if (frameUI != null) frameUI.OpenPage(EquipCanvasPrefab, page => currentSubCanvas = page);
        yield return new WaitForSeconds(FrameUIAnimations.DoorDuration);
        operating = false;
    }
    private IEnumerator ShowEquipFromMap(string[] enemies = null, string[] restrictions = null, bool blindEnemyIcons = false)
    {
        CloseDoor();
        yield return new WaitForSeconds(FrameUIAnimations.DoorDuration);
        if (frameUI != null)
        {
            frameUI.OpenPage(EquipCanvasPrefab, page =>
            {
                currentSubCanvas = page;
                if (page != null)
                {
                    var equip = page.GetComponent<EquipCanvas>();
                    if (equip != null) equip.ChanageSEBShowInfo(enemies, restrictions, blindEnemyIcons);
                }
            });
        }
        yield return new WaitForSeconds(0.1f);
        if (currentLevelCanvas != null)
        {
            var tiler = currentLevelCanvas.GetComponent<LevelTiler>();
            if (tiler != null) tiler.SetWorldMapVisible(false);
            currentLevelCanvas.gameObject.SetActive(false);
        }
    }
    private IEnumerator ShowMapFromEquip()
    {
        operating = true;
        CloseDoor();
        yield return new WaitForSeconds(FrameUIAnimations.DoorDuration);
        if (currentSubCanvas != null) Destroy(currentSubCanvas.gameObject);
        if (currentLevelCanvas != null)
        {
            currentLevelCanvas.gameObject.SetActive(true);
            var tiler = currentLevelCanvas.GetComponent<LevelTiler>();
            if (tiler != null) tiler.SetWorldMapVisible(true);
        }
        OpenDoor();
        operating = false;
    }
    private IEnumerator ShowEnemyCanvas()
    {
        operating = true;
        if (frameUI != null) frameUI.OpenPage(EnemyCanvasPrefab, page => currentSubCanvas = page);
        yield return new WaitForSeconds(FrameUIAnimations.DoorDuration);
        operating = false;
    }
    private IEnumerator ShowCapsuleCanvas()
    {
        operating = true;
        if (frameUI != null)
        {
            frameUI.OpenPage(
                CapsuleDrawCanvasPrefab,
                page => currentSubCanvas = page,
                null,
                FrameUIDisplayer.DoorAction.None
            );
        }
        yield return new WaitForSeconds(FrameUIAnimations.DoorDuration);
        operating = false;
    }
    private IEnumerator ShowStorageCanvas()
    {
        operating = true;
        if (frameUI != null) frameUI.OpenPage(StorageCanvasPrefab, page => currentSubCanvas = page);
        yield return new WaitForSeconds(FrameUIAnimations.DoorDuration);
        operating = false;
    }
    private IEnumerator ShowBontiqueCanvas()
    {
        operating = true;
        if (frameUI != null)
        {
            frameUI.OpenPage(
                BontiqueCanvasPrefab,
                page => currentSubCanvas = page,
                null,
                FrameUIDisplayer.DoorAction.None
            );
        }
        yield return new WaitForSeconds(FrameUIAnimations.DoorDuration);
        operating = false;
    }
    private IEnumerator ReturnBaseFromSub()
    {
        operating = true;
        if (frameUI != null)
        {
            frameUI.ReturnToPrevious(FrameUIDisplayer.DoorAction.Close);
            yield return new WaitForSeconds(FrameUIAnimations.DoorDuration);
        }
        currentSubCanvas = null;
        operating = false;
    }
    private void FixBaseOutofSight()
    {
        leftPinned.anchoredPosition = new Vector2(-3000, 0);
        rightPinned.anchoredPosition = new Vector2(3000, 0);
    }
    private void OpenDoor()
    {
        if (frameUI != null) frameUI.OpenDoor();
    }
    private void CloseDoor()
    {
        if (frameUI != null) frameUI.CloseDoor();
    }
    private void DirectToMap()
    {
        FixBaseOutofSight();
        string loadPath= $"LevelData/Chapters/" +
            $"{PlayerPrefs.GetString(UXPref.ChapterName)}/" +
            $"{PlayerPrefs.GetString(UXPref.SectionName)}";
        try
        {
            LoadMap(Resources.Load<MapInfo>(loadPath));
        }
        catch { Debug.LogError($"No map info at path: {loadPath}"); };
    }
    public override IEnumerator OnEnter()
    {
        if (canvasSize.x <= 0f)
        {
            RectTransform canvasRect = GetComponent<RectTransform>();
            if (canvasRect != null) canvasSize = canvasRect.sizeDelta;
        }

        float swapdistance = canvasSize.x / 1.5f;
        float t = 0;
        float T = 0.5f;
        while (t < T)
        {
            t += Time.deltaTime;
            leftPinned.anchoredPosition = new Vector2(-swapdistance * (T - t) * (T - t) / T / T - canvasSize.x / 2, 0);
            rightPinned.anchoredPosition = new Vector2(swapdistance * (T - t) * (T - t) / T / T + canvasSize.x / 2, 0);
            yield return new WaitForFixedUpdate();
        }

        leftPinned.anchoredPosition = new Vector2(-canvasSize.x / 2, 0);
        rightPinned.anchoredPosition = new Vector2(canvasSize.x / 2, 0);
    }

    public override IEnumerator OnExit()
    {
        if (canvasSize.x <= 0f)
        {
            RectTransform canvasRect = GetComponent<RectTransform>();
            if (canvasRect != null) canvasSize = canvasRect.sizeDelta;
        }

        float swapdistance = canvasSize.x / 1.5f;
        float t = 0;
        float T = 0.5f;
        while (t < T)
        {
            t += Time.deltaTime;
            leftPinned.anchoredPosition = new Vector2(-swapdistance * t * t / T / T - canvasSize.x / 2, 0);
            rightPinned.anchoredPosition = new Vector2(swapdistance * t * t / T / T + canvasSize.x / 2, 0);
            yield return new WaitForFixedUpdate();
        }

        leftPinned.anchoredPosition = new Vector2(-canvasSize.x-swapdistance, 0);
        rightPinned.anchoredPosition = new Vector2(canvasSize.x+swapdistance, 0);
    }

    public override void Initialize(FrameUIDisplayer frameUI)
    {
        base.Initialize(frameUI);
        this.frameUI = frameUI;
    }

    public override string GetPageBgmName()
    {
        string chapterName = PlayerPrefs.GetString(UXPref.ChapterName);
        string chapterBgm = BGMTool.ResolveBaseMapBgmName(chapterName);
        if (!string.IsNullOrEmpty(chapterBgm)) return chapterBgm;
        return BGMTool.NormalizeBgmAddress(base.GetPageBgmName());
    }
}
