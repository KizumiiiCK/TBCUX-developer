using System.Collections;
using System.Collections.Generic;
using System.IO;
using TMPro;
using UnityEngine;
using UnityEngine.Localization.SmartFormat;
using UnityEngine.Localization.SmartFormat.Core.Parsing;
using UnityEngine.UI;

public class CatIndexCanvas : UICanvasMain
{
    private GameObject mainCamera;
    public int rality = 0;
    public string current_code = "000";
    public int current_tire = 0;
    public int current_level = 1;
    public CharacterProficiency current_prof = null;
    private GameObject current_display_character;
    private RalityOption ralityOption;
    [SerializeField] private Image[] Char_Rarity_Btns = new Image[4];
    [SerializeField] private Button Animation_switch_btn;
    [SerializeField] private Button Show_info_btn;
    [SerializeField] private Button GoToEquip_btn;
    [SerializeField] private Button ToggleUnowned_btn;
    [SerializeField] private Image ToggleUnowned_image;
    [SerializeField] private Sprite ToggleShowUnowned_sprite;
    [SerializeField] private Sprite ToggleHideUnowned_sprite;
    //Upgrade
    [SerializeField] private KiButton Upgrade_btn;
    private RewardName cateyeConsuming = RewardName.Cateye_EX;
    private int cateyeConsume_Amount = 0;
    //Tire Up
    [SerializeField] private KiButton TireUp_btn;
    [SerializeField] private EvolveComfirm EvolveComfirmCanvas;
    //Talent
    [SerializeField] private KiButton Talent_btn;
    protected bool TireUp_itemNeeded = true;
    protected RewardName[] TireUp_consumeItems = new RewardName[6];
    protected int[] TireUp_consumeAmount = new int[6];
    //Legend Ticket Exchange
    [SerializeField] private Button LegendExchange_btn;
    /// <summary>按钮上的文字框。没填就在 InitializeButtons 里往子物体找一个。</summary>
    [SerializeField] private TMP_Text LegendExchange_text;
    //UI elements
    [SerializeField] private Button Prof_btn;
    [SerializeField] private FrameCurrencyItem upgradeCostXpItem;
    [SerializeField] private FrameCurrencyItem upgradeCostCatEyeItem;
    [SerializeField] private TMP_Text current_level_text;
    [SerializeField] private TMP_Text name_txt;
    [SerializeField] private Image background;
    [SerializeField] private GameObject character_info_board;
    [SerializeField] private Material ghostMaterialTemplate;
    [Header("Instantiators")]
    //private static Vector3 scroll_gap = new Vector3(0, -200, 0);
    [SerializeField] private RectTransform HeadIcon_ScrollingArea;
    [SerializeField] private ScrollRect HeadIconScrollRect;
    [SerializeField] private GameObject CatHeadIcon;
    [SerializeField] private IndexViewer IV;
    [SerializeField] private int headIconColumns = 1;
    [SerializeField] private float iconStepX = 200f;
    [SerializeField] private float iconStepY = 200f;
    [SerializeField] private int preloadRows = 2;
    //[SerializeField] private CustomScrollbar scrollbar_setting;
    [SerializeField] private GameObject indexUnit;
    [SerializeField] private GameObject ProficiencyTable;
    [Header("Main Controllers")]
    private BaseCanvas baseCanvas;
    //
    private bool UnityAnimated = false;
    private int current_animation_num = 0;
    private int playableAnimCount = 4;
    int upgrade_cost = 0;
    private bool upgradeLock = true;
    private CharacterProficiency proficiency;
    private readonly List<int> runtimeExtraCurrencyIds = new List<int>();
    private RectTransform headIconViewport;
    private readonly List<string> currentRarityCodes = new List<string>();
    private readonly Dictionary<string, Sprite> currentRarityIconCache = new Dictionary<string, Sprite>();
    private readonly Dictionary<int, List<string>> rarityCodesCache = new Dictionary<int, List<string>>();
    private readonly Dictionary<int, Dictionary<string, Sprite>> rarityIconsCache = new Dictionary<int, Dictionary<string, Sprite>>();
    private readonly Dictionary<string, bool> unlockedBaseTireCache = new Dictionary<string, bool>();
    private readonly Dictionary<int, GameObject> activeHeadIcons = new Dictionary<int, GameObject>();
    private readonly Stack<GameObject> pooledHeadIcons = new Stack<GameObject>();
    private int lastVisibleStart = -1;
    private int lastVisibleEnd = -1;
    private bool virtualListReady = false;
    private VirtualizedScrollGrid<string> headIconGrid;
    private CatBackgroundSwitcher backgroundSwitcher;
    private static readonly Color CostEnoughColor = new Color(1f, 1f, 0.5f, 1f);
    private static readonly Color CostInsufficientColor = new Color(1f, 0f, 0f, 1f);
    private static readonly Color UpgradeButtonEnabledColor = new Color(0.55f, 0.95f, 0.9f, 1f);
    private static readonly Color UpgradeButtonDisabledColor = new Color(1f, 0.72f, 0.72f, 1f);
    // 传说票直接兑换：只在 Uber Rare 页可用，一次一张。
    private const int LegendExchangeRarity = 4;
    private const RewardName LegendExchangeTicket = RewardName.Ticket_Legend;
    private const int LegendExchangeCost = 1;
    private const string LegendExchangeRedeemTextId = "id:LTRedeem";
    private const string LegendExchangeUnlockTextId = "id:LTUnlock";
    private const string LegendExchangePlusOneTextId = "id:LTPlusOne";
    private const string LegendExchangeMaxLabel = "MAX";
    // 本地化是异步回来的，先用 id 兜底，回调到了再重刷一次按钮文字。
    private string legendExchangeRedeemText = LegendExchangeRedeemTextId;
    private string legendExchangeUnlockText = LegendExchangeUnlockTextId;
    private string legendExchangePlusOneText = LegendExchangePlusOneTextId;
    /// <summary>已经点过一次、正在等第二次确认。任何一次刷新（换角色/换稀有度/兑换完成）都会清掉。</summary>
    private bool legendExchangeArmed = false;
    private bool returnToEquipMode = false;
    private bool showUnownedCharacters = true;
    private bool isLoadingRarityCharacters = false;
    private Coroutine loadRarityRoutine;
    private const string EquipCanvasPrefab = "EquipCanvas";

    private static Dictionary<int, RewardName> CateyeConsume_rality = new Dictionary<int, RewardName>
    {
        { 0, RewardName.Cateye_EX},
        { 1, RewardName.Cateye_EX},
        { 2, RewardName.Cateye_Rare},
        { 3, RewardName.Cateye_SuperRare},
        { 4, RewardName.Cateye_UberRare},
        { 5, RewardName.Cateye_Legend},
        { 6, RewardName.Cateye_UberRare}
    };

    // Start is called before the first frame update
    void Start()
    {
        //RewardingSystem.GainReward(RewardName.XP, 10000000);
        mainCamera = GameObject.Find("Main Camera");
        GetComponent<Canvas>().worldCamera=mainCamera.GetComponent<Camera>();
        baseCanvas=GameObject.Find("BaseCanvas").GetComponent<BaseCanvas>();
        InitializeVirtualizedHeadIconList();
        InitializeButtons();
        InitializeRalityOption();
        InitializeBackgroundSwitcher();
        UpdateBackground();
        LoadCharatersFromRality(0);
    }

    public void LoadCharatersFromRality(int R)
    {
        if (isLoadingRarityCharacters) return;
        if (loadRarityRoutine != null) StopCoroutine(loadRarityRoutine);
        loadRarityRoutine = StartCoroutine(LoadCharatersFromRalityRoutine(R));
    }

    private IEnumerator LoadCharatersFromRalityRoutine(int R)
    {
        SetRarityLoadingState(true);
        rality = R;
        RefreshFrameUICurrenciesForRarity();
        yield return EnsureRarityCacheBuilt(rality);
        currentRarityCodes.Clear();
        currentRarityIconCache.Clear();
        List<string> sourceCodes = GetVisibleCodesForCurrentRarity();
        Dictionary<string, Sprite> iconMap = rarityIconsCache[rality];
        for (int i = 0; i < sourceCodes.Count; i++)
        {
            string code = sourceCodes[i];
            currentRarityCodes.Add(code);
            if (iconMap.TryGetValue(code, out var icon)) currentRarityIconCache[code] = icon;
            if ((i + 1) % 80 == 0) yield return null;
        }

        if (virtualListReady)
        {
            headIconGrid.SetData(currentRarityCodes, true);
        }
        else
        {
            RebuildHeadIconsLegacy();
        }

        loadRarityRoutine = null;
        // 必须先结束加载态：ShowCertainCharacter / ShowCertainCharInTire 在 isLoadingRarityCharacters 为 true 时会直接 return
        SetRarityLoadingState(false);
        if (currentRarityCodes.Count > 0)
        {
            ShowCertainCharacter(currentRarityCodes[0]);
            ShowCertainCharInTire(current_tire);
        }
        // 空稀有度时上面两步不会跑，按钮状态得在这里兜一次
        RefreshLegendExchangeButton();
    }
    public void ShowCertainCharacter(string char_code)
    {
        if (isLoadingRarityCharacters) return;
        current_code = char_code;
        Sprite[] head_icons=new Sprite[4];
        CharacterUpgradeSave.UpgradeDetails UD = CharacterUpgradeSave.GetDetails($"{rality}{current_code}");
        bool[] unlocked = UD.tire_unlocked;
        current_level = UD.TotalLevel();
        current_tire = GetDefaultDisplayTire(unlocked);
        if (unlocked[0]) { upgradeLock = false; }
        else { upgradeLock = true; }
        bool enableTireButton = true;
        TotalUpgradeCost tuc= BundledAddressables.LoadSync<TotalUpgradeCost>($"Units/Cat Units/{rality}/{current_code}/upgrade");
        for (int i = 0; i < 4; i++)
        {
            head_icons[i]= BundledAddressables.LoadSync<Sprite>($"Units/Cat Units/{rality}/{current_code}/{i}/icon_deploy");
            if (i > 0) if (tuc.cost[(i - 1)].method == UpgradeMethod.unavailable)
            {
                Char_Rarity_Btns[i].gameObject.SetActive(false); continue;
            }
            if (head_icons[i] == null)
            {
                Char_Rarity_Btns[i].gameObject.SetActive(false);
            }
            else
            {
                Char_Rarity_Btns[i].gameObject.SetActive(true);
                Char_Rarity_Btns[i].sprite= head_icons[i];
                Button b = Char_Rarity_Btns[i].GetComponent<Button>();
                if (enableTireButton) b.interactable = true;
                else b.interactable = false;
                if(unlocked[i]) b.transform.GetChild(0).gameObject.SetActive(false);
                else { enableTireButton = false; b.transform.GetChild(0).gameObject.SetActive(true); }
            }
        }
    }

    private int GetDefaultDisplayTire(bool[] unlocked)
    {
        if (unlocked == null || unlocked.Length == 0) return 0;
        for (int i = unlocked.Length - 1; i >= 0; i--)
        {
            if (unlocked[i]) return i;
        }
        return 0;
    }

    public int GetCurrentCharacterDefaultTire()
    {
        CharacterUpgradeSave.UpgradeDetails ud = CharacterUpgradeSave.GetDetails($"{rality}{current_code}");
        return GetDefaultDisplayTire(ud.tire_unlocked);
    }
    public void ShowCertainCharInTire(int tire, bool resetAnimation=true)
    {
        if (isLoadingRarityCharacters) return;
        current_tire= tire;
        string loadPath = $"Units/Cat Units/{rality}/{current_code}/{tire}/";
        CharacterData CD = BundledAddressables.LoadSync<CharacterData>(loadPath + "data");
        if (resetAnimation)
        {
            Application.targetFrameRate = 30;
            if (current_display_character != null) DestroyImmediate(current_display_character.gameObject);
            //string loadPath = $"Units/Cat Units/{rarity}/{current_code}/{tire}/";
            //CharacterData CD = Resources.Load<CharacterData>(loadPath + "data");
            current_display_character = CharacterSummoner.CreateACharacter(true, $"{rality}{current_code}{tire}", true);
            CharacterSummoner.SetCharacterPosition(current_display_character,
                mainCamera.transform.position + new Vector3(CD.UNITYAnimated ? -2 : 0, -6, 10));
            CharacterVisualLoader.ResetAnimationOrderLayer(current_display_character, "UI", 3);
            current_display_character.transform.localScale = current_display_character.transform.localScale * 1.3f;
            UnityAnimated = CD.UNITYAnimated;
            if (UnityAnimated) current_display_character.transform.localScale *= 1.2f;
            current_animation_num = 0;
            playableAnimCount = CharacterVisualLoader.GetPlayableAnimCount(
                current_display_character, UnityAnimated, true, $"{rality}{current_code}{tire}", CD);
            CharacterVisualLoader.SwitchAnimation(current_display_character, UnityAnimated, current_animation_num);
            LocalizationHelper.GetLocalizedText("UnitNames", $"{rality}{current_code}{tire}", localizedText => name_txt.text = localizedText ?? $"{rality}{current_code}{tire}");
        }
        //
        CharacterUpgradeSave.UpgradeDetails ud = CharacterUpgradeSave.GetDetails($"{rality}{current_code}");
        TalentData talent = ud.talent_unlocked ? TalentData.Load($"{rality}{current_code}") : null;
        IV.ShowCharacterDetails(ApplyTalentForDisplay(CD, talent), true, current_level, talent);
        CheckUpgradeAvailable(ud);
    }

    /// <summary>
    /// 已解锁本能的角色，图鉴要看到叠加后的样子。
    /// LoadSync 给的是 Addressables 缓存里的资源本体，绝不能就地改，所以先 Clone 再叠。
    /// </summary>
    private CharacterData ApplyTalentForDisplay(CharacterData source, TalentData talent)
    {
        if (source == null || talent == null) return source;
        CharacterData merged = source.Clone();
        talent.ApplyTo(merged);
        return merged;
    }
    public void InitializeButtons()
    {
        Animation_switch_btn.onClick.AddListener(SwitchAnimation);
        Upgrade_btn.onClick.AddListener(UpgradeCharacter);
        TireUp_btn.onClick.AddListener(TireUpComfirm);
        Prof_btn.onClick.AddListener(ShowProfTable);
        EvolveComfirmCanvas.SetController(this);
        Show_info_btn.onClick.AddListener(InfoBoardDisplay);
        if (GoToEquip_btn != null) GoToEquip_btn.onClick.AddListener(OpenEquipFromCatIndex);
        if (ToggleUnowned_btn != null) ToggleUnowned_btn.onClick.AddListener(ToggleUnownedCharacters);
        if (LegendExchange_btn != null) LegendExchange_btn.onClick.AddListener(OnLegendExchangeClicked);
        if (Talent_btn != null) Talent_btn.onClick.AddListener(OnTalentClicked);
        // 普通 Button 没有自带 label，文字框没在 Inspector 里指就自己往子物体找一个。
        if (LegendExchange_text == null && LegendExchange_btn != null)
            LegendExchange_text = LegendExchange_btn.GetComponentInChildren<TMP_Text>(true);
        RefreshUnownedToggleVisual();
        CacheLegendExchangeTexts();
        RefreshLegendExchangeButton();
    }

    private void ToggleUnownedCharacters()
    {
        if (isLoadingRarityCharacters) return;
        showUnownedCharacters = !showUnownedCharacters;
        RefreshUnownedToggleVisual();
        LoadCharatersFromRality(rality);
    }

    private void RefreshUnownedToggleVisual()
    {
        if (ToggleUnowned_image == null) return;
        ToggleUnowned_image.sprite = showUnownedCharacters ? ToggleShowUnowned_sprite : ToggleHideUnowned_sprite;
    }

    private void SetRarityLoadingState(bool loading)
    {
        isLoadingRarityCharacters = loading;
        if (ToggleUnowned_btn != null) ToggleUnowned_btn.interactable = !loading;
        if (HeadIconScrollRect != null) HeadIconScrollRect.enabled = !loading;
    }

    private IEnumerator EnsureRarityCacheBuilt(int rarity)
    {
        if (rarityCodesCache.ContainsKey(rarity)) yield break;
        List<string> codes = new List<string>();
        Dictionary<string, Sprite> iconMap = new Dictionary<string, Sprite>();
        for (int i = 0; i < 1000; i++)
        {
            string code = i.ToString("000");
            Sprite icon = BundledAddressables.LoadSync<Sprite>($"Units/Cat Units/{rarity}/{code}/0/icon_deploy");
            if (icon == null) continue;
            codes.Add(code);
            iconMap[code] = icon;
            if ((i + 1) % 80 == 0) yield return null;
        }
        rarityCodesCache[rarity] = codes;
        rarityIconsCache[rarity] = iconMap;
    }

    private List<string> GetVisibleCodesForCurrentRarity()
    {
        var result = new List<string>();
        if (!rarityCodesCache.TryGetValue(rality, out var allCodes)) return result;
        if (showUnownedCharacters) return new List<string>(allCodes);
        for (int i = 0; i < allCodes.Count; i++)
        {
            string code = allCodes[i];
            string key = $"{rality}{code}";
            if (!unlockedBaseTireCache.TryGetValue(key, out bool unlocked))
            {
                unlocked = CharacterUpgradeSave.GetDetails(key).tire_unlocked[0];
                unlockedBaseTireCache[key] = unlocked;
            }
            if (unlocked) result.Add(code);
        }
        return result;
    }

    private void OpenEquipFromCatIndex()
    {
        if (FrameUI == null) return;
        FrameUI.OpenPage(EquipCanvasPrefab, null, null, FrameUIDisplayer.DoorAction.None);
    }

    private void InitializeRalityOption()
    {
        ralityOption = GetComponentInChildren<RalityOption>(true);
        // if (ralityOption != null)
        if (ralityOption != null)
        {
            ralityOption.Initialize(OnRalitySelected, 0);
        }
        else
        {
            LoadCharatersFromRality(0);
        }
    }

    private void OnRalitySelected(int selectedRarity)
    {
        if (isLoadingRarityCharacters) return;
        LoadCharatersFromRality(selectedRarity);
    }
    private void SwitchAnimation()
    {
        if (current_display_character == null) return;
        int count = playableAnimCount > 0 ? playableAnimCount : 4;
        current_animation_num = (current_animation_num + 1) % count;
        CharacterVisualLoader.SwitchAnimation(current_display_character, UnityAnimated, current_animation_num);
    }
    private void UpgradeCharacter()
    {
        CharacterUpgradeSave.UpgradeCharacterByXP($"{rality}{current_code}");
        RewardingSystem.ConsumeItem(RewardName.XP, upgrade_cost);
        if (cateyeConsume_Amount > 0)
        {
            RewardingSystem.ConsumeItem(cateyeConsuming, cateyeConsume_Amount);
        }
        baseCanvas.UpdateCurrencies();
        ShowCertainCharInTire(current_tire, false);
        CheckUpgradeAvailable();
    }
    private void CheckUpgradeAvailable(CharacterUpgradeSave.UpgradeDetails details = null)
    {
        Debug.Log($"Try getting ID from Characters: {rality}{current_code}");
        CharacterUpgradeSave.UpgradeDetails UD = details ?? CharacterUpgradeSave.GetDetails($"{rality}{current_code}");
        current_level = UD.TotalLevel();
        upgrade_cost = (int)(UpgradeCost.XPcost[rality, UD.upgraded_level % 10] * (1 + UD.upgraded_level / 10 * 0.5f));
        cateyeConsuming = GetCateyeConsumeReward(rality);
        cateyeConsume_Amount = GetCateyeConsumeAmount(rality, UD.upgraded_level);
        bool XPenough = RewardingSystem.CheckItemIsEnough(RewardName.XP, upgrade_cost);
        bool EYEenough = cateyeConsume_Amount <= 0 || RewardingSystem.CheckItemIsEnough(cateyeConsuming, cateyeConsume_Amount);
        int xpRewardId = RewardingSystem.RewardNumMap[RewardName.XP];
        int catEyeRewardId = RewardingSystem.RewardNumMap[cateyeConsuming];
        if (CharacterUpgradeSave.XPUpgradeAvailable($"{rality}{current_code}"))
        {
            if (upgradeCostXpItem != null)
                upgradeCostXpItem.SetData(xpRewardId, upgrade_cost, XPenough ? CostEnoughColor : CostInsufficientColor);
            if (upgradeCostCatEyeItem != null)
                upgradeCostCatEyeItem.SetData(catEyeRewardId, cateyeConsume_Amount, EYEenough ? CostEnoughColor : CostInsufficientColor);
            if (upgradeLock) Upgrade_btn.interactable = false;
            else Upgrade_btn.interactable = XPenough && EYEenough;
        }
        else
        {
            if (upgradeCostXpItem != null) upgradeCostXpItem.SetData(xpRewardId, 0, CostEnoughColor);
            if (upgradeCostCatEyeItem != null) upgradeCostCatEyeItem.SetData(catEyeRewardId, 0, CostEnoughColor);
            Upgrade_btn.interactable = false;
        }
        current_level_text.text = $"LEVEL\n{UD.upgraded_level} + {UD.plus_level}";
        TireUp_btn.interactable = CheckTireUpAvailable($"{rality}{current_code}");
        if (Talent_btn != null) Talent_btn.interactable = TalentUnlockAvailable(UD);
        RefreshUpgradeButtonsVisual();
        // 换角色/换阶段都会走到这里，顺便把传说票按钮的文字和二次确认状态复位
        RefreshLegendExchangeButton();
        //
        // Proficiency
        current_prof = UD.proficiency;
        UD.proficiency.UpdateLevel();
        //current_prof = UD.proficiency.level;
        ////int current_prof_lvl = Random.Range(0,5);
        //current_prof.level = current_prof_lvl;
        Image pbi = Prof_btn.GetComponent<Image>();
        if (current_prof.level > 0)
        {
            pbi.sprite=StorageImageHelper.GetItemImageByOrder(current_prof.level + 99);
            pbi.color = Color.white;
        }
        else
        {
            pbi.sprite = StorageImageHelper.GetItemImageByOrder(100);
            pbi.color = new Color(0.5f, 0.5f, 0.5f);
        }
    }

    #region Tire Operation
    public void TireUpComfirm()
    {
        if (TireUp_consumeItems.Length == 0)
        {
            TireUpCurrentCharacter();
        }
        else
        {
            EvolveComfirmCanvas.gameObject.SetActive(true);
            EvolveComfirmCanvas.SetConsumeItems(TireUp_consumeItems, TireUp_consumeAmount);
        }
    }
    public void TireUpCurrentCharacter()
    {
        bool lockHere = EvolveComfirmCanvas == null || !EvolveComfirmCanvas.gameObject.activeInHierarchy;
        if (lockHere) SetPageInProgress(true);
        for (int i = 0; i < TireUp_consumeItems.Length; i++) 
        {
            int ca=RewardingSystem.GetAmount(TireUp_consumeItems[i]);
            if (TireUp_consumeAmount[i] > ca)
            {
                Debug.LogWarning("Not enouth evolve items!");
                if (lockHere) SetPageInProgress(false);
                return;
            }
        }
        for (int i = 0; i < TireUp_consumeItems.Length; i++) RewardingSystem.ConsumeItem(TireUp_consumeItems[i], TireUp_consumeAmount[i]);
        CharacterUpgradeSave.UnlockCharacterTire($"{rality}{current_code}", current_tire + 1);
        //ShowCertainCharacter(current_code);
        //
        Char_Rarity_Btns[current_tire].GetComponent<Button>().interactable=true;
        Char_Rarity_Btns[current_tire].transform.GetChild(0).gameObject.SetActive(false);
        //
        Destroy(current_display_character);
        ShowCertainCharInTire(current_tire + 1);
        PlatformAudio.PlaySfx(GetComponent<AudioSource>());
        if (lockHere) SetPageInProgress(false);
    }
    protected bool CheckTireUpAvailable(string code4)
    {
        if (current_tire == 3) return false;
        CharacterUpgradeSave.UpgradeDetails CUD = CharacterUpgradeSave.GetDetails(code4);
        if (CUD == null) { Debug.LogWarning("Insufficient CUD."); return false; }
        int upgradedLevel = CUD.upgraded_level;
        if (upgradedLevel < 10) return false;
        if (!CUD.tire_unlocked[current_tire]) { Debug.Log("Current Tire not unlocked."); return false; }
        if (CUD.tire_unlocked[current_tire + 1]) { Debug.Log("Next Tire already unlocked."); return false; }
        if (current_tire == 1 && upgradedLevel < 30) { Debug.Log("Tire Not enough level."); return false; }
        if (current_tire == 2 && upgradedLevel < 60) return false;
        //
        TotalUpgradeCost TUC = BundledAddressables.LoadSync<TotalUpgradeCost>($"Units/Cat Units/{code4[0]}/{code4.Substring(1, 3)}/upgrade");
        if (TUC == null) return false;
        if (current_tire >= TUC.cost.Length) return false;
        TireUp_consumeItems = new RewardName[TUC.cost[current_tire].upgrade_consume.Length];
        TireUp_consumeAmount = new int[TireUp_consumeItems.Length];
        for (int i = 0; i < TireUp_consumeItems.Length; i++)
        {
            TireUp_consumeItems[i] = TUC.cost[current_tire].upgrade_consume[i].reward_name;
            TireUp_consumeAmount[i] = TUC.cost[current_tire].upgrade_consume[i].count;
        }
        UpgradeMethod um = TUC.cost[current_tire].method;
        switch (um)
        {
            case UpgradeMethod.unavailable: return false;
            case UpgradeMethod.none: return true; 
            case UpgradeMethod.clearStage:
                if (PlayerPrefs.HasKey(string.Format(UXPref.TIREUPUNLOCKMARK, code4))) return true;
                else return false;
            case UpgradeMethod.clearStageAndItems:
                if (PlayerPrefs.HasKey(string.Format(UXPref.TIREUPUNLOCKMARK, code4)))return true;
                else return false;
            case UpgradeMethod.drawCards:return false;
            case UpgradeMethod.items: return true;
            default: return false;
        }
    }
    private void BackToBase()
    {
        if (returnToEquipMode && FrameUI != null)
        {
            FrameUI.ReturnToPrevious(FrameUIDisplayer.DoorAction.None);
            return;
        }
        baseCanvas.SubBacktoBase();
    }

    public void SetReturnToEquipMode(bool enabled)
    {
        returnToEquipMode = enabled;
    }
    #endregion

    #region Legend Ticket Exchange
    /// <summary>
    /// 传说票直接兑换（只在 Uber Rare 页出现）：第一次点亮出 "Confirm?"，第二次才真正扣票。
    /// 获得方式和抽卡完全一致——CharacterUpgradeSave.UpgradeCharacterByDraw，
    /// 它 plus_level++ 之后会顺手 UnlockCharacterTire(id, 0)，所以未解锁的角色一次到位。
    /// </summary>
    private void OnLegendExchangeClicked()
    {
        if (isLoadingRarityCharacters) return;
        if (!LegendExchangeUsable(out string id)) { RefreshLegendExchangeButton(); return; }

        if (!legendExchangeArmed)
        {
            legendExchangeArmed = true;
            ApplyLegendExchangeState();
            return;
        }

        // ConsumeItem 自带余量检查，扣不动就当无事发生（保底，正常走不到）
        if (!RewardingSystem.ConsumeItem(LegendExchangeTicket, LegendExchangeCost))
        {
            RefreshLegendExchangeButton();
            return;
        }

        CharacterUpgradeSave.UpgradeCharacterByDraw(id);
        // 隐藏未拥有角色时用的是这个缓存，刚解锁的角色要同步，否则切一次筛选才会出现
        unlockedBaseTireCache[id] = true;
        if (virtualListReady && headIconGrid != null) headIconGrid.RebindVisible();

        baseCanvas.UpdateCurrencies();
        if (FrameUI != null) FrameUI.RefreshCurrencyAmounts();
        PlatformAudio.PlaySfx(GetComponent<AudioSource>());

        // ShowCertainCharacter 负责刷新阶段按钮上的锁和 upgradeLock（刚解锁的角色要解开升级按钮），
        // 随后 ShowCertainCharInTire 不重播动画，只更新数值面板，并回头把本按钮刷成新状态。
        ShowCertainCharacter(current_code);
        ShowCertainCharInTire(current_tire, false);
    }

    /// <summary>清掉二次确认并重算显隐/文字。换角色、换阶段、换稀有度时都会走到。</summary>
    private void RefreshLegendExchangeButton()
    {
        legendExchangeArmed = false;
        ApplyLegendExchangeState();
    }

    private void ApplyLegendExchangeState()
    {
        if (LegendExchange_btn == null) return;

        bool show = !isLoadingRarityCharacters && LegendExchangeVisible();
        LegendExchange_btn.gameObject.SetActive(show);
        if (!show) return;

        string id = $"{rality}{current_code}";
        bool unlocked = CharacterUpgradeSave.GetDetails(id).tire_unlocked[0];
        // plus_level 已满：按钮照常显示，只是不给点，文字换成 MAX
        bool available = CharacterUpgradeSave.DrawUpgradeAvailable(id);

        LegendExchange_btn.interactable = available;
        // 二次确认的文字分两种：没解锁过是「确认解锁？」，已解锁是「确认 +1？」。
        // 未解锁的角色 plus_level 必然是 0，而上限至少是 10，所以 MAX 只会出现在已解锁的角色身上。
        SetLegendExchangeLabel(
            !available ? LegendExchangeMaxLabel
            : legendExchangeArmed ? (unlocked ? legendExchangePlusOneText : legendExchangeUnlockText)
            : legendExchangeRedeemText);
    }

    /// <summary>显隐条件：Uber Rare 页 + 至少有一张票 + 当前选中的角色确实属于这一页。</summary>
    private bool LegendExchangeVisible()
    {
        if (rality != LegendExchangeRarity) return false;
        if (RewardingSystem.GetAmount(LegendExchangeTicket) < LegendExchangeCost) return false;
        // current_code 还停在上一个稀有度时不能继续往下查：
        // CharacterUpgradeSave.GetDetails 碰到不存在的 id 会直接建一条并存盘，会污染存档。
        return currentRarityCodes.Contains(current_code);
    }

    private bool LegendExchangeUsable(out string id)
    {
        id = $"{rality}{current_code}";
        return LegendExchangeVisible() && CharacterUpgradeSave.DrawUpgradeAvailable(id);
    }

    private void SetLegendExchangeLabel(string text)
    {
        if (LegendExchange_text != null) LegendExchange_text.text = text;
    }

    /// <summary>
    /// 三段文字都是异步取的，回来一个就重刷一次按钮（只改文字，不动二次确认状态）。
    /// 表里没这条 id 时 LocalizationHelper 回 null，退回显示 id 本身，不报错。
    /// </summary>
    private void CacheLegendExchangeTexts()
    {
        LocalizationHelper.GetLocalizedText(UXPref.Localized_UI, LegendExchangeRedeemTextId, localized =>
        {
            if (this == null) return;
            legendExchangeRedeemText = localized ?? LegendExchangeRedeemTextId;
            ApplyLegendExchangeState();
        });
        LocalizationHelper.GetLocalizedText(UXPref.Localized_UI, LegendExchangeUnlockTextId, localized =>
        {
            if (this == null) return;
            legendExchangeUnlockText = localized ?? LegendExchangeUnlockTextId;
            ApplyLegendExchangeState();
        });
        LocalizationHelper.GetLocalizedText(UXPref.Localized_UI, LegendExchangePlusOneTextId, localized =>
        {
            if (this == null) return;
            legendExchangePlusOneText = localized ?? LegendExchangePlusOneTextId;
            ApplyLegendExchangeState();
        });
    }
    #endregion

    protected override void OnDestroy()
    {
        if (loadRarityRoutine != null) StopCoroutine(loadRarityRoutine);
        Destroy(current_display_character);
        if (headIconGrid != null) headIconGrid.Dispose();
        base.OnDestroy();
    }

    public void ShowChangeBGPage()
    {
        Transform t=Instantiate(Resources.Load<GameObject>("UI/ChangeBackgroundPage")).transform;
        t.SetParent(gameObject.transform,false);
        t.localScale = Vector3.one;
        if (current_display_character != null) Destroy(current_display_character);
    }
    public void UpdateBackground()
    {
        if (backgroundSwitcher != null) backgroundSwitcher.ApplyCurrentBackgroundImmediate();
        else
        {
            int bgn = PlayerPrefs.GetInt(UXPref.Localized_BGnum, 0);
            if (background != null) background.sprite = BundledAddressables.LoadSync<Sprite>($"Background/Maps/{bgn}");
        }
    }

    private void ShowProfTable()
    {
        Instantiate(ProficiencyTable).GetComponent<ProficientTable>().Initialize(current_prof);
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

    private void InitializeVirtualizedHeadIconList()
    {
        if (HeadIcon_ScrollingArea == null || CatHeadIcon == null) return;

        if (HeadIconScrollRect == null) HeadIconScrollRect = HeadIcon_ScrollingArea.GetComponentInParent<ScrollRect>();
        if (HeadIconScrollRect == null) return;

        var grid = HeadIcon_ScrollingArea.GetComponent<GridLayoutGroup>();
        if (grid != null) grid.enabled = false;
        var vertical = HeadIcon_ScrollingArea.GetComponent<VerticalLayoutGroup>();
        if (vertical != null) vertical.enabled = false;
        var fitter = HeadIcon_ScrollingArea.GetComponent<ContentSizeFitter>();
        if (fitter != null) fitter.enabled = false;

        if (iconStepY <= 1f)
        {
            var iconRect = CatHeadIcon.GetComponent<RectTransform>();
            iconStepY = iconRect != null ? Mathf.Max(1f, iconRect.sizeDelta.y) : 200f;
        }

        float iconWidth = iconStepX;
        var itemRect = CatHeadIcon.GetComponent<RectTransform>();
        if (iconWidth <= 1f && itemRect != null) iconWidth = itemRect.sizeDelta.x;
        iconWidth = Mathf.Max(1f, iconWidth);

        headIconGrid = new VirtualizedScrollGrid<string>(
            new VirtualizedScrollGrid<string>.Settings
            {
                Content = HeadIcon_ScrollingArea,
                ScrollRect = HeadIconScrollRect,
                ItemPrefab = CatHeadIcon,
                Columns = Mathf.Max(1, headIconColumns),
                CellWidth = iconWidth,
                CellHeight = iconStepY,
                PreloadRows = Mathf.Max(0, preloadRows),
                DisableAutoLayout = true
            },
            BindHeadIconData
        );
        headIconGrid.Initialize();

        virtualListReady = true;
    }

    private void BindHeadIconData(GameObject iconGO, int _, string code)
    {
        if (iconGO == null) return;
        var icb = iconGO.GetComponent<IndexCatButton>();
        if (icb != null)
        {
            icb.SetCharacterCode(rality, code);
            icb.CIC = this;
            if (currentRarityIconCache.TryGetValue(code, out var sprite))
            {
                icb.SetCatHead(sprite);
            }

            bool unlocked = CharacterUpgradeSave.GetDetails($"{rality}{code}").tire_unlocked[0];
            icb.SetUnlocked(unlocked);
        }
    }

    private void OnHeadIconScrolled(Vector2 _)
    {
        RefreshVisibleHeadIcons(false);
    }

    private void ResetVirtualizedIcons()
    {
        RecycleAllActiveHeadIcons();
        lastVisibleStart = -1;
        lastVisibleEnd = -1;
        if (HeadIcon_ScrollingArea != null) HeadIcon_ScrollingArea.anchoredPosition = new Vector2(HeadIcon_ScrollingArea.anchoredPosition.x, 0f);
        if (HeadIconScrollRect != null)
        {
            Vector2 n = HeadIconScrollRect.normalizedPosition;
            HeadIconScrollRect.normalizedPosition = new Vector2(n.x, 1f);
        }
    }

    private void RefreshVirtualizedContentSize()
    {
        if (!virtualListReady || HeadIcon_ScrollingArea == null) return;
        float viewportHeight = headIconViewport != null ? headIconViewport.rect.height : 0f;
        float contentHeight = Mathf.Max(viewportHeight, currentRarityCodes.Count * iconStepY);
        Vector2 size = HeadIcon_ScrollingArea.sizeDelta;
        HeadIcon_ScrollingArea.sizeDelta = new Vector2(size.x, contentHeight);
    }

    private void RefreshVisibleHeadIcons(bool forceRefresh)
    {
        if (!virtualListReady || HeadIcon_ScrollingArea == null) return;
        if (currentRarityCodes.Count == 0)
        {
            RecycleAllActiveHeadIcons();
            return;
        }

        float viewportHeight = headIconViewport != null ? headIconViewport.rect.height : 0f;
        float contentY = Mathf.Max(0f, HeadIcon_ScrollingArea.anchoredPosition.y);
        int startIndex = Mathf.FloorToInt(contentY / iconStepY) - preloadRows;
        int visibleCount = Mathf.CeilToInt(Mathf.Max(iconStepY, viewportHeight) / iconStepY) + preloadRows * 2 + 1;
        startIndex = Mathf.Clamp(startIndex, 0, Mathf.Max(0, currentRarityCodes.Count - 1));
        int endIndex = Mathf.Clamp(startIndex + visibleCount - 1, 0, currentRarityCodes.Count - 1);

        if (!forceRefresh && startIndex == lastVisibleStart && endIndex == lastVisibleEnd) return;
        lastVisibleStart = startIndex;
        lastVisibleEnd = endIndex;

        var recycleList = new List<int>();
        foreach (var kv in activeHeadIcons)
        {
            if (kv.Key < startIndex || kv.Key > endIndex) recycleList.Add(kv.Key);
        }
        for (int i = 0; i < recycleList.Count; i++)
        {
            int idx = recycleList[i];
            if (!activeHeadIcons.TryGetValue(idx, out var go)) continue;
            activeHeadIcons.Remove(idx);
            RecycleHeadIcon(go);
        }

        for (int i = startIndex; i <= endIndex; i++)
        {
            if (activeHeadIcons.ContainsKey(i)) continue;
            var iconGO = GetOrCreateHeadIcon();
            BindHeadIcon(iconGO, i);
            activeHeadIcons[i] = iconGO;
        }
    }

    private GameObject GetOrCreateHeadIcon()
    {
        if (pooledHeadIcons.Count > 0)
        {
            var go = pooledHeadIcons.Pop();
            go.SetActive(true);
            return go;
        }

        var icon = Instantiate(CatHeadIcon, HeadIcon_ScrollingArea);
        icon.transform.localScale = Vector3.one;
        return icon;
    }

    private void RecycleHeadIcon(GameObject iconGO)
    {
        if (iconGO == null) return;
        iconGO.SetActive(false);
        pooledHeadIcons.Push(iconGO);
    }

    private void RecycleAllActiveHeadIcons()
    {
        foreach (var kv in activeHeadIcons)
        {
            RecycleHeadIcon(kv.Value);
        }
        activeHeadIcons.Clear();
    }

    private void DestroyAllHeadIconPoolObjects()
    {
        foreach (var kv in activeHeadIcons)
        {
            if (kv.Value != null) Destroy(kv.Value);
        }
        activeHeadIcons.Clear();

        while (pooledHeadIcons.Count > 0)
        {
            var go = pooledHeadIcons.Pop();
            if (go != null) Destroy(go);
        }
    }

    private void BindHeadIcon(GameObject iconGO, int dataIndex)
    {
        if (iconGO == null || dataIndex < 0 || dataIndex >= currentRarityCodes.Count) return;

        string code = currentRarityCodes[dataIndex];
        var rt = iconGO.GetComponent<RectTransform>();
        if (rt != null)
        {
            rt.SetParent(HeadIcon_ScrollingArea, false);
            rt.localScale = Vector3.one;
            rt.anchorMin = new Vector2(0.5f, 1f);
            rt.anchorMax = new Vector2(0.5f, 1f);
            rt.pivot = new Vector2(0.5f, 0.5f);
            rt.anchoredPosition = new Vector2(0f, -(dataIndex * iconStepY + iconStepY * 0.5f));
        }

        var icb = iconGO.GetComponent<IndexCatButton>();
        if (icb != null)
        {
            icb.SetCharacterCode(rality, code);
            icb.CIC = this;
            if (currentRarityIconCache.TryGetValue(code, out var sprite))
            {
                icb.SetCatHead(sprite);
            }

            bool unlocked = CharacterUpgradeSave.GetDetails($"{rality}{code}").tire_unlocked[0];
            icb.SetUnlocked(unlocked);
        }
    }

    private void RebuildHeadIconsLegacy()
    {
        if (HeadIcon_ScrollingArea == null || CatHeadIcon == null) return;

        for (int i = HeadIcon_ScrollingArea.childCount - 1; i >= 0; i--)
        {
            DestroyImmediate(HeadIcon_ScrollingArea.GetChild(i).gameObject);
        }

        for (int i = 0; i < currentRarityCodes.Count; i++)
        {
            string code = currentRarityCodes[i];
            GameObject uicon = Instantiate(CatHeadIcon);
            uicon.GetComponent<RectTransform>().SetParent(HeadIcon_ScrollingArea);
            uicon.GetComponent<RectTransform>().localScale = Vector3.one;

            var icb = uicon.GetComponent<IndexCatButton>();
            if (icb != null)
            {
                icb.SetCharacterCode(rality, code);
                icb.CIC = this;
                if (currentRarityIconCache.TryGetValue(code, out var sprite))
                {
                    icb.SetCatHead(sprite);
                }

                bool unlocked = CharacterUpgradeSave.GetDetails($"{rality}{code}").tire_unlocked[0];
                icb.SetUnlocked(unlocked);
            }
        }
    }

    #region Talent Operation
    /// <summary>
    /// 本能按钮什么时候能点：开了三阶 + 这角色确实做了 talent 资源 + 还没解锁过。
    /// 45 级的门槛不在这里拦——按用户设定，那是本能进化展示框里要说明的事，
    /// 按钮只负责「这角色有没有本能这条路可走」。
    /// </summary>
    private bool TalentUnlockAvailable(CharacterUpgradeSave.UpgradeDetails ud)
    {
        if (ud == null) return false;
        if (ud.tire_unlocked == null || ud.tire_unlocked.Length < 3 || !ud.tire_unlocked[2]) return false;
        if (ud.talent_unlocked) return false;
        return TalentData.Exists($"{rality}{current_code}");
    }

    private void OnTalentClicked()
    {
        // TODO: 本能进化展示框还没做。这个面板要负责：展示本能会追加的属性/效果、
        // 说明并校验 45 级门槛（不够级时给出提示而不是直接放行）、确认后调
        // CharacterUpgradeSave.UnlockCharacterTalent($"{rality}{current_code}")，
        // 回来再 ShowCertainCharInTire(current_tire, false) 刷新图鉴。
        Debug.Log($"[Talent] TODO: 打开本能进化展示框 ({rality}{current_code})");
    }
    #endregion

    private void InitializeBackgroundSwitcher()
    {
        backgroundSwitcher = GetComponent<CatBackgroundSwitcher>();
        if (backgroundSwitcher == null) backgroundSwitcher = gameObject.AddComponent<CatBackgroundSwitcher>();
        backgroundSwitcher.Initialize(background, ghostMaterialTemplate, null);
    }

    private void RefreshUpgradeButtonsVisual()
    {
        if (Upgrade_btn != null)
        {
            Upgrade_btn.SetFrameColorPersistent(Upgrade_btn.interactable ? UpgradeButtonEnabledColor : UpgradeButtonDisabledColor);
        }
        if (TireUp_btn != null)
        {
            TireUp_btn.SetFrameColorPersistent(TireUp_btn.interactable ? UpgradeButtonEnabledColor : UpgradeButtonDisabledColor);
        }
        if (Talent_btn != null)
        {
            Talent_btn.SetFrameColorPersistent(Talent_btn.interactable ? UpgradeButtonEnabledColor : UpgradeButtonDisabledColor);
        }
    }

    private RewardName GetCateyeConsumeReward(int rarity)
    {
        if (CateyeConsume_rality.TryGetValue(rarity, out RewardName reward))
        {
            return reward;
        }
        return RewardName.Cateye_EX;
    }

    private int GetCateyeConsumeAmount(int rarity, int upgradedLevel)
    {
        if (rarity <= 0 || upgradedLevel < 30) return 0;
        if (upgradedLevel < 40) return 1;
        if (upgradedLevel < 50) return 2;
        return 5;
    }

    private void RefreshFrameUICurrenciesForRarity()
    {
        cateyeConsuming = GetCateyeConsumeReward(rality);
        runtimeExtraCurrencyIds.Clear();

        if (ExtraCurrencyIds != null)
        {
            for (int i = 0; i < ExtraCurrencyIds.Count; i++)
            {
                int id = ExtraCurrencyIds[i];
                if (!runtimeExtraCurrencyIds.Contains(id)) runtimeExtraCurrencyIds.Add(id);
            }
        }

        int catEyeRewardId = RewardingSystem.RewardNumMap[cateyeConsuming];
        if (!runtimeExtraCurrencyIds.Contains(catEyeRewardId)) runtimeExtraCurrencyIds.Add(catEyeRewardId);

        // Uber Rare 页多挂一个传说票，和直接兑换按钮对应。这里不看票数，
        // 免得刚好花完最后一张时货币条当场少一格。
        if (rality == LegendExchangeRarity)
        {
            int legendTicketId = RewardingSystem.RewardNumMap[LegendExchangeTicket];
            if (!runtimeExtraCurrencyIds.Contains(legendTicketId)) runtimeExtraCurrencyIds.Add(legendTicketId);
        }

        if (FrameUI != null) FrameUI.SetCurrentExtraCurrencies(runtimeExtraCurrencyIds);
    }
    private void InfoBoardDisplay()=>character_info_board.SetActive(!character_info_board.activeSelf);
}
