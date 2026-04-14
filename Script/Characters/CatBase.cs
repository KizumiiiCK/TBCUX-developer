using System.Collections;
using System.Collections.Generic;
using TMPro;
using UnityEngine.UI;
using UnityEngine;

public class CatBase : CatCharacter
{
    protected override TargetRegistrationKind RegistrationKind => TargetRegistrationKind.PersistentBase;

    private const float CANNON_CHARGE_TIME = 1350f;
    private const string CANNON_UNIT_PATH = "Units/CatBases/effectUnits/{0}/cannonUnit";
    private const string CANNON_INSTALL_COMPLETE_EFFECT_PATH = "Units/CatBases/effectUnits/5/eff/1";
    private const float CANNON_INSTALL_DURATION = 10f;
    private int cannon_type = 0;

    private Transform main;
    private Transform headTransform;
    private GameObject TowerStrike;
    private AudioSource audioSource;
    private TMP_Text healthInfo;
    private Coroutine shakecoroutine;
    private int num_base;
    private int num_deco;
    private int cannonCharged = 0;
    private Button cannonButton;
    private Image cannonButtonImage;
    private Animator cannonButtonAnimation;
    private bool isCannonInstalling = false;
    private bool isBaseDefeated = false;
    public override void SetAttackRange(float far, float near) { }
    //public override void GetStrategy() { }
    public override void InitializeCharacter()
    {
        int wt = RewardingSystem.GetAmount(RewardName.WorldTreasures);
        maxHealth = Health;
        realHealth = maxHealth;
        realDamage = new int[atkInfos.Length];
        for (int i = 0; i < atkInfos.Length; i++)
        {
            realDamage[i] = (int)atkInfos[i].ATK;
        }
        realReload = 0;
        num_base = PlayerPrefs.GetInt(UXPref.BASE_BaseNum, 0);
        num_deco = PlayerPrefs.GetInt(UXPref.BASE_DecorationNum, 0);
        cannon_type = PlayerPrefs.GetInt(UXPref.BASE_CannonNum, 0);
        Sprite towerBase=Resources.Load<Sprite>($"Units/CatBases/base/{num_base}");
        Sprite towerDecoration = Resources.Load<Sprite>($"Units/CatBases/decorations/{num_deco}");
        audioSource=GetComponent<AudioSource>();
        main = transform.GetChild(0);
        healthInfo=transform.GetChild(1).GetChild(0).GetComponent<TMP_Text>();
        UpdateHealthInfo();
        SpriteRenderer renderer_base=main.GetChild(0).GetComponent<SpriteRenderer>();
        SpriteRenderer renderer_deco=main.GetChild(1).GetComponent<SpriteRenderer>();
        renderer_base.sprite = towerBase;
        renderer_deco.sprite=towerDecoration;
        SetCannonHead(cannon_type);
        BaseResist();
    }
    public override void ReceiveAttack(float DMG, Traits enemyTraits, SubTraits opponentSubtraits, AgainstCareer opponentCE, DamageRelatedEffect dre, List<CharacterEffect> enemyEffect, List<AttackType> atkType)
    {
        if (realHealth <= 0) { realHealth = 0; return; }
        if (DMG < 0) return;
        if (atkType != null) foreach (var ar in atkTypeResis)
        {
            foreach (var at in atkType)
            {
                if (ar.type == at)
                {
                    if (ar.intensity > 99) { return; }
                    else { DMG *= (100 - ar.intensity) / 100f; break; }
                }
            }
        }
        realHealth -= (int)DMG;
        if (realHealth <= 0) 
        { 
            realHealth = 0; shakecoroutine = StartCoroutine(ShakeTower(atkType,true));
            GameObject[] cats = GameObject.FindGameObjectsWithTag("Cat");
            for (int i = 0; i < cats.Length; i++)
            {
                CatCharacter cc = cats[i].GetComponent<CatCharacter>();
                if (cc != null) cc.Dead();
            }
        }
        else if (shakecoroutine == null) shakecoroutine = StartCoroutine(ShakeTower(atkType));
        UpdateHealthInfo();
    }
    public void ApplyLevelBaseHealth(int health)
    {
        Health = health;
        maxHealth = health;
        realHealth = health;
        if (healthInfo != null) UpdateHealthInfo();
    }
    private void UpdateHealthInfo() {healthInfo.text = $"{realHealth} / {maxHealth}";}
    private IEnumerator ShakeTower(List<AttackType> types, bool permanent=false)
    {
        audioSource.Play();
        HitEffect(types);
        float deviation = 0.05f;
        for(int i = 0; i < 10; i++)
        {
            if (permanent) i = 0;
            float px = Random.Range(-deviation, deviation);
            main.localPosition = new Vector3(px, 0, 0);
            yield return new WaitForFixedUpdate();
        }
        main.localPosition = Vector3.zero;
        shakecoroutine = null;
    }
    public override void UpdateAnimation() { }
    public override float GetFactor() { return 0; }
    public override void StartKBCoroutine(KB_Type kbt = KB_Type.none, float DX = 350) { }
    public override void Dead()
    {
        isBaseDefeated = true;
        GameObject.Find("Level Initializer").GetComponent<LevelController>().Failed();
        StartCoroutine(BreakingDown());
    }
    private IEnumerator BreakingDown()
    {
        float width = 6f;
        float height = 6f;
        while (true)
        {
            float dx = Random.Range(0, width);
            float dy = Random.Range(0, height);
            EM.InstantiateBattleObject(SEnums.bite, dx + transform.position.x, dy, false);
            yield return new WaitForFixedUpdate();
        }
    }

    /// <summary>
    /// 初始化大炮UI
    /// </summary>
    public void SetupCannonUI(Button button, Image buttonImage, Animator buttonAnimation)
    {
        cannonButton = button;
        cannonButtonImage = buttonImage;
        cannonButtonAnimation = buttonAnimation;
        cannonCharged = 0;
        RefreshCannonUI();
    }

    /// <summary>
    /// 大炮充能
    /// </summary>
    public void ChargeCannon()
    {
        if (cannonButton == null || cannonButtonImage == null || cannonButtonAnimation == null) return;
        if (isCannonInstalling) return;

        cannonCharged++;
        RefreshCannonUI();
    }

    /// <summary>
    /// 发射大炮
    /// </summary>
    public void CannonFire()
    {
        if (cannonButton == null || cannonButtonAnimation == null) return;
        if (isCannonInstalling) return;

        cannonCharged = 0;
        cannonButton.interactable = false;
        cannonButtonAnimation.enabled = false;
        Instantiate(Resources.Load(string.Format(CANNON_UNIT_PATH, cannon_type)));
    }

    private void RefreshCannonUI()
    {
        float progress = Mathf.Clamp01(cannonCharged / CANNON_CHARGE_TIME);
        cannonButtonImage.fillAmount = progress;

        if (cannonCharged >= CANNON_CHARGE_TIME)
        {
            cannonButton.interactable = true;
            cannonButtonAnimation.enabled = true;
            cannonButtonAnimation.transform.GetChild(0).gameObject.SetActive(false);
        }
        else
        {
            cannonButton.interactable = false;
            cannonButtonAnimation.enabled = false;
        }
    }

    public void SetCannonHead(int headIndex)
    {
        if (headTransform == null)
        {
            headTransform = main.GetChild(2);
        }
        int clamped = Mathf.Max(0, headIndex);
        ApplyCannonHeadImmediate(clamped, false);
    }

    public bool TrySetCannonHead(int headIndex)
    {
        int clamped = Mathf.Max(0, headIndex);
        if (clamped == cannon_type) return true;
        if (isBaseDefeated || realHealth <= 0) return false;
        if (isCannonInstalling) return false;

        StartCoroutine(InstallCannonHeadRoutine(clamped));
        return true;
    }

    private IEnumerator InstallCannonHeadRoutine(int nextHead)
    {
        isCannonInstalling = true;

        if (cannonButton != null) cannonButton.interactable = false;
        if (cannonButtonAnimation != null) cannonButtonAnimation.enabled = false;

        float width = 6f;
        float height = 3f;
        float elapsed = 0f;
        while (elapsed < CANNON_INSTALL_DURATION)
        {
            elapsed += Time.deltaTime;
            float px = Random.Range(-0.05f, 0.05f);
            if (headTransform != null) headTransform.localPosition = new Vector2(px, 0);
            float dx = Random.Range(0f, width);
            float dy = Random.Range(0f, height)+3;
            EM.InstantiateBattleObject(SEnums.bite, dx + transform.position.x, dy, false);
            yield return new WaitForFixedUpdate();
        }
        if (headTransform != null) headTransform.localPosition = Vector2.zero;

        ApplyCannonHeadImmediate(nextHead, true);
        var completeFx = Resources.Load<GameObject>(CANNON_INSTALL_COMPLETE_EFFECT_PATH);
        if (completeFx != null)
        {
            Instantiate(completeFx, transform.position, Quaternion.identity);
        }

        isCannonInstalling = false;
        RefreshCannonUI();
    }

    private void ApplyCannonHeadImmediate(int headIndex, bool savePref)
    {
        int clamped = Mathf.Max(0, headIndex);
        cannon_type = clamped;
        if (savePref) PlayerPrefs.SetInt(UXPref.BASE_CannonNum, clamped);

        if (main == null || main.childCount <= 2) return;
        SpriteRenderer renderer_head = main.GetChild(2).GetComponent<SpriteRenderer>();
        if (renderer_head == null) return;

        Sprite towerHead = Resources.Load<Sprite>($"Units/CatBases/head/{clamped}");
        if (towerHead != null) renderer_head.sprite = towerHead;
    }
}
