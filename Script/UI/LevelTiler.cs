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

    [Header("Cleared Team")]
    /// <summary>开关式按钮：用本关记录的通关队伍出击。文字和边框色都由 KiButton 自己带，不用外挂 TMP_Text。</summary>
    [SerializeField] private KiButton ClearTeamBtn;
    [SerializeField] private Color clearTeamSelectedColor = new Color(1f, 0.85f, 0.35f);
    /// <summary>没有通关记录、按钮不可点时的边框色。KiButton 不重写 disabled 视觉，只靠 Button
    /// 的 transition，预制体上如果是 None 就完全看不出禁用，所以颜色在代码里显式压下去。</summary>
    [SerializeField] private Color clearTeamDisabledColor = new Color(0.22f, 0.22f, 0.24f);

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
    private const string ClearTeamUseTextId = "id:clear_team_use";
    private const string ClearTeamConfirmTextId = "id:clear_team_confirm";
    private readonly List<GameObject> spawnedMapPoints = new List<GameObject>();
    private readonly List<GameObject> spawnedLevelTiles = new List<GameObject>();
    private readonly Dictionary<string, LevelData> levelDataCache = new Dictionary<string, LevelData>();
    /// <summary>关卡大地图根物体（由本组件创建与销毁，不再由 BaseCanvas 管理）。</summary>
    private GameObject worldMapRoot;
    private Coroutine buildLevelTilesRoutine;
    private bool isDailyMapLocked;
    /// <summary>本关是否有可用的通关队伍记录（按 cleared_teams 行非空判定，不分难度）。</summary>
    private bool clearedTeamAvailable;
    private bool useClearedTeam;
    private string clearTeamUseText = ClearTeamUseTextId;
    private string clearTeamConfirmText = ClearTeamConfirmTextId;
    /// <summary>未选定时的边框色，取自预制体里配的 initialColor（见 RefreshClearTeamButton）。</summary>
    private Color clearTeamNormalColor = Color.white;
    private bool clearTeamNormalColorCached;
    /// <summary>
    /// KiButton 的 Awake 才会记下 label 的原始字号，SetText 不传 size 时要拿它回填。
    /// 首次激活时本组件的 OnEnable 早于 KiButton.Awake，那时调 SetText 会把字号写成 0，
    /// 所以按钮的外观刷新一律等到 Start（所有 Awake 都跑完了）之后才开始。
    /// </summary>
    private bool clearTeamBtnReady;

    public GameProgressSave.SectionClearList secClearList;

    private void Start()
    {
        cam=GameObject.FindGameObjectWithTag("MainCamera").GetComponent<Camera>();
        GetComponent<Canvas>().worldCamera = cam;
        InitializeMap();
        TeamBtn.onClick.AddListener(SwitchTeamBtn);
        ShowteamBtn.onClick.AddListener(ToggleSelectionsPanel);
        CombatBtn.onClick.AddListener(LaunchAttack);
        if (ClearTeamBtn != null) ClearTeamBtn.onClick.AddListener(OnClearTeamBtnClicked);
        clearTeamBtnReady = true;
        RefreshClearTeamButton();
        CacheClearTeamTexts();
        cam.backgroundColor = MI.coverColor;
        InitializeDragSelector();
        RefreshDailyMapChallengeState();
    }
    private void OnEnable()
    {
        // 每次回到选关页都回到「不选通关队伍」。
        ResetClearTeamSelection();
        // 装备页可能换了队伍或改了队名，队名标签和格子都要重读存档。
        // 这里必须无条件刷：ResetClearTeamSelection 只在原本是选定态时才刷标签。
        RefreshTeamLabel();
        if (selectionsPanel != null) selectionsPanel.ReloadFromSave();
        TeamBtn.interactable = true;
        RestoreMapVisibility();
        RefreshDailyMapChallengeState();
    }

    /// <summary>
    /// 两条按钮文案都在 UI Elements 表里，本地化是异步的：先用 id 占位，回调到了再写进去。
    /// 回调可能晚于销毁，所以用组件是否还在做闸。
    /// </summary>
    private void CacheClearTeamTexts()
    {
        LocalizationHelper.GetLocalizedText(UXPref.Localized_UI, ClearTeamUseTextId, localized =>
        {
            if (this == null) return;
            clearTeamUseText = localized ?? ClearTeamUseTextId;
            RefreshClearTeamButton();
        });
        LocalizationHelper.GetLocalizedText(UXPref.Localized_UI, ClearTeamConfirmTextId, localized =>
        {
            if (this == null) return;
            clearTeamConfirmText = localized ?? ClearTeamConfirmTextId;
            RefreshClearTeamButton();
        });
    }

    /// <summary>
    /// 取当前关卡记录的通关队伍。cleared_teams 不分难度，所以这里也不带难度参数。
    /// 直接用已经读进内存的 secClearList，不再走 LoadSectionProgress（那条路会重写整个存档）。
    /// </summary>
    private string[] GetClearedTeamForCurrentLevel()
    {
        if (secClearList == null) return null;
        return GameProgressSave.ExtractClearedTeam(secClearList, current_level_num);
    }

    /// <summary>只判断有没有记录，不取内容：拖动选关时每跨一格都要问，这条路上不能有分配。</summary>
    private bool HasClearedTeamForCurrentLevel()
    {
        return secClearList != null && GameProgressSave.HasClearedTeam(secClearList, current_level_num);
    }

    /// <summary>进页面、换关卡、建完关卡列表时都调：一律回到「不选」，并按新关卡重算能不能选。</summary>
    private void ResetClearTeamSelection()
    {
        bool available = HasClearedTeamForCurrentLevel();
        bool wasSelected = useClearedTeam;
        useClearedTeam = false;
        if (wasSelected)
        {
            if (selectionsPanel != null) selectionsPanel.ExitPreview();
            RefreshTeamLabel();
        }
        // 这里是拖动选关的热路径（每跨半格 LevelDragSelector 就回调一次）。可用性没变就别去重刷
        // KiButton：SetText 会触发 TMP 重建，SetFrameColorPersistent 会重扫并重写 9 个边框 Image。
        if (available == clearedTeamAvailable && !wasSelected) return;
        clearedTeamAvailable = available;
        RefreshClearTeamButton();
    }

    private void RefreshClearTeamButton()
    {
        if (!clearTeamBtnReady || ClearTeamBtn == null) return;
        // 未选定色取预制体里配的边框色：SetFrameColorPersistent 会把 initialColor 一起改掉，
        // 所以必须在第一次染色之前记下来，否则原值就找不回来了。
        if (!clearTeamNormalColorCached)
        {
            clearTeamNormalColor = ClearTeamBtn.GetInitialColor();
            clearTeamNormalColorCached = true;
        }
        ClearTeamBtn.interactable = clearedTeamAvailable;
        ClearTeamBtn.SetText(useClearedTeam ? clearTeamConfirmText : clearTeamUseText);
        // 禁用优先于选定：不可点时一律灰黑，玩家一眼就知道这关没通关记录。
        Color frame = !clearedTeamAvailable ? clearTeamDisabledColor
            : useClearedTeam ? clearTeamSelectedColor : clearTeamNormalColor;
        ClearTeamBtn.SetFrameColorPersistent(frame);
    }

    /// <summary>地图上的队名标签：选定通关队伍时显示固定文字，否则显示 pref 索引那一队的队名。</summary>
    private void RefreshTeamLabel()
    {
        if (team_txt == null) return;
        if (useClearedTeam)
        {
            team_txt.text = EquipTeamSelectionPanel.ClearedTeamPreviewLabel;
            return;
        }
        int teamIndex = PlayerPrefs.GetInt(SelectionsSave.pref_teamnum, 0);
        team_txt.text = TeamNameSave.GetTeamNameOrDefault(teamIndex);
        if (selectionsPanel != null) selectionsPanel.SetTeamDisplay(teamIndex, team_txt.text);
    }

    private void OnClearTeamBtnClicked()
    {
        if (!clearedTeamAvailable) return;
        SetUseClearedTeam(!useClearedTeam);
    }

    private void SetUseClearedTeam(bool use)
    {
        useClearedTeam = use && clearedTeamAvailable;
        ApplyClearedTeamPreview();
        RefreshClearTeamButton();
        RefreshTeamLabel();
    }

    /// <summary>把选定状态同步到（可能没打开的）队伍面板上。</summary>
    private void ApplyClearedTeamPreview()
    {
        if (selectionsPanel == null) return;
        if (useClearedTeam)
        {
            string[] team = GetClearedTeamForCurrentLevel();
            if (team != null) selectionsPanel.ShowPreviewTeam(team, OnClearedTeamPreviewCancelled);
        }
        else
        {
            selectionsPanel.ExitPreview();
        }
    }

    /// <summary>
    /// 面板里左右换队时由面板回调。面板此刻已经自己退出预览并重载了存档队伍，
    /// 这里只更新本页状态和显示，不能再回头调面板，否则就绕回去了。
    /// </summary>
    private void OnClearedTeamPreviewCancelled()
    {
        useClearedTeam = false;
        RefreshClearTeamButton();
        RefreshTeamLabel();
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
        // 列表建完（含中途 break）才知道最终停在哪一关，这时重算一次按钮可用性。
        ResetClearTeamSelection();
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
        Image bc=Instantiate(Resources.Load<GameObject>("UI/ZDKS")).transform.GetChild(1).GetComponent<Image>();
        IReadOnlyList<Sprite> portraits = DialoguePortraitCatalog.GetVisiblePortraits();
        if (portraits.Count > 0)
        {
            int index = Mathf.Clamp(PlayerPrefs.GetInt("base_character", 0), 0, portraits.Count - 1);
            bc.sprite = portraits[index];
        }
        PlayerPrefs.SetInt(UXPref.LevelNum, current_level_num);
        // 通关队伍标记由本页独占：每次出击都显式写或删，绝不留残值给下一关。
        if (useClearedTeam && HasClearedTeamForCurrentLevel()) PlayerPrefs.SetInt(UXPref.UseClearedTeam, 1);
        else PlayerPrefs.DeleteKey(UXPref.UseClearedTeam);
        this.enabled = false;
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
                // 选关页的格子只做展示：这里换位/移除没有意义，编队只在 EquipCanvas 里做。
                selectionsPanel.SetSlotEditing(false);
                // 面板是每次开关都重建的，打开时要把当前的选定状态补上。
                ApplyClearedTeamPreview();
            }
            return;
        }

        Destroy(selectionsPanelInstance);
        selectionsPanelInstance = null;
        selectionsPanel = null;
    }

    private void OnPanelTeamStateChanged(int teamIndex, string teamName)
    {
        // 选定通关队伍时地图标签固定显示 CLEARED TEAMS，不能被面板的队伍通知盖回真实队名。
        // 面板每次打开都会 Initialize→LoadCurrentTeamFromPrefs→通知一次，没这道闸就会把标签打回去。
        if (useClearedTeam) return;
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
        // 换关卡就等于换了一份通关记录，选定必须作废。
        ResetClearTeamSelection();
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
        if (MI == null || !MI.HasDailyTimesLimit)
        {
            isDailyMapLocked = false;
            return;
        }

        string currentDateToken = CheckInSystem.GetCachedWorldDateToken();
        isDailyMapLocked = DailyMapChallengeSave.HasReachedDailyLimit(currentDateToken, MI.sectionName, MI.timesLimit);
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
