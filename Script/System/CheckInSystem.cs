using System;
using System.Collections;
using System.Collections.Generic;
using System.Globalization;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class CheckInSystem : MonoBehaviour
{
    [SerializeField] private Transform RewardDispalyer;
    [SerializeField] private TMP_Text checkinStatement;
    [SerializeField] private Animator rewardAnimator;

    private int consecutiveDays = 0;
    private bool hasRewardToShow = false;
    private DateTime currentServerDate = DateTime.MinValue;
    private LoadingPage loadingPage;

    private const int typeCount = 3;
    private static float consecutiveBonus = 1 / 30f;
    private static RewardName[] rewardNames = new RewardName[typeCount]
    {
        RewardName.XP,RewardName.CANs,RewardName.Ticket_Gold
    };
    private static int[] rewardCount = new int[typeCount]
    {
        50000,150,1
    };
    private int[] bonusCount = new int[typeCount];

    /// <summary>周年庆期间 XP 换成的抽奖券基础张数。同样吃连签倍率和下面的双倍。</summary>
    private const int AnniversaryTicketBase = 1;

    /// <summary>周年庆期间所有奖励的额外倍率。和连签倍率相乘后才取整，见 BuildTodayRewards。</summary>
    private const int AnniversaryBonusMultiplier = 2;

    /// <summary>
    /// 本次签到实际发放的奖励种类。周年庆期间第一项的 XP 会换成抽奖券，所以必须落在实例数组上
    /// ——上面的 rewardNames 是静态的，活过场景切换，改一次就会污染同一次运行里之后的所有签到。
    /// 初值是默认表的副本，让 SetRewardDisplay 在任何路径下都不会读到未赋值的枚举。
    /// </summary>
    private readonly RewardName[] activeRewardNames = (RewardName[])rewardNames.Clone();

    /// <summary>
    /// 旧的日期缓存键。只留着用来擦除历史值——数据本身已经废弃，见
    /// <see cref="ReadCachedWorldDateToken"/>。
    /// </summary>
    private const string LegacyLastWorldDateCacheKey = "CHECKIN_LAST_WORLD_DATE";
    private static readonly TimeSpan Utc8Offset = TimeSpan.FromHours(8);

    /// <summary>
    /// Raised once today's UTC+8 date is established for this session. Fires at most once per
    /// session; late subscribers read <see cref="VerifiedToday"/> instead.
    /// </summary>
    public static event Action<DateTime> VerifiedTodayResolved;

    /// <summary>
    /// Today's date, or null while it is still unproven.
    /// <para>
    /// Only set through two equally trustworthy paths: a fresh network fetch, or a cached date
    /// that still matches the local clock. The latter is sound because the cache is written only
    /// after a successful fetch — if the device clock had been moved since, the two would differ
    /// and the online flow would run instead. A failed fetch leaves this null: the cache then
    /// proves only that some earlier day was verified, not which day today is.
    /// </para>
    /// </summary>
    public static DateTime? VerifiedToday { get; private set; }

    private static void ResolveVerifiedToday(DateTime date)
    {
        DateTime value = date.Date;
        if (VerifiedToday == value) return;
        VerifiedToday = value;
        VerifiedTodayResolved?.Invoke(value);
    }

    /// <summary>
    /// Today's date when it is proven, or null while it is not.
    /// <para>
    /// Same trust rule as <see cref="VerifiedToday"/>, but usable before the check-in flow has run:
    /// it also accepts a cached date that still matches the local clock, which is the "already went
    /// online today" fast path. Scenes that live ahead of the check-in canvas (the title screen) gate
    /// date-locked content through this. An empty cache, or one disagreeing with the clock, yields
    /// null — the cache then proves only that some earlier day was verified, not which day it is now.
    /// </para>
    /// </summary>
    public static DateTime? GetVerifiedToday()
    {
        if (VerifiedToday.HasValue) return VerifiedToday;

        string token = ReadCachedWorldDateToken();
        if (string.IsNullOrEmpty(token)) return null;
        if (token != GetUtc8TodayToken()) return null;
        return DateTime.TryParseExact(token, "yyyy-MM-dd", CultureInfo.InvariantCulture, DateTimeStyles.None, out DateTime cached)
            ? cached
            : (DateTime?)null;
    }

    /// <summary>
    /// Publishes the cached date on the fast path where the local clock still agrees with it,
    /// so a player who already went online today needs no further network round trip.
    /// </summary>
    private static void ResolveVerifiedTodayFromValidCache()
    {
        DateTime? proven = GetVerifiedToday();
        if (proven.HasValue) ResolveVerifiedToday(proven.Value);
    }

    private void Start()
    {
        if (ShouldSkipByLocalDate())
        {
            // 今天已经结算过，而且缓存日期和设备时钟仍然吻合：把它当成已验证日期发出去。
            // 否则跳过签到的那些天，周年庆这类按日期开的入口会永远等不到 VerifiedToday。
            ResolveVerifiedTodayFromValidCache();
            Close();
            return;
        }
        // No SupabaseSaveRemote.Initialize: the host blocks non-platform network.
        // Streak lives in privateKV; the clock is the device (host polices tampering).
        // Do not early-return on !HasSupabaseConfig — that would disable check-in on WebGL.

        if (rewardAnimator == null) rewardAnimator = GetComponent<Animator>();
        if (rewardAnimator != null) rewardAnimator.speed = 0f;
        StartLoadingCheckIn();
    }

    private void StartLoadingCheckIn()
    {
        GameObject loadingPrefab = Resources.Load<GameObject>("UI/Pages/loading");
        if (loadingPrefab == null)
        {
            Debug.LogError("[CheckInSystem] Missing loading prefab at UI/Pages/loading");
            Close();
            return;
        }

        GameObject loadingObj = Instantiate(loadingPrefab);
        loadingPage = loadingObj.GetComponent<LoadingPage>();
        if (loadingPage == null)
        {
            Debug.LogError("[CheckInSystem] Loading prefab has no LoadingPage component.");
            Destroy(loadingObj);
            Close();
            return;
        }

        var tasks = new List<LoadingTask>
        {
            new LoadingTask("Getting world time...", ExecuteFetchTimeTask),
            new LoadingTask("Processing check-in...", ExecuteLocalCheckInTask)
        };
        loadingPage.Initialize(tasks, OnLoadingCompleted);
    }

    private void OnLoadingCompleted(bool success)
    {
        if (loadingPage != null) Destroy(loadingPage.gameObject);

        if (!success)
        {
            // Today's date stays unproven: the cached token is stale or the clock may have moved.
            // Leave VerifiedToday null so date-gated content neither unlocks nor tears itself down.
            Close();
            return;
        }

        if (!hasRewardToShow)
        {
            Close();
            return;
        }

        SetRewardDisplay();
        if (rewardAnimator != null) rewardAnimator.speed = 1f;
        UpdateCurrency();
    }

    private IEnumerator ExecuteFetchTimeTask(LoadingTask task)
    {
        // 这里不做 main 那样的 Application.internetReachability 门禁：Builda 宿主本来就屏蔽
        // 非平台网络，加上它会让 WebGL 版签到永远失败。日期改由下面的设备时钟路径给出。
        if (loadingPage != null) loadingPage.SetDetail("Reading date...");

        DateTime? serverDate = null;
        // PlatformTimeSystem now reads the device clock rather than a time server (the platform
        // blocks external network access and polices clock tampering host-side), so this cannot
        // fail - but the null branch stays as a guard against a future source that can.
        yield return PlatformTimeSystem.FetchUtc8DateTime(
            value => serverDate = value,
            detail =>
            {
                if (loadingPage != null) loadingPage.SetDetail(detail);
            });

        if (serverDate == null)
        {
            task.Success = false;
            task.Result = null;
            if (loadingPage != null) loadingPage.SetDetail("Failed to determine the date.");
            yield break;
        }

        currentServerDate = serverDate.Value.Date;
        ResolveVerifiedToday(currentServerDate);
        task.Success = true;
        task.Result = currentServerDate;
        if (loadingPage != null) loadingPage.SetDetail($"Date OK: {currentServerDate:yyyy-MM-dd}");
    }

    private bool ShouldSkipByLocalDate()
    {
        string cachedDate = ReadCachedWorldDateToken();
        string today = GetUtc8TodayToken();
        if (string.IsNullOrEmpty(cachedDate))
        {
            return false;
        }
        return cachedDate == today;
    }

    /// <summary>
    /// 纯本地结算：连签状态存在 privateKV（<see cref="SaveKeys.CheckIn"/>）里，不依赖 Supabase，
    /// 也不需要本地账号文件——privateKV 由宿主按玩家隔离，记录天然绑在 Builda 账号上。
    /// 落盘顺序是「先写签到记录，再发奖」——两步之间崩溃玩家只会少拿一次奖励，
    /// 绝不会因为记录没写上而下次重复发奖（旧流程发奖在上传之前，上传失败时就会重复发）。
    /// </summary>
    private IEnumerator ExecuteLocalCheckInTask(LoadingTask task)
    {
        if (currentServerDate == DateTime.MinValue)
        {
            task.Success = false;
            task.Result = null;
            if (loadingPage != null) loadingPage.SetDetail("Check-in failed: invalid date state.");
            yield break;
        }
        if (loadingPage != null) loadingPage.SetDetail("Reading check-in data...");
        yield return null;

        DateTime today = currentServerDate;

        // main 在这里判「本机没有账号存档」然后跳过签到。WebGL 上没有这种情形：玩家在 Builda
        // 平台天然有账号（BuildaSDK.Whoami），连签记录就在宿主按玩家隔离的 privateKV 里，
        // 不存在「没地方持久化」。
        //
        // 剩下的唯一风险是缓存没水合就结算——那会被当成「从未签到」重复发一次奖。正常路径下
        // 这不可能发生：PrewarmGate.RunBoot 的 PullSaveTask 水合失败就不会放行主菜单。所以这里
        // 是一条不变式断言，只有绕过开机门禁（比如编辑器里直接 Play 某个场景）才会命中。
        // 注意不能走 SaveCachedWorldDate()：缓存了日期就等于把今天的签到永久烧掉。
        if (!BuildaSaveBackend.IsLoaded)
        {
            Debug.LogError("[CheckInSystem] Save cache not hydrated; boot gate was bypassed. Check-in postponed.");
            task.Success = false;
            task.Result = null;
            if (loadingPage != null) loadingPage.SetDetail("Save data not ready; check-in postponed.");
            yield break;
        }

        SaveCodec.TryDecodeCheckIn(
            BuildaSaveBackend.Get(SaveKeys.CheckIn),
            out DateTime storedLastDate,
            out int storedConsecutive);

        DateTime lastDate = storedLastDate.Date;
        if (lastDate == DateTime.MinValue)
        {
            // 从未签到：当作昨天签过，首次签到拿到第 1 天奖励（和旧的远端行为一致）。
            lastDate = today.AddDays(-1);
        }

        if (lastDate == today)
        {
            Debug.Log("Already signed in!");
            SaveCachedWorldDate();
            task.Success = true;
            task.Result = false;
            if (loadingPage != null) loadingPage.SetDetail("Already signed in today.");
            yield break;
        }

        if (today.Month != lastDate.Month || today.Year != lastDate.Year)
        {
            consecutiveDays = 0;
        }
        else if ((today - lastDate).Days > 1)
        {
            consecutiveDays = 0;
        }
        else
        {
            consecutiveDays = Mathf.Max(0, storedConsecutive);
        }
        consecutiveDays++;

        // 记录先落盘，发奖在后面：privateKV 的写是先进本地缓存再排队上传，所以这里拿不到
        // 「写失败」的信号，但顺序仍然按 main 的约定来——两步之间崩溃只会少发一次奖励，
        // 不会因为记录没落上而下次重复发。
        BuildaSaveBackend.Set(SaveKeys.CheckIn, SaveCodec.EncodeCheckIn(today, consecutiveDays));

        SaveCachedWorldDate();

        float bonusRate = 1 + consecutiveDays * consecutiveBonus;
        float effectiveRate = BuildTodayRewards(today, bonusRate);
        for (int i = 0; i < typeCount; i++)
        {
            RewardingSystem.GainReward(activeRewardNames[i], bonusCount[i]);
        }

        hasRewardToShow = true;
        task.Success = true;
        task.Result = true;
        if (loadingPage != null) loadingPage.SetDetail("Check-in saved.");
        Debug.Log($"Check in successful for {consecutiveDays} day(s)! You have gained {effectiveRate} reward bonus.");
    }

    /// <summary>
    /// 结算本次签到实际发放的奖励种类和数量。
    /// <para>
    /// 传进来的日期落在周年庆窗口内（<see cref="FirstAnniversarySchedule.IsWithinWindow"/>）时做两件事：
    /// 把 XP 换成周年庆抽奖券，并给所有奖励再叠一层双倍。两层倍率先相乘、最后只取整一次，因为
    /// <see cref="CalculateRewardAmount"/> 是向下取整——分两步取整会吃掉小数部分，比如连签 15 天
    /// （倍率 1.5）的 1 张券，乘完再取整是 3 张，先取整就只剩 2 张。
    /// </para>
    /// <para>
    /// 判定用的是已验证的服务器日期，不是本地时钟：调用点在本地结算任务里，此时 currentServerDate
    /// 必然已经拿到（任务开头就会因为它是 MinValue 而失败退出）。
    /// </para>
    /// </summary>
    private float BuildTodayRewards(DateTime date, float bonusRate)
    {
        bool anniversary = FirstAnniversarySchedule.IsWithinWindow(date);
        float rate = anniversary ? bonusRate * AnniversaryBonusMultiplier : bonusRate;
        for (int i = 0; i < typeCount; i++)
        {
            bool swapToTicket = anniversary && rewardNames[i] == RewardName.XP;
            activeRewardNames[i] = swapToTicket ? RewardName.Anniversary_Ticket : rewardNames[i];
            bonusCount[i] = CalculateRewardAmount(swapToTicket ? AnniversaryTicketBase : rewardCount[i], rate);
        }
        return rate;
    }

    private int CalculateRewardAmount(int origin, float bonus) => Mathf.FloorToInt(origin * bonus);

    private static string GetUtc8TodayToken() => ToDateToken(DateTime.UtcNow.Add(Utc8Offset));

    /// <summary>
    /// 日期令牌的唯一写法。固定 InvariantCulture：<see cref="GetVerifiedToday"/> 用
    /// <c>TryParseExact</c> 配 InvariantCulture 读回来，而 "yyyy" 在非公历日历的区域下会输出
    /// 佛历之类的年份，两边不一致就再也对不上。
    /// </summary>
    private static string ToDateToken(DateTime date) =>
        date.Date.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture);

    /// <summary>
    /// 读取宿主记录的「最后一次被平台时钟确认的日期」，从未确认过时返回空串。
    /// <para>
    /// 住在 privateKV 而不是 PlayerPrefs：WebGL 上的 PlayerPrefs 就是浏览器存储，玩家清一次
    /// 站点数据就能把它抹掉，改一次就能把它写成任意一天。privateKV 只有宿主能写，玩家碰不到。
    /// </para>
    /// <para>
    /// 缓存没水合时同样返回空串，而不是去读设备时钟——没水合就等于「这台机器现在证明不了今天
    /// 是哪天」，和从未确认过是同一种状态。这里绕开 <see cref="BuildaSaveBackend.Get"/> 的
    /// 未水合报错，是因为标题画面的日期门禁本来就会在开机拉取完成之前问一次。
    /// </para>
    /// </summary>
    private static string ReadCachedWorldDateToken()
    {
        if (!BuildaSaveBackend.IsLoaded) return string.Empty;
        return SaveCodec.DecodeWorldDate(BuildaSaveBackend.Get(SaveKeys.WorldDate)) ?? string.Empty;
    }

    /// <summary>
    /// 每日次数限制用的「今天」。没有可信日期时返回空串，调用方必须把它当成「今天是哪天未知」
    /// 而不是「今天」——旧实现在这里退回设备时钟，于是清掉站点数据就能把当日次数刷回零。
    /// </summary>
    public static string GetCachedWorldDateToken() => ReadCachedWorldDateToken();

    /// <summary>
    /// 记下平台时钟确认的日期。<see cref="currentServerDate"/> 未确立时什么都不写：这份记录现在
    /// 是反作弊凭据，往里塞一个设备时钟的日期等于把玩家能改的值洗成可信值。
    /// </summary>
    private void SaveCachedWorldDate()
    {
        if (currentServerDate == DateTime.MinValue)
        {
            Debug.LogWarning("[CheckInSystem] No verified date; world-date record left untouched.");
            return;
        }
        BuildaSaveBackend.Set(
            SaveKeys.WorldDate,
            SaveCodec.EncodeWorldDate(ToDateToken(currentServerDate)));

        // 旧的 PlayerPrefs 值不迁移、只清除：它是玩家可写的，搬过来等于把漏洞一起搬过来。
        if (PlayerPrefs.HasKey(LegacyLastWorldDateCacheKey))
        {
            PlayerPrefs.DeleteKey(LegacyLastWorldDateCacheKey);
            PlayerPrefs.Save();
        }
    }

    public void CloseBtnEvent() { SwitchAnimation(); UpdateCurrency(); }
    private void SwitchAnimation() => GetComponent<Animator>().SetBool("state", true);
    public void PlayGetSound() => PlatformAudio.PlaySfx(GetComponent<AudioSource>());
    public void Close() => Destroy(gameObject);

    public void SetRewardDisplay()
    {
        for(int i = 0; i < typeCount; i++)
        {
            // 图标走 RewardNumMap 而不是枚举的序号：RewardName 中间有成段注释掉的值，
            // 序号和图片编号只是在前 13 项里凑巧一致，Anniversary_Ticket 会算成 61（别的道具）
            // 而正确编号是 75。
            Transform R = RewardDispalyer.GetChild(i);
            if (R != null)
            {
                R.GetChild(0).GetComponent<Image>().sprite = StorageImageHelper.GetItemImage(activeRewardNames[i]);
                R.GetChild(2).GetComponent<TMP_Text>().text = $"x  {bonusCount[i]}";
            }
        }
        string statement = string.Empty;
        LocalizationHelper.GetLocalizedText(UXPref.Localized_UI,"id:checkin",
            localizedText => checkinStatement.text = string.Format(localizedText,consecutiveDays) ?? "id:checkin");
    }
    public void UpdateCurrency()
    {
        var frameUI = FindObjectOfType<FrameUIDisplayer>();
        if (frameUI != null) frameUI.RefreshCurrencyAmounts();
    }
}