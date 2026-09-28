using System.Collections;
using System.Collections.Generic;
using System.Linq;
using TMPro;
using UnityEngine;
using UnityEngine.Localization;
using UnityEngine.UI;
using UnityEngine.Video;

public class DrawCapsuleCanvas : UICanvasMain
{
    [Header("Prefab")]
    [SerializeField] private KiButton posterButtonPrefab;
    [SerializeField] private GameObject indexUnit;
    [SerializeField] private PosterUIRoulette posterRoulette;
    [Header("Elements")]
    [SerializeField] private Button DrawBtn_x1;
    [SerializeField] private Button DrawBtn_x10;
    [SerializeField] private Button DrawBtn_x1Minus;
    [SerializeField] private Button DrawBtn_x1Plus;
    [SerializeField] private RectTransform posterContainer;
    //[SerializeField] private VideoPlayer poolVideoPlayer;
    //[SerializeField] private RawImage poolVideoRawImage;
    [SerializeField] private Image item_x1;
    [SerializeField] private Image item_x10;
    [SerializeField] private TMP_Text days_left_text;
    [SerializeField] private TMP_Text count_x1;
    [SerializeField] private TMP_Text count_x10;
    [SerializeField] private TMP_Text draw_x1;
    [SerializeField] private TMP_Text draw_x10;
    //[SerializeField] private GameObject AllDrawElements;
    [SerializeField] private GameObject ConfirmElements;
    [SerializeField] private RectTransform leftPinnedElements;
    [SerializeField] private RectTransform rightPinnedElements;
    private float pinnedElementsMoveDistance = 2000f;
    private float pinnedElementsMoveDuration = 0.5f;
    [Header("AfterDrawElements")]
    [SerializeField] private Button GainBtn;
    [SerializeField] private Button ExchangeBtn;
    [SerializeField] private Button DrawSkipBtn;
    private bool allow_continue = false;
    [SerializeField] private GameObject NewMark;
    [SerializeField] private Image result_icon;
    [SerializeField] private ParticleSystem particles;
    [SerializeField] private TMP_Text char_name;
    [SerializeField] private TMP_Text NP_valueTxt;
    [SerializeField] private AudioSource getBGM;
    [SerializeField] private VideoPlayer drawStageVideoPlayer;
    // Editor fallback only. WebGL cannot play VideoClip; playback uses StreamingAssets URLs.
    [SerializeField] private VideoClip drawStageIdleClip;
    [SerializeField] private VideoClip drawStageRollingClip;
    [SerializeField] private VideoClip drawStageRevealClip;
    [SerializeField] private VideoClip drawStageResultClip;
    [SerializeField] private IndexViewer IV;
    private int current_charactercode;
    [Header("Drop Rate Box")]
    [SerializeField] private DropRateBoard DRB;

    [Header("Others")]
    public BaseCanvas baseCanvas;
    private int current_pool_num = 0;
    private GameObject current_display_character;
    //private GameObject current_display_item;
    private List<Pool> pools;
    private readonly List<int> runtimeExtraCurrencyIds = new List<int>();
    //private List<int> DrawCharacters=new List<int>();
    private bool draw_skip = false;
    private int quickSingleDrawCount = 1;
    //private Coroutine poolVideoSwitchRoutine;
    //private float poolVideoDefaultAlpha = 0.7f;
    private enum DrawStageVideoState { Idle, Rolling, Reveal, Result }
    private const string DrawStageVideoFolder = "video/draw/";
    private const string DrawStageIdleFile = "draw_wait.mp4";
    private const string DrawStageRollingFile = "draw_fin.mp4";
    private const string DrawStageRevealFile = "draw_shine.mp4";
    private const string DrawStageResultFile = "draw_show.mp4";
    private bool pinnedBaseCached;
    private Vector2 leftPinnedBasePos;
    private Vector2 rightPinnedBasePos;

    /// <summary>必需资源的预热重试次数与退避基数。见 <see cref="PrewarmWithRetry"/>。</summary>
    private const int PrewarmMaxAttempts = 3;
    private const float PrewarmRetryBackoff = 0.75f;

    /// <summary>单次 Prepare 的等待上限与重试次数。见 <see cref="PrepareThenPlayRoutine"/>。</summary>
    private const int DrawStageVideoMaxAttempts = 3;
    private const float DrawStageVideoPrepareTimeout = 8f;
    private Coroutine drawStageVideoRoutine;
    private bool drawStageVideoErrored;

    // Start is called before the first frame update
    void Start()
    {
        //GameObject.Find("Main Camera").transform.localPosition = new Vector3(0, 0, -10);
        baseCanvas = GameObject.Find("BaseCanvas").GetComponent<BaseCanvas>();
        GetComponent<Canvas>().worldCamera = GameObject.Find("Main Camera").GetComponent<Camera>();
        InitializePosterRoulette();
        LoadAllPools();
        InitializeButtons();
        //AllDrawElements.SetActive(true);
        ConfirmElements.SetActive(false);
        DRB.gameObject.SetActive(false);
        SetParticleRate(0);
        //InitializePoolVideoPlayer();
        InitializeDrawStageVideoPlayer();
        CachePinnedElementsBasePosition();
        CheckPrevioulyDrawed();
    }

    public void LoadAllPools()
    {
        pools = PoolInfo.pools
            .Select((pool, originalIndex) => new { pool, originalIndex })
            .Where(entry => entry.pool.ShouldShowInCapsuleList())
            .OrderBy(entry => GetPoolDisplayGroup(entry.pool))
            .ThenBy(entry => GetPoolDisplayGroup(entry.pool) == PoolDisplayGroupUpcoming
                ? entry.pool.DaysUntilNextStart()
                : 0)
            .ThenBy(entry => entry.originalIndex)
            .Select(entry => entry.pool)
            .ToList();

        var posters = new List<Sprite>(pools.Count);
        for (int i = 0; i < pools.Count; i++) posters.Add(pools[i].GetPoolPoster());
        if (posterRoulette != null) posterRoulette.SetPosters(posters, 0);
        if (pools.Count > 0) LoadPool(0);
    }

    private const int PoolDisplayGroupLimited = 0;
    private const int PoolDisplayGroupPermanent = 1;
    private const int PoolDisplayGroupUpcoming = 2;

    private static int GetPoolDisplayGroup(Pool pool)
    {
        if (pool.IsPermanent()) return PoolDisplayGroupPermanent;
        if (pool.IsPoolActivating()) return PoolDisplayGroupLimited;
        return PoolDisplayGroupUpcoming;
    }

    private static bool IsPoolDrawable(Pool pool) =>
        pool != null && (pool.IsPermanent() || pool.IsPoolActivating());

    public void LoadPool(int poolNum)
    {
        if (pools == null || poolNum < 0 || poolNum >= pools.Count) return;
        current_pool_num = poolNum;
        Pool p= pools[poolNum];
        bool upcoming = !IsPoolDrawable(p);
        item_x1.sprite = StorageImageHelper.GetItemImage(p.cost_item[0]);
        item_x10.sprite = StorageImageHelper.GetItemImage(p.cost_item[1]);
        count_x10.text = "x " + p.cost_amount[1].ToString();
        if (upcoming)
        {
            int daysUntil = Mathf.Max(1, p.DaysUntilNextStart());
            days_left_text.text = $"COMING IN  <color=#00FFFF>{daysUntil}</color>  DAY(S)!";
        }
        else if (p.IsPermanent())
        {
            days_left_text.text = string.Empty;
        }
        else
        {
            days_left_text.text = $"<color=#FF6060>{PoolSystemTime.ActivityDayLeft(p.pool_start_delay,p.pool_cycle_period,p.pool_duration)}</color>  DAY(S)  LEFT !";
        }
        RefreshFrameUICurrenciesForPool(p);
        RefreshQuickSingleDrawControls(p);
        if (upcoming)
        {
            count_x1.color = Color.white;
            count_x10.color = Color.white;
            DrawBtn_x1.interactable = false;
            DrawBtn_x10.interactable = false;
        }
        else if (PoolInfo.test_free)
        {
            count_x10.text = "x 0";
            count_x1.color = Color.white;
            count_x10.color = Color.white;
            DrawBtn_x1.interactable = true;
            DrawBtn_x10.interactable = true;
        }
        else
        {
            int singleCost = p.cost_amount[0] * quickSingleDrawCount;
            count_x1.color = RewardingSystem.CheckItemIsEnough(p.cost_item[0], singleCost) ? Color.white : Color.red;
            DrawBtn_x1.interactable = RewardingSystem.CheckItemIsEnough(p.cost_item[0], singleCost);
            count_x10.color = RewardingSystem.CheckItemIsEnough(p.cost_item[1], p.cost_amount[1]) ? Color.white : Color.red;
            DrawBtn_x10.interactable = RewardingSystem.CheckItemIsEnough(p.cost_item[1], p.cost_amount[1]);
        }
        draw_x10.text=p.draw_times[1].ToString();
    }
    private void InitializeButtons()
    {
        DrawBtn_x1.onClick.AddListener(Draw_1_Times);
        DrawBtn_x10.onClick.AddListener(Draw_10_Times);
        if (DrawBtn_x1Minus != null) DrawBtn_x1Minus.onClick.AddListener(() => ChangeQuickSingleDrawCount(-1));
        if (DrawBtn_x1Plus != null) DrawBtn_x1Plus.onClick.AddListener(() => ChangeQuickSingleDrawCount(1));
        GainBtn.onClick.AddListener(GainCharacter);
        ExchangeBtn.onClick.AddListener(ExchangeNP);
        DrawSkipBtn.onClick.AddListener(Skip);
        DrawSkipBtn.gameObject.SetActive(false);
    }
    public void Draw_1_Times()
    {
        Pool p = pools[current_pool_num];
        if (!IsPoolDrawable(p)) return;
        int singleCost = p.cost_amount[0] * quickSingleDrawCount;
        int singleDrawTimes = p.draw_times[0] * quickSingleDrawCount;
        if(!PoolInfo.test_free)
            if(!RewardingSystem.ConsumeItem(p.cost_item[0], singleCost)) return;
        DrawBtn_x1.interactable = false;
        DrawBtn_x10.interactable = false;
        baseCanvas.UpdateCurrencies();
        if (FrameUI != null) FrameUI.RefreshCurrencyAmounts();
        for (int i = 0; i < singleDrawTimes; i++) DrawSave.SaveDrawed(p.Draw());
        StartCoroutine(DisplayDrawedCharacters(singleDrawTimes));
    }
    public void Draw_10_Times()
    {
        if (!IsPoolDrawable(pools[current_pool_num])) return;
        if (!PoolInfo.test_free)
            if (!RewardingSystem.ConsumeItem(pools[current_pool_num].cost_item[1], pools[current_pool_num].cost_amount[1])) return;
        DrawBtn_x1.interactable = false;
        DrawBtn_x10.interactable = false;
        baseCanvas.UpdateCurrencies();
        if (FrameUI != null) FrameUI.RefreshCurrencyAmounts();
        int dt = pools[current_pool_num].draw_times[1];
        //if (dt == 11) DrawCharacters.Add(pools[current_pool_num].Draw(true));
        if (dt == 11) DrawSave.SaveDrawed(pools[current_pool_num].Draw(true));
        //for (int i = 0; i < 10;i++) DrawCharacters.Add(pools[current_pool_num].Draw());
        for (int i = 0; i < 10;i++) DrawSave.SaveDrawed(pools[current_pool_num].Draw());
        StartCoroutine(DisplayDrawedCharacters(pools[current_pool_num].draw_times[1]));
    }
    private IEnumerator DisplayDrawedCharacters(int drawtimes)
    {
        SetPageInProgress(true);
        try
        {
        draw_skip = false;
        SetDrawStageVideoState(DrawStageVideoState.Rolling);
        yield return MovePinnedElementsRoutine(true);
        //AllDrawElements.SetActive(false);
        List<int> DrawCharacters = DrawSave.GetPreviouslyDrawed();

        // 抽卡结果是运行时随机的，无法提前预热。转场动画的这几秒正好是天然的下载窗口：
        // 在动画播放的同时把本次全部中奖角色的资源拉下来，玩家感知不到等待。
        Coroutine preload = StartCoroutine(PreloadDrawedCharacters(DrawCharacters));
        yield return new WaitForSeconds(3.5f);
        SetParticleColor(FindMostRare(DrawCharacters),25);
        SetParticleRate(25);
        yield return new WaitForSeconds(2);
        SetParticleRate(0);
        yield return new WaitForSeconds(1);
        // 万一网络慢于动画，这里补等，保证首个角色一定能显示出来
        if (preload != null) yield return preload;
        for (int i = DrawCharacters.Count - 1; i >= 0; i--)
        {
            ConfirmElements.SetActive(false);
            allow_continue = false;
            yield return ShowCertainCharacterRoutine(DrawCharacters[i]);
            current_charactercode = DrawCharacters[i];
            CheckSkip(DrawCharacters[i]);
            yield return SetupInfoBoardRoutine(current_charactercode);
            yield return PlayRevealThenResultStageVideo();
            GainBtn.interactable = CharacterUpgradeSave.DrawUpgradeAvailable(DrawCharacters[i].ToString("0000"));
            PlatformAudio.PlaySfx(getBGM);
            ConfirmElements.SetActive(true);
            draw_skip = false;
            DrawSkipBtn.gameObject.SetActive(false);

            while (true)
            {
                if (!allow_continue) yield return null;
                else break;
            }
        }
        Instantiate(Resources.Load<GameObject>("UI/Tag Out"));
        yield return new WaitForSeconds(0.9f);
        Instantiate(Resources.Load<GameObject>("UI/Tag In"));
        //AllDrawElements.SetActive(true);
        SetDrawStageVideoState(DrawStageVideoState.Idle);
        LoadPool(current_pool_num);
        DrawCharacters.Clear();
        if (current_display_character != null) DestroyImmediate(current_display_character.gameObject);
        ConfirmElements.SetActive(false);
        DrawBtn_x1.interactable = true;
        DrawBtn_x10.interactable = true;
        SetParticleRate(0);
        DrawSkipBtn.gameObject.SetActive(false);
        yield return MovePinnedElementsRoutine(false);
        }
        finally
        {
            SetPageInProgress(false);
        }
    }
    /// <summary>
    /// 「这个地址不需要，或者已经预热到位」。
    ///
    /// 不能一律要求命中：<see cref="BattlePrewarm.AddUnit"/> 会把 UA 动画（uaunit）和自绘动画
    /// （sprite/imgcut/mamodel）两套都排进列表，而任一单位只存在其中一套，缺的那套是正常的。
    /// 所以先用 catalog 判断该不该有，再看缓存里在不在。
    ///
    /// 用 TryGetPrewarmed 而不是 LoadSync：后者把每次未命中都当成预热漏洞记进 miss 报告，
    /// 拿它来做轮询判断会刷一堆假的 [PREWARM MISS]，把真的漏洞埋掉。
    /// </summary>
    private static bool Ready<T>(string address) where T : UnityEngine.Object
    {
        if (!BundledAddressables.Exists(address, typeof(T))) return true;
        return BundledAddressables.TryGetPrewarmed<T>(address, out _);
    }

    /// <summary>一个单位「显示所需资源是否到位」。</summary>
    private static bool UnitVisualsReady(string root)
    {
        return Ready<GameObject>("Units/Cat Units/catunit")
            && Ready<CharacterData>(root + "data")
            && Ready<GameObject>(root + "uaunit")
            && Ready<Texture2D>(root + "sprite")
            && Ready<TextAsset>(root + "imgcut")
            && Ready<TextAsset>(root + "mamodel");
    }

    /// <summary>
    /// 预热必需资源，未到位就重试。
    ///
    /// PrewarmRoutine 在弱网下的失败是静默的：BundledAddressables 只打一条 LogWarning，
    /// 失败的 handle 被释放且不入缓存，于是 LoadSync 返回 null、面板留白、角色不出现，
    /// 而协程本身正常走完。抽卡页没有 PrewarmGate，也没有任何重新拉取的时机，
    /// 所以那一瞬间的失败会永久固化——这正是「等多久都不显示」的由来。
    ///
    /// 重跑同一个 PrewarmList 是安全的：PrewarmRoutine 只按下标读 Entries、不改动它，
    /// 而 PrewarmSingle 对已在缓存里的地址直接早退，所以重试只会重新去拉真正失败的那几个。
    /// </summary>
    private static IEnumerator PrewarmWithRetry(
        BundledAddressables.PrewarmList list,
        System.Func<bool> satisfied,
        string what)
    {
        for (int attempt = 1; attempt <= PrewarmMaxAttempts; attempt++)
        {
            yield return BundledAddressables.PrewarmRoutine(list);
            if (satisfied == null || satisfied()) yield break;

            if (attempt < PrewarmMaxAttempts)
            {
                Debug.LogWarning($"[DrawCapsule] {what} 预热未到位(第 {attempt} 次)，退避后重试。");
                yield return new WaitForSecondsRealtime(PrewarmRetryBackoff * attempt);
            }
            else
            {
                Debug.LogError($"[DrawCapsule] {what} 预热连续 {PrewarmMaxAttempts} 次未到位，显示会不完整。");
            }
        }
    }

    /// <summary>
    /// 预加载本次抽到的全部角色资源。抽卡结果随机，只能在结果产生之后拉取，
    /// 因此放在转场动画期间并行进行。
    /// </summary>
    private IEnumerator PreloadDrawedCharacters(List<int> codes)
    {
        if (codes == null || codes.Count == 0) yield break;

        var list = new BundledAddressables.PrewarmList();
        var seen = new HashSet<int>();
        for (int i = 0; i < codes.Count; i++)
        {
            int code = codes[i];
            if (!seen.Add(code)) continue;

            int rarity = code / 1000;
            string charCode = (code % 1000).ToString("000");
            string root = $"Units/Cat Units/{rarity}/{charCode}/0/";

            // 展示用的完整单位资源 + 结果面板的图标
            BattlePrewarm.AddUnit(list, true, $"{code}0");
            list.Add<CharacterData>(root + "data");
            list.Add<Sprite>(root + "icon_deploy");
        }
        list.Add<GameObject>("Units/Cat Units/catunit");
        yield return PrewarmWithRetry(list, () => AllDrawedReady(seen), "本次抽卡结果");
    }

    private static bool AllDrawedReady(HashSet<int> codes)
    {
        foreach (int code in codes)
        {
            string root = $"Units/Cat Units/{code / 1000}/{(code % 1000).ToString("000")}/0/";
            if (!UnitVisualsReady(root) || !Ready<Sprite>(root + "icon_deploy")) return false;
        }
        return true;
    }

    /// <summary>
    /// 显示中奖角色。资源已由 PreloadDrawedCharacters 拉好，这里补一次预热兜底。
    /// </summary>
    public void ShowCertainCharacter(int code) => StartCoroutine(ShowCertainCharacterRoutine(code));

    private IEnumerator ShowCertainCharacterRoutine(int code)
    {
        int rarity = code / 1000;
        string char_code = (code % 1000).ToString("000");
        string characterCode = $"{code}0";
        string root = $"Units/Cat Units/{rarity}/{char_code}/0/";

        // 兜底：若转场期间的预加载未覆盖（例如网络失败重试），此处补齐
        var list = new BundledAddressables.PrewarmList();
        BattlePrewarm.AddUnit(list, true, characterCode);
        list.Add<GameObject>("Units/Cat Units/catunit");
        yield return PrewarmWithRetry(list, () => UnitVisualsReady(root), $"角色 {characterCode}");

        Application.targetFrameRate = 30;
        if (current_display_character != null) DestroyImmediate(current_display_character.gameObject);
        current_display_character = CharacterSummoner.CreateACharacter(true, characterCode, true);
        if (current_display_character == null) yield break;

        CharacterSummoner.SetCharacterPosition(current_display_character, new Vector3(0, -3.5f, 10));
        CharacterVisualLoader.ResetAnimationOrderLayer(current_display_character, "Units", 3);
        CharacterData CD = BundledAddressables.LoadSync<CharacterData>(root + "data");
        if (CD != null)
        {
            CharacterVisualLoader.SwitchAnimation(current_display_character, CD.UNITYAnimated, 0);
            if (CD.UNITYAnimated) current_display_character.transform.localScale *= 1.2f;
        }
    }
    private void CheckSkip(int code)
    {
        bool skipable = CharacterUpgradeSave.GetDetails(code.ToString("0000")).plus_level > 0;
        DrawSkipBtn.gameObject.SetActive(skipable);
        NewMark.SetActive(!skipable);
    }
    private void Skip() => draw_skip = true;
    private void GainCharacter()
    {
        if (current_charactercode > 7000) return;

        CharacterUpgradeSave.UpgradeCharacterByDraw((current_charactercode).ToString("0000"));
        DrawSave.Used();
        current_charactercode = 99999;
        allow_continue = true;
    }
    private void ExchangeNP()
    {
        if (current_charactercode > 7000) return;
        RewardingSystem.GainReward(RewardName.NP, ralityNPMap[current_charactercode/1000]);
        DrawSave.Used();
        current_charactercode = 99999;
        allow_continue = true;
    }
    private IEnumerator SetupInfoBoardRoutine(int charcode)
    {
        int rality = charcode / 1000;
        string code = (charcode % 1000).ToString("000");
        string address = $"Units/Cat Units/{rality}/{code}/0/";

        var list = new BundledAddressables.PrewarmList();
        list.Add<CharacterData>(address + "data");
        list.Add<Sprite>(address + "icon_deploy");
        yield return PrewarmWithRetry(
            list,
            () => Ready<CharacterData>(address + "data") && Ready<Sprite>(address + "icon_deploy"),
            $"结果面板 {rality}{code}");

        // ShowCharacterDetails 要读 CharacterData 的一堆字段，还要走 ApplyTraitGroups /
        // ShowEAIcons 去取别的资源。它排在 result_icon 赋值之前，一旦抛异常，头像那行就永远
        // 执行不到，而 DisplayDrawedCharacters 的 try 只有 finally（C# 不允许在带 catch 的
        // try 里 yield），异常会直接掀掉整个展示流程、只留下一个 SetPageInProgress(false)，
        // 页面看起来「空着但已就绪」。所以在这里就地兜住，宁可少一块信息也要把头像贴上。
        try
        {
            IV.ShowCharacterDetails(BundledAddressables.LoadSync<CharacterData>(address + "data"), true, 1);
        }
        catch (System.Exception e)
        {
            Debug.LogError($"[DrawCapsule] ShowCharacterDetails 失败({rality}{code})，跳过详情继续显示头像: {e}");
        }
        result_icon.sprite = BundledAddressables.LoadSync<Sprite>(address + "icon_deploy");
        LocalizationHelper.GetLocalizedText("UnitNames", $"{rality}{code}0", localizedText => char_name.text = localizedText ?? $"{rality}{code}0");
        SetParticleColor(rality, 15);
        SetParticleRate(25);
        NP_valueTxt.text = $"+{ralityNPMap[rality]}";
    }
    private void SetParticleColor(int rality, int speed)
    {
        particles.GetComponent<RectTransform>().anchoredPosition = Vector2.zero;
        Color c = Color.white;
        if (LP != null) StopCoroutine(LP);
        switch (rality)
        {
            case 0: c = Color.white; break;
            case 1: c = Color.green; break;
            case 2: c = Color.cyan; break;
            case 3: c = new Color(1, 0, 1); break;
            case 4: c = Color.yellow; break;
            case 5: LP = StartCoroutine(LegendParticle()); break;
            case 6: c = Color.red; break;
            default: break;
        }
        var pm = particles.main;
        pm.startColor = c;
        pm.startSpeed = speed;
    }
    private Coroutine LP = null;
    private IEnumerator LegendParticle()
    {
        var pm = particles.main;
        Color c;
        while (true)
        {
            float r = Random.Range(0f, 1f);
            float g = Random.Range(0f, 1f);
            float b = Random.Range(0f, 1f);
            c = new Color(r, g, b);
            pm.startColor = c;
            yield return new WaitForFixedUpdate();
        }
    }
    private void SetParticleRate(int rate)
    {
        var pe = particles.emission;
        pe.rateOverTime = rate;
    }
    public static readonly Dictionary<int, int> ralityNPMap = new Dictionary<int, int>()
    {
        { 0, 2 },
        { 1, 2 },
        { 2, 5 },
        { 3, 15 },
        { 4, 50 },
        { 5, 150 },
        { 6, 50 },
    };
    private int FindMostRare(List<int> DrawCharacters)
    {
        int mr = DrawCharacters.Max()/1000;
        if (mr < 0) mr = 0;
        return mr;
    }
    private void CheckPrevioulyDrawed()
    {
        List<int> codes = DrawSave.GetPreviouslyDrawed();
        if (codes != null && codes.Count > 0) StartCoroutine(DisplayDrawedCharacters(codes.Count));
    }
    public void ShowDetails()
    {
        DRB.gameObject.SetActive(true);
        DRB.InitializeDropDetails(pools[current_pool_num]);
    }
    public void CloseDetails()=>DRB.gameObject.SetActive(false);

    private void InitializePosterRoulette()
    {
        if (posterRoulette == null)
        {
            posterRoulette = GetComponentInChildren<PosterUIRoulette>(true);
            if (posterRoulette == null && posterContainer != null)
            {
                posterRoulette = posterContainer.GetComponent<PosterUIRoulette>();
                if (posterRoulette == null) posterRoulette = posterContainer.gameObject.AddComponent<PosterUIRoulette>();
            }
        }
        if (posterRoulette == null) return;
        posterRoulette.Configure(posterContainer, posterButtonPrefab);
        posterRoulette.Initialize(OnPoolPosterSelected, OnPoolPosterClicked);
        //posterRoulette.SetOnDragEnded(OnPoolPosterDragEnded);
    }

    private void OnPoolPosterSelected(int poolIndex)
    {
        if (poolIndex != current_pool_num) quickSingleDrawCount = 1;
        LoadPool(poolIndex);
    }

    private void ChangeQuickSingleDrawCount(int delta)
    {
        if (pools == null || pools.Count == 0) return;
        int next = Mathf.Clamp(quickSingleDrawCount + delta, 1, 10);
        if (next == quickSingleDrawCount) return;
        quickSingleDrawCount = next;
        LoadPool(current_pool_num);
    }

    private void RefreshQuickSingleDrawControls(Pool pool)
    {
        if (pool == null) return;
        bool showQuickButtons = pool.cost_item[0] != pool.cost_item[1];
        if (DrawBtn_x1Minus != null) DrawBtn_x1Minus.gameObject.SetActive(showQuickButtons);
        if (DrawBtn_x1Plus != null) DrawBtn_x1Plus.gameObject.SetActive(showQuickButtons);

        int singleCost = PoolInfo.test_free ? 0 : pool.cost_amount[0] * quickSingleDrawCount;
        int singleDrawTimes = pool.draw_times[0] * quickSingleDrawCount;
        count_x1.text = "x " + singleCost.ToString();
        draw_x1.text = singleDrawTimes.ToString();
    }

    private void OnPoolPosterClicked(int poolIndex)
    {
        if (poolIndex != current_pool_num) return;
        ShowDetails();
    }

    //private void OnPoolPosterDragEnded(int poolIndex)
    //{
    //    UpdatePoolVideoByIndex(poolIndex, false);
    //}

    //private void InitializePoolVideoPlayer()
    //{
    //    if (poolVideoPlayer == null) poolVideoPlayer = GetComponentInChildren<VideoPlayer>(true);
    //    if (poolVideoRawImage == null && poolVideoPlayer != null) poolVideoRawImage = poolVideoPlayer.GetComponent<RawImage>();
    //    poolVideoDefaultAlpha = GetPoolVideoAlpha();
    //}

    //private void UpdatePoolVideoByIndex(int poolIndex, bool immediate)
    //{
    //    if (pools == null || poolIndex < 0 || poolIndex >= pools.Count) return;
    //    string poolName = pools[poolIndex]?.pool_name;
    //    if (string.IsNullOrEmpty(poolName)) return;
    //    if (poolVideoPlayer == null) return;

    //    VideoClip nextClip = LoadPoolVideoClip(poolName);
    //    if (nextClip == null) return;
    //    if (poolVideoPlayer.clip == nextClip && poolVideoPlayer.isPlaying) return;
    //}

    //private float GetPoolVideoAlpha()
    //{
    //    if (poolVideoRawImage != null) return poolVideoRawImage.color.a;
    //    return 1f;
    //}

    //private void SetPoolVideoAlpha(float alpha)
    //{
    //    if (poolVideoRawImage != null)
    //    {
    //        Color c = poolVideoRawImage.color;
    //        c.a = alpha;
    //        poolVideoRawImage.color = c;
    //    }
    //}

    private void InitializeDrawStageVideoPlayer()
    {
        if (drawStageVideoPlayer == null) return;
        drawStageVideoPlayer.playOnAwake = false;
        // 只订阅一次。PrepareThenPlayRoutine 会被状态切换 StopCoroutine 打断，
        // 在协程里成对订阅/退订必然漏掉退订，攒成重复回调。
        drawStageVideoPlayer.errorReceived -= OnDrawStageVideoError;
        drawStageVideoPlayer.errorReceived += OnDrawStageVideoError;
        SetDrawStageVideoState(DrawStageVideoState.Idle);
    }

    protected override void OnDestroy()
    {
        if (drawStageVideoPlayer != null) drawStageVideoPlayer.errorReceived -= OnDrawStageVideoError;
        base.OnDestroy();
    }

    private void OnDrawStageVideoError(VideoPlayer source, string message)
    {
        drawStageVideoErrored = true;
        Debug.LogError($"[DrawCapsule] 抽卡视频报错：{message}(url={(source != null ? source.url : "?")})");
    }

    private static string DrawStageVideoUrl(string fileName)
    {
#if UNITY_WEBGL && !UNITY_EDITOR
        return Application.streamingAssetsPath + "/" + DrawStageVideoFolder + fileName;
#else
        string path = System.IO.Path.Combine(Application.streamingAssetsPath, "video", "draw", fileName);
        return new System.Uri(path).AbsoluteUri;
#endif
    }

    private void SetDrawStageVideoState(DrawStageVideoState state)
    {
        if (drawStageVideoPlayer == null) return;
        VideoClip clip = null;
        string fileName;
        bool loop = true;
        switch (state)
        {
            case DrawStageVideoState.Rolling:
                clip = drawStageRollingClip;
                fileName = DrawStageRollingFile;
                loop = false;
                break;
            case DrawStageVideoState.Reveal:
                clip = drawStageRevealClip;
                fileName = DrawStageRevealFile;
                loop = false;
                break;
            case DrawStageVideoState.Result:
                clip = drawStageResultClip;
                fileName = DrawStageResultFile;
                loop = true;
                break;
            default:
                clip = drawStageIdleClip;
                fileName = DrawStageIdleFile;
                loop = true;
                break;
        }
#if UNITY_WEBGL && !UNITY_EDITOR
        PlayDrawStageFromUrl(DrawStageVideoUrl(fileName), loop);
#else
        if (clip != null)
            PlayDrawStageFromClip(clip, loop);
        else
            PlayDrawStageFromUrl(DrawStageVideoUrl(fileName), loop);
#endif
    }

    private void PlayDrawStageFromUrl(string url, bool loop)
    {
        if (string.IsNullOrEmpty(url)) return;
        if (drawStageVideoPlayer.source == VideoSource.Url
            && drawStageVideoPlayer.url == url
            && drawStageVideoPlayer.isPlaying)
            return;

        if (drawStageVideoRoutine != null) StopCoroutine(drawStageVideoRoutine);
        drawStageVideoRoutine = StartCoroutine(PrepareThenPlayRoutine(url, loop));
    }

    /// <summary>
    /// 等 Prepare 完成再 Play，失败则重试。
    ///
    /// 原来是设完 url 直接 Play() 就返回。WebGL 上 VideoPlayer 背后是浏览器 &lt;video&gt;，
    /// 冷缓存时第一帧还没解出来，RenderTexture 就是纯黑；而 SetDrawStageVideoState 只在
    /// 状态切换时被调用，同一个状态里不会有第二次触发，于是黑屏一直持续到下次状态切换。
    ///
    /// 视频已去掉音轨、audioOutputMode 也设成 None，所以不会撞上浏览器
    /// 「带声音的视频禁止自动播放」策略，Prepare 成功后 Play 一定能起来。
    /// </summary>
    private IEnumerator PrepareThenPlayRoutine(string url, bool loop)
    {
        for (int attempt = 1; attempt <= DrawStageVideoMaxAttempts; attempt++)
        {
            drawStageVideoErrored = false;
            drawStageVideoPlayer.source = VideoSource.Url;
            drawStageVideoPlayer.url = url;
            drawStageVideoPlayer.isLooping = loop;
            drawStageVideoPlayer.Prepare();

            float waited = 0f;
            while (!drawStageVideoPlayer.isPrepared
                   && !drawStageVideoErrored
                   && waited < DrawStageVideoPrepareTimeout)
            {
                // 抽卡过程里会改 timeScale，必须用不受缩放影响的时间来计超时。
                waited += Time.unscaledDeltaTime;
                yield return null;
            }

            if (drawStageVideoPlayer.isPrepared && !drawStageVideoErrored)
            {
                drawStageVideoPlayer.Play();
                drawStageVideoRoutine = null;
                yield break;
            }

            Debug.LogWarning($"[DrawCapsule] 视频 Prepare 未就绪(第 {attempt} 次, 等了 {waited:F1}s, " +
                             $"errored={drawStageVideoErrored}): {url}");
        }

        Debug.LogError($"[DrawCapsule] 视频连续 {DrawStageVideoMaxAttempts} 次无法就绪，这一段会保持黑屏: {url}");
        drawStageVideoRoutine = null;
    }

    private void PlayDrawStageFromClip(VideoClip clip, bool loop)
    {
        if (clip == null) return;
        if (drawStageVideoPlayer.source == VideoSource.VideoClip
            && drawStageVideoPlayer.clip == clip
            && drawStageVideoPlayer.isPlaying)
            return;
        drawStageVideoPlayer.source = VideoSource.VideoClip;
        drawStageVideoPlayer.clip = clip;
        drawStageVideoPlayer.isLooping = loop;
        drawStageVideoPlayer.Play();
    }

    private IEnumerator PlayRevealThenResultStageVideo()
    {
        if (drawStageVideoPlayer != null)
        {
            SetDrawStageVideoState(DrawStageVideoState.Reveal);
            float elapsed = 0f;
            while (elapsed < 1f)
            {
                if (draw_skip) break;
                elapsed += Time.unscaledDeltaTime;
                yield return null;
            }
        }
        SetDrawStageVideoState(DrawStageVideoState.Result);
    }

    private void CachePinnedElementsBasePosition()
    {
        if (pinnedBaseCached) return;
        if (leftPinnedElements != null) leftPinnedBasePos = leftPinnedElements.anchoredPosition;
        if (rightPinnedElements != null) rightPinnedBasePos = rightPinnedElements.anchoredPosition;
        pinnedBaseCached = true;
    }

    private IEnumerator MovePinnedElementsRoutine(bool moveOut)
    {
        CachePinnedElementsBasePosition();
        if (leftPinnedElements == null && rightPinnedElements == null) yield break;

        Vector2 leftStart = leftPinnedElements != null ? leftPinnedElements.anchoredPosition : Vector2.zero;
        Vector2 rightStart = rightPinnedElements != null ? rightPinnedElements.anchoredPosition : Vector2.zero;
        Vector2 leftTarget = leftPinnedBasePos;
        Vector2 rightTarget = rightPinnedBasePos;
        if (moveOut)
        {
            leftTarget += Vector2.left * pinnedElementsMoveDistance;
            rightTarget += Vector2.right * pinnedElementsMoveDistance;
        }

        float duration = Mathf.Max(0.01f, pinnedElementsMoveDuration);
        float elapsed = 0f;
        while (elapsed < duration)
        {
            elapsed += Time.unscaledDeltaTime;
            float t = Mathf.Clamp01(elapsed / duration);
            if (leftPinnedElements != null) leftPinnedElements.anchoredPosition = Vector2.Lerp(leftStart, leftTarget, t);
            if (rightPinnedElements != null) rightPinnedElements.anchoredPosition = Vector2.Lerp(rightStart, rightTarget, t);
            yield return null;
        }
        if (leftPinnedElements != null) leftPinnedElements.anchoredPosition = leftTarget;
        if (rightPinnedElements != null) rightPinnedElements.anchoredPosition = rightTarget;
    }

    private void RefreshFrameUICurrenciesForPool(Pool pool)
    {
        if (FrameUI == null || pool == null || pool.cost_item == null) return;

        runtimeExtraCurrencyIds.Clear();
        if (ExtraCurrencyIds != null)
        {
            for (int i = 0; i < ExtraCurrencyIds.Count; i++)
            {
                int id = ExtraCurrencyIds[0];
                if (!runtimeExtraCurrencyIds.Contains(id)) runtimeExtraCurrencyIds.Add(id);
            }
        }
        int nid = RewardingSystem.RewardNumMap[pool.cost_item[0]];
        if (!runtimeExtraCurrencyIds.Contains(nid)) runtimeExtraCurrencyIds.Add(nid);
        FrameUI.SetCurrentExtraCurrencies(runtimeExtraCurrencyIds);
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
        //if (poolVideoSwitchRoutine != null)
        //{
        //    StopCoroutine(poolVideoSwitchRoutine);
        //    poolVideoSwitchRoutine = null;
        //}
        if (FrameUI != null)
        {
            FrameUI.CloseDoor();
            yield return new WaitForSecondsRealtime(FrameUIAnimations.DoorDuration);
        }
    }
}
public static class DrawSave
{
    public static readonly string filename = "8FFA527B4C49DD6A4477B477830CA71D"; // drawsave
    private static List<int> LoadOrCreate()
    {
        var data = SaveCodec.DecodeIntList(BuildaSaveBackend.Get(SaveKeys.DrawPending));
        if (data == null)
        {
            data = new List<int>();
            Save(data);
        }
        return data;
    }

    private static void Save(List<int> data) =>
        BuildaSaveBackend.Set(SaveKeys.DrawPending, SaveCodec.EncodeIntList(data));

    public static void SaveDrawed(List<int> codes)
    {
        List<int> drawed = LoadOrCreate();
        for (int i = 0; i < codes.Count; i++) {
            drawed.Add(codes[i]);
        }
        Save(drawed);
    }
    public static void SaveDrawed(int code)
    {
        List<int> drawed = LoadOrCreate();
        drawed.Add(code);
        Save(drawed);
    }
    public static List<int> GetPreviouslyDrawed()=> LoadOrCreate();
    public static void Used()
    {
        List<int> drawed = LoadOrCreate();
        drawed.RemoveAt(drawed.Count-1);
        Save(drawed);
    }
}
