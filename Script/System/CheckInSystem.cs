using System;
using System.Collections;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class CheckInSystem : MonoBehaviour
{
    [SerializeField] private Transform RewardDispalyer;
    [SerializeField] private TMP_Text checkinStatement;
    [SerializeField] private Animator rewardAnimator;

    private DateTime lastCheckInDate;
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
    public const string LastWorldDateCacheKey = "CHECKIN_LAST_WORLD_DATE";
    private static readonly TimeSpan Utc8Offset = TimeSpan.FromHours(8);

    private void Start()
    {
        if (ShouldSkipByLocalDate())
        {
            Close();
            return;
        }
        string pid = PlayerPrefs.GetString(UXPref.UserPrefKey, "KIZUMIII");
        SupabaseSaveRemote.Initialize(UXPref.SupabaseUrl, UXPref.SupabaseKey, pid);

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
            new LoadingTask("Pulling check-in data...", ExecuteFetchCheckInDataTask),
            new LoadingTask("Uploading check-in data...", ExecuteUploadCheckInDataTask)
        };
        loadingPage.Initialize(tasks, OnLoadingCompleted);
    }

    private void OnLoadingCompleted(bool success)
    {
        if (loadingPage != null) Destroy(loadingPage.gameObject);

        if (!success)
        {
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
        if (!EnsureNetworkAndRemoteReady(task))
        {
            yield break;
        }
        if (loadingPage != null) loadingPage.SetDetail("Fetching time utc+8...");

        DateTime? serverDate = null;
        yield return WorldTimeService.FetchUtc8DateTime(
            value => serverDate = value,
            detail =>
            {
                if (loadingPage != null) loadingPage.SetDetail(detail);
            });

        if (serverDate == null)
        {
            task.Success = false;
            task.Result = null;
            if (loadingPage != null) loadingPage.SetDetail("Connection Failed: unable to get Beijing time.");
            yield break;
        }

        currentServerDate = serverDate.Value.Date;
        task.Success = true;
        task.Result = currentServerDate;
        if (loadingPage != null) loadingPage.SetDetail($"Time OK: {currentServerDate:yyyy-MM-dd}");
    }

    private bool ShouldSkipByLocalDate()
    {
        string cachedDate = PlayerPrefs.GetString(LastWorldDateCacheKey, string.Empty);
        string today = GetUtc8TodayToken();
        if (string.IsNullOrEmpty(cachedDate))
        {
            return false;
        }
        return cachedDate == today;
    }

    private IEnumerator ExecuteFetchCheckInDataTask(LoadingTask task)
    {
        if (!EnsureNetworkAndRemoteReady(task))
        {
            yield break;
        }
        if (loadingPage != null) loadingPage.SetDetail("Pulling check-in data...");

        DateTime? remoteLastDate = null;
        int remoteConsecutive = 0;
        yield return SupabaseSaveRemote.GetUserCheckInData((lastDate, consecutive) =>
        {
            remoteLastDate = lastDate;
            remoteConsecutive = consecutive;
        });

        lastCheckInDate = remoteLastDate?.Date ?? DateTime.MinValue;
        consecutiveDays = remoteConsecutive;
        hasRewardToShow = false;
        task.Success = true;
        task.Result = new Vector2Int(lastCheckInDate == DateTime.MinValue ? -1 : lastCheckInDate.DayOfYear, consecutiveDays);
        if (loadingPage != null) loadingPage.SetDetail("Check-in data pulled.");
    }

    private IEnumerator ExecuteUploadCheckInDataTask(LoadingTask task)
    {
        if (!EnsureNetworkAndRemoteReady(task) || currentServerDate == DateTime.MinValue)
        {
            task.Success = false;
            task.Result = null;
            if (loadingPage != null) loadingPage.SetDetail("Connection Failed: invalid upload state.");
            yield break;
        }
        if (loadingPage != null) loadingPage.SetDetail("Uploading check-in update...");

        DateTime today = currentServerDate;
        if (lastCheckInDate == today)
        {
            Debug.Log("Already signed in!");
            SaveCachedWorldDate();
            task.Success = true;
            task.Result = false;
            if (loadingPage != null) loadingPage.SetDetail("Already signed in today.");
            yield break;
        }

        if (lastCheckInDate == DateTime.MinValue)
        {
            lastCheckInDate = today.AddDays(-1);
        }

        if (today.Month != lastCheckInDate.Month || today.Year != lastCheckInDate.Year)
        {
            consecutiveDays = 0;
        }
        else if ((today - lastCheckInDate).Days > 1)
        {
            consecutiveDays = 0;
        }
        consecutiveDays++;

        float bonusRate = 1 + consecutiveDays * consecutiveBonus;
        for (int i = 0; i < typeCount; i++) bonusCount[i] = CalculateRewardAmount(rewardCount[i], bonusRate);
        for (int i = 0; i < typeCount; i++) RewardingSystem.GainReward(rewardNames[i], bonusCount[i]);

        lastCheckInDate = today;
        bool uploadOk = false;
        yield return SupabaseSaveRemote.UpdateUserCheckInData(today, consecutiveDays, ok => uploadOk = ok);
        if (!uploadOk)
        {
            task.Success = false;
            task.Result = null;
            if (loadingPage != null) loadingPage.SetDetail("Connection Failed: upload check-in data failed.");
            yield break;
        }

        hasRewardToShow = true;
        SaveCachedWorldDate();
        task.Success = true;
        task.Result = true;
        if (loadingPage != null) loadingPage.SetDetail("Check-in upload success.");
        Debug.Log($"Check in successful for {consecutiveDays} day(s)! You have gained {bonusRate} reward bonus.");
    }

    private int CalculateRewardAmount(int origin, float bonus) => Mathf.FloorToInt(origin * bonus);

    private static string GetUtc8TodayToken() => DateTime.UtcNow.Add(Utc8Offset).Date.ToString("yyyy-MM-dd");

    public static string GetCachedWorldDateToken()
    {
        string cachedDate = PlayerPrefs.GetString(LastWorldDateCacheKey, string.Empty);
        return string.IsNullOrEmpty(cachedDate) ? GetUtc8TodayToken() : cachedDate;
    }

    private void SaveCachedWorldDate()
    {
        DateTime date = currentServerDate != DateTime.MinValue ? currentServerDate.Date : DateTime.UtcNow.Add(Utc8Offset).Date;
        PlayerPrefs.SetString(LastWorldDateCacheKey, date.ToString("yyyy-MM-dd"));
        PlayerPrefs.Save();
    }

    private bool EnsureNetworkAndRemoteReady(LoadingTask task)
    {
        if (Application.internetReachability == NetworkReachability.NotReachable)
        {
            task.Success = false;
            task.Result = null;
            if (loadingPage != null) loadingPage.NotifyFailure("No network connection.");
            return false;
        }
        if (!SupabaseSaveRemote.IsReady())
        {
            task.Success = false;
            task.Result = null;
            if (loadingPage != null) loadingPage.NotifyFailure("Supabase not ready.");
            return false;
        }
        return true;
    }

    public void CloseBtnEvent() { SwitchAnimation(); UpdateCurrency(); }
    private void SwitchAnimation() => GetComponent<Animator>().SetBool("state", true);
    public void PlayGetSound() => PlatformAudio.PlaySfx(GetComponent<AudioSource>());
    public void Close() => Destroy(gameObject);

    public void SetRewardDisplay()
    {
        for(int i = 0; i < typeCount; i++)
        {
            int index = Array.IndexOf(Enum.GetValues(typeof(RewardName)), rewardNames[i]);
            Transform R = RewardDispalyer.GetChild(i);
            if (R != null)
            {
                R.GetChild(0).GetComponent<Image>().sprite = Resources.Load<Sprite>($"Reward/{index}");
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