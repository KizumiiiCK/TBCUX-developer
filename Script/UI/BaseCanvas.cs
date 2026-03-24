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
    private GameObject currentMap;
    [Header("Initializers")]
    [SerializeField] private FrameUIDisplayer frameUI;
    [SerializeField] private Button StartBtn;
    [SerializeField] private Button UpgradeBtn;
    [SerializeField] private Button EquipBtn;
    [SerializeField] private Button CatBtn;
    [SerializeField] private Button EnemyBtn;
    [SerializeField] private Button MedalBtn;
    [SerializeField] private Button StorageBtn;
    [SerializeField] private Button CatCapsBtn;
    //[SerializeField] private Button EnterMapBtn;

    private Vector2 canvasSize;
    private bool operating = false;

    private const string SectionCanvasPrefab = "SectionCanvas";
    private const string LevelCanvasPrefab = "LevelsCanvas";
    private const string UpgradeCanvasPrefab = "UpgradeCanvas";
    private const string EnemyCanvasPrefab = "EnemyIndexCanvas";
    private const string EquipCanvasPrefab = "EquipCanvas";
    private const string CapsuleDrawCanvasPrefab = "DrawCapsuleCanvas";
    private const string StorageCanvasPrefab = "StorageCanvas";

    void Start()
    {
        Application.targetFrameRate = 30;
        PositionCorners();
        AddButtonListener();
        if (PlayerPrefs.GetInt(UXPref.DirectMark, 0) == 1) DirectToMap();
        //PlayerPrefs.DeleteKey(UXPref.DirectMark);
        Instantiate(Resources.Load<GameObject>("UI/Tag In"));
        Instantiate(Resources.Load<GameObject>("UI/Pages/CheckInCanvas"));
        RewardingSystem.GainReward(RewardName.XP, 0);
        UpdateCurrencies();
    }
    private void Awake()
    {
        Input.multiTouchEnabled = false;
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
        //MedalBtn.onClick.AddListener(delegate { StartCoroutine(ShowNextCanvas("Medal")); });
        StorageBtn.onClick.AddListener(delegate { if (operating) return; ToStorageCanvas(); });
        //CatCapsBtn.onClick.AddListener(delegate { StartCoroutine(ShowNextCanvas("CatCapsule")); });
        //RareCapsBtn.onClick.AddListener(delegate { StartCoroutine(ShowNextCanvas("RareCapsule")); });
    }
    public void UpdateCurrencies()
    {
        if (frameUI != null) frameUI.RefreshCurrencyAmounts();
    }
    public void ToSectionCanvas() { StartCoroutine(ShowSectionPage()); }
    public void LoadMap(MapInfo map)
    {
        try
        {
            currentMap = Instantiate(Resources.Load<GameObject>($"LevelData/Maps/{map.mapName}"), Vector3.zero, Quaternion.identity);
        }
        catch
        {
            Debug.LogError($"Error loading map: {map.mapName}");
        }
        if (frameUI != null)
        {
            frameUI.OpenPage(LevelCanvasPrefab, page =>
            {
                currentLevelCanvas = page;
                var tiler = page != null ? page.GetComponent<LevelTiler>() : null;
                if (tiler != null) tiler.SetMapInfo(map);
            });
        }
        OpenDoor();
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
        if (currentMap != null)
        {
            Destroy(currentMap);
            currentMap = null;
        }
        currentLevelCanvas = null;
        if (frameUI != null) frameUI.ReturnToPrevious();
    }
    public void ToUpgradeCanvas() { StartCoroutine(ShowUpgradeCanvas()); }
    public void SubBacktoBase() { StartCoroutine(ReturnBaseFromSub()); Instantiate(Resources.Load<GameObject>("UI/CheckInCanvas")); }
    public void ToEnemyCanvas() { StartCoroutine(ShowEnemyCanvas()); }
    public void ToCapsuleDrawCanvas() { StartCoroutine(ShowCapsuleCanvas()); }
    public void ToStorageCanvas() { StartCoroutine(ShowStorageCanvas()); }
    public void MapToEquip(string[] enemies=null) { StartCoroutine(ShowEquipFromMap(enemies)); }
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
                FrameUIDisplayer.DoorAction.Open
            );
        }
        yield return new WaitForSeconds(FrameUIAnimations.DoorDuration);
        operating = false;
    }
    
    private IEnumerator ShowEquipCanvas()
    {
        operating = true;
        if (frameUI != null) frameUI.OpenPage(EquipCanvasPrefab, page => currentSubCanvas = page);
        OpenDoor();
        yield return new WaitForSeconds(FrameUIAnimations.DoorDuration);
        operating = false;
    }
    private IEnumerator ShowEquipFromMap(string[] enemies=null)
    {
        CloseDoor();
        yield return new WaitForSeconds(FrameUIAnimations.DoorDuration);
        if (frameUI != null)
        {
            frameUI.OpenPage(EquipCanvasPrefab, page =>
            {
                currentSubCanvas = page;
                if (enemies != null && page != null)
                {
                    var equip = page.GetComponent<EquipCanvas>();
                    if (equip != null) equip.ChanageSEBShowInfo(enemies);
                }
            });
        }
        yield return new WaitForSeconds(0.1f);
        if (currentLevelCanvas != null) currentLevelCanvas.gameObject.SetActive(false);
        if (currentMap != null) currentMap.SetActive(false);
        OpenDoor();
    }
    private IEnumerator ShowMapFromEquip()
    {
        operating = true;
        CloseDoor();
        yield return new WaitForSeconds(FrameUIAnimations.DoorDuration);
        if (currentSubCanvas != null) Destroy(currentSubCanvas.gameObject);
        if (currentLevelCanvas != null) currentLevelCanvas.gameObject.SetActive(true);
        if (currentMap != null) currentMap.SetActive(true);
        OpenDoor();
        operating = false;
    }
    private IEnumerator ShowEnemyCanvas()
    {
        operating = true;
        if (frameUI != null) frameUI.OpenPage(EnemyCanvasPrefab, page => currentSubCanvas = page);
        OpenDoor();
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
                FrameUIDisplayer.DoorAction.Open
            );
        }
        yield return new WaitForSeconds(FrameUIAnimations.DoorDuration);
        operating = false;
    }
    private IEnumerator ShowStorageCanvas()
    {
        operating = true;
        if (frameUI != null) frameUI.OpenPage(StorageCanvasPrefab, page => currentSubCanvas = page);
        OpenDoor();
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
    public Transform GetCurrentMap()
    {
        if (currentMap == null) return null;
        else return currentMap.transform;
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
        return base.GetPageBgmName();
    }
}
