using System.Collections;
using System.Collections.Generic;
using System.IO;
using TMPro;
using Unity.VisualScripting;
using UnityEngine;
using UnityEngine.Android;
using UnityEngine.Rendering.PostProcessing;
using UnityEngine.UI;

/// <summary>
/// �ؿ������� - �����ؿ��ĺ����߼���������Ǯϵͳ������ϵͳ������ϵͳ��
/// Ϊδ����չ����ؿ������ṩ�ӿ�֧��/// </summary>
public class LevelController : MonoBehaviour
{
    #region Constants - 常量定义

    // Money Related Constants
    private const float MONEY_CHARGING_SPEED = 187f;
    private const float MONEY_CHARGING_BONUS = 9f;
    private const int MAX_MONEY = 6000;
    private const int MONEY_UPGRADE_COST = 560;
    private const int MAX_MONEY_BONUS = 1500;
    protected const int MAX_MONEY_LEVEL = 8;
    private const float MIN_MULTIPLIER = 0.06f;
    private const float MAX_MULTIPLIER = 1.0f;

    // Tresure Related Constants
    private const int MAX_TREASURE_COUNT = 150;
    private const int MIN_TREASURE_COUNT = 0;

    // Level score settings
    private const int DEFAULT_TARGET_FPS = 30;
    private const float NORMAL_TIME_SCALE = 1f;
    private const float SPEED_UP_TIME_SCALE = 2f;
    private const float PAUSED_TIME_SCALE = 0f;
    private const int INITIAL_LEVEL_SCORE = 10000;

    // Deployers count
    protected const int MAIN_DEPLOYER_COUNT = 10;
    protected const int GUEST_DEPLOYER_COUNT = 3;
    protected const int TOTAL_DEPLOYER_COUNT = 13;

    // Base positioner
    private const float BASE_OFFSET = 1600f;
    private const float BASE_POSITION_DIVISOR = 200f;
    private const float BASE_Y_POSITION = 1f;

    // 角色渲染分层：11 条深度线，每条 1000 个席位。
    // 部署器抽线、本控制器发席位号，order = line * VisualLayerStride + slot。
    // 必须和两个部署器的 Random.Range(0, VisualLayerCount) 保持一致。
    public const int VisualLayerCount = 11;
    public const int VisualLayerStride = 1000;

    // Option page
    private const string OptionPagePath = "UI/Pages/Options";

    // Props
    /// <summary>狙击手猫的单位编号。单位尚未导入，填上之后道具才会真的放人。</summary>
    private const string SniperUnitCode = "";
    /// <summary>狙击手的出场位置：同部署器放猫一样，落在猫咪基地右侧半格。</summary>
    private const float SNIPER_SPAWN_X_OFFSET = 0.5f;

    // Accounter
    private const float XP_TREASURE_MULTIPLIER = 1.5f;
    private const float XP_RANDOM_RANGE = 0.05f;
    private const float STUDY_POWER_MULTIPLIER = 2.5f;
    private const float STUDY_POWER_DIVISOR = 30f;

    // Reward and Penalty
    private const float REWARD_PENALTY_MULTIPLIER = 2.5f;
    private const int PERCENTAGE_BASE = 100;

    // Cat Base Max Health
    private const int CAT_BASE_BASE_HEALTH = 1000;
    private const int CAT_BASE_TREASURE_MULTIPLIER = 235000;
    private const int CAT_BASE_HEALTH_UPGRADE_MAX = 30;
    private const int CAT_BASE_HEALTH_UPGRADE_PER = 6000;

    // Skip Game Damage
    private const int SKIP_GAME_DAMAGE = 99999999;

    #endregion

    #region Level Information

    protected string chapterName = string.Empty;
    protected string sectionName = string.Empty;
    protected int sectionNum = 0;
    protected int diff = 0;
    protected int levelNum = 0;
    protected string levelName = "0";
    public LevelData LD;
    protected bool testificateMode = false;

    #endregion

    #region Game Static Objects

    protected GameObject dogeBase;
    protected GameObject catBase;
    private CatBase catBaseComponent;
    protected string[] characters_code;
    protected LevelRestrictionHelper.RestrictionRules levelRestrictions;
    /// <summary>本关解析后的限制规则（无关卡数据时可能为 null）。</summary>
    public LevelRestrictionHelper.RestrictionRules LevelRestrictions => levelRestrictions;
    protected bool requiredUnitsSatisfied = true;

    #endregion

    #region Money System

    public static float moneyCharching_speed = MONEY_CHARGING_SPEED;
    public static float moneyCharching_bonus = MONEY_CHARGING_BONUS;
    // 大炮充能时间由CatBase维护

    public float currentMoney = 0;
    protected int current_money_level = 1;
    protected static int maxMoney = MAX_MONEY;
    protected static int money_upgrade_cost = MONEY_UPGRADE_COST;
    protected static int maxMoney_bonus = MAX_MONEY_BONUS;
    protected float multiplier = MIN_MULTIPLIER;
    protected int treasureCount = 0;

    #endregion

    #region UI Components

    [Header("Money UI")]
    [SerializeField] protected TMP_Text money_txt;
    [SerializeField] protected TMP_Text money_upgrade_txt;
    [SerializeField] protected GameObject Upgrade_image;
    [SerializeField] protected Button Upgrade_btn;

    [Header("Cannon UI")]
    [SerializeField] protected Button Cannon_btn;
    [SerializeField] protected Image Cannon_btn_image;
    [SerializeField] protected Animator Cannon_btn_animation;

    [Header("Game Control UI")]
    [SerializeField] protected Button Pause_btn;
    [SerializeField] protected Button GiveUp_btn;
    [SerializeField] protected Button Option_btn;
    [SerializeField] protected Button Speed_btn;
    [SerializeField] protected Button Skip_btn;
    [SerializeField] protected GameObject pause_black_shade;
    [SerializeField] protected GameObject pause_table;
    [SerializeField] private GameObject pauseCharacterStatementRoot;
    [SerializeField] private PauseCharacterInspector pauseInspector;

    [Header("Level Info UI")]
    [SerializeField] protected TMP_Text levelName_txt;

    [Header("Props UI")]
    /// <summary>狙击手道具的开关键：随时把场上那只狙击手 SetActive 开关掉。没带道具时整个隐藏。</summary>
    [SerializeField] protected Button Sniper_btn;
    /// <summary>工作狂猫的纯展示图标，没带道具时隐藏。</summary>
    [SerializeField] protected GameObject WorkerCat_icon;

    [Header("Deployment UI")]
    [SerializeField] protected Transform Deployers;
    [SerializeField] protected Transform GuestDeployers;
    [SerializeField] protected GameObject MaxDeployed;

    #endregion

    #region Game States
    protected bool game_paused = false;
    protected bool speed_up = false;
    protected bool disable_controll = false;
    public bool isPloting = false;
    protected int level_score = INITIAL_LEVEL_SCORE;
    protected int gain_XP = 0;
    private int cachedDisplayedMoney = -1;
    private int cachedDisplayedMoneyMax = -1;
    private int cachedUpgradeCost = -1;
    private int cachedUpgradeLevel = -1;
    private bool cachedUpgradeButtonState = true;
    private bool upgradeUiDirty = true;
    private GameObject optionInstance;
    /// <summary>本关实际带上的道具（标记 + 库存双重确认后的结果），胜利时按这两个值结算。</summary>
    private bool workerCatActive;
    private bool sniperActive;
    private GameObject sniperUnit;
    private bool sniperDeployed = true;
    /// <summary>狙击手键是否该出现：没带道具、单位没导入、单位已阵亡都为 false。</summary>
    private bool sniperBtnAvailable;
    public Character PausedSelectedCharacter => pauseInspector != null ? pauseInspector.SelectedCharacter : null;

    #endregion

    #region Deployment System

    public int maxEnemyDeploy = 50;
    public int maxCatDeploy = 50;
    protected int enemyDeployed = 0;
    protected int catDeployed = 0;
    /// <summary>
    /// 每条视觉深度线上已发出的席位号。下标就是部署器抽到的 so（0..10，即 Random.Range(0, 11)），
    /// 敌我共用一套：两边的 y 都是 base.y - so/10f 且两个基地同为 BASE_Y_POSITION，
    /// 所以同一个 so 在两队是同一条深度线，各自独立随机会让一猫一敌有 1/6 概率同序。
    /// 跟着 LevelController 实例走，所以每场战斗自动从 0 开始，不需要额外归零。
    /// </summary>
    private readonly int[] visualLayerSlots = new int[VisualLayerCount];

    #endregion

    #region Reward System

    protected Sprite[] reward_sprites;
    protected int[] reward_count;

    #endregion

    #region Proficency System
    protected Level_ProficencyUpdator LPU = new Level_ProficencyUpdator();

    #endregion

    #region Unity Lifecycle

    private void Awake()
    {
        Input.multiTouchEnabled = true;
        EnsurePauseInspector();
    }

    private void OnDisable()
    {
        pauseInspector?.ClearSelection();
    }

    private void Start()
    {
        Application.targetFrameRate = DEFAULT_TARGET_FPS;
        GameOptions.Refresh();

        // 加载入场UI
        try
        {
            Instantiate(Resources.Load<GameObject>("UI/Tag In"));
        }
        catch { }

        InitializeButtons();
        SetUpgradeActive(false);
        InitializeLevelData();
        // 必须在 ApplyInitialMoneyRestrictions 之前：工作狂猫要改的就是开局钱包等级。
        ResolveProps();
        ApplyInitialMoneyRestrictions();
        BindCannonToBase();
        LoadPlot();
        MaxDeployed.SetActive(false);

        if (!isPloting)
        {
            SetupCatDeployers();
            SetupEnemySummoner();
            TrySpawnSniper();
            ApplyDefaultSpeed();
        }
    }

    protected void FixedUpdate()
    {
        if (isPloting || disable_controll) return;

        // 更新金钱
        float moneyGain = (moneyCharching_speed + (current_money_level - 1) * moneyCharching_bonus)
            * multiplier
            * Time.deltaTime
            * LevelRestrictionHelper.GetMoneyProductionMultiplier(levelRestrictions);
        AddMoney(moneyGain);

        // 更新分数
        level_score--;

        // 检查升级条�?
        if (current_money_level < MAX_MONEY_LEVEL)
        {
            float current_upgrade_cost = money_upgrade_cost * current_money_level * multiplier;
            SetUpgradeActive(currentMoney > current_upgrade_cost);
        }

        // 更新大炮充能（由CatBase处理�?
        catBaseComponent?.ChargeCannon();
    }

    private void Update()
    {
        if (!game_paused || isPloting || disable_controll) return;

        pauseInspector?.Tick();
    }

    #endregion

    #region Initialization
    private void InitializeButtons()
    {
        Pause_btn.onClick.AddListener(() => Pause(!game_paused));

        GiveUp_btn.onClick.AddListener(() =>
        {
            // 这里必须用 SpeedUp(false) 而不是 Pause(false)：Time.timeScale 是跨场景保留的，
            // 而 Pause(false) 现在会按「默认二倍速」恢复，放弃关卡就会把 2 倍速带进 BaseScene。
            SpeedUp(false);
            Pause_btn.interactable = false;
            Speed_btn.interactable = false;
            GetComponent<SceneSwitcher>().TagOutToDirectly("BaseScene");
        });

        Speed_btn.onClick.AddListener(() => SpeedUp(!speed_up));
        if (Sniper_btn != null) Sniper_btn.onClick.AddListener(ToggleSniper);
        Upgrade_btn.onClick.AddListener(UpgradeMoney);
#if UNITY_EDITOR
        Skip_btn.gameObject.SetActive(true);
        Skip_btn.onClick.AddListener(() => { Pause(false); SkipGame(); });
#else
        if (Skip_btn != null)
            Skip_btn.gameObject.SetActive(false);
#endif

        if (Option_btn != null) Option_btn.onClick.AddListener(ShowOption);

        pause_table.SetActive(false);
    }

    protected virtual void RefreshTeamRestrictionState()
    {
        requiredUnitsSatisfied = LevelRestrictionHelper.AreRequiredUnitsSelected(levelRestrictions, characters_code)
            && LevelRestrictionHelper.AreForcedSlotsSatisfied(levelRestrictions, characters_code);
    }

    protected virtual bool ShouldLockAllCatsByRestriction()
    {
        return !requiredUnitsSatisfied;
    }

    protected virtual int GetDeploymentLevel(string code, int configuredLevel = -1)
    {
        int resolvedLevel = configuredLevel;
        if (resolvedLevel < 1)
        {
            resolvedLevel = 1;
            if (CharacterPlacer.TryParse(code, true, out UnitIdentity identity)
                && identity.IsValid
                && identity.AssetIsCat
                && !identity.IsOpposite
                && identity.CharacterCode.Length >= 4)
            {
                try
                {
                    resolvedLevel = CharacterUpgradeSave.GetDetails(identity.CharacterCode.Substring(0, 4)).TotalLevel();
                    if (resolvedLevel < 1) resolvedLevel = 1;
                }
                catch
                {
                    resolvedLevel = 1;
                }
            }
        }

        return LevelRestrictionHelper.ApplyUnitLevelCap(levelRestrictions, resolvedLevel);
    }

    protected virtual int GetSlotDeploymentLevel(int slotIndex, string characterCode, int configuredLevel = -1)
    {
        if (LevelRestrictionHelper.TryGetForcedSlotLevel(levelRestrictions, slotIndex, out int forcedLevel))
            return forcedLevel;
        return GetDeploymentLevel(characterCode, configuredLevel);
    }

    protected virtual float GetDeploymentCostMultiplier(string code)
    {
        return LevelRestrictionHelper.GetUnitCostMultiplier(levelRestrictions, code);
    }

    /// <summary>
    /// 鍏冲崱杩愯鏃堕檺鍒舵敤浜庝笅鍙戝埌閮ㄧ讲鍣ㄧ瓑绯荤�?    /// </summary>
    protected virtual void ApplyLevelRestrictionSettings()
    {
        maxCatDeploy = LevelRestrictionHelper.GetMaxCatDeploy(levelRestrictions, maxCatDeploy);

        if (IsOneHitBaseRestricted())
        {
            AbilityInstaller.Install(catBaseComponent, new CharacterAbility
            {
                name = AbilityName.Aux_OneHit,
                probability = 100,
                duration = 0,
                intensity = 0
            });
        }
    }

    /// <summary>
    /// 初始化关卡数据（可由子类重写�?    /// </summary>
    protected virtual void InitializeLevelData()
    {
        treasureCount = RewardingSystem.GetAmount(RewardName.WorldTreasures);
        LoadLevelInfoFromPref();
        string levelLoadPath = $"LevelData/LevelEnemyData/{chapterName}/{sectionName}/dif{diff}/{levelNum}";
        LD = Resources.Load<LevelData>(levelLoadPath);

        if (LD == null)
        {
            Debug.LogError($"Level not found in \"{levelLoadPath}\"!");
            Application.Quit();
            return;
        }
        int mapSize = LD.mapSize;

        // 限制条件必须在这里解析：tl 会改写 treasureCount，而下面的金钱倍率、基地血量、经验奖励都拿它算。
        // ApplyLevelRestrictionSettings 仍留在 SetupMapAndBases 之后，它要用那里才赋值的 catBaseComponent。
        levelRestrictions = LevelRestrictionHelper.Parse(LD.Restriction);
        treasureCount = LevelRestrictionHelper.GetTreasureCount(levelRestrictions, treasureCount);

        // 计算金钱倍率
        CalculateMoneyMultiplier();

        // 设置地图和基�?
        SetupMapAndBases(mapSize);

        // 设置关卡信息
        SetupLevelInfo();
        ApplyLevelRestrictionSettings();

        // 设置战斗效果
        SetupCombatEffects();

        // 设置光环效果
        SetupCombatAura();
    }

    private int ComputeCatBaseHealth()
    {
        int health = CAT_BASE_BASE_HEALTH + CAT_BASE_TREASURE_MULTIPLIER * treasureCount / MAX_TREASURE_COUNT;
        if (treasureCount < MAX_TREASURE_COUNT) return health;
        int n = Mathf.Clamp(RewardingSystem.GetAmount(RewardName.Base_Health), 0, CAT_BASE_HEALTH_UPGRADE_MAX);
        return health + n * CAT_BASE_HEALTH_UPGRADE_PER;
    }

    /// <summary>
    /// 计算金钱倍率
    /// </summary>
    protected void CalculateMoneyMultiplier()
    {
        treasureCount = Mathf.Clamp(treasureCount, MIN_TREASURE_COUNT, MAX_TREASURE_COUNT);
        float treasureRatio = treasureCount / (float)MAX_TREASURE_COUNT;
        multiplier = MIN_MULTIPLIER + (MAX_MULTIPLIER - MIN_MULTIPLIER) * treasureRatio;

        Debug.Log($"Treasures: {treasureCount} / {MAX_TREASURE_COUNT}.");
    }

    /// <summary>
    /// 设置地图和基�?    /// </summary>
    protected void SetupMapAndBases(int mapSize)
    {
        // 设置背景
        GameObject.Find("Background").GetComponent<BackgroundInitializer>().UpdateMaterialProperties(LD.BackgroundID);

        // 获取基地引用
        dogeBase = GameObject.Find("DogeBase");
        catBase = GameObject.Find("CatBase");
        catBaseComponent = catBase != null ? catBase.GetComponent<CatBase>() : null;

        // 设置基地位置
        mapSize=Mathf.Clamp(mapSize, 1500, 6000);
        float dogeBaseX = (-mapSize + BASE_OFFSET) / BASE_POSITION_DIVISOR;
        float catBaseX = (mapSize - BASE_OFFSET) / BASE_POSITION_DIVISOR;
        dogeBase.transform.position = new Vector3(dogeBaseX, BASE_Y_POSITION, 0);
        catBase.transform.position = new Vector3(catBaseX, BASE_Y_POSITION, 0);

        // 设置相机限制
        GameObject.Find("Main Camera").GetComponent<CameraController>().SetLimitation(mapSize);

        // 设置基地生命值（同步到当前真实血量，避免Start执行顺序导致沿用预制体默认值）
        dogeBase.GetComponent<DogeBase>().ApplyLevelBaseHealth(LD.BaseHealth);
        catBaseComponent?.ApplyLevelBaseHealth(ComputeCatBaseHealth());

        // 设置狗基地外�?
        Sprite baseSprite = BundledAddressables.LoadSync<Sprite>($"Units/DogeBases/{LD.BaseImageID}");
        dogeBase.transform.GetChild(0).GetChild(0).GetComponent<SpriteRenderer>().sprite = baseSprite;
    }

    /// <summary>
    /// 设置关卡信息
    /// </summary>
    protected void SetupLevelInfo()
    {
        levelName = LD.levelName;
        LocalizationHelper.GetLocalizedText(UXPref.Localized_LevelNames, levelName,
            localizedText => levelName_txt.text = localizedText ?? levelName);

        // 计算经验�?
        float xpMultiplier = 1 + treasureCount / (float)MAX_TREASURE_COUNT * XP_TREASURE_MULTIPLIER + Random.Range(0f, XP_RANDOM_RANGE);
        gain_XP = (int)(LD.gainXP * xpMultiplier);

        maxEnemyDeploy = LD.maxEmenyCount;
    }

    /// <summary>
    /// 设置战斗效果
    /// </summary>
    protected void SetupCombatEffects()
    {
        if (LD.CombatEffect == null) return;

        for (int i = 0; i < LD.CombatEffect.Length; i++)
        {
            GameObject combatEffect = BundledAddressables.LoadSync<GameObject>($"Background/CombatEffects/{LD.CombatEffect[i]}");
            if (combatEffect != null)
            {
                Instantiate(combatEffect);
            }
        }
    }

    /// <summary>
    /// 设置战斗光环
    /// </summary>
    protected void SetupCombatAura()
    {
        if (LD.CombatAura == null) return;

        PostProcessVolume ppv = AuraController.FindAnAura();
        if (ppv != null)
        {
            for (int i = 0; i < LD.CombatAura.Length; i++)
            {
                AuraController.SetUpAura(ppv, LD.CombatAura[i]);
            }
        }
    }

    /// <summary>
    /// 从PlayerPrefs加载关卡信息
    /// </summary>
    private void LoadLevelInfoFromPref()
    {
        chapterName = PlayerPrefs.GetString(UXPref.ChapterName, UXPref.DefaultChapterName);
        sectionName = PlayerPrefs.GetString(UXPref.SectionName);
        sectionNum = PlayerPrefs.GetInt(UXPref.SectionNum);
        diff = PlayerPrefs.GetInt(UXPref.Difficulty);
        levelNum = PlayerPrefs.GetInt(UXPref.LevelNum);

        if (chapterName == string.Empty)
        {
            Debug.LogError("Chapter name not found!");
            Application.Quit();
        }

        if (sectionName == string.Empty)
        {
            Debug.LogError("Section name not found!");
            Application.Quit();
        }
    }

    #endregion

    #region Plot System - 剧情系统

    /// <summary>
    /// 加载剧情
    /// </summary>
    private void LoadPlot()
    {
        if (ShouldSkipRepeatPlot()) return;

        string plotLoadPath = $"LevelData/LevelEnemyData/{chapterName}/{sectionName}/dif{diff}/plots/{levelNum}";
        GamePlot gamePlot = Resources.Load<GamePlot>(plotLoadPath);

        if (gamePlot != null)
        {
            Chatbox chatbox = Instantiate(Resources.Load<GameObject>("UI/ChatBox")).GetComponent<Chatbox>();
            chatbox.SetFullDialogue(gamePlot);
            StartCoroutine(chatbox.ShowAllDialogue());
            isPloting = true;
            disable_controll = true;
        }
    }

    /// <summary>
    /// 设置页勾了「重复挑战直接跳过剧情」且本关已通关过时为 true。
    /// </summary>
    private bool ShouldSkipRepeatPlot()
    {
        if (!GameOptions.SkipRepeatPlot) return false;
        if (string.IsNullOrEmpty(chapterName) || string.IsNullOrEmpty(sectionName)) return false;

        GameProgressSave.SectionClearList sec = GameProgressSave.LoadSectionProgress(chapterName, sectionName);
        int[,] times = sec?.clear_times;
        // 章节改过关卡数时旧存档会比现在短，越界就当没通关过，照常播剧情。
        if (times == null) return false;
        if (diff < 0 || diff >= times.GetLength(0)) return false;
        if (levelNum < 0 || levelNum >= times.GetLength(1)) return false;
        return times[diff, levelNum] > 0;
    }

    /// <summary>
    /// 退出剧�?    /// </summary>
        public void ExitPlot()
    {
        isPloting = false;
        disable_controll = false;
        SetupCatDeployers();
        SetupEnemySummoner();
        TrySpawnSniper();
        ApplyDefaultSpeed();
    }

    #endregion

    #region Money System Methods - 金钱系统方法

    /// <summary>
    /// 增加金钱
    /// </summary>
    public void AddMoney(float money)
    {
        float currentMaxMoney = (maxMoney + (current_money_level - 1) * maxMoney_bonus) * multiplier;
        currentMoney += money;

        if (currentMoney > currentMaxMoney)
        {
            currentMoney = currentMaxMoney;
        }

        RefreshMoneyText((int)currentMoney, (int)currentMaxMoney);
    }

    /// <summary>
    /// 扣除部署费用
    /// </summary>
    public bool DeployCost(int cost)
    {
        if (cost > currentMoney) return false;
        currentMoney -= cost;
        return true;
    }

    /// <summary>
    /// 设置升级按钮状�?    /// </summary>
    protected void SetUpgradeActive(bool active)
    {
        int upgradeCost = (int)(current_money_level * money_upgrade_cost * multiplier);
        if (cachedUpgradeCost != upgradeCost || cachedUpgradeLevel != current_money_level || upgradeUiDirty)
        {
            money_upgrade_txt.text = $"Level. {current_money_level}\n\n\nLvl UP: {upgradeCost}";
            cachedUpgradeCost = upgradeCost;
            cachedUpgradeLevel = current_money_level;
        }
        if (cachedUpgradeButtonState != active || upgradeUiDirty)
        {
            Upgrade_btn.interactable = active;
            cachedUpgradeButtonState = active;
        }
        upgradeUiDirty = false;
    }

    /// <summary>
    /// 升级金钱系统
    /// </summary>
    protected void UpgradeMoney()
    {
        if (current_money_level == MAX_MONEY_LEVEL)
        {
            SetUpgradeMaxUI();
            return;
        }

        float upgradeCost = money_upgrade_cost * current_money_level * multiplier;
        if (currentMoney < upgradeCost) return;

        currentMoney -= upgradeCost;
        current_money_level++;
        PlatformAudio.PlaySfx(Upgrade_btn.GetComponent<AudioSource>());

        if (current_money_level == MAX_MONEY_LEVEL)
        {
            SetUpgradeMaxUI();
        }
        else
        {
            upgradeUiDirty = true;
            SetUpgradeActive(currentMoney > (money_upgrade_cost * current_money_level * multiplier));
        }
    }

    #endregion

    #region Cannon System Methods - 大炮系统方法
    /// <summary>
    /// 绑定大炮UI到CatBase
    /// </summary>
    private void BindCannonToBase()
    {
        if (catBaseComponent == null) return;

        catBaseComponent.SetupCannonUI(Cannon_btn, Cannon_btn_image, Cannon_btn_animation);
        Cannon_btn.onClick.RemoveAllListeners();
        Cannon_btn.onClick.AddListener(catBaseComponent.CannonFire);
    }
    private void RefreshMoneyText(int money, int maxMoneyDisplay)
    {
        if (cachedDisplayedMoney == money && cachedDisplayedMoneyMax == maxMoneyDisplay)
        {
            return;
        }
        cachedDisplayedMoney = money;
        cachedDisplayedMoneyMax = maxMoneyDisplay;
        money_txt.text = $"{money} / {maxMoneyDisplay}$";
    }
    private void SetUpgradeMaxUI()
    {
        if (cachedUpgradeLevel != MAX_MONEY_LEVEL || cachedUpgradeCost != int.MinValue || upgradeUiDirty)
        {
            money_upgrade_txt.text = $"Level. {MAX_MONEY_LEVEL}\n\n\nLvl UP: MAX";
            cachedUpgradeLevel = MAX_MONEY_LEVEL;
            cachedUpgradeCost = int.MinValue;
        }
        if (cachedUpgradeButtonState || upgradeUiDirty)
        {
            Upgrade_btn.interactable = false;
            cachedUpgradeButtonState = false;
        }
        upgradeUiDirty = false;
    }
    #endregion

    #region Game Control Methods - 游戏控制方法

    /// <summary>
    /// 暂停/继续游戏
    /// </summary>
    protected void Pause(bool pause)
    {
        if (pause)
        {
            ShowPauseTable(true);
            Time.timeScale = PAUSED_TIME_SCALE;
            Application.targetFrameRate = 0;
            Pause_btn.GetComponent<Image>().color = Color.red;
            Speed_btn.GetComponent<Image>().color = new Color(1, 1, 1, 0.5f);
            speed_up = false;
        }
        else
        {
            // 恢复时的倍速、按钮高亮、表情面板清理全交给 SpeedUp，它两种分支都写全了。
            // 设置页勾了「关卡默认二倍速」就直接回到二倍速，否则照旧一倍速。
            SpeedUp(GameOptions.DefaultDoubleSpeed);
        }

        game_paused = pause;
    }

    /// <summary>
    /// 加速游�?    /// </summary>
    protected void SpeedUp(bool speedUp)
    {
        if (speedUp)
        {
            ShowPauseTable(false);
            Time.timeScale = SPEED_UP_TIME_SCALE;
            // Keep render FPS cap stable; speed is controlled only by Time.timeScale.
            Application.targetFrameRate = DEFAULT_TARGET_FPS;
            Speed_btn.GetComponent<Image>().color = new Color(1, 1, 1, 1);
            Pause_btn.GetComponent<Image>().color = Color.white;
            game_paused = false;
            pauseInspector?.ClearSelection();
        }
        else
        {
            ShowPauseTable(false);
            Time.timeScale = NORMAL_TIME_SCALE;
            Application.targetFrameRate = DEFAULT_TARGET_FPS;
            Speed_btn.GetComponent<Image>().color = new Color(1, 1, 1, 0.5f);
            Pause_btn.GetComponent<Image>().color = Color.white;
            game_paused = false;
            pauseInspector?.ClearSelection();
        }

        speed_up = speedUp;
    }

    /// <summary>
    /// 开局（或剧情结束真正开打）时按设置页的「关卡默认二倍速」定一次倍速。
    /// 不勾也要走一遍 SpeedUp(false)：Speed_btn 的高亮要初始化，而且 Time.timeScale
    /// 是跨场景保留的，上一关留下的二倍速必须在这里被压回来。
    /// </summary>
    private void ApplyDefaultSpeed()
    {
        SpeedUp(GameOptions.DefaultDoubleSpeed);
    }

    /// <summary>
    /// 跳过游戏（仅编辑器测试；导出包不会绑定按钮也不会生效）
    /// </summary>
    public void SkipGame()
    {
#if UNITY_EDITOR
        dogeBase.GetComponent<DogeBase>().ReceiveAttack(null, SKIP_GAME_DAMAGE, null, null, null, null, null, null);
#endif
    }

    #endregion

    #region Props System - 道具系统

    /// <summary>
    /// 把选关页写下的道具标记落实成本关的实际状态。
    /// 标记本身不由关卡删除（同 UseClearedTeam：删了重开本关就带不上道具了），
    /// 所以这里必须拿库存复核一遍 —— 胜利时已经结算过、玩家又从结算页重开本关的话，
    /// 标记还在但库存可能已经是 0，不复核就等于白送一次。
    /// </summary>
    private void ResolveProps()
    {
        workerCatActive = PlayerPrefs.GetInt(UXPref.UseWorkerCat, 0) == 1
            && RewardingSystem.GetAmount(RewardName.Prop_max) > 0;
        sniperActive = PlayerPrefs.GetInt(UXPref.UseSniper, 0) == 1
            && RewardingSystem.GetAmount(RewardName.Prop_Sniper) > 0;

        if (WorkerCat_icon != null) WorkerCat_icon.SetActive(workerCatActive);
        sniperBtnAvailable = sniperActive;
        sniperDeployed = true;
        RefreshSniperButton();
    }

    /// <summary>
    /// 开局生成狙击手。单位还没导入，SniperUnitCode 留空，这条路径现在只会走到「隐掉按钮」那一步；
    /// 等编号填上就按部署器那套管线放人（TryParse → LoadData → Decrypt → Place）。
    /// 不计入 catDeployed：它不是常规部署的单位，而且按钮会来回 SetActive，计数会被搞花。
    /// </summary>
    private void TrySpawnSniper()
    {
        if (!sniperActive || sniperUnit != null) return;

        CharacterData data = null;
        UnitIdentity identity = default;
        if (!string.IsNullOrEmpty(SniperUnitCode)
            && CharacterPlacer.TryParse(SniperUnitCode, true, out identity)
            && identity.IsValid)
        {
            data = CharacterPlacer.LoadData(identity);
        }

        if (data == null)
        {
            // 单位缺失就降级成「道具不生效」：隐掉按钮，别留一个点了没反应的键。
            // 这里连 sniperActive 一起清掉，于是胜利时也不会结算 —— 道具实际上什么都没给。
            Debug.LogWarning($"[LevelController] Sniper unit not available: '{SniperUnitCode}'");
            sniperActive = false;
            sniperBtnAvailable = false;
            RefreshSniperButton();
            return;
        }

        int visualLayer = Random.Range(0, VisualLayerCount);
        int visualOrder = visualLayer * VisualLayerStride + NextVisualSlot(visualLayer);
        sniperUnit = CharacterPlacer.Place(
            identity,
            data,
            CharacterPlacer.Decrypt(identity, data),
            catBase.transform.position + new Vector3(SNIPER_SPAWN_X_OFFSET, -visualLayer / 10f, 0),
            this,
            1,
            treasureCount,
            1f,
            visualOrder,
            visualOrder);
        sniperDeployed = sniperUnit != null;
        sniperBtnAvailable = sniperUnit != null;
        RefreshSniperButton();
    }

    /// <summary>
    /// 狙击手开关。SetActive(false) 足够让它从战场上消失：CharacterTargetManager 的每条查询
    /// 都过滤 activeInHierarchy，所以关掉之后既不会被索敌也不会参战。
    /// 单位被打死后对象就销毁了，这时按钮自隐；道具的这一次使用照样算在头上（胜利仍结算）。
    /// </summary>
    private void ToggleSniper()
    {
        if (!sniperBtnAvailable) return;
        if (sniperUnit == null)
        {
            sniperBtnAvailable = false;
            RefreshSniperButton();
            return;
        }
        sniperDeployed = !sniperDeployed;
        sniperUnit.SetActive(sniperDeployed);
        RefreshSniperButton();
    }

    /// <summary>
    /// 没带道具（或单位已阵亡）就整个隐掉；带了就按「在场/收起」给 alpha，同加速键那套约定。
    /// 按钮的 Transition 必须是 None，否则 Button 自己的 Color Tint 会把这里的 alpha 覆盖回去。
    /// </summary>
    private void RefreshSniperButton()
    {
        if (Sniper_btn == null) return;
        Sniper_btn.gameObject.SetActive(sniperBtnAvailable);
        if (!sniperBtnAvailable) return;
        Image img = Sniper_btn.GetComponent<Image>();
        if (img != null) img.color = new Color(1, 1, 1, sniperDeployed ? 1f : 0.5f);
    }

    /// <summary>
    /// 道具使用数量只在胜利时结算一次，失败和放弃都不扣。
    /// ConsumeItem 库存不足会原样返回 false 不扣，所以这里不用再判一次。
    /// </summary>
    private void ConsumeProps()
    {
        if (workerCatActive) RewardingSystem.ConsumeItem(RewardName.Prop_max, 1);
        if (sniperActive) RewardingSystem.ConsumeItem(RewardName.Prop_Sniper, 1);
    }

    #endregion

    #region Audio Methods - 音频方法

    /// <summary>
    /// 暂停菜单里的设置入口。音量调节已经整体搬到 MMOption，这里只负责把那个页面生成出来；
    /// 战斗中不允许上传/删除账号，所以生成后立刻禁掉账号操作。
    /// </summary>
    private void ShowOption()
    {
        if (optionInstance != null) return;
        GameObject prefab = Resources.Load<GameObject>(OptionPagePath);
        if (prefab == null)
        {
            Debug.LogError($"[LevelController] Missing page prefab: {OptionPagePath}");
            return;
        }
        optionInstance = Instantiate(prefab);
        MMOption option = optionInstance.GetComponent<MMOption>();
        if (option != null) option.DisableAccountActions();
    }

    /// <summary>
    /// 显示/隐藏暂停菜单
    /// </summary>
    private void ShowPauseTable(bool show)
    {
        if (show)
        {
            pause_black_shade.SetActive(true);
            pause_table.SetActive(true);
            Speed_btn.gameObject.SetActive(false);
            // 狙击手键跟加速键并排，暂停面板盖上来时一起收掉；没带道具就一直是隐藏的。
            if (Sniper_btn != null) Sniper_btn.gameObject.SetActive(false);
        }
        else
        {
            pause_black_shade.SetActive(false);
            pause_table.SetActive(false);
            Speed_btn.gameObject.SetActive(true);
            if (Sniper_btn != null) Sniper_btn.gameObject.SetActive(sniperBtnAvailable);
        }
    }

    /// <summary>
    /// Change or stop level BGM via Addressables (BGM group addresses).
    /// </summary>
    public void ChangeBGM(string audio_name)
    {
        if (string.IsNullOrEmpty(audio_name))
        {
            BGMTool.StopBGM();
            return;
        }
        BGMTool.ChangeBGM(audio_name);
    }

    #endregion

    #region Victory and Failure - 胜利与失�?
    /// <summary>
    /// 关卡胜利
    /// </summary>
    public virtual void Victory()
    {
        disable_controll = true;
        SpeedUp(false);
        Pause_btn.gameObject.SetActive(false);
        Speed_btn.gameObject.SetActive(false);
        if (Sniper_btn != null) Sniper_btn.gameObject.SetActive(false);

        if (testificateMode) return;

        // 创建胜利UI
        GameObject clearCanvas = Instantiate(Resources.Load<GameObject>("UI/Clear_Canvas"));
        clearCanvas.transform.GetChild(0).GetChild(0).GetComponent<TMP_Text>().text = level_score.ToString();
        ClearCanvas clearCanvasComponent = clearCanvas.GetComponent<ClearCanvas>();

        ChangeBGM(null);
        QuitUI();

        // 处理奖励
        bool gainreward = ProcessRewards(clearCanvasComponent);

        // 带进来的道具在这里结算，只扣这一次
        ConsumeProps();

        // 处理经验�?        
        ProcessExperience(clearCanvas);

        // 保存进度
        //int catHeadCount = catBase.GetComponent<CatBase>().;
        GameProgressSave.SaveProgress(chapterName, sectionName, diff, levelNum, level_score,
            gainreward, characters_code, 0);
        bool dailyMapCleared = RecordDailyMapClearIfNeeded();
        if (dailyMapCleared) clearCanvasComponent.DisableRestartBtn();

        UnlockEnemiesMet();
        LPU.EndAccounting();
    }

    /// <summary>
    /// 关卡失败
    /// </summary>
    public virtual void Failed()
    {
        disable_controll = true;
        SpeedUp(false);
        Pause_btn.gameObject.SetActive(false);
        Speed_btn.gameObject.SetActive(false);
        if (Sniper_btn != null) Sniper_btn.gameObject.SetActive(false);

        if (testificateMode) return;

        UnlockEnemiesMet();
        //LPU.EndAccounting();

        GameObject failedCanvas = Instantiate(Resources.Load<GameObject>("UI/Failed_Canvas"));
        ChangeBGM(null);
        QuitUI();
    }

    /// <summary>
    /// 处理奖励
    /// </summary>
    private bool ProcessRewards(ClearCanvas clearCanvas)
    {
        GameProgressSave.SectionClearList sectionClearList = GameProgressSave.LoadSectionProgress(chapterName, sectionName);
        bool gainedReward = sectionClearList.reward_gained[levelNum];
        bool hasNewReward = false;
        var rewardList = LD.rewardlist;
        int rewardPenalty = PlayerPrefs.GetInt(UXPref.RewardPenalty, 0);

        for (int i = 0; i < rewardList.Length; i++)
        {
            if (rewardList[i].onlyOnce && gainedReward) continue;

            int gainTimes = CalculateRewardGainTimes(rewardList[i], rewardPenalty);
            if (gainTimes == 0) continue;

            // 应用奖励惩罚：每次 XP_PUNCH 保留 (100-2.5)%，连乘后趋近 0，不会变负或爆表。
            if (rewardPenalty > 0 && !rewardList[i].onlyOnce)
            {
                float keepRate = Mathf.Pow(1f - REWARD_PENALTY_MULTIPLIER / PERCENTAGE_BASE, rewardPenalty);
                keepRate = Mathf.Clamp01(keepRate);
                gainTimes = Mathf.Max(0, Mathf.CeilToInt(gainTimes * keepRate));
            }

            // 发放奖励
            ApplyReward(rewardList[i], gainTimes);
            clearCanvas.AppendReward(rewardList[i].type, rewardList[i].id, gainTimes);

            if (rewardList[i].onlyOnce)
            {
                hasNewReward = true;
            }
        }
        PlayerPrefs.DeleteKey(UXPref.RewardPenalty);
        return hasNewReward;
    }

    /// <summary>
    /// 计算奖励获得次数
    /// </summary>
    private int CalculateRewardGainTimes(Reward rewardInfo, int rewardPenalty)
    {
        //int gainTimes = 0;
        int p = Random.Range(0, 100);
        if(p<rewardInfo.droprate) return rewardInfo.drawtimes;
        else return 0;
    }

    /// <summary>
    /// 应用奖励
    /// </summary>
    private void ApplyReward(Reward rewardInfo, int gainTimes)
    {
        switch (rewardInfo.type)
        {
            case RewardType.item:
                RewardingSystem.GainRewardByOrder(rewardInfo.id, gainTimes);
                break;
            case RewardType.character:
                CharacterUpgradeSave.UpgradeCharacterByClear(rewardInfo.id.ToString("0000"));
                break;
            case RewardType.UnlockTire:
                if (gainTimes <= 0) break;
                RewardIconHelper.ParseUnlockTireId(rewardInfo.id, out string characterId, out int tire);
                CharacterUpgradeSave.UnlockCharacterTire(characterId, tire);
                break;
            default:
                break;
        }
    }

    /// <summary>
    /// 检查是否有新奖�?    /// </summary>
    private bool HasNewReward()
    {
        GameProgressSave.SectionClearList sectionClearList = GameProgressSave.LoadSectionProgress(chapterName, sectionName);
        bool gainedReward = sectionClearList.reward_gained[levelNum];

        foreach (var reward in LD.rewardlist)
        {
            if (reward.onlyOnce && !gainedReward)
            {
                return true;
            }
        }

        return false;
    }

    /// <summary>
    /// 处理经验�?    /// </summary>
    private void ProcessExperience(GameObject clearCanvas)
    {
        GameProgressSave.SectionClearList sectionClearList = GameProgressSave.LoadSectionProgress(chapterName, sectionName);
        int studyPower = RewardingSystem.GetAmount(RewardName.Base_Study); // legacy code
        // int studyPower = GenericSaveSystem.LoadData<int[]>(RewardingSystem.filename)[RewardingSystem.RewardNumMap[RewardName.Base_Study]];

        float xpMultiplier = 1 + studyPower * STUDY_POWER_MULTIPLIER / STUDY_POWER_DIVISOR;
        int clearTimes = sectionClearList.clear_times[diff, levelNum];
        gain_XP = (int)(gain_XP * xpMultiplier) / (clearTimes + 1);

        clearCanvas.transform.GetChild(0).GetChild(1).GetComponent<TMP_Text>().text = gain_XP.ToString();
        RewardingSystem.GainReward(RewardName.XP, gain_XP);
    }

    #endregion

    #region Deployment System - 部署系统

    /// <summary>
    /// 设置猫单位部署器
    /// </summary>
    public virtual void SetupCatDeployers()
    {
        SetupCatDeployersNormal();
    }

    /// <summary>
    /// 设置猫单位部署器
    /// </summary>
    protected void SetupCatDeployersNormal()
    {
        characters_code = SelectionsSave.GetRow(PlayerPrefs.GetInt(SelectionsSave.pref_teamnum, 0));
        TryUseClearedTeam(ref characters_code);
        LevelRestrictionHelper.TryApplyForcedSlots(levelRestrictions, ref characters_code);
        int[] proficiencyLevels = LPU.SetUp(characters_code);
        int teamProficiencyBonus = CalculateTeamProficiencyBonus(proficiencyLevels);
        RefreshTeamRestrictionState();

        // 设置主要部署�?
        for (int i = 0; i < MAIN_DEPLOYER_COUNT; i++)
        {
            UnitDeployer deployer = Deployers.GetChild(i).GetComponent<UnitDeployer>();
            string code = characters_code[i];
            deployer.SetupDeployer(
                code,
                treasureCount,
                proficiencyLevels[i],
                teamProficiencyBonus,
                GetSlotDeploymentLevel(i, code),
                GetDeploymentCostMultiplier(code));
            LevelRestrictionHelper.ApplyToDeployer(deployer, code, levelRestrictions, false, ShouldLockAllCatsByRestriction());
        }

        // 设置访客部署      
        SetupGuestDeployersNormal(proficiencyLevels, teamProficiencyBonus);
    }

    /// <summary>
    /// 设置嘉宾部署�?    /// </summary>
    protected void SetupGuestDeployersNormal(int[] proficiencyLevels, int teamProficiencyBonus)
    {
        for (int i = MAIN_DEPLOYER_COUNT; i < TOTAL_DEPLOYER_COUNT; i++)
        {
            CharacterData characterData = LoadSlotCharacterData(characters_code[i], true);
            if (characterData == null)
            {
                GuestDeployers.GetChild(i - MAIN_DEPLOYER_COUNT).gameObject.SetActive(false);
                continue;
            }

            UnitDeployer deployer = GuestDeployers.GetChild(i - MAIN_DEPLOYER_COUNT).GetComponent<UnitDeployer>();
            GuestDeployers.GetChild(i - MAIN_DEPLOYER_COUNT).gameObject.SetActive(true);
            string code = characters_code[i];
            deployer.SetupDeployer(
                code,
                treasureCount,
                proficiencyLevels[i],
                teamProficiencyBonus,
                GetSlotDeploymentLevel(i, code),
                GetDeploymentCostMultiplier(code));
            LevelRestrictionHelper.ApplyToDeployer(deployer, code, levelRestrictions, true, ShouldLockAllCatsByRestriction());
        }
    }

    /// <summary>
    /// 选关页勾了「使用通关的队伍」时，用存档里记下的通关阵容替换自选队伍。
    /// 标记保留不删：重开关卡会重载场景，删了就退回自选队伍了；标记只由选关页在出击时写或删。
    /// 取到的记录不完整就当没勾，宁可用自选队伍也不能让玩家带着空卡组进场。
    /// </summary>
    private void TryUseClearedTeam(ref string[] codes)
    {
        if (PlayerPrefs.GetInt(UXPref.UseClearedTeam, 0) != 1) return;
        string[] cleared = GameProgressSave.GetClearedTeam(chapterName, sectionName, levelNum);
        if (cleared == null || cleared.Length != SelectionsSave.SIZE) return;
        codes = cleared;
    }

    /// <summary>
    /// 加载角色数据
    /// </summary>
    protected CharacterData LoadGuestCharacterData(string characterCode)
    {
        return LoadSlotCharacterData(characterCode, true);
    }

    protected CharacterData LoadSlotCharacterData(string characterCode, bool guestSlot)
    {
        if (string.IsNullOrEmpty(characterCode)) return null;
        if (!CharacterPlacer.TryParse(characterCode, true, out UnitIdentity identity) || !identity.IsValid)
            return null;

        if (guestSlot && !LevelRestrictionHelper.HasForcedGuestSlots(levelRestrictions))
        {
            if (identity.IsOpposite || !identity.AssetIsCat) return null;
            if (identity.CharacterCode.Length < 1 || identity.CharacterCode[0] != '6') return null;
        }

        CharacterData data = CharacterPlacer.LoadData(identity);
        if (data == null) Debug.LogWarning($"No such character way path: {characterCode}");
        return data;
    }

    /// <summary>
    /// 计算团队熟练度加�?    /// </summary>
    protected int CalculateTeamProficiencyBonus(int[] proficiencyLevels)
    {
        int bonus = 0;
        for (int i = 0; i < proficiencyLevels.Length; i++)
        {
            if (proficiencyLevels[i] > 3)
            {
                bonus++;
            }
        }
        return bonus;
    }

    private void SetupEnemySummoner()
    {
        if (LD == null || LD.enemySummoners == null) return;
        for (int i = 0; i < LD.enemySummoners.Length; i++)
        {
            int healthPercentage = LD.enemySummoners[i].HealthPercentageOnBreak;
            LevelEnemySummoner enemySummoner = gameObject.AddComponent<LevelEnemySummoner>();
            ConfigureEnemySummoner(enemySummoner, healthPercentage, LD.enemySummoners[i].enemySummonInfos);
            enemySummoner.SetChangeBGM(LD.enemySummoners[i].bgm);
        }
    }

        protected virtual void ConfigureEnemySummoner(LevelEnemySummoner enemySummoner, int healthPercentage, EnemySummonInfo[] enemySummonInfos)
    {
        if (enemySummoner == null) return;
        if (dogeBase == null) dogeBase = GameObject.Find("DogeBase");
        if (dogeBase == null)
        {
            Debug.LogError("[LevelController] DogeBase not found. Enemy summoner setup skipped.");
            enemySummoner.enabled = false;
            return;
        }
        enemySummoner.SetBase(dogeBase);
        enemySummoner.ApplyLevelRestrictions(levelRestrictions);
        enemySummoner.SetupEnemyDeployer(healthPercentage, enemySummonInfos);
    }

    public bool CanDeployACat()
    {
        return catDeployed < maxCatDeploy;
    }

    /// <summary>
    /// 为一条深度线发一个席位号，供部署器算 order = line * VisualLayerStride + 返回值。
    /// 取代原先敌我各自 Random.Range(0, 6) * 50 的随机占位：随机在同一条线上有 1/6 撞号率，
    /// 累加则保证同线不重号，且后来的角色一定在先来的上层，符合直觉。
    ///
    /// 取模而不是放任累加：第 1001 个角色若拿到 1000，就会和下一条线的首席同序 ——
    /// 下一条线 y 更低、看着更靠前，order 却被看着更靠后的角色追平，正是这次要消灭的那类错位。
    /// 再往上还会越过 20000（盖住护盾和弹道，两者都写死 20000）和 31000
    /// （OrderLayerStart 的 setter 钳死，该线此后全部同序且整场不可恢复）。
    /// 回绕的代价只是第 1001 个角色排到本线最底，而那时本线先来的近千个几乎已全部阵亡。
    /// </summary>
    public int NextVisualSlot(int layer)
    {
        if (layer < 0) layer = 0;
        else if (layer >= VisualLayerCount) layer = VisualLayerCount - 1;
        int slot = visualLayerSlots[layer] % VisualLayerStride;
        visualLayerSlots[layer] = slot + 1;
        return slot;
    }

    public UnitDeployer FindCatDeployer(Character character)
    {
        if (character == null) return null;
        return FindCatDeployer(character.SpawnIdentity);
    }

    public UnitDeployer FindCatDeployer(UnitIdentity spawn)
    {
        UnitDeployer found = FindCatDeployerIn(Deployers, spawn);
        if (found != null) return found;
        return FindCatDeployerIn(GuestDeployers, spawn);
    }

    private static UnitDeployer FindCatDeployerIn(Transform root, UnitIdentity spawn)
    {
        if (root == null || !spawn.IsValid) return null;
        for (int i = 0; i < root.childCount; i++)
        {
            UnitDeployer deployer = root.GetChild(i).GetComponent<UnitDeployer>();
            if (deployer != null && deployer.MatchesSpawn(spawn)) return deployer;
        }
        return null;
    }

    /// <summary>
    /// 部署一只猫
    /// </summary>
    public bool DeployACat()
    {
        if (!CanDeployACat()) return false;

        catDeployed++;
        Debug.Log($"Current Cats: {catDeployed} / {maxCatDeploy}");
        MaxDeployed.SetActive(catDeployed >= maxCatDeploy);
        return true;
    }

    /// <summary>
    /// 部署一个敌�?    /// </summary>
    public bool DeployAnEnemy()
    {
        if (enemyDeployed >= maxEnemyDeploy) return false;

        enemyDeployed++;
        Debug.Log($"Current Enemies: {enemyDeployed} / {maxEnemyDeploy}");
        return true;
    }

    /// <summary>
    /// 移除一只猫
    /// </summary>
    public void RemoveACat()
    {
        catDeployed = Mathf.Max(0, catDeployed - 1);
        MaxDeployed.SetActive(catDeployed >= maxCatDeploy);
        Debug.Log($"Current Cats: {catDeployed} / {maxCatDeploy}");
    }

    /// <summary>
    /// 移除一个敌�?    /// </summary>
    public void RemoveAnEnemy()
    {
        enemyDeployed = Mathf.Max(0, enemyDeployed - 1);
        Debug.Log($"Current Enemies: {enemyDeployed} / {maxEnemyDeploy}");
    }

    /// <summary>
    /// 设置Boss锁定
    /// </summary>
    public void SetBossLock()
    {
        dogeBase.GetComponent<DogeBase>().bossLock = true;
    }

    #endregion

    #region Utility Methods

    /// <summary>
    /// 退出UI动画
    /// </summary>
    public void QuitUI()
    {
        GameObject.Find("UI Canvas").GetComponent<Animator>().enabled = true;
    }

    private void EnsurePauseInspector()
    {
        if (pauseInspector == null) pauseInspector = GetComponent<PauseCharacterInspector>();
        if (pauseInspector == null) pauseInspector = gameObject.AddComponent<PauseCharacterInspector>();
        pauseInspector.Initialize(pauseCharacterStatementRoot);
    }

    public void ClearPausedCharacterSelection()
    {
        pauseInspector?.ClearSelection();
    }

    /// <summary>
    /// 解锁遇到的敌�?    /// </summary>
    public void UnlockEnemiesMet()
    {
        if (LD == null) return;

        for (int i = 0; i < LD.enemySummoners.Length; i++)
        {
            for (int j = 0; j < LD.enemySummoners[i].enemySummonInfos.Length; j++)
            {
                string enemyID = LD.enemySummoners[i].enemySummonInfos[j].enemyID;
                if (!CharacterPlacer.TryParse(enemyID, false, out UnitIdentity identity) || !identity.IsValid) continue;
                if (identity.AssetIsCat) continue;
                if (identity.CharacterCode.Length < 4) continue;
                if (!int.TryParse(identity.CharacterCode.Substring(1, 3), out int code)) continue;
                EnemyMeetSave.SetMetEnemyCode(code);
            }
        }
    }

    #endregion

    #region Proficency System Methods

    /// <summary>
    /// On character deployed, record proficiency progress 0 for that character. 
    /// </summary>
    public void RecordProficency_Deploy(string code)
    {
        if (!disable_controll)
        {
            LPU.Record_CharacterDeploy(code);
        }
    }

    /// <summary>
    /// On character attack, record proficiency progress 1 for that character.     
    /// /// </summary>
    public void RecordProficency_DamageDealt(string code, int dmg)
    {
        if (!disable_controll)
        {
            LPU.Record_CharacterDamageDealt(code, dmg);
        }
    }

    /// <summary>
    /// On character under attacked, record proficiency progress 2 for that character. 
    /// /// </summary>
    public void RecordProficency_DamageTaken(string code, int dmg)
    {
        if (!disable_controll)
        {
            LPU.Record_CharacterDamageTaken(code, dmg);
        }
    }

    /// <summary>
    /// On character suffers debuff, record proficiency progress 3 for that character.     
    /// /// </summary>
    public void RecordProficency_DebuffSuffered(string code, int t)
    {
        if (!disable_controll)
        {
            LPU.Record_CharacterDebuffSuffered(code, t);
        }
    }

    //public void RecordProficency_EffectDuration(string code, EffectName effectName, int durationFrames)
    //{
    //    if (disable_controll || durationFrames <= 0 || string.IsNullOrEmpty(code)) return;

    //    switch (effectName)
    //    {
    //        case EffectName.weaken:
    //        case EffectName.stop:
    //        case EffectName.slow:
    //        case EffectName.wrap:
    //        case EffectName.curse:
    //        case EffectName.toxic:
    //        case EffectName.lacerate:
    //        case EffectName.deathmark:
    //            LPU.Record_CharacterDebuffSuffered(code, durationFrames);
    //            break;
    //        default:
    //            break;
    //    }
    //}

    private bool IsOneHitBaseRestricted()
    {
        return levelRestrictions != null && levelRestrictions.rawValuesByKey.ContainsKey("OH");
    }

    private bool RecordDailyMapClearIfNeeded()
    {
        if (string.IsNullOrEmpty(chapterName) || string.IsNullOrEmpty(sectionName)) return false;

        MapInfo mapInfo = TryGetCurrentMapInfo();
        if (mapInfo == null || !mapInfo.HasDailyTimesLimit) return false;
        DailyMapChallengeSave.RecordSectionClear(CheckInSystem.GetCachedWorldDateToken(), sectionName);
        return DailyMapChallengeSave.HasReachedDailyLimit(
            CheckInSystem.GetCachedWorldDateToken(), sectionName, mapInfo.timesLimit);
    }

    private MapInfo TryGetCurrentMapInfo()
    {
        if (string.IsNullOrEmpty(chapterName) || string.IsNullOrEmpty(sectionName)) return null;
        MapInfo[] mapInfos = Resources.LoadAll<MapInfo>($"LevelData/Chapters/{chapterName}");
        if (mapInfos == null || mapInfos.Length == 0) return null;
        for (int i = 0; i < mapInfos.Length; i++)
        {
            MapInfo mapInfo = mapInfos[i];
            if (mapInfo == null) continue;
            if (string.Equals(mapInfo.sectionName, sectionName, System.StringComparison.Ordinal))
            {
                return mapInfo;
            }
        }
        return null;
    }

    private void ApplyInitialMoneyRestrictions()
    {
        // 工作狂猫只是把「没有特别规定时的开局等级」顶到满级：关卡自己写了 initialMoneyLevel 就按关卡来，
        // 限制关是围着它那个钱包等级设计的，不能被一个可购买的道具破掉。
        int baseLevel = workerCatActive ? MAX_MONEY_LEVEL : current_money_level;
        current_money_level = Mathf.Clamp(
            LevelRestrictionHelper.GetInitialMoneyLevel(levelRestrictions, baseLevel),
            1,
            MAX_MONEY_LEVEL);

        int initialMoney = LevelRestrictionHelper.GetInitialMoney(
            levelRestrictions,
            Mathf.Max(0, Mathf.FloorToInt(currentMoney)));

        currentMoney = 0f;
        AddMoney(initialMoney);

        bool canUpgrade = current_money_level < MAX_MONEY_LEVEL &&
            currentMoney > money_upgrade_cost * current_money_level * multiplier;
        // 置脏要在刷 UI 之前：顺序反了的话这一帧写进去的缓存会被接着清掉。
        upgradeUiDirty = true;
        cachedUpgradeCost = -1;
        cachedUpgradeLevel = -1;
        cachedUpgradeButtonState = !canUpgrade;
        // 开局就满级时必须走 MAX 那条：SetUpgradeActive 会印出「Level. 8 / Lvl UP: 2800」，
        // 而 FixedUpdate 在满级时根本不再刷升级 UI，那行错文本会挂整场。
        if (current_money_level >= MAX_MONEY_LEVEL) SetUpgradeMaxUI();
        else SetUpgradeActive(canUpgrade);
    }

    #endregion
}


