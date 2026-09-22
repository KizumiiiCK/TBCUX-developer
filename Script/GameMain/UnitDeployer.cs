using System.Collections;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class UnitDeployer : MonoBehaviour
{
    // UI elements
    [SerializeField] private Image Proficiency_Mark;
    //
    public string unitCode = "00000";
    private UnitIdentity identity;
    private int unitCost = 0;
    private int cd = 1;
    private int t = 0;
    private bool deployEnabled = false;
    private int treasure_count = 0;
    private bool isGuest = false;
    private int lvl = 1;
    private bool talentApplied = false;
    private Vector2 catBasePosition=Vector2.zero;
    //
    private Button btn;
    [SerializeField] private KiPanel deployPanel;
    [SerializeField] private Image blackShade;
    [SerializeField] private TMP_Text cost_txt;
    /// <summary>可选：出战格子上的等级显示。不填就只是不显示，其余照常。</summary>
    [SerializeField] private TMP_Text level_txt;
    private LevelController LI;
    private CharacterData CD;
    private AnimDecryptPack characterDecryptedFiles;
    private bool isRuntimeInitialized;
    private float cachedShadeFill = -1f;
    private bool cachedEnoughMoney;
    private bool cachedDeployAvailable;
    private int moneyCheckFrameCounter = 0;
    private const int MoneyCheckIntervalFrames = 2;

    // Start is called before the first frame update
    void Start()
    {
        EnsureLevelControllerReference();
    }
    private void FixedUpdate()
    {
        if (!deployEnabled) return;
        if (!EnsureLevelControllerReference()) return;
        if (LI.isPloting) return;
        t++;
        float fill = 1 - Mathf.Clamp01((float)t / cd);
        if (Mathf.Abs(fill - cachedShadeFill) > 0.0001f)
        {
            blackShade.fillAmount = fill;
            cachedShadeFill = fill;
        }
        moneyCheckFrameCounter++;
        bool shouldCheckMoney = moneyCheckFrameCounter >= MoneyCheckIntervalFrames || t >= cd;
        bool enoughMoney = cachedEnoughMoney;
        if (shouldCheckMoney)
        {
            moneyCheckFrameCounter = 0;
            enoughMoney = LI.currentMoney >= unitCost;
            if (enoughMoney != cachedEnoughMoney)
            {
                cachedEnoughMoney = enoughMoney;
                MoneyColor(enoughMoney);
            }
        }
        bool canDeploy = t >= cd && enoughMoney;
        if (canDeploy != cachedDeployAvailable)
        {
            cachedDeployAvailable = canDeploy;
            DeployAvailable(canDeploy);
        }
    }
    public void SetupDeployer(string code, int treasureCount, int proficency, int teambonus, int forceLevel = -1, float costMultiplier = 1f)
    {
        EnsureLevelControllerReference();
        btn = GetComponent<Button>();
        if (deployPanel == null) deployPanel = GetComponent<KiPanel>();
        catBasePosition = GameObject.Find("CatBase").transform.position;
        unitCode = code;
        isRuntimeInitialized = false;
        talentApplied = false;
        characterDecryptedFiles = null;
        if (!CharacterPlacer.TryParse(code, true, out identity) || !identity.IsValid)
        {
            Debug.LogWarning($"No such code for deploy: {code}");
            deployEnabled = false; btn.interactable = false; cost_txt.text = string.Empty;
            SetLevelText(0);
            SetProficiencyMark(0);
            return;
        }
        CharacterData loaded = CharacterPlacer.LoadData(identity);
        if (loaded == null) {
            deployEnabled = false;
            btn.interactable = false;
            cost_txt.text = string.Empty;
            SetLevelText(0);
            SetProficiencyMark(0);
            return;
        }
        else
        {
            CD = loaded;
            deployEnabled = true;
            btn.onClick.RemoveListener(Deploy);
            btn.onClick.AddListener(Deploy);
        }
        Image icon=GetComponent<Image>();
        icon.sprite = CharacterPlacer.LoadIcon(identity);
        bool talentUnlocked = false;
        if (forceLevel >= 1) lvl = forceLevel;
        else if (identity.IsOpposite) lvl = 1;
        else try
        {
            // 等级和本能都在同一条存档记录里，读一次就够——GetDetails 每次都会重新读盘。
            CharacterUpgradeSave.UpgradeDetails ud = CharacterUpgradeSave.GetDetails(identity.CharacterCode.Substring(0, 4));
            lvl = ud.TotalLevel(); if (lvl < 1) lvl = 1;
            talentUnlocked = ud.talent_unlocked;
        }
        catch { lvl = 1; }
        SetLevelText(lvl);
        //treasure_bonus = 1 + 1.5f * (RewardingSystem.GetAmount(RewardName.WorldTreasures) / 150f);
        treasure_count = treasureCount;
        unitCost = CD.Cost;
        cd = (int)(CD.Cooldown * (2.5f - 1.5f*treasure_count / 150f));
        t = cd;
        cachedShadeFill = -1f;
        int appliedProficiency = identity.IsOpposite ? 0 : proficency;
        SetProficiencyMark(appliedProficiency);
        if (appliedProficiency > 0) CD.Health = (int)(CD.Health * (1.05f + 0.02f * teambonus));
        if (appliedProficiency > 1) for(int i = 0; i < CD.atkInfos.Length; i++) CD.atkInfos[i].ATK *= 1.05f + 0.02f * teambonus;
        if (appliedProficiency > 2) { CD.Cost = CD.Cost * 93 / 100; unitCost = CD.Cost; }
        CD.Cost = unitCost;
        // 本能放在熟练度之后：熟练度只动血量/攻击/花费这些数值，本能只动属性和效果，互不重叠。
        if (talentUnlocked) ApplyTalent();
        cachedEnoughMoney = LI != null && LI.currentMoney >= unitCost;
        cachedDeployAvailable = t >= cd && cachedEnoughMoney;
        moneyCheckFrameCounter = 0;
        MoneyColor(cachedEnoughMoney);
        DeployAvailable(cachedDeployAvailable);
        cost_txt.text = unitCost + " $";
        ApplyRarityFrameColor();
    }

    /// <summary>
    /// 把本能叠到 CD 上。CD 是 CharacterVisualLoader.LoadCharacterData 给的副本，可以安全就地改。
    /// 本能资源在角色根目录（和 upgrade 同级），没做出来的角色读不到就当没有，不报错。
    /// </summary>
    private void ApplyTalent()
    {
        if (CD == null) return;
        TalentData talent = TalentData.Load(identity.CharacterCode);
        if (talent == null) return;
        talent.ApplyTo(CD);
        talentApplied = true;
    }

    /// <summary>等级传 0 表示这个格子没装上角色，清空显示而不是写 "Lv. 0"。</summary>
    private void SetLevelText(int level)
    {
        if (level_txt == null) return;
        level_txt.text = level > 0 ? $"Lv. {level}" : string.Empty;
    }

    public void SetProficiencyMark(int lvl)
    {
        if (lvl <= 0 || lvl > 4)
        {
            Proficiency_Mark.gameObject.SetActive(false);
        }
        else
        {
            Proficiency_Mark.gameObject.SetActive(true);
            Proficiency_Mark.sprite = StorageImageHelper.GetItemImageByOrder(lvl + 99);
        }
    }
    public void ResetCoolDown()
    {
        t = 0;
        cachedShadeFill = -1f;
        cachedDeployAvailable = false;
        moneyCheckFrameCounter = 0;
        DeployAvailable(false);
    }
    public void GuestMark() => isGuest = true;
    public void LockByRestriction()
    {
        deployEnabled = false;
        if (btn == null) btn = GetComponent<Button>();
        if (btn != null) btn.interactable = false;
        if (blackShade != null) blackShade.fillAmount = 1f;
    }
    private void Deploy()
    {
        if (!EnsureLevelControllerReference()) return;
        if (LI.isPloting) return;
        if (!LI.CanDeployACat()) return;
        if (!EnsureRuntimeInitialized()) return;
        if (!LI.DeployCost(unitCost)) return;
        if (!LI.DeployACat())
        {
            LI.AddMoney(unitCost);
            return;
        }
        ResetCoolDown();
        int sortingOrder = Random.Range(0, 11);
        int sr_samelayer = Random.Range(0, 6);
        float deviationY = -sortingOrder / 10f;
        GameObject spawned = CharacterPlacer.Place(
            identity,
            CD,
            characterDecryptedFiles,
            catBasePosition + new Vector2(0.5f, deviationY),
            LI,
            lvl,
            treasure_count,
            1f,
            sortingOrder * 1000 + sr_samelayer * 100,
            sortingOrder * 1000 + sr_samelayer * 50);
        // 本能是在 SetupDeployer 里就叠进 CD 的，这里只是把「叠过」这件事记到单位身上，
        // 变身的时候好照着给新形态补上，免得战斗中再去读一次存档。
        if (talentApplied && spawned != null)
        {
            Character deployed = spawned.GetComponent<Character>();
            if (deployed != null) deployed.MarkTalentApplied();
        }
        DeployAvailable(false);
        if (isGuest) { gameObject.SetActive(false); }
        if (!identity.IsOpposite) LI.RecordProficency_Deploy(identity.CharacterCode);
    }

    private bool EnsureRuntimeInitialized()
    {
        if (isRuntimeInitialized) return true;
        if (CD == null) return false;

        if (!CD.UNITYAnimated)
        {
            characterDecryptedFiles = CharacterPlacer.Decrypt(identity, CD);

            if (characterDecryptedFiles == null) return false;
        }

        isRuntimeInitialized = true;
        return true;
    }
    private bool EnsureLevelControllerReference()
    {
        if (LI != null) return true;
        GameObject levelInitializer = GameObject.Find("Level Initializer");
        if (levelInitializer == null) return false;
        LI = levelInitializer.GetComponent<LevelController>();
        return LI != null;
    }
    private void DeployAvailable(bool a)
    {
        if (btn.interactable == a) return;
        btn.interactable = a;
    }
    private void MoneyColor(bool enough)
    {
        Color targetColor = enough ? Color.white : Color.red;
        if (cost_txt.color == targetColor) return;
        cost_txt.color = targetColor;
    }

    private void ApplyRarityFrameColor()
    {
        if (deployPanel == null) return;

        int rarity = 0;
        int outfitType = 10;
        if (identity.AssetIsCat && !string.IsNullOrEmpty(identity.CharacterCode) && identity.CharacterCode.Length > 0)
        {
            rarity = Mathf.Clamp(identity.CharacterCode[0] - '0', 0, 6);
            outfitType = rarity + 1;
        }
        else
        {
            rarity = 10;
        }
        deployPanel.SetOutfit(KiOutfit.Border, outfitType);
        deployPanel.ApplyFrameColor(UXPref.GetRarityFrameColor(rarity));
    }
    public CharacterData GetCharacterData() => CD;
}
