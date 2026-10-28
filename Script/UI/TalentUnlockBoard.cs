using System;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// 本能（Talent）解锁面板。挂在 Resources/UI/FunctionalPanels/TalentUnlockBoard 根节点上，
/// 和同物体的 <see cref="IndexViewer"/> 搭配使用：IndexViewer 负责画「解锁本能之后」的属性/图标，
/// 本脚本负责头像、名字、等级与物品条件、以及真正的解锁动作。
///
/// 职责划分：图鉴页的本能按钮只判断「这角色有没有本能这条路、有没有开到三阶、是不是已经解锁过」，
/// 等级和物品够不够一律放到这里——不够时只是文字标红 + 解锁按钮不可点，面板照样能打开查看消耗。
/// </summary>
public class TalentUnlockBoard : MonoBehaviour
{
    /// <summary>asset 里 talent_required_level 填 &lt;=0 时用的兜底门槛。</summary>
    public const int DefaultRequiredLevel = 45;
    /// <summary>本能预览固定看第三阶段（tire 2）：能解锁本能的前提就是三阶已开。</summary>
    private const int TalentPreviewTire = 2;
    private const string LevelRequirementFormat = "LEVEL:\n{0} + _";
    private const string UnitNamesTable = "UnitNames";
    private static readonly Color CostEnoughColor = new Color(1f, 1f, 0.5f, 1f);
    private static readonly Color CostInsufficientColor = new Color(1f, 0f, 0f, 1f);

    [Header("Character")]
    [SerializeField] private IndexViewer IV;
    [SerializeField] private Image headIcon;
    [SerializeField] private TMP_Text charName_txt;
    [Header("Conditions")]
    [SerializeField] private TMP_Text levelRequirement_txt;
    /// <summary>消耗槽的父物体（CostParent/Currencies）。costItems 没在 Inspector 里填就从这里抓。</summary>
    [SerializeField] private Transform costParent;
    [SerializeField] private FrameCurrencyItem[] costItems = new FrameCurrencyItem[4];
    [Header("Buttons")]
    [SerializeField] private Button Unlock_btn;
    [SerializeField] private Button Cancel_btn;
    /// <summary>
    /// 可选。预制体里的两个按钮已经各自挂了 onClick → AudioSource.Play 的点击音，
    /// 所以这里只在想给「解锁成功」单独配一条音效时才填，空着就不发声（不会自动去找，免得和按钮音撞车）。
    /// </summary>
    [SerializeField] private AudioSource AS;

    private int rarity;
    private string code;
    private string id4;
    private int requiredLevel = DefaultRequiredLevel;
    private TotalUpgradeCost upgradeCost;
    private CatIndexCanvas owner;
    private Action onUnlocked;
    /// <summary>只有自己上过锁才负责解锁，免得把别人的进行中状态清掉。</summary>
    private bool pageLocked;
    private bool initialized;
    /// <summary>等级文字的原色（够级时用）。预制体上配的是什么就用什么，不写死白色。</summary>
    private Color levelTextNormalColor = Color.white;

    private void Awake()
    {
        if (IV == null) IV = GetComponent<IndexViewer>();
        CacheCostItems();
        if (levelRequirement_txt != null) levelTextNormalColor = levelRequirement_txt.color;
        if (Unlock_btn != null) Unlock_btn.onClick.AddListener(OnUnlockClicked);
        if (Cancel_btn != null) Cancel_btn.onClick.AddListener(Close);
        // Initialize 之前先别让人点出事故
        if (Unlock_btn != null) Unlock_btn.interactable = false;
    }

    /// <summary>
    /// 打开面板。owner 只用来在面板开着时占住「页面进行中」（挡住返回键），
    /// onUnlocked 在真正解锁成功后回调一次，用于刷新调用方的货币条与图鉴显示。
    /// </summary>
    public void Initialize(int rarity, string code, CatIndexCanvas owner = null, Action onUnlocked = null)
    {
        this.rarity = rarity;
        this.code = code;
        this.owner = owner;
        this.onUnlocked = onUnlocked;
        id4 = $"{rarity}{code}";

        upgradeCost = BundledAddressables.LoadSync<TotalUpgradeCost>($"Units/Cat Units/{rarity}/{code}/upgrade");
        requiredLevel = upgradeCost != null && upgradeCost.talent_required_level > 0
            ? upgradeCost.talent_required_level
            : DefaultRequiredLevel;
        if (upgradeCost == null)
            Debug.LogWarning($"[TalentUnlockBoard] Missing upgrade asset for {id4}, talent unlock disabled.");

        if (owner != null && !pageLocked)
        {
            owner.SetPageInProgress(true);
            pageLocked = true;
        }

        initialized = true;
        ShowCharacter();
        Refresh();
    }

    /// <summary>头像、名字、以及「叠加本能之后」的 IndexViewer 预览，都取第三阶段。</summary>
    private void ShowCharacter()
    {
        string tirePath = $"Units/Cat Units/{rarity}/{code}/{TalentPreviewTire}/";

        if (headIcon != null)
        {
            Sprite icon = BundledAddressables.LoadSync<Sprite>(tirePath + "icon_deploy");
            if (icon != null) headIcon.sprite = icon;
        }

        string nameKey = $"{id4}{TalentPreviewTire}";
        if (charName_txt != null)
        {
            charName_txt.text = nameKey;
            LocalizationHelper.GetLocalizedText(UnitNamesTable, nameKey, localized =>
            {
                if (this == null || charName_txt == null) return;
                charName_txt.text = localized ?? nameKey;
            });
        }

        if (IV == null) return;
        CharacterData cd = BundledAddressables.LoadSync<CharacterData>(tirePath + "data");
        if (cd == null)
        {
            Debug.LogWarning($"[TalentUnlockBoard] Missing character data at {tirePath}data");
            return;
        }
        // 这里永远按「已解锁本能」来画，面板本来就是给人看解锁后效果的，
        // 所以不看存档里的 talent_unlocked，直接读本能资源叠上去。
        TalentData talent = TalentData.Load(id4);
        int level = CharacterUpgradeSave.GetDetails(id4).TotalLevel();
        IV.ShowCharacterDetails(TalentData.MergeForDisplay(cd, talent), true, level, talent);
    }

    /// <summary>重算等级/物品条件并刷新显示。打开时和解锁后各走一次。</summary>
    public void Refresh()
    {
        if (!initialized) return;

        CharacterUpgradeSave.UpgradeDetails ud = CharacterUpgradeSave.GetDetails(id4);
        // 只看升级等级，plus level（抽卡叠的 +N）不算
        bool levelEnough = ud.upgraded_level >= requiredLevel;

        if (levelRequirement_txt != null)
        {
            levelRequirement_txt.text = string.Format(LevelRequirementFormat, requiredLevel);
            levelRequirement_txt.color = levelEnough ? levelTextNormalColor : CostInsufficientColor;
        }

        bool itemsEnough = ApplyCostSlots();
        bool available = upgradeCost != null && upgradeCost.talent_available && !ud.talent_unlocked;
        if (Unlock_btn != null) Unlock_btn.interactable = available && levelEnough && itemsEnough;
    }

    /// <summary>把消耗列表画到 4 个槽位上，返回「是否全部足够」。</summary>
    private bool ApplyCostSlots()
    {
        CacheCostItems();
        List<UpgradeConsume> costs = GetTalentCosts();
        bool allEnough = true;

        // 槽位只有 4 个，但余量校验要覆盖全部条目，否则多出来的那条等于白送
        for (int i = 0; i < costs.Count; i++)
        {
            if (RewardingSystem.GetAmount(costs[i].reward_name) < costs[i].count) allEnough = false;
        }

        int slots = costItems != null ? costItems.Length : 0;
        for (int i = 0; i < slots; i++)
        {
            FrameCurrencyItem item = costItems[i];
            if (item == null) continue;
            if (i >= costs.Count)
            {
                item.gameObject.SetActive(false);
                continue;
            }
            RewardName rn = costs[i].reward_name;
            int need = costs[i].count;
            int held = RewardingSystem.GetAmount(rn);
            item.gameObject.SetActive(true);
            item.SetData(GetRewardId(rn), need, held >= need ? CostEnoughColor : CostInsufficientColor, 56, $"{held} / {need}",true);
        }

        if (costs.Count > slots)
            Debug.LogWarning($"[TalentUnlockBoard] {id4} talent_consume has {costs.Count} entries but only {slots} slots.");

        return allEnough;
    }

    /// <summary>
    /// 取规整后的消耗列表：丢掉空条目和数量 &lt;=0 的条目，同名条目合并成一条。
    /// 不合并的话，同一种物品填两行会让「持有/需要」每行各自判断，出现两行都显示够、实际扣不动的情况。
    /// </summary>
    private List<UpgradeConsume> GetTalentCosts()
    {
        var result = new List<UpgradeConsume>();
        UpgradeConsume[] raw = upgradeCost != null ? upgradeCost.talent_consume : null;
        if (raw == null) return result;
        for (int i = 0; i < raw.Length; i++)
        {
            UpgradeConsume c = raw[i];
            if (c == null || c.count <= 0) continue;
            int at = result.FindIndex(r => r.reward_name == c.reward_name);
            if (at >= 0) result[at].count += c.count;
            else result.Add(new UpgradeConsume { reward_name = c.reward_name, count = c.count });
        }
        return result;
    }

    private void OnUnlockClicked()
    {
        if (!initialized) return;

        CharacterUpgradeSave.UpgradeDetails ud = CharacterUpgradeSave.GetDetails(id4);
        // 下面每一条都是兜底：正常情况下按钮早就是不可点的状态
        if (upgradeCost == null || !upgradeCost.talent_available || ud.talent_unlocked) { Refresh(); return; }
        if (ud.upgraded_level < requiredLevel) { Refresh(); return; }

        List<UpgradeConsume> costs = GetTalentCosts();
        for (int i = 0; i < costs.Count; i++)
        {
            if (RewardingSystem.GetAmount(costs[i].reward_name) < costs[i].count) { Refresh(); return; }
        }
        // 余量已经整体确认过，这里才开始扣，避免扣了一半发现不够
        for (int i = 0; i < costs.Count; i++)
        {
            RewardingSystem.ConsumeItem(costs[i].reward_name, costs[i].count);
        }

        CharacterUpgradeSave.UnlockCharacterTalent(id4);
        PlatformAudio.PlaySfx(AS);
        onUnlocked?.Invoke();
        Close();
    }

    public void Close() => Destroy(gameObject);

    private void CacheCostItems()
    {
        bool needScan = costItems == null || costItems.Length == 0;
        if (!needScan)
        {
            needScan = true;
            for (int i = 0; i < costItems.Length; i++)
            {
                if (costItems[i] != null) { needScan = false; break; }
            }
        }
        if (!needScan || costParent == null) return;
        costItems = costParent.GetComponentsInChildren<FrameCurrencyItem>(true);
    }

    private static int GetRewardId(RewardName rn)
    {
        return RewardingSystem.RewardNumMap.TryGetValue(rn, out int id) ? id : 0;
    }

    private void OnDestroy()
    {
        if (pageLocked)
        {
            owner?.SetPageInProgress(false);
            pageLocked = false;
        }
    }
}
