using System.IO;
using TMPro;
using Unity.VisualScripting;
using UnityEngine;
using UnityEngine.Android;
using UnityEngine.Audio;
using UnityEngine.Rendering.PostProcessing;
using UnityEngine.UI;

/// <summary>
/// 关卡控制器 - 管理关卡的核心逻辑，包括金钱系统、大炮系统、部署系统等
/// 为未来扩展更多关卡机制提供接口支持
/// </summary>
public class LevelController : MonoBehaviour
{
    #region Constants - 常量定义
    
    // 金钱系统常量
    private const float MONEY_CHARGING_SPEED = 187f;
    private const float MONEY_CHARGING_BONUS = 9f;
    private const int MAX_MONEY = 6000;
    private const int MONEY_UPGRADE_COST = 560;
    private const int MAX_MONEY_BONUS = 1500;
    protected const int MAX_MONEY_LEVEL = 8;
    private const float MIN_MULTIPLIER = 0.06f;
    private const float MAX_MULTIPLIER = 1.0f;
    
    // 大炮系统常量（已迁移到CatBase）
    
    // 宝藏系统常量
    private const int MAX_TREASURE_COUNT = 150;
    private const int MIN_TREASURE_COUNT = 0;
    
    // 游戏状态常量
    private const int DEFAULT_TARGET_FPS = 30;
    private const float NORMAL_TIME_SCALE = 1f;
    private const float SPEED_UP_TIME_SCALE = 2f;
    private const float PAUSED_TIME_SCALE = 0f;
    private const int INITIAL_LEVEL_SCORE = 10000;
    
    // 部署系统常量
    protected const int MAIN_DEPLOYER_COUNT = 10;
    protected const int GUEST_DEPLOYER_COUNT = 3;
    protected const int TOTAL_DEPLOYER_COUNT = 13;
    
    // 基地位置计算常量
    private const float BASE_OFFSET = 1600f;
    private const float BASE_POSITION_DIVISOR = 200f;
    private const float BASE_Y_POSITION = 1f;
    
    // 音频常量
    private const float MIN_VOLUME_DB = -80f;
    private const float VOLUME_LOG_MULTIPLIER = 20f;
    
    // 经验值计算常量
    private const float XP_TREASURE_MULTIPLIER = 1.5f;
    private const float XP_RANDOM_RANGE = 0.05f;
    private const float STUDY_POWER_MULTIPLIER = 2.5f;
    private const float STUDY_POWER_DIVISOR = 30f;
    
    // 奖励惩罚常量
    private const int REWARD_PENALTY_MULTIPLIER = 3;
    private const int PERCENTAGE_BASE = 100;
    private const int GUARANTEED_DROP_THRESHOLD = 99;
    
    // 猫基地生命值计算常量
    private const int CAT_BASE_BASE_HEALTH = 1000;
    private const int CAT_BASE_TREASURE_MULTIPLIER = 235000;
    
    // 跳过游戏伤害值
    private const int SKIP_GAME_DAMAGE = 99999999;
    
    #endregion

    #region Level Information - 关卡信息
    
    protected string chapterName = string.Empty;
    protected string sectionName = string.Empty;
    protected int sectionNum = 0;
    protected int diff = 0;
    protected int levelNum = 0;
    protected string levelName = "0";
    public LevelData LD;
    protected bool testificateMode = false;
    
    #endregion

    #region Game Objects - 游戏对象引用
    
    protected GameObject dogeBase;
    protected GameObject catBase;
    protected string[] characters_code;
    protected LevelRestrictionHelper.RestrictionRules levelRestrictions;
    
    #endregion

    #region Money System - 金钱系统
    
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

    #region Cannon System - 大炮系统
    // 逻辑已迁移到CatBase
    #endregion

    #region UI Components - UI组件
    
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
    [SerializeField] protected Button Speed_btn;
    [SerializeField] protected Button Skip_btn;
    [SerializeField] protected GameObject pause_black_shade;
    [SerializeField] protected GameObject pause_table;
    
    [Header("Level Info UI")]
    [SerializeField] protected TMP_Text levelName_txt;
    
    [Header("Deployment UI")]
    [SerializeField] protected Transform Deployers;
    [SerializeField] protected Transform GuestDeployers;
    [SerializeField] protected GameObject MaxDeployed;
    
    [Header("Audio Mixer")]
    [SerializeField] protected AudioMixer mixer;
    
    [Header("UI Sliders")]
    [SerializeField] protected Slider bgmSlider;
    [SerializeField] protected Slider seSlider;
    
    #endregion

    #region Game State - 游戏状态
    
    protected bool game_paused = false;
    protected bool speed_up = false;
    protected bool disable_controll = false;
    public bool isPloting = false;
    protected int level_score = INITIAL_LEVEL_SCORE;
    protected int gain_XP = 0;
    
    #endregion

    #region Deployment System - 部署系统
    
    public int maxEnemyDeploy = 50;
    public int maxCatDeploy = 50;
    protected int enemyDeployed = 0;
    protected int catDeployed = 0;
    
    // 注意：部署上限会在SetupLevelInfo中自动+1，因为CatBase和DogeBase也会注册到管理器
    // Base单位全局不可删除，不计入部署数量限制
    
    #endregion

    #region Reward System - 奖励系统
    
    protected Sprite[] reward_sprites;
    protected int[] reward_count;
    
    #endregion

    #region Proficency System - 熟练度系统
    
    protected Level_ProficencyUpdator LPU = new Level_ProficencyUpdator();
    
    #endregion

    #region Unity Lifecycle - Unity生命周期
    
    private void Awake()
    {
        Input.multiTouchEnabled = true;
    }
    
    private void Start()
    {
        Application.targetFrameRate = DEFAULT_TARGET_FPS;
        
        // 加载入场UI
        try 
        { 
            Instantiate(Resources.Load<GameObject>("UI/Tag In")); 
        }
        catch { }
        
        InitializeButtons();
        SetUpgradeActive(false);
        InitializeLevelData();
        BindCannonToBase();
        LoadPlot();
        MaxDeployed.SetActive(false);
        
        if (!isPloting)
        {
            SetupCatDeployers();
            SetupEnemySummoner();
        }
    }
    
    protected void FixedUpdate()
    {
        if (isPloting || disable_controll) return;
        
        // 更新金钱
        float moneyGain = (moneyCharching_speed + (current_money_level - 1) * moneyCharching_bonus) * multiplier * Time.deltaTime;
        AddMoney(moneyGain);
        
        // 更新分数
        level_score--;
        
        // 检查升级条件
        if (current_money_level < MAX_MONEY_LEVEL)
        {
            float current_upgrade_cost = money_upgrade_cost * current_money_level * multiplier;
            SetUpgradeActive(currentMoney > current_upgrade_cost);
        }
        
        // 更新大炮充能（由CatBase处理）
        if (catBase != null)
        {
            catBase.GetComponent<CatBase>()?.ChargeCannon();
        }
    }
    
    #endregion

    #region Initialization - 初始化方法
    
    /// <summary>
    /// 初始化所有按钮事件
    /// </summary>
    private void InitializeButtons()
    {
        Pause_btn.onClick.AddListener(() => Pause(!game_paused));
        
        GiveUp_btn.onClick.AddListener(() =>
        {
            Pause(false);
            Pause_btn.interactable = false;
            Speed_btn.interactable = false;
            GetComponent<SceneSwitcher>().TagOutToDirectly("BaseScene");
        });
        
        Speed_btn.onClick.AddListener(() => SpeedUp(!speed_up));
        Upgrade_btn.onClick.AddListener(UpgradeMoney);
        // 大炮按钮绑定由CatBase在关卡初始化后处理
        Skip_btn.onClick.AddListener(() => { Pause(false); SkipGame(); });
        
        bgmSlider.onValueChanged.AddListener(SetBGMVolume);
        seSlider.onValueChanged.AddListener(SetSEVolume);
        
        pause_table.SetActive(false);
    }
    
    /// <summary>
    /// 初始化关卡数据（可由子类重写）
    /// </summary>
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
        
        // 计算金钱倍率
        CalculateMoneyMultiplier();
        
        // 设置地图和基地
        SetupMapAndBases(mapSize);
        
        // 设置关卡信息
        SetupLevelInfo();
        levelRestrictions = LevelRestrictionHelper.Parse(LD.Restriction);
        
        // 设置战斗效果
        SetupCombatEffects();
        
        // 设置光环效果
        SetupCombatAura();
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
    /// 设置地图和基地
    /// </summary>
    protected void SetupMapAndBases(int mapSize)
    {
        // 设置背景
        GameObject.Find("Background").GetComponent<BackgroundInitializer>().UpdateMaterialProperties(LD.BackgroundID);
        
        // 获取基地引用
        dogeBase = GameObject.Find("DogeBase");
        catBase = GameObject.Find("CatBase");
        
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
        catBase.GetComponent<CatBase>().ApplyLevelBaseHealth(
            CAT_BASE_BASE_HEALTH + CAT_BASE_TREASURE_MULTIPLIER * treasureCount / MAX_TREASURE_COUNT);
        
        // 设置狗基地外观
        Sprite baseSprite = Resources.Load<Sprite>($"Units/DogeBases/{LD.BaseImageID}");
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
        
        // 计算经验值
        float xpMultiplier = 1 + treasureCount / (float)MAX_TREASURE_COUNT * XP_TREASURE_MULTIPLIER + Random.Range(0f, XP_RANDOM_RANGE);
        gain_XP = (int)(LD.gainXP * xpMultiplier);
        
        // 设置部署限制（+1因为CatBase和DogeBase也会注册到管理器）
        maxEnemyDeploy = LD.maxEmenyCount;
        maxCatDeploy = LD.maxCatCount;
    }
    
    /// <summary>
    /// 设置战斗效果
    /// </summary>
    protected void SetupCombatEffects()
    {
        if (LD.CombatEffect == null) return;
        
        for (int i = 0; i < LD.CombatEffect.Length; i++)
        {
            GameObject combatEffect = Resources.Load<GameObject>($"Background/CombatEffects/{LD.CombatEffect[i]}");
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
        string plotLoadPath = $"LevelData/LevelEnemyData/{chapterName}/{sectionName}/dif{diff}/plots/{levelNum}";
        GamePlot gamePlot = Resources.Load<GamePlot>(plotLoadPath);
        
        if (gamePlot != null)
        {
            Chatbox chatbox = Instantiate(Resources.Load<GameObject>("UI/ChatBox")).GetComponent<Chatbox>();
            chatbox.SetFullDialogue(gamePlot);
            StartCoroutine(chatbox.ShowAllDialogue());
            isPloting = true;
        }
    }
    
    /// <summary>
    /// 退出剧情
    /// </summary>
    public void ExitPlot()
    {
        isPloting = false;
        SetupCatDeployers();
        SetupEnemySummoner();
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
        
        money_txt.text = $"{(int)currentMoney} / {(int)currentMaxMoney}$";
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
    /// 设置升级按钮状态
    /// </summary>
    protected void SetUpgradeActive(bool active)
    {
        int upgradeCost = (int)(current_money_level * money_upgrade_cost * multiplier);
        money_upgrade_txt.text = $"Level. {current_money_level}\n\n\nLvl UP: {upgradeCost}";
        Upgrade_btn.interactable = active;
    }
    
    /// <summary>
    /// 升级金钱系统
    /// </summary>
    protected void UpgradeMoney()
    {
        if (current_money_level == MAX_MONEY_LEVEL)
        {
            money_upgrade_txt.text = $"Level. {MAX_MONEY_LEVEL}\n\n\nLvl UP: MAX";
            Upgrade_btn.interactable = false;
            return;
        }
        
        float upgradeCost = money_upgrade_cost * current_money_level * multiplier;
        if (currentMoney < upgradeCost) return;
        
        currentMoney -= upgradeCost;
        current_money_level++;
        Upgrade_btn.GetComponent<AudioSource>().Play();
        
        if (current_money_level == MAX_MONEY_LEVEL)
        {
            money_upgrade_txt.text = $"Level. {MAX_MONEY_LEVEL}\n\n\nLvl UP: MAX";
            Upgrade_btn.interactable = false;
        }
    }
    
    #endregion

    #region Cannon System Methods - 大炮系统方法
    /// <summary>
    /// 绑定大炮UI到CatBase
    /// </summary>
    private void BindCannonToBase()
    {
        if (catBase == null) return;
        CatBase baseUnit = catBase.GetComponent<CatBase>();
        if (baseUnit == null) return;

        baseUnit.SetupCannonUI(Cannon_btn, Cannon_btn_image, Cannon_btn_animation);
        Cannon_btn.onClick.RemoveAllListeners();
        Cannon_btn.onClick.AddListener(baseUnit.CannonFire);
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
            ShowPauseTable(false);
            Time.timeScale = NORMAL_TIME_SCALE;
            Application.targetFrameRate = DEFAULT_TARGET_FPS;
            Pause_btn.GetComponent<Image>().color = Color.white;
            Speed_btn.GetComponent<Image>().color = new Color(1, 1, 1, 0.5f);
            speed_up = false;
        }
        
        game_paused = pause;
    }
    
    /// <summary>
    /// 加速游戏
    /// </summary>
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
        }
        else
        {
            ShowPauseTable(false);
            Time.timeScale = NORMAL_TIME_SCALE;
            Application.targetFrameRate = DEFAULT_TARGET_FPS;
            Speed_btn.GetComponent<Image>().color = new Color(1, 1, 1, 0.5f);
            Pause_btn.GetComponent<Image>().color = Color.white;
            game_paused = false;
        }
        
        speed_up = speedUp;
    }
    
    /// <summary>
    /// 跳过游戏（用于测试）
    /// </summary>
    public void SkipGame()
    {
        dogeBase.GetComponent<DogeBase>().ReceiveAttack(SKIP_GAME_DAMAGE, null, null, null, null, null, null);
    }
    
    #endregion

    #region Audio Methods - 音频方法
    
    /// <summary>
    /// 设置BGM音量
    /// </summary>
    public void SetBGMVolume(float linear)
    {
        float dB = linear <= 0 ? MIN_VOLUME_DB : VOLUME_LOG_MULTIPLIER * Mathf.Log10(linear);
        Debug.Log(dB);
        mixer.SetFloat(UXPref.BGM_PARAM, dB);
        PlayerPrefs.SetFloat(UXPref.BGM_PARAM, linear);
    }
    
    /// <summary>
    /// 设置SE音量
    /// </summary>
    public void SetSEVolume(float linear)
    {
        float dB = linear <= 0 ? MIN_VOLUME_DB : VOLUME_LOG_MULTIPLIER * Mathf.Log10(linear);
        Debug.Log(dB);
        mixer.SetFloat(UXPref.SE_PARAM, dB);
        PlayerPrefs.SetFloat(UXPref.SE_PARAM, linear);
    }
    
    /// <summary>
    /// 刷新暂停菜单
    /// </summary>
    private void RefreshPauseTable()
    {
        bgmSlider.value = PlayerPrefs.GetFloat(UXPref.BGM_PARAM, 1);
        seSlider.value = PlayerPrefs.GetFloat(UXPref.SE_PARAM, 1);
        SetBGMVolume(bgmSlider.value);
        SetSEVolume(seSlider.value);
    }
    
    /// <summary>
    /// 显示/隐藏暂停菜单
    /// </summary>
    private void ShowPauseTable(bool show)
    {
        if (show)
        {
            RefreshPauseTable();
            pause_black_shade.SetActive(true);
            pause_table.SetActive(true);
            Speed_btn.gameObject.SetActive(false);
        }
        else
        {
            pause_black_shade.SetActive(false);
            pause_table.SetActive(false);
            Speed_btn.gameObject.SetActive(true);
        }
    }
    
    /// <summary>
    /// 改变BGM
    /// </summary>
    public void ChangeBGM(string audio_name)
    {
        if (audio_name == null)
        {
            GameObject.Find("BGM").GetComponent<AudioSource>().clip = null;
        }
    }
    
    #endregion

    #region Victory and Failure - 胜利与失败
    
    /// <summary>
    /// 关卡胜利
    /// </summary>
    public virtual void Victory()
    {
        disable_controll = true;
        SpeedUp(false);
        Pause_btn.gameObject.SetActive(false);
        Speed_btn.gameObject.SetActive(false);
        
        if (testificateMode) return;
        
        // 创建胜利UI
        GameObject clearCanvas = Instantiate(Resources.Load<GameObject>("UI/Clear_Canvas"));
        clearCanvas.transform.GetChild(0).GetChild(0).GetComponent<TMP_Text>().text = level_score.ToString();
        ClearCanvas clearCanvasComponent = clearCanvas.GetComponent<ClearCanvas>();
        
        ChangeBGM(null);
        QuitUI();

        // 处理奖励
        bool gainreward = ProcessRewards(clearCanvasComponent);

        // 处理经验值
        ProcessExperience(clearCanvas);
        
        // 保存进度
        //int catHeadCount = catBase.GetComponent<CatBase>().;
        GameProgressSave.SaveProgress(chapterName, sectionName, diff, levelNum, level_score, 
            gainreward, characters_code, 0);
        
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
        
        if (testificateMode) return;
        
        UnlockEnemiesMet();
        LPU.EndAccounting();
        
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
            
            // 应用奖励惩罚
            if (rewardPenalty > 0 && !rewardList[i].onlyOnce)
            {
                gainTimes = gainTimes * (PERCENTAGE_BASE - REWARD_PENALTY_MULTIPLIER * rewardPenalty) / PERCENTAGE_BASE;
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
        int gainTimes = 0;
        
        if (rewardInfo.droprate > GUARANTEED_DROP_THRESHOLD)
        {
            gainTimes = rewardInfo.drawtimes;
        }
        else if (rewardInfo.drawtimes > GUARANTEED_DROP_THRESHOLD)
        {
            gainTimes = rewardInfo.drawtimes * rewardInfo.droprate / PERCENTAGE_BASE;
        }
        else
        {
            for (int gt = 0; gt < rewardInfo.drawtimes; gt++)
            {
                int random = Random.Range(0, PERCENTAGE_BASE);
                if (random < rewardInfo.droprate)
                {
                    gainTimes++;
                }
            }
        }
        
        return gainTimes;
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
            default:
                break;
        }
    }
    
    /// <summary>
    /// 检查是否有新奖励
    /// </summary>
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
    /// 处理经验值
    /// </summary>
    private void ProcessExperience(GameObject clearCanvas)
    {
        GameProgressSave.SectionClearList sectionClearList = GameProgressSave.LoadSectionProgress(chapterName, sectionName);
        int studyPower = GenericSaveSystem.LoadData<int[]>(RewardingSystem.filename)[RewardingSystem.RewardNumMap[RewardName.Base_Study]];
        
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
        int[] proficiencyLevels = LPU.SetUp(characters_code);
        int teamProficiencyBonus = CalculateTeamProficiencyBonus(proficiencyLevels);
        
        // 设置主要部署器
        for (int i = 0; i < MAIN_DEPLOYER_COUNT; i++)
        {
            UnitDeployer deployer = Deployers.GetChild(i).GetComponent<UnitDeployer>();
            string code = characters_code[i];
            deployer.SetupDeployer(code, treasureCount, proficiencyLevels[i], teamProficiencyBonus);
            LevelRestrictionHelper.ApplyToDeployer(deployer, code, levelRestrictions, false);
        }
        
        // 设置访客部署器
        SetupGuestDeployersNormal(proficiencyLevels, teamProficiencyBonus);
    }
    
    /// <summary>
    /// 设置嘉宾部署器
    /// </summary>
    protected void SetupGuestDeployersNormal(int[] proficiencyLevels, int teamProficiencyBonus)
    {
        for (int i = MAIN_DEPLOYER_COUNT; i < TOTAL_DEPLOYER_COUNT; i++)
        {
            CharacterData characterData = LoadGuestCharacterData(characters_code[i]);
            if (characterData == null)
            {
                GuestDeployers.GetChild(i - MAIN_DEPLOYER_COUNT).gameObject.SetActive(false);
                continue;
            }
            
            UnitDeployer deployer = GuestDeployers.GetChild(i - MAIN_DEPLOYER_COUNT).GetComponent<UnitDeployer>();
            string code = characters_code[i];
            deployer.SetupDeployer(code, treasureCount, proficiencyLevels[i], teamProficiencyBonus);
            LevelRestrictionHelper.ApplyToDeployer(deployer, code, levelRestrictions, true);
        }
    }
    
    /// <summary>
    /// 加载角色数据
    /// </summary>
    protected CharacterData LoadGuestCharacterData(string characterCode)
    {
        try
        {
            string path = $"Units/Cat Units/6/{characterCode.Substring(1, 3)}/{characterCode[4]}/data";
            return Resources.Load<CharacterData>(path);
        }
        catch
        {
            Debug.LogWarning($"No such character way path: {characterCode}");
            return null;
        }
    }
    
    /// <summary>
    /// 计算团队熟练度加成
    /// </summary>
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
    
    /// <summary>
    /// 设置敌人召唤器
    /// </summary>
    private void SetupEnemySummoner()
    {
        for (int i = 0; i < LD.enemySummoners.Length; i++)
        {
            int healthPercentage = LD.enemySummoners[i].HealthPercentageOnBreak;
            LevelEnemySummoner enemySummoner = gameObject.AddComponent<LevelEnemySummoner>();
            enemySummoner.SetBase(dogeBase);
            enemySummoner.SetupEnemyDeployer(healthPercentage, LD.enemySummoners[i].enemySummonInfos);
            enemySummoner.SetChangeBGM(LD.enemySummoners[i].bgm);
        }
    }
    
    /// <summary>
    /// 部署一只猫
    /// </summary>
    public bool DeployACat()
    {
        if (catDeployed >= maxCatDeploy) return false;
        
        catDeployed++;
        Debug.Log($"Current Cats: {catDeployed} / {maxCatDeploy}");
        MaxDeployed.SetActive(catDeployed >= maxCatDeploy);
        return true;
    }
    
    /// <summary>
    /// 部署一个敌人
    /// </summary>
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
        MaxDeployed.SetActive(false);
        catDeployed--;
        Debug.Log($"Cat Remove: {catDeployed+1} - 1.");
    }
    
    /// <summary>
    /// 移除一个敌人
    /// </summary>
    public void RemoveAnEnemy()
    {
        enemyDeployed--;
        Debug.Log($"Enemy Remove: {enemyDeployed + 1} - 1.");
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
    
    /// <summary>
    /// 解锁遇到的敌人
    /// </summary>
    public void UnlockEnemiesMet()
    {
        if (LD == null) return;
        
        for (int i = 0; i < LD.enemySummoners.Length; i++)
        {
            for (int j = 0; j < LD.enemySummoners[i].enemySummonInfos.Length; j++)
            {
                string enemyID = LD.enemySummoners[i].enemySummonInfos[j].enemyID;
                int code = int.Parse(enemyID.Substring(1, 3));
                EnemyMeetSave.SetMetEnemyCode(code);
            }
        }
    }
    
    #endregion

    #region Proficency System Methods
    
    /// <summary>
    /// 记录角色部署
    /// </summary>
    public void RecordProficency_Deploy(string code)
    {
        if (!disable_controll)
        {
            LPU.Record_CharacterDeploy(code);
        }
    }
    
    /// <summary>
    /// 记录角色造成的伤害
    /// </summary>
    public void RecordProficency_DamageDealt(string code, int dmg)
    {
        if (!disable_controll)
        {
            LPU.Record_CharacterDamageDealt(code, dmg);
        }
    }
    
    /// <summary>
    /// 记录角色受到的伤害
    /// </summary>
    public void RecordProficency_DamageTaken(string code, int dmg)
    {
        if (!disable_controll)
        {
            LPU.Record_CharacterDamageTaken(code, dmg);
        }
    }
    
    /// <summary>
    /// 记录角色受到的减益效果
    /// </summary>
    public void RecordProficency_DebuffSuffered(string code, int t)
    {
        if (!disable_controll)
        {
            LPU.Record_CharacterDebuffSuffered(code, t);
        }
    }
    
    #endregion
}
