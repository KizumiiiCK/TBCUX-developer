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
    private int unitCost = 0;
    private int cd = 1;
    private int t = 0;
    private bool deployEnabled = false;
    private int treasure_count = 0;
    private bool isGuest = false;
    //
    private GameObject catUnit;
    private int lvl = 1;
    private Vector2 catBasePosition=Vector2.zero;
    private string loadPath;
    //
    private Button btn;
    [SerializeField] private KiPanel deployPanel;
    [SerializeField] private Image blackShade;
    [SerializeField] private TMP_Text cost_txt;
    private LevelController LI;
    private CharacterData CD;
    private AnimDecryptPack characterDecryptedFiles;
    private bool isRuntimeInitialized;

    // Start is called before the first frame update
    void Start()
    {
        LI=GameObject.Find("Level Initializer").GetComponent<LevelController>();
    }
    private void FixedUpdate()
    {
        if (!deployEnabled) return;
        t++;
        blackShade.fillAmount = 1 - Mathf.Clamp01((float)t / cd);
        bool enoughMoney = LI.currentMoney >= unitCost;
        MoneyColor(enoughMoney);
        DeployAvailable(t >= cd && enoughMoney);
    }
    public void SetupDeployer(string code, int treasureCount, int proficency, int teambonus, int forceLevel=1)
    {
        btn = GetComponent<Button>();
        if (deployPanel == null) deployPanel = GetComponent<KiPanel>();
        //blackShade = transform.GetChild(0).GetComponent<Image>();
        //cost_txt = transform.GetChild(1).GetComponent<TMP_Text>();
        catBasePosition = GameObject.Find("CatBase").transform.position;
        unitCode = code;
        isRuntimeInitialized = false;
        characterDecryptedFiles = null;
        try { loadPath = $"Units/Cat Units/{code[0]}/{code.Substring(1,3)}/{code[4]}/"; }
        catch
        {
            Debug.LogWarning($"No such code for deploy: {code}");
            deployEnabled = false; btn.interactable = false; cost_txt.text = string.Empty;
            SetProficiencyMark(0);
            return;
        }
        CD = Resources.Load<CharacterData>(loadPath + "data").Clone();
        if ( CD == null ) { 
            deployEnabled = false; 
            btn.interactable = false; 
            cost_txt.text = string.Empty;
            SetProficiencyMark(0);
            return; 
        }
        else { deployEnabled = true; btn.onClick.AddListener(Deploy); }
        catUnit = Resources.Load<GameObject>("Units/Cat Units/catunit");
        Image icon=GetComponent<Image>();
        icon.sprite = Resources.Load<Sprite>(loadPath+"icon_deploy");
        //CharacterData C = Resources.Load<CharacterData>(loadPath + "data");
        //
        if (forceLevel > 1) lvl = forceLevel;
        else try { lvl = CharacterUpgradeSave.GetDetails(code.Substring(0,4)).TotalLevel(); if (lvl < 1) lvl = 1; }
        catch { lvl = 1; }
        //treasure_bonus = 1 + 1.5f * (RewardingSystem.GetAmount(RewardName.WorldTreasures) / 150f);
        treasure_count = treasureCount;
        unitCost = CD.Cost;
        cd = (int)(CD.Cooldown * (2.5f - 1.5f*treasure_count / 150f));
        t = cd;
        //Proficiency
        SetProficiencyMark(proficency);
        if (proficency > 0) CD.Health = (int)(CD.Health * (1.05f + 0.02f * teambonus));
        if (proficency > 1) for(int i = 0; i < CD.atkInfos.Length; i++) CD.atkInfos[i].ATK *= 1.05f + 0.02f * teambonus;
        if (proficency > 2) { CD.Cost = CD.Cost * 9 / 10; unitCost = CD.Cost; }
        cost_txt.text = unitCost + " $";
        ApplyRarityFrameColor();
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
    public void ResetCoolDown() => t = 0;
    public void GuestMark() => isGuest = true;
    private void Deploy()
    {
        if (!LI.DeployCost(unitCost)) return;
        if (!LI.DeployACat()) return;
        if (!EnsureRuntimeInitialized()) return;
        ResetCoolDown();
        int sortingOrder = Random.Range(0, 11);
        int sr_samelayer = Random.Range(0, 6);
        float deviationY = -sortingOrder / 10f;
        GameObject cat=Instantiate(catUnit,catBasePosition+new Vector2(0.5f,deviationY),Quaternion.identity);
        cat.GetComponent<Character>().LoadCharacterData(LI, CD, lvl, treasure_count);
        if (CD.UNITYAnimated) {
            GameObject uaunit=Instantiate(Resources.Load<GameObject>($"Units/Cat Units/{unitCode[0]}/{unitCode.Substring(1, 3)}/{unitCode[4]}/uaunit"), cat.transform.position, Quaternion.identity);
            uaunit.transform.SetParent(cat.transform);
            ResetAnimationOrderLayer(uaunit, "Units", sortingOrder * 1000 + sr_samelayer * 100); 
        }
        else
        {
            AnimationDisplayer ad = cat.GetComponent<AnimationDisplayer>();
            ad.Initialization(characterDecryptedFiles);
            ad.OrderLayerStart = sortingOrder * 1000 + sr_samelayer * 50;
            ad.ResetModelOrderLayer();
        }
        DeployAvailable(false);
        if (isGuest) { gameObject.SetActive(false); }
        // Proficiency
        LI.RecordProficency_Deploy(unitCode);
    }

    private bool EnsureRuntimeInitialized()
    {
        if (isRuntimeInitialized) return true;
        if (CD == null) return false;

        if (!CD.UNITYAnimated)
        {
            Texture2D unitTexture = Resources.Load<Texture2D>(loadPath + "sprite");
            TextAsset imagecut = Resources.Load<TextAsset>(loadPath + "imgcut");
            TextAsset mamodel = Resources.Load<TextAsset>(loadPath + "mamodel");
            TextAsset maanim_walk = Resources.Load<TextAsset>(loadPath + "maanim_walk");
            TextAsset maanim_idle = Resources.Load<TextAsset>(loadPath + "maanim_idle");
            TextAsset maanim_attack = Resources.Load<TextAsset>(loadPath + "maanim_attack");
            TextAsset maanim_kb = Resources.Load<TextAsset>(loadPath + "maanim_kb");

            List<TextAsset> maanims = new List<TextAsset> { maanim_walk, maanim_idle, maanim_attack, maanim_kb };
            if (CD.career.Practician)
            {
                TextAsset maanim_p = Resources.Load<TextAsset>(loadPath + "maanim_p");
                if (maanim_p != null) maanims.Add(maanim_p);
            }

            AnimEncryptPack animEncryptPack = new AnimEncryptPack(unitTexture, imagecut, mamodel, maanims.ToArray());
            characterDecryptedFiles = AnimFileDecrypter.DecryptEncryptPack(animEncryptPack);

            if (characterDecryptedFiles == null) return false;
        }

        isRuntimeInitialized = true;
        return true;
    }
    private void DeployAvailable(bool a)
    {
        btn.interactable = a;
    }
    private void MoneyColor(bool enough)
    {
        if(enough)cost_txt.color = Color.white;
        else cost_txt.color = Color.red;
    }

    private void ApplyRarityFrameColor()
    {
        if (deployPanel == null) return;

        int rarity = 0;
        if (!string.IsNullOrEmpty(unitCode) && unitCode.Length > 0)
        {
            rarity = Mathf.Clamp(unitCode[0] - '0', 0, 6);
        }
        deployPanel.SetOutfit(KiOutfit.Border, rarity + 1);
        deployPanel.ApplyFrameColor(UXPref.GetRarityFrameColor(rarity));
    }
    private static void ResetAnimationOrderLayer(GameObject go, string sortingLayer, int order)
    {
        if (go == null) return;
        if (go.TryGetComponent(out SpriteRenderer sr))
        {
            sr.sortingLayerName = sortingLayer;
            sr.sortingOrder = order;
        }
        foreach (Transform child in go.transform) ResetAnimationOrderLayer(child.gameObject, sortingLayer, order);
    }
}
