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

    public int CannonType => cannon_type;
    public GameObject CurrentCannonPrefab => currentCannonPrefab;
    public bool IsCannonInstalling => isCannonInstalling;

    private GameObject currentCannonPrefab;
    private Transform main;
    private Transform headTransform;
    private GameObject TowerStrike;
    private AudioSource audioSource;
    private TMP_Text healthInfo;
    private Coroutine shakecoroutine;
    private readonly List<Character> teamDeadBuffer = new List<Character>(128);
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
        Sprite towerBase=BundledAddressables.LoadSync<Sprite>($"Units/CatBases/base/{num_base}");
        Sprite towerDecoration = BundledAddressables.LoadSync<Sprite>($"Units/CatBases/decorations/{num_deco}");
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

        // 游戏开始时加载选定的基地大炮攻击单位及其特效
        LoadCannonUnit(cannon_type);
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
        Passive_OnBeforeTakeDamage(ref DMG, atkType);
        realHealth -= (int)DMG;
        Passive_OnAfterTakeDamage();
        if (realHealth <= 0) 
        { 
            Dead();
            realHealth = 0; shakecoroutine = StartCoroutine(ShakeTower(atkType,true));
            CharacterTargetManager manager = CharacterTargetManager.Instance;
            int count = manager.FillTeamCharacters(true, teamDeadBuffer, this, includeUndetectable: true);
            for (int i = 0; i < count; i++)
            {
                CatCharacter cc = teamDeadBuffer[i] as CatCharacter;
                if (cc != null) cc.Dead();
            }
            teamDeadBuffer.Clear();
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
        PlatformAudio.PlaySfx(audioSource);
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
        if (isBaseDefeated) return;
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
            EM.InstantiateBattleObject(SEnums.bite, dx + transform.position.x, transform.position.y + dy, false);
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

    public static string GetCannonUnitAddress(int headIndex) => $"Units/CatBases/effectUnits/{Mathf.Max(0, headIndex)}/cannonUnit";
    public static string GetCannonEffFolder(int headIndex) => $"Units/CatBases/effectUnits/{Mathf.Max(0, headIndex)}/eff";

    /// <summary>
    /// 异步预热并加载指定大炮的攻击单位及全部特效资源，确保可被直接生成
    /// </summary>
    public Coroutine LoadCannonUnit(int headIndex, System.Action<GameObject> onComplete = null)
    {
        return StartCoroutine(LoadCannonUnitRoutine(headIndex, onComplete));
    }

    private IEnumerator LoadCannonUnitRoutine(int headIndex, System.Action<GameObject> onComplete = null)
    {
        int clamped = Mathf.Max(0, headIndex);
        string unitAddress = GetCannonUnitAddress(clamped);
        string effFolder = GetCannonEffFolder(clamped);
        string headAddress = $"Units/CatBases/head/{clamped}";

        var list = new BundledAddressables.PrewarmList();
        list.Add<Sprite>(headAddress);
        list.Add<GameObject>(unitAddress);
        list.AddNumbered<GameObject>(effFolder, 16);
        list.Add<GameObject>(CANNON_INSTALL_COMPLETE_EFFECT_PATH);

        yield return BundledAddressables.PrewarmRoutine(list);

        GameObject loadedPrefab = BundledAddressables.LoadSync<GameObject>(unitAddress);
        if (loadedPrefab != null)
        {
            if (cannon_type == clamped)
            {
                currentCannonPrefab = loadedPrefab;
            }
        }
        else
        {
            Debug.LogWarning($"[CatBase] Failed to load cannon unit prefab for cannon_type={clamped} at '{unitAddress}'");
        }

        onComplete?.Invoke(loadedPrefab);
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

        GameObject cannonPrefab = currentCannonPrefab;
        if (cannonPrefab == null)
        {
            string unitAddress = GetCannonUnitAddress(cannon_type);
            cannonPrefab = BundledAddressables.LoadSync<GameObject>(unitAddress);
            if (cannonPrefab != null) currentCannonPrefab = cannonPrefab;
        }

        if (cannonPrefab != null)
        {
            GameObject cannonObj = Instantiate(cannonPrefab);
            CannonUnit unit = cannonObj.GetComponent<CannonUnit>();
            if (unit != null)
            {
                unit.cannon_type = cannon_type;
            }
        }
        else
        {
            Debug.LogError($"[CatBase] Cannon fire failed: cannot load cannonUnit for cannon_type {cannon_type}. Retrying load...");
            LoadCannonUnit(cannon_type);
        }
    }

    private void RefreshCannonUI()
    {
        float progress = Mathf.Clamp01(cannonCharged / CANNON_CHARGE_TIME);
        cannonButtonImage.fillAmount = progress;

        if (cannonCharged >= CANNON_CHARGE_TIME && !isCannonInstalling)
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

        // 战斗中更换基地攻击单位时，立即开始加载对应类型的攻击单位
        Coroutine loadRoutine = LoadCannonUnit(nextHead);

        float width = 6f;
        float height = 3f;
        float elapsed = 0f;
        while (elapsed < CANNON_INSTALL_DURATION)
        {
            if (isBaseDefeated || realHealth <= 0)
            {
                isCannonInstalling = false;
                yield break;
            }
            elapsed += Time.deltaTime;
            float px = Random.Range(-0.05f, 0.05f);
            if (headTransform != null) headTransform.localPosition = new Vector2(px, 0);
            float dx = Random.Range(0f, width);
            float dy = Random.Range(0f, height)+3;
            EM.InstantiateBattleObject(SEnums.bite, dx + transform.position.x, transform.position.y + dy, false);
            yield return new WaitForFixedUpdate();
        }
        if (headTransform != null) headTransform.localPosition = Vector2.zero;

        // 确保新攻击单位资源已加载完成
        if (loadRoutine != null) yield return loadRoutine;

        ApplyCannonHeadImmediate(nextHead, true);
        var completeFx = BundledAddressables.LoadSync<GameObject>(CANNON_INSTALL_COMPLETE_EFFECT_PATH);
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

        string unitAddress = GetCannonUnitAddress(clamped);
        GameObject loaded = BundledAddressables.LoadSync<GameObject>(unitAddress);
        if (loaded != null) currentCannonPrefab = loaded;

        if (main == null || main.childCount <= 2) return;
        SpriteRenderer renderer_head = main.GetChild(2).GetComponent<SpriteRenderer>();
        if (renderer_head == null) return;

        Sprite towerHead = BundledAddressables.LoadSync<Sprite>($"Units/CatBases/head/{clamped}");
        if (towerHead != null) renderer_head.sprite = towerHead;
    }
}
