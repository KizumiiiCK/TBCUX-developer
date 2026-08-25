using System.Collections;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class LevelTiler : UICanvasMain
{
    [Header("Map Prefabs")]
    [SerializeField] private GameObject LevelPrefab;
    [SerializeField] private GameObject mapPoints;

    [Header("Team Preview")]
    [SerializeField] private Button TeamBtn;
    [SerializeField] private TMP_Text team_txt;
    [SerializeField] private Button ShowteamBtn;

    [Header("Battle Controls")]
    [SerializeField] private Button CombatBtn;

    [Header("Panels")]
    [SerializeField] private GameObject characterBoard;    
    [SerializeField] private ShowEnemyBoard SEB;
    [SerializeField] private LevelRewardBoard LRB;
    [SerializeField] private LevelRestrictionBoard levelRestrictionBoard;

    [Header("Drag")]
    [SerializeField] private LevelDragSelector dragSelector;
    [SerializeField] private RectTransform target;

    [Header("Map Scroll")]
    public float minX = 0f;
    public float maxX = 375f;
    public static float moveSpeed = 5f;
    public static float level_tile_gap = 375;

    private Camera cam;
    private MapInfo MI;
    private int current_level_num = 0;

    private GameObject selectionsPanelInstance;
    private EquipTeamSelectionPanel selectionsPanel;
    private string[,] enemyAppears;
    private int[,] enemyMultipliers;
    private List<Reward[]> rewardlist=new List<Reward[]>();
    private readonly List<string[]> restrictionList = new List<string[]>();
    private int mapSectionDifficulty;
    private const string CatSelectionsPrefabPath = "UI/FunctionalPanels/Cat Selections";
    private const string RestrictionWarningPrefabPath = "UI/FunctionalPanels/WarningMark";
    private readonly List<GameObject> spawnedMapPoints = new List<GameObject>();
    private readonly List<GameObject> spawnedLevelTiles = new List<GameObject>();
    private readonly Dictionary<string, LevelData> levelDataCache = new Dictionary<string, LevelData>();
    /// <summary>关卡大地图根物体（由本组件创建与销毁，不再由 BaseCanvas 管理）。</summary>
    private GameObject worldMapRoot;
    private Coroutine buildLevelTilesRoutine;
    private bool isDailyMapLocked;

    public GameProgressSave.SectionClearList secClearList;

    private void Start()
    {
        cam=GameObject.FindGameObjectWithTag("MainCamera").GetComponent<Camera>();
        GetComponent<Canvas>().worldCamera = cam;
        InitializeMap();
        TeamBtn.onClick.AddListener(SwitchTeamBtn);
        ShowteamBtn.onClick.AddListener(ToggleSelectionsPanel);
        CombatBtn.onClick.AddListener(LaunchAttack);
        cam.backgroundColor = MI.coverColor;
        InitializeDragSelector();
        RefreshDailyMapChallengeState();
    }
    private void OnEnable()
    {
        team_txt.text = TeamNameSave.GetTeamNameOrDefault(PlayerPrefs.GetInt(SelectionsSave.pref_teamnum, 0));
        if (selectionsPanel != null) selectionsPanel.SetTeamDisplay(PlayerPrefs.GetInt(SelectionsSave.pref_teamnum, 0), team_txt.text);
        TeamBtn.interactable = true;
        RestoreMapVisibility();
        RefreshDailyMapChallengeState();
    }
    void Update()
    {
        if (CombatBtn != null)
        {
            CombatBtn.interactable = !isDailyMapLocked && (dragSelector == null || dragSelector.IsSettled);
        }
        cam.transform.position = Vector2.Lerp(cam.transform.position, MI.levelsOnMap[current_level_num].levelPosition, Time.deltaTime * moveSpeed);
        cam.transform.position = new Vector3(cam.transform.position.x, cam.transform.position.y, -10);
    }
    public void SetMapInfo(MapInfo mi) { MI = mi; }
    public void SetLevelMapSize(int levelNums)
    {
        minX = -levelNums * level_tile_gap;
        maxX = 0;
        if (dragSelector != null) dragSelector.SetBounds(minX, maxX);
    }
    private void InitializeMap()
    {
        if (MI == null)
        {
            Debug.LogError("LevelTiler.InitializeMap: MapInfo (MI) is null.");
            return;
        }

        GameObject mapPrefab = Resources.Load<GameObject>($"LevelData/Maps/{MI.mapName}");
        if (mapPrefab == null)
        {
            Debug.LogError($"LevelTiler: missing map prefab Resources/LevelData/Maps/{MI.mapName}");
            return;
        }
        worldMapRoot = Instantiate(mapPrefab, Vector3.zero, Quaternion.identity);
        Transform mapt = worldMapRoot.transform;

        string chapter = PlayerPrefs.GetString(UXPref.ChapterName, UXPref.DefaultChapterName);
        string sectionName= PlayerPrefs.GetString(UXPref.SectionName);
        int sectionNum= PlayerPrefs.GetInt(UXPref.SectionNum, 0);
        int diff= PlayerPrefs.GetInt(UXPref.Difficulty, 0);
        mapSectionDifficulty = diff;
        //
        int mark_label = 0;
        if(sectionName=="0_worldi"|| sectionName == "0_worldii" || sectionName == "0_worldiii") mark_label = 60;
        //
        string levelLoadPath = $"LevelData/LevelEnemyData/{chapter}/{sectionName}/dif{diff}/";
        secClearList = GameProgressSave.LoadSectionProgress(chapter, sectionName);
        enemyAppears = new string[MI.levelsOnMap.Length,20];
        enemyMultipliers = new int[MI.levelsOnMap.Length, 20];
        int exact_maplength = MI.levelsOnMap.Length;
        for (int i = 0; i < exact_maplength; i++)
        {
            LevelPoint lp = Instantiate(mapPoints, MI.levelsOnMap[i].levelPosition, Quaternion.identity).GetComponent<LevelPoint>();
            lp.transform.SetParent(mapt);
            spawnedMapPoints.Add(lp.gameObject);
            if (i > 0) lp.SetPathLine(MI.levelsOnMap[i - 1].levelPosition);
            if (secClearList.clear_times[diff, i] > 0) lp.UnlockPoint();
        }
        if (buildLevelTilesRoutine != null) StopCoroutine(buildLevelTilesRoutine);
        buildLevelTilesRoutine = StartCoroutine(BuildLevelTilesRoutine(exact_maplength, diff, mark_label, levelLoadPath));
    }
    private IEnumerator BuildLevelTilesRoutine(int mapLength, int diff, int markLabel, string levelLoadPath)
    {
        const int buildBatchSize = 8;
        int directMark = PlayerPrefs.GetInt(UXPref.DirectMark, 0);
        int directLevel = PlayerPrefs.GetInt(UXPref.LevelNum);
        for (int i = 0; i < mapLength; i++)
        {
            RectTransform lvl = Instantiate(LevelPrefab, Vector3.zero, Quaternion.identity).GetComponent<RectTransform>();
            lvl.SetParent(target);
            spawnedLevelTiles.Add(lvl.gameObject);
            lvl.anchoredPosition = new Vector2(i * level_tile_gap, 350);
            Level levelComponent = lvl.GetComponent<Level>();
            levelComponent.SetLevelInfo(MI.levelsOnMap[i]);
            levelComponent.SetLT(this);
            levelComponent.SetClearedInfo(secClearList.clear_times[diff, i], secClearList.level_score[diff, i]);
            if (markLabel > 0 && secClearList.reward_gained[i])
            {
                levelComponent.SetMark(markLabel);
            }

            LevelData levelData = GetCachedLevelData(levelLoadPath + i);
            if (levelData == null)
            {
                Debug.LogError($"Error Loading level {i}.");
                int validLastIndex = Mathf.Max(0, i - 1);
                SetLevelMapSize(validLastIndex);
                MoveToLevel(directMark == 1 ? directLevel : validLastIndex);
                break;
            }
            SetEnemyAppears(i, levelData);
            AttachRestrictionWarningMarkIfNeeded(lvl, levelData);
            if (secClearList.clear_times[diff, i] <= 0)
            {
                SetLevelMapSize(i);
                MoveToLevel(directMark == 1 ? directLevel : i);
                break;
            }
            if (i == mapLength - 1)
            {
                SetLevelMapSize(mapLength - 1);
                MoveToLevel(directMark == 1 ? directLevel : mapLength - 1);
            }
            if ((i + 1) % buildBatchSize == 0)
            {
                yield return null;
            }
        }
        PlayerPrefs.DeleteKey(UXPref.DirectMark);
        buildLevelTilesRoutine = null;
    }
    private LevelData GetCachedLevelData(string path)
    {
        if (levelDataCache.TryGetValue(path, out LevelData cached) && cached != null)
        {
            return cached;
        }
        LevelData loaded = Resources.Load<LevelData>(path);
        levelDataCache[path] = loaded;
        return loaded;
    }
    private void MoveToLevel(int levelnum)
    {
        int clamped = Mathf.Max(0, levelnum);
        ChangeCurrentLevelNum(clamped);
        if (dragSelector != null)
        {
            dragSelector.MoveToLevel(clamped, true);
        }
        else if (target != null)
        {
            float x = Mathf.Clamp(-clamped * level_tile_gap, minX, maxX);
            target.anchoredPosition = new Vector2(x, target.anchoredPosition.y);
        }
    }
    private void OnDestroy()
    {
        if (buildLevelTilesRoutine != null)
        {
            StopCoroutine(buildLevelTilesRoutine);
            buildLevelTilesRoutine = null;
        }
        if (selectionsPanelInstance != null) Destroy(selectionsPanelInstance);
        ReleaseMapObjects();
        if (cam!=null)cam.transform.position =new Vector3(0,0,-10);
    }

    private void ReleaseMapObjects()
    {
        if (worldMapRoot != null)
        {
            Destroy(worldMapRoot);
            worldMapRoot = null;
        }
        spawnedMapPoints.Clear();

        for (int i = 0; i < spawnedLevelTiles.Count; i++)
        {
            if (spawnedLevelTiles[i] != null) Destroy(spawnedLevelTiles[i]);
        }
        spawnedLevelTiles.Clear();
        levelDataCache.Clear();
    }

    /// <summary>与关卡 UI 分离时隐藏/显示大地图（例如从关卡页进入装备页）。</summary>
    public void SetWorldMapVisible(bool visible)
    {
        if (worldMapRoot != null) worldMapRoot.SetActive(visible);
    }
    private void LaunchAttack()
    {
        // 出击横幅的立绘可能尚未下载，异步确保后再显示
        StartCoroutine(LaunchAttackRoutine());
    }

    private IEnumerator LaunchAttackRoutine()
    {
        PlayerPrefs.SetInt(UXPref.LevelNum, current_level_num);
        this.enabled = false;

        Image bc = Instantiate(Resources.Load<GameObject>("UI/ZDKS")).transform.GetChild(1).GetComponent<Image>();
        yield return DialoguePortraitCatalog.EnsureLoadedRoutine();

        IReadOnlyList<Sprite> portraits = DialoguePortraitCatalog.GetVisiblePortraits();
        if (portraits.Count > 0 && bc != null)
        {
            int index = Mathf.Clamp(PlayerPrefs.GetInt("base_character", 0), 0, portraits.Count - 1);
            bc.sprite = portraits[index];
        }
    }
    private void ToggleSelectionsPanel()
    {
        if (selectionsPanelInstance == null)
        {
            var prefab = Resources.Load<GameObject>(CatSelectionsPrefabPath);
            if (prefab == null)
            {
                Debug.LogError($"Missing prefab: {CatSelectionsPrefabPath}");
                return;
            }
            selectionsPanelInstance = Instantiate(prefab, transform);
            RectTransform panelRect = selectionsPanelInstance.GetComponent<RectTransform>();
            if (panelRect != null)
            {
                panelRect.anchoredPosition = new Vector2(360, -300);
                panelRect.localScale = Vector3.one*0.75f;
            }
            selectionsPanelInstance.SetActive(true);
            selectionsPanelInstance.transform.SetAsLastSibling();
            selectionsPanel = selectionsPanelInstance.GetComponentInChildren<EquipTeamSelectionPanel>(true);
            if (selectionsPanel != null)
            {
                selectionsPanel.Initialize(
                    null,
                    null,
                    null,
                    null,
                    null,
                    OnPanelTeamStateChanged
                );
                selectionsPanel.SetTeamDisplay(PlayerPrefs.GetInt(SelectionsSave.pref_teamnum, 0), TeamNameSave.GetTeamNameOrDefault(PlayerPrefs.GetInt(SelectionsSave.pref_teamnum, 0)));
            }
            return;
        }

        Destroy(selectionsPanelInstance);
        selectionsPanelInstance = null;
        selectionsPanel = null;
    }

    private void OnPanelTeamStateChanged(int teamIndex, string teamName)
    {
        team_txt.text = TeamNameSave.NormalizeTeamName(teamIndex, teamName);
    }
    public void SetEnemyAppears(int levelNum, LevelData led)
    {
        for (int i = 0; i < led.enemySummoners.Length; i++)
        {
            for (int j = 0; j < led.enemySummoners[i].enemySummonInfos.Length; j++)
            {
                string e = led.enemySummoners[i].enemySummonInfos[j].enemyID;
                int ratio = led.enemySummoners[i].enemySummonInfos[j].ratio;
                for (int k = 0; k < 16; k++)
                {
                    if (enemyAppears[levelNum,k] == null || enemyAppears[levelNum,k] == string.Empty)
                    {
                        enemyAppears[levelNum, k] = e;
                        enemyMultipliers[levelNum, k] = ratio;
                        break;
                    }
                    if (enemyAppears[levelNum, k] == e) break;
                }
            }
        }
        rewardlist.Add(led.rewardlist);
        restrictionList.Add(led.Restriction != null ? led.Restriction : new string[0]);
    }
    private void ChangeCurrentLevelNum(int cln)
    {
        if (current_level_num == cln) return;
        current_level_num = cln;
        ChanageSEBShowInfo(current_level_num);
    }
    public void ShowSEB()
    {
        SEB.gameObject.SetActive(!SEB.gameObject.activeSelf);
        LRB.gameObject.SetActive(SEB.gameObject.activeSelf);
        if (levelRestrictionBoard != null)
            levelRestrictionBoard.gameObject.SetActive(SEB.gameObject.activeSelf && CurrentLevelHasRestrictions());
        ChanageSEBShowInfo(current_level_num);
    }
    public void ChanageSEBShowInfo(int level_num)
    {
        if (!SEB.gameObject.activeSelf) return;
        SEB.ShowEnemies(GetCurrentEnemies(level_num), GetCurrentEnemyMultipliers(level_num), ShouldBlindEnemyIconsForLevel(level_num));
        ChangeLRBInfo();
    }

    private bool ShouldBlindEnemyIconsForLevel(int level_num)
    {
        if (level_num < 0 || level_num >= restrictionList.Count) return false;
        if (!LevelRestrictionHelper.HasIvRestriction(restrictionList[level_num])) return false;
        if (secClearList == null || secClearList.clear_times == null) return false;
        int d0 = mapSectionDifficulty;
        if (d0 < 0 || d0 >= secClearList.clear_times.GetLength(0)) return false;
        if (level_num >= secClearList.clear_times.GetLength(1)) return false;
        return secClearList.clear_times[d0, level_num] <= 0;
    }
    public void ChangeLRBInfo()
    {
        LRB.SetRewards(rewardlist[current_level_num]);
        LRB.ShowLevelRewards(secClearList.reward_gained[current_level_num]);
        if (levelRestrictionBoard == null) return;
        if (!CurrentLevelHasRestrictions())
        {
            levelRestrictionBoard.gameObject.SetActive(false);
            return;
        }
        levelRestrictionBoard.gameObject.SetActive(true);
        levelRestrictionBoard.ShowRestrictions(restrictionList[current_level_num]);
    }

    private bool CurrentLevelHasRestrictions()
    {
        if (current_level_num < 0 || current_level_num >= restrictionList.Count) return false;
        string[] r = restrictionList[current_level_num];
        return r != null && r.Length > 0;
    }

    /// <summary>关卡有待生效的限制条件时在关卡卡角显示警告图标。</summary>
    private static void AttachRestrictionWarningMarkIfNeeded(RectTransform levelTile, LevelData led)
    {
        if (levelTile == null || led == null) return;
        string[] r = led.Restriction;
        if (r == null || r.Length == 0) return;

        GameObject prefab = Resources.Load<GameObject>(RestrictionWarningPrefabPath);
        if (prefab == null) return;

        GameObject instance = Instantiate(prefab, levelTile);
        RectTransform rt = instance.GetComponent<RectTransform>();
        if (rt != null)
        {
            rt.anchorMin = new Vector2(1f, 1f);
            rt.anchorMax = new Vector2(1f, 1f);
            rt.pivot = new Vector2(1f, 1f);
            rt.anchoredPosition = new Vector2(10f, -30f);
            rt.localScale = Vector3.one;
        }
        else
        {
            instance.transform.localPosition = new Vector3(120f, 120f, 0f);
        }
    }
    private string[] GetCurrentEnemies(int level_num)
    {
        int realLength = 0;
        for (int i = 0; i < enemyAppears.GetLength(1); i++) if (enemyAppears[level_num, i] == null || enemyAppears[level_num, i] == string.Empty) break; else realLength++;
        string[] ens = new string[realLength];
        for (int i = 0; i < realLength; i++) ens[i] = enemyAppears[level_num, i];
        return ens;
    }

    private int[] GetCurrentEnemyMultipliers(int level_num)
    {
        int realLength = 0;
        for (int i = 0; i < enemyAppears.GetLength(1); i++) if (enemyAppears[level_num, i] == null || enemyAppears[level_num, i] == string.Empty) break; else realLength++;
        int[] multipliers = new int[realLength];
        for (int i = 0; i < realLength; i++) multipliers[i] = enemyMultipliers[level_num, i];
        return multipliers;
    }

    private string[] GetCurrentRestrictions()
    {
        if (current_level_num < 0 || current_level_num >= restrictionList.Count) return null;
        return restrictionList[current_level_num];
    }
    private void SwitchTeamBtn()
    {
        TeamBtn.interactable = false; 
        GameObject.Find("BaseCanvas").GetComponent<BaseCanvas>().MapToEquip(
            GetCurrentEnemies(current_level_num),
            GetCurrentRestrictions(),
            ShouldBlindEnemyIconsForLevel(current_level_num));
    }

    public override IEnumerator OnEnter()
    {
        if (FrameUI != null)
        {
            FrameUI.OpenDoor();
            yield return new WaitForSecondsRealtime(FrameUIAnimations.DoorDuration);
        }
    }

    public override IEnumerator OnExit()
    {
        if (FrameUI != null)
        {
            FrameUI.CloseDoor();
            yield return new WaitForSecondsRealtime(FrameUIAnimations.DoorDuration);
        }
    }

    /// <summary>
    /// Page BGM address for Addressables BGM group (from MapInfo.BGM).
    /// </summary>
    public override string GetPageBgmName()
    {
        if (MI != null && !string.IsNullOrEmpty(MI.BGM))
            return BGMTool.NormalizeBgmAddress(MI.BGM);
        return BGMTool.NormalizeBgmAddress(base.GetPageBgmName());
    }

    private void InitializeDragSelector()
    {
        if (dragSelector == null) dragSelector = GetComponentInChildren<LevelDragSelector>(true);
        if (dragSelector != null)
        {
            dragSelector.Configure(
                target,
                minX,
                maxX,
                level_tile_gap,
                moveSpeed,
                OnDragLevelChanged,
                OnDragSettleStateChanged
            );
            dragSelector.MoveToLevel(current_level_num, true);
        }
    }

    private void RestoreMapVisibility()
    {
        if (worldMapRoot != null && !worldMapRoot.activeSelf)
            worldMapRoot.SetActive(true);
    }

    private void RefreshDailyMapChallengeState()
    {
        if (MI == null || !MI.oncePerDay)
        {
            isDailyMapLocked = false;
            return;
        }

        string currentDateToken = CheckInSystem.GetCachedWorldDateToken();
        isDailyMapLocked = DailyMapChallengeSave.HasSectionClearRecordToday(currentDateToken, MI.sectionName);
    }

    private void OnDragLevelChanged(int levelIndex)
    {
        ChangeCurrentLevelNum(levelIndex);
    }

    private void OnDragSettleStateChanged(bool settled)
    {
        if (CombatBtn != null) CombatBtn.interactable = !isDailyMapLocked && settled;
    }

    public void ExitLevelPage()
    {
        if (FrameUI != null)
        {
            FrameUI.ReturnToPrevious();
            return;
        }
        var baseCanvas = GameObject.Find("BaseCanvas")?.GetComponent<BaseCanvas>();
        if (baseCanvas != null) baseCanvas.ReturnFromMap();
    }
}
