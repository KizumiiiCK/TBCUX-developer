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
    [SerializeField] private GameObject characterBoard;    [SerializeField] private ShowEnemyBoard SEB;
    [SerializeField] private LevelRewardBoard LRB;

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
    private List<Reward[]> rewardlist=new List<Reward[]>();
    private const string CatSelectionsPrefabPath = "UI/FunctionalPanels/Cat Selections";
    private readonly List<GameObject> spawnedMapPoints = new List<GameObject>();
    private readonly List<GameObject> spawnedLevelTiles = new List<GameObject>();
    /// <summary>关卡大地图根物体（由本组件创建与销毁，不再由 BaseCanvas 管理）。</summary>
    private GameObject worldMapRoot;

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
    }
    private void OnEnable()
    {
        team_txt.text = TeamNameSave.GetTeamNameOrDefault(PlayerPrefs.GetInt(SelectionsSave.pref_teamnum, 0));
        if (selectionsPanel != null) selectionsPanel.SetTeamDisplay(PlayerPrefs.GetInt(SelectionsSave.pref_teamnum, 0), team_txt.text);
        TeamBtn.interactable = true;
        RestoreMapVisibility();
    }
    void Update()
    {
        if (CombatBtn != null)
        {
            CombatBtn.interactable = dragSelector == null || dragSelector.IsSettled;
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
        //
        int mark_label = 0;
        if(sectionName=="0_worldi"|| sectionName == "0_worldii" || sectionName == "0_worldiii") mark_label = 60;
        //
        string levelLoadPath = $"LevelData/LevelEnemyData/{chapter}/{sectionName}/dif{diff}/";
        secClearList = GameProgressSave.LoadSectionProgress(chapter, sectionName);
        enemyAppears = new string[MI.levelsOnMap.Length,16];
        int exact_maplength = MI.levelsOnMap.Length;
        for (int i = 0; i < exact_maplength; i++)
        {
            LevelPoint lp = Instantiate(mapPoints, MI.levelsOnMap[i].levelPosition, Quaternion.identity).GetComponent<LevelPoint>();
            lp.transform.SetParent(mapt);
            spawnedMapPoints.Add(lp.gameObject);
            if (i > 0) lp.SetPathLine(MI.levelsOnMap[i - 1].levelPosition);
            if (secClearList.clear_times[diff, i] > 0) lp.UnlockPoint();
        }
        for (int i=0;i< exact_maplength; i++)
        {
            RectTransform lvl=Instantiate(LevelPrefab, Vector3.zero, Quaternion.identity).GetComponent<RectTransform>();
            lvl.SetParent(target);//
            spawnedLevelTiles.Add(lvl.gameObject);
            lvl.anchoredPosition = new Vector2(i * level_tile_gap, 350);
            Level L = lvl.GetComponent<Level>();
            L.SetLevelInfo(MI.levelsOnMap[i]);
            L.SetLT(this);
            L.SetClearedInfo(secClearList.clear_times[diff, i], secClearList.level_score[diff,i]);
            if (mark_label > 0)
            {
                if (secClearList.reward_gained[i]) L.SetMark(mark_label);
            }
            try { 
                SetEnemyAppears(i, Resources.Load<LevelData>(levelLoadPath + i.ToString())); 
            }
            catch 
            { 
                Debug.LogError($"Error Loading level {i}."); 
                exact_maplength = i-1;
                SetLevelMapSize(exact_maplength);
                if (PlayerPrefs.GetInt(UXPref.DirectMark, 0) == 1)
                {
                    MoveToLevel(PlayerPrefs.GetInt(UXPref.LevelNum));
                }
                else
                {
                    MoveToLevel(exact_maplength);
                }
                break; 
            }

            if (secClearList.clear_times[diff, i] <= 0) 
            { 
                SetLevelMapSize(i);
                if (PlayerPrefs.GetInt(UXPref.DirectMark, 0) == 1)
                {
                    MoveToLevel(PlayerPrefs.GetInt(UXPref.LevelNum));
                }
                else
                {
                    MoveToLevel(i);
                }
                break; 
            }
            if (i == MI.levelsOnMap.Length - 1)
            {
                SetLevelMapSize(MI.levelsOnMap.Length - 1);
                if (PlayerPrefs.GetInt(UXPref.DirectMark, 0) == 1)
                {
                    MoveToLevel(PlayerPrefs.GetInt(UXPref.LevelNum));
                }
                else
                {
                    MoveToLevel(MI.levelsOnMap.Length - 1);
                }
            }
        }
        PlayerPrefs.DeleteKey(UXPref.DirectMark);
        
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
    }

    /// <summary>与关卡 UI 分离时隐藏/显示大地图（例如从关卡页进入装备页）。</summary>
    public void SetWorldMapVisible(bool visible)
    {
        if (worldMapRoot != null) worldMapRoot.SetActive(visible);
    }
    private void LaunchAttack()
    {
        Image bc=Instantiate(Resources.Load<GameObject>("UI/ZDKS")).transform.GetChild(1).GetComponent<Image>();
        Sprite[] chars_img = Resources.LoadAll<Sprite>("DialogueImage");
        List<Sprite> filtered = new List<Sprite>();
        foreach (var sprite in chars_img)
        {
            string path = sprite.name;
            if (!path[0].Equals('_')) filtered.Add(sprite);
        }
        bc.sprite = filtered[PlayerPrefs.GetInt("base_character", 0)];
        PlayerPrefs.SetInt(UXPref.LevelNum, current_level_num);
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
                panelRect.anchoredPosition = new Vector2(250, -250);
                panelRect.localScale = Vector3.one*0.8f;
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
                for (int k = 0; k < 16; k++)
                {
                    if (enemyAppears[levelNum,k] == null || enemyAppears[levelNum,k] == string.Empty)
                    {
                        enemyAppears[levelNum, k] = e; break;
                    }
                    if (enemyAppears[levelNum, k] == e) break;
                }
            }
        }
        rewardlist.Add(led.rewardlist);
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
        ChanageSEBShowInfo(current_level_num);
    }
    public void ChanageSEBShowInfo(int level_num)
    {
        if (!SEB.gameObject.activeSelf) return;
        SEB.ShowEnemies(GetCurrentEnemies(level_num));
        ChangeLRBInfo();
    }
    public void ChangeLRBInfo()
    {
        LRB.SetRewards(rewardlist[current_level_num]);
        LRB.ShowLevelRewards(secClearList.reward_gained[current_level_num]);
    }
    private string[] GetCurrentEnemies(int level_num)
    {
        int realLength = 0;
        for (int i = 0; i < enemyAppears.GetLength(1); i++) if (enemyAppears[level_num, i] == null || enemyAppears[level_num, i] == string.Empty) break; else realLength++;
        string[] ens = new string[realLength];
        for (int i = 0; i < realLength; i++) ens[i] = enemyAppears[level_num, i];
        return ens;
    }
    private void SwitchTeamBtn()
    {
        TeamBtn.interactable = false; 
        GameObject.Find("BaseCanvas").GetComponent<BaseCanvas>().MapToEquip(GetCurrentEnemies(current_level_num));
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

    public override string GetPageBgmName()
    {
        if (MI != null && !string.IsNullOrEmpty(MI.BGM)) return MI.BGM;
        return base.GetPageBgmName();
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

    private void OnDragLevelChanged(int levelIndex)
    {
        ChangeCurrentLevelNum(levelIndex);
    }

    private void OnDragSettleStateChanged(bool settled)
    {
        if (CombatBtn != null) CombatBtn.interactable = settled;
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
