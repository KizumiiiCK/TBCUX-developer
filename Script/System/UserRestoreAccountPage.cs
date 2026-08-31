using System;
using System.Collections;
using System.Collections.Generic;
using System.Globalization;
using System.Text;
using TMPro;
using UnityEngine;
using UnityEngine.Networking;
using UnityEngine.UI;

public class UserRestoreAccountPage : MonoBehaviour
{
    private const string UserTable = "user_accounts";
    private const string BontiquePurchaseTable = "bontique_purchases";
    private const string LoadingPagePath = "UI/Pages/loading";
    private const string LoginCheckPagePath = "UI/Pages/user/UserLoginCheckPage";

    [Header("Prepare Page")]
    [SerializeField] private GameObject preparePage;
    [SerializeField] private TMP_InputField userIdInput;
    [SerializeField] private TMP_InputField transferCodeInput;
    [SerializeField] private TMP_Text infoText;
    [SerializeField] private Button cancelButton;
    [SerializeField] private Button restoreButton;

    [Header("Confirm Page")]
    [SerializeField] private GameObject confirmPage;
    [SerializeField] private TMP_Text confirmUserNameText;
    [SerializeField] private Button confirmNoButton;
    [SerializeField] private Button confirmYesButton;

    [Header("Success Page")]
    [SerializeField] private GameObject successPage;
    [SerializeField] private Button successOkButton;

    private LoadingPage loadingPage;
    private bool isWorking;

    private string pendingPid;
    private string pendingTransferCode;
    private string pendingUserName;
    private string pendingDeviceCode;
    private DateTime utc8Now;

    private CachedRestoreData cache;

    private void Awake()
    {
        if (cancelButton != null) cancelButton.onClick.AddListener(OnCancelClicked);
        if (restoreButton != null) restoreButton.onClick.AddListener(OnRestoreClicked);
        if (confirmNoButton != null) confirmNoButton.onClick.AddListener(OnConfirmNoClicked);
        if (confirmYesButton != null) confirmYesButton.onClick.AddListener(OnConfirmYesClicked);
        if (successOkButton != null) successOkButton.onClick.AddListener(OnSuccessOkClicked);

        if (preparePage != null) preparePage.SetActive(true);
        if (confirmPage != null) confirmPage.SetActive(false);
        if (successPage != null) successPage.SetActive(false);
    }

    private void OnCancelClicked()
    {
        if (isWorking) return;
        ReturnToLoginCheckPage();
    }

    private void OnRestoreClicked()
    {
        if (isWorking) return;
        if (!UXPref.HasSupabaseConfig)
        {
            SetInfo(SupabaseSettings.MissingConfigHint);
            return;
        }

        string pid = userIdInput != null ? userIdInput.text.Trim() : string.Empty;
        string code = transferCodeInput != null ? transferCodeInput.text.Trim() : string.Empty;

        if (!ValidatePid(pid, out string pidError))
        {
            SetInfo(pidError);
            return;
        }
        if (!ValidateTransferCode(code, out string codeError))
        {
            SetInfo(codeError);
            return;
        }

        pendingPid = pid;
        pendingTransferCode = code;
        StartCoroutine(ValidateAccountFlow());
    }

    private void OnConfirmNoClicked()
    {
        if (isWorking) return;
        if (confirmPage != null) confirmPage.SetActive(false);
        if (preparePage != null) preparePage.SetActive(true);
        SetInfo("Please re-check your user ID and transfer code.");
    }

    private void OnConfirmYesClicked()
    {
        if (isWorking) return;
        StartCoroutine(RestoreFlow());
    }

    private void OnSuccessOkClicked()
    {
        if (isWorking) return;
        ReturnToLoginCheckPage();
    }

    private IEnumerator ValidateAccountFlow()
    {
        isWorking = true;
        SetButtonsInteractable(false);
        SetInfo("Validating account...");

        bool done = false;
        bool ok = false;
        StartLoading(
            new List<LoadingTask>
            {
                new LoadingTask("Checking account in user_accounts...", ExecuteValidateAccountTask)
            },
            success =>
            {
                ok = success;
                done = true;
            });

        while (!done) yield return null;
        CleanupLoading();

        isWorking = false;
        SetButtonsInteractable(true);
        if (!ok) yield break;

        if (preparePage != null) preparePage.SetActive(false);
        if (confirmPage != null) confirmPage.SetActive(true);
        if (confirmUserNameText != null) confirmUserNameText.text = pendingUserName ?? string.Empty;
    }

    private IEnumerator RestoreFlow()
    {
        isWorking = true;
        SetButtonsInteractable(false);
        SetInfo("Restoring account data...");
        pendingDeviceCode = UserInfoLocalStore.GetDeviceCode();
        cache = new CachedRestoreData();

        if (confirmPage != null) confirmPage.SetActive(false);

        bool done = false;
        bool success = false;
        StartLoading(
            new List<LoadingTask>
            {
                new LoadingTask("Fetching world time (UTC+8)...", ExecuteFetchUtc8Task),
                new LoadingTask("Downloading character_upgrades...", ExecuteDownloadCharacterUpgradesTask),
                new LoadingTask("Downloading game_progress_sections...", ExecuteDownloadGameProgressTask),
                new LoadingTask("Downloading reward_inventory...", ExecuteDownloadRewardInventoryTask),
                new LoadingTask("Downloading team_selections...", ExecuteDownloadTeamSelectionsTask),
                new LoadingTask("Downloading enemy_meet...", ExecuteDownloadEnemyMeetTask),
                new LoadingTask("Downloading boutique purchases...", ExecuteDownloadBontiquePurchasesTask),
                new LoadingTask("Downloading user profile...", ExecuteFetchUserProfileForLocalTask),
                new LoadingTask("Applying downloaded data to local save...", ExecuteApplyLocalCacheTask),
                new LoadingTask("Writing local user profile...", ExecuteWriteLocalUserTask),
                new LoadingTask("Updating user_accounts device_code and last_update...", ExecutePatchUserTask),
                new LoadingTask("Clearing remote gameplay tables...", ExecuteCleanupRemoteTask),
            },
            ok =>
            {
                success = ok;
                done = true;
            });

        while (!done) yield return null;
        CleanupLoading();

        isWorking = false;
        SetButtonsInteractable(true);

        if (!success)
        {
            if (preparePage != null) preparePage.SetActive(true);
            SetInfo("Restore failed or was canceled. Local data is unchanged.");
            yield break;
        }

        if (successPage != null) successPage.SetActive(true);
    }

    private IEnumerator ExecuteValidateAccountTask(LoadingTask task)
    {
        if (loadingPage != null) loadingPage.SetDetail("Querying user_accounts by pid...");
        if (Application.internetReachability == NetworkReachability.NotReachable)
        {
            task.Success = false;
            if (loadingPage != null) loadingPage.NotifyFailure("No network connection.");
            SetInfo("No network connection.");
            yield break;
        }

        string encodedTransferCode = UnityWebRequest.EscapeURL(pendingTransferCode ?? string.Empty);
        string url = $"{UXPref.SupabaseUrl}/rest/v1/{UserTable}?pid=eq.{UnityWebRequest.EscapeURL(pendingPid)}&transfer_code=eq.{encodedTransferCode}&select=pid,user_name,transfer_code,device_code&limit=1";
        bool reqDone = false;
        bool reqOk = false;
        string body = "[]";

        yield return GetRequest(url, ok =>
        {
            reqOk = ok.success;
            body = ok.body;
            reqDone = true;
        });
        while (!reqDone) yield return null;

        if (!reqOk)
        {
            task.Success = false;
            if (loadingPage != null) loadingPage.NotifyFailure("Failed to query user_accounts.");
            SetInfo("Failed to query account.");
            yield break;
        }

        List<object> rows = ParseJsonArray(body);
        if (rows.Count == 0)
        {
            task.Success = false;
            SetInfo("No account found for this user ID and transfer code.");
            if (loadingPage != null) loadingPage.NotifyFailure("No account found.");
            yield break;
        }

        Dictionary<string, object> row = rows[0] as Dictionary<string, object>;
        if (row == null)
        {
            task.Success = false;
            SetInfo("Invalid account response.");
            if (loadingPage != null) loadingPage.NotifyFailure("Invalid account response.");
            yield break;
        }

        string remoteTransferCode = GetString(row, "transfer_code");
        string remoteDeviceCode = GetString(row, "device_code");
        string remoteUserName = GetString(row, "user_name");
        if (string.IsNullOrWhiteSpace(remoteTransferCode) || !string.IsNullOrWhiteSpace(remoteDeviceCode))
        {
            task.Success = false;
            SetInfo("This account cannot be inherited.");
            if (loadingPage != null) loadingPage.NotifyFailure("Account is still active on a local device.");
            yield break;
        }
        if (!string.Equals(remoteTransferCode ?? string.Empty, pendingTransferCode, StringComparison.Ordinal))
        {
            task.Success = false;
            SetInfo("Transfer code is incorrect.");
            if (loadingPage != null) loadingPage.NotifyFailure("Transfer code mismatch.");
            yield break;
        }

        pendingUserName = string.IsNullOrWhiteSpace(remoteUserName) ? string.Empty : remoteUserName;
        task.Success = true;
    }

    private IEnumerator ExecuteFetchUtc8Task(LoadingTask task)
    {
        DateTime? value = null;
        yield return WorldTimeService.FetchUtc8DateTime(
            result => value = result,
            detail =>
            {
                if (loadingPage != null) loadingPage.SetDetail(detail);
            });
        if (!value.HasValue)
        {
            task.Success = false;
            if (loadingPage != null) loadingPage.NotifyFailure("Failed to fetch world time.");
            yield break;
        }
        utc8Now = value.Value;
        task.Success = true;
        if (loadingPage != null) loadingPage.SetDetail($"World time: {utc8Now:yyyy-MM-dd HH:mm:ss}");
    }

    private IEnumerator ExecuteDownloadCharacterUpgradesTask(LoadingTask task)
    {
        string url = $"{UXPref.SupabaseUrl}/rest/v1/character_upgrades?pid=eq.{UnityWebRequest.EscapeURL(pendingPid)}";
        bool reqDone = false;
        bool reqOk = false;
        string body = "[]";

        yield return GetRequest(url, r =>
        {
            reqOk = r.success;
            body = r.body;
            reqDone = true;
        });
        while (!reqDone) yield return null;

        if (!reqOk)
        {
            task.Success = false;
            if (loadingPage != null) loadingPage.NotifyFailure("Failed to download character_upgrades.");
            yield break;
        }

        var dict = new Dictionary<string, CharacterUpgradeSave.UpgradeDetails>();
        List<object> rows = ParseJsonArray(body);
        for (int i = 0; i < rows.Count; i++)
        {
            Dictionary<string, object> row = rows[i] as Dictionary<string, object>;
            if (row == null) continue;

            string id = GetString(row, "character_id");
            if (string.IsNullOrWhiteSpace(id)) continue;

            var ud = new CharacterUpgradeSave.UpgradeDetails
            {
                tire_unlocked = ToBoolArray(GetValue(row, "tire_unlocked"), 4),
                talent_unlocked = GetBool(row, "talent_unlocked", false),
                upgraded_level = GetInt(row, "upgraded_level", 0),
                plus_level = GetInt(row, "plus_level", 0),
                proficiency = new CharacterProficiency
                {
                    level = GetInt(row, "proficiency_level", 0)
                }
            };
            ud.proficiency.LoadFromLongProgressArray(ToLongArray(GetValue(row, "proficiency_stack"), 4));
            ud.proficiency.UpdateLevel();
            dict[id] = ud;
        }

        cache.characterUpgrades = dict;
        task.Success = true;
    }

    private IEnumerator ExecuteDownloadGameProgressTask(LoadingTask task)
    {
        string url = $"{UXPref.SupabaseUrl}/rest/v1/game_progress_sections?pid=eq.{UnityWebRequest.EscapeURL(pendingPid)}";
        bool reqDone = false;
        bool reqOk = false;
        string body = "[]";

        yield return GetRequest(url, r =>
        {
            reqOk = r.success;
            body = r.body;
            reqDone = true;
        });
        while (!reqDone) yield return null;

        if (!reqOk)
        {
            task.Success = false;
            if (loadingPage != null) loadingPage.NotifyFailure("Failed to download game_progress_sections.");
            yield break;
        }

        List<object> rows = ParseJsonArray(body);
        var chapterMap = new Dictionary<string, List<GameProgressSave.SectionClearList>>();
        for (int i = 0; i < rows.Count; i++)
        {
            Dictionary<string, object> row = rows[i] as Dictionary<string, object>;
            if (row == null) continue;

            string chapter = GetString(row, "chapter_name");
            string section = GetString(row, "section_name");
            if (string.IsNullOrWhiteSpace(chapter) || string.IsNullOrWhiteSpace(section)) continue;

            var sec = new GameProgressSave.SectionClearList
            {
                SectionName = section,
                cleared = GetBool(row, "cleared", false),
                reward_gained = ToBoolArray(GetValue(row, "reward_gained"), 0),
                clear_times = ToInt2D(GetValue(row, "clear_times")),
                level_score = ToInt2D(GetValue(row, "level_score")),
                cleared_teams = ToString2D(GetValue(row, "cleared_teams")),
                cleared_cannon = ToIntArray(GetValue(row, "cleared_cannon"), 0)
            };

            if (!chapterMap.TryGetValue(chapter, out List<GameProgressSave.SectionClearList> list))
            {
                list = new List<GameProgressSave.SectionClearList>();
                chapterMap[chapter] = list;
            }
            list.Add(sec);
        }

        var chapterList = new List<GameProgressSave.ChapterClearList>();
        foreach (var kv in chapterMap)
        {
            chapterList.Add(new GameProgressSave.ChapterClearList
            {
                ChapterName = kv.Key,
                SectionList = kv.Value.ToArray()
            });
        }

        cache.gameProgress = chapterList.ToArray();
        task.Success = true;
    }

    private IEnumerator ExecuteDownloadRewardInventoryTask(LoadingTask task)
    {
        string url = $"{UXPref.SupabaseUrl}/rest/v1/reward_inventory?pid=eq.{UnityWebRequest.EscapeURL(pendingPid)}";
        bool reqDone = false;
        bool reqOk = false;
        string body = "[]";

        yield return GetRequest(url, r =>
        {
            reqOk = r.success;
            body = r.body;
            reqDone = true;
        });
        while (!reqDone) yield return null;

        if (!reqOk)
        {
            task.Success = false;
            if (loadingPage != null) loadingPage.NotifyFailure("Failed to download reward_inventory.");
            yield break;
        }

        int expected = RewardingSystem.ExpectedInventoryLength;
        int[] amounts = new int[expected];
        List<object> rows = ParseJsonArray(body);

        if (rows.Count > 0)
        {
            Dictionary<string, object> first = rows[0] as Dictionary<string, object>;
            object amountsObj = first != null ? GetValue(first, "amounts") : null;
            if (amountsObj != null)
            {
                int[] remoteAmounts = ToIntArray(amountsObj, 0);
                for (int i = 0; i < Mathf.Min(expected, remoteAmounts.Length); i++)
                    amounts[i] = Mathf.Max(0, remoteAmounts[i]);
            }
            else
            {
                // Fallback for old row-by-row schema.
                for (int i = 0; i < rows.Count; i++)
                {
                    Dictionary<string, object> row = rows[i] as Dictionary<string, object>;
                    if (row == null) continue;
                    int rewardId = GetInt(row, "reward_id", -1);
                    int amount = GetInt(row, "amount", 0);
                    if (rewardId >= 0 && rewardId < amounts.Length)
                        amounts[rewardId] = Mathf.Max(0, amount);
                }
            }
        }

        cache.rewardInventory = amounts;
        task.Success = true;
    }

    private IEnumerator ExecuteDownloadTeamSelectionsTask(LoadingTask task)
    {
        string url = $"{UXPref.SupabaseUrl}/rest/v1/team_selections?pid=eq.{UnityWebRequest.EscapeURL(pendingPid)}";
        bool reqDone = false;
        bool reqOk = false;
        string body = "[]";

        yield return GetRequest(url, r =>
        {
            reqOk = r.success;
            body = r.body;
            reqDone = true;
        });
        while (!reqDone) yield return null;

        if (!reqOk)
        {
            task.Success = false;
            if (loadingPage != null) loadingPage.NotifyFailure("Failed to download team_selections.");
            yield break;
        }

        string[,] selections = new string[SelectionsSave.TeamNum, SelectionsSave.SIZE];
        string[] teamNames = new string[SelectionsSave.TeamNum];
        for (int i = 0; i < teamNames.Length; i++) teamNames[i] = $"Team {i + 1}";

        List<object> rows = ParseJsonArray(body);
        for (int i = 0; i < rows.Count; i++)
        {
            Dictionary<string, object> row = rows[i] as Dictionary<string, object>;
            if (row == null) continue;
            int teamIndex = GetInt(row, "team_index", -1);
            if (teamIndex < 0 || teamIndex >= SelectionsSave.TeamNum) continue;

            string[] slots = ToStringArray(GetValue(row, "slots"), SelectionsSave.SIZE);
            for (int j = 0; j < SelectionsSave.SIZE; j++)
                selections[teamIndex, j] = slots[j] ?? string.Empty;

            string teamName = GetString(row, "team_name");
            if (!string.IsNullOrWhiteSpace(teamName))
                teamNames[teamIndex] = TeamNameSave.NormalizeTeamName(teamIndex, teamName);
        }

        cache.teamSelections = selections;
        cache.teamNames = teamNames;
        task.Success = true;
    }

    private IEnumerator ExecuteDownloadEnemyMeetTask(LoadingTask task)
    {
        string url = $"{UXPref.SupabaseUrl}/rest/v1/enemy_meet?pid=eq.{UnityWebRequest.EscapeURL(pendingPid)}";
        bool reqDone = false;
        bool reqOk = false;
        string body = "[]";

        yield return GetRequest(url, r =>
        {
            reqOk = r.success;
            body = r.body;
            reqDone = true;
        });
        while (!reqDone) yield return null;

        if (!reqOk)
        {
            task.Success = false;
            if (loadingPage != null) loadingPage.NotifyFailure("Failed to download enemy_meet.");
            yield break;
        }

        bool[] flags = new bool[EnemyMeetSave.SIZE];
        List<object> rows = ParseJsonArray(body);
        if (rows.Count > 0)
        {
            Dictionary<string, object> first = rows[0] as Dictionary<string, object>;
            object flagsObj = first != null ? GetValue(first, "met_flags") : null;
            if (flagsObj != null)
            {
                bool[] remoteFlags = ToBoolArray(flagsObj, 0);
                for (int i = 0; i < Mathf.Min(flags.Length, remoteFlags.Length); i++)
                    flags[i] = remoteFlags[i];
            }
            else
            {
                // Fallback for old row-by-row schema.
                for (int i = 0; i < rows.Count; i++)
                {
                    Dictionary<string, object> row = rows[i] as Dictionary<string, object>;
                    if (row == null) continue;
                    int code = GetInt(row, "enemy_code", -1);
                    bool met = GetBool(row, "met", false);
                    if (code >= 0 && code < flags.Length) flags[code] = met;
                }
            }
        }

        cache.enemyMeet = flags;
        task.Success = true;
    }

    private IEnumerator ExecuteDownloadBontiquePurchasesTask(LoadingTask task)
    {
        string url = $"{UXPref.SupabaseUrl}/rest/v1/{BontiquePurchaseTable}?pid=eq.{UnityWebRequest.EscapeURL(pendingPid)}";
        bool reqDone = false;
        bool reqOk = false;
        string body = "[]";

        yield return GetRequest(url, r =>
        {
            reqOk = r.success;
            body = r.body;
            reqDone = true;
        });
        while (!reqDone) yield return null;

        if (!reqOk)
        {
            task.Success = false;
            if (loadingPage != null) loadingPage.NotifyFailure("Failed to download bontique_purchases.");
            yield break;
        }

        List<object> rows = ParseJsonArray(body);
        var entries = new List<BontiquePurchaseEntry>(rows.Count);
        for (int i = 0; i < rows.Count; i++)
        {
            Dictionary<string, object> row = rows[i] as Dictionary<string, object>;
            if (row == null) continue;

            string bid = GetString(row, "bid");
            if (string.IsNullOrWhiteSpace(bid)) continue;

            entries.Add(new BontiquePurchaseEntry
            {
                bid = bid,
                firstPurchaseDate = GetDateTime(row, "first_purchase_date"),
                purchaseCount = Mathf.Max(0, GetInt(row, "purchase_count", 0))
            });
        }

        cache.bontiquePurchases = entries;
        task.Success = true;
    }

    private IEnumerator ExecuteFetchUserProfileForLocalTask(LoadingTask task)
    {
        string encodedTransferCode = UnityWebRequest.EscapeURL(pendingTransferCode ?? string.Empty);
        string url = $"{UXPref.SupabaseUrl}/rest/v1/{UserTable}?pid=eq.{UnityWebRequest.EscapeURL(pendingPid)}&transfer_code=eq.{encodedTransferCode}&select=pid,user_name,transfer_code,device_code&limit=1";
        bool reqDone = false;
        bool reqOk = false;
        string body = "[]";

        yield return GetRequest(url, r =>
        {
            reqOk = r.success;
            body = r.body;
            reqDone = true;
        });
        while (!reqDone) yield return null;

        if (!reqOk)
        {
            task.Success = false;
            if (loadingPage != null) loadingPage.NotifyFailure("Failed to fetch user profile.");
            yield break;
        }

        List<object> rows = ParseJsonArray(body);
        if (rows.Count == 0)
        {
            task.Success = false;
            if (loadingPage != null) loadingPage.NotifyFailure("User profile not found.");
            yield break;
        }

        Dictionary<string, object> row = rows[0] as Dictionary<string, object>;
        if (row == null)
        {
            task.Success = false;
            if (loadingPage != null) loadingPage.NotifyFailure("Invalid user profile payload.");
            yield break;
        }

        string remoteTransferCode = GetString(row, "transfer_code");
        string remoteDeviceCode = GetString(row, "device_code");
        if (string.IsNullOrWhiteSpace(remoteTransferCode) || !string.IsNullOrWhiteSpace(remoteDeviceCode))
        {
            task.Success = false;
            if (loadingPage != null) loadingPage.NotifyFailure("Account is still active on a local device.");
            yield break;
        }
        if (!string.Equals(remoteTransferCode ?? string.Empty, pendingTransferCode, StringComparison.Ordinal))
        {
            task.Success = false;
            if (loadingPage != null) loadingPage.NotifyFailure("Transfer code no longer matches.");
            yield break;
        }

        pendingUserName = GetString(row, "user_name");
        task.Success = true;
    }

    private IEnumerator ExecuteApplyLocalCacheTask(LoadingTask task)
    {
        try
        {
            GenericSaveSystem.SaveData(cache.characterUpgrades ?? new Dictionary<string, CharacterUpgradeSave.UpgradeDetails>(), CharacterUpgradeSave.filename);
            GenericSaveSystem.SaveData(cache.gameProgress ?? Array.Empty<GameProgressSave.ChapterClearList>(), GameProgressSave.filename);
            GenericSaveSystem.SaveData(cache.rewardInventory ?? new int[RewardingSystem.ExpectedInventoryLength], RewardingSystem.filename);
            GenericSaveSystem.SaveData(cache.teamSelections ?? new string[SelectionsSave.TeamNum, SelectionsSave.SIZE], SelectionsSave.filename);
            GenericSaveSystem.SaveData(cache.teamNames ?? BuildDefaultTeamNames(), TeamNameSave.filename);
            GenericSaveSystem.SaveData(cache.enemyMeet ?? new bool[EnemyMeetSave.SIZE], EnemyMeetSave.filename);
            BontiquePurchaseSave.ReplaceAll(cache.bontiquePurchases);
        }
        catch (Exception e)
        {
            Debug.LogError($"[UserRestoreAccountPage] Failed to apply local cache: {e.Message}");
            task.Success = false;
            if (loadingPage != null) loadingPage.NotifyFailure("Failed to write restored save files.");
            yield break;
        }

        task.Success = true;
        yield break;
    }

    private IEnumerator ExecuteWriteLocalUserTask(LoadingTask task)
    {
        bool ok = UserInfoLocalStore.Save(new UserInfoLocalData
        {
            pid = pendingPid,
            user_name = pendingUserName ?? string.Empty,
            device_code = pendingDeviceCode
        });
        if (!ok)
        {
            task.Success = false;
            if (loadingPage != null) loadingPage.NotifyFailure("Failed to write local user profile.");
            yield break;
        }

        PlayerPrefs.SetString(UXPref.UserPrefKey, pendingPid);
        PlayerPrefs.SetString("USER_NAME", pendingUserName ?? string.Empty);
        PlayerPrefs.Save();
        task.Success = true;
        yield break;
    }

    private IEnumerator ExecutePatchUserTask(LoadingTask task)
    {
        string url = $"{UXPref.SupabaseUrl}/rest/v1/{UserTable}?pid=eq.{UnityWebRequest.EscapeURL(pendingPid)}";
        string payload = "{"
                         + $"\"device_code\":\"{JsonEscape(pendingDeviceCode)}\","
                         + "\"transfer_code\":null,"
                         + $"\"last_update\":\"{utc8Now.ToString("o", CultureInfo.InvariantCulture)}\""
                         + "}";

        bool reqDone = false;
        bool reqOk = false;
        yield return PatchRequest(url, payload, ok =>
        {
            reqOk = ok.success;
            reqDone = true;
        });
        while (!reqDone) yield return null;

        task.Success = reqOk;
        if (!reqOk && loadingPage != null) loadingPage.NotifyFailure("Failed to update user_accounts.");
    }

    private IEnumerator ExecuteCleanupRemoteTask(LoadingTask task)
    {
        string[] tables =
        {
            "character_upgrades",
            "game_progress_sections",
            "reward_inventory",
            "team_selections",
            "enemy_meet",
            BontiquePurchaseTable,
        };

        for (int i = 0; i < tables.Length; i++)
        {
            string table = tables[i];
            if (loadingPage != null) loadingPage.SetDetail($"Clearing remote table: {table}...");
            bool reqDone = false;
            bool reqOk = false;
            string url = $"{UXPref.SupabaseUrl}/rest/v1/{table}?pid=eq.{UnityWebRequest.EscapeURL(pendingPid)}";

            yield return DeleteRequest(url, ok =>
            {
                reqOk = ok.success;
                reqDone = true;
            });
            while (!reqDone) yield return null;

            if (!reqOk)
            {
                task.Success = false;
                if (loadingPage != null) loadingPage.NotifyFailure($"Failed to clear remote table: {table}");
                yield break;
            }
        }

        task.Success = true;
    }

    private IEnumerator GetRequest(string url, Action<RequestResult> onDone)
    {
        using (UnityWebRequest request = UnityWebRequest.Get(url))
        {
            request.timeout = 25;
            request.SetRequestHeader("apikey", UXPref.SupabaseKey);
            request.SetRequestHeader("Authorization", $"Bearer {UXPref.SupabaseKey}");
            yield return request.SendWebRequest();

            bool success = request.result == UnityWebRequest.Result.Success;
            string body = request.downloadHandler != null ? request.downloadHandler.text : string.Empty;
            onDone?.Invoke(new RequestResult(success, body, request.error));
        }
    }

    private IEnumerator PatchRequest(string url, string payload, Action<RequestResult> onDone)
    {
        byte[] bodyRaw = Encoding.UTF8.GetBytes(payload);
        using (UnityWebRequest request = new UnityWebRequest(url, "PATCH"))
        {
            request.uploadHandler = new UploadHandlerRaw(bodyRaw);
            request.downloadHandler = new DownloadHandlerBuffer();
            request.timeout = 25;
            request.SetRequestHeader("Content-Type", "application/json");
            request.SetRequestHeader("apikey", UXPref.SupabaseKey);
            request.SetRequestHeader("Authorization", $"Bearer {UXPref.SupabaseKey}");
            request.SetRequestHeader("Prefer", "return=minimal");
            yield return request.SendWebRequest();

            bool success = request.result == UnityWebRequest.Result.Success;
            string body = request.downloadHandler != null ? request.downloadHandler.text : string.Empty;
            onDone?.Invoke(new RequestResult(success, body, request.error));
        }
    }

    private IEnumerator DeleteRequest(string url, Action<RequestResult> onDone)
    {
        using (UnityWebRequest request = new UnityWebRequest(url, "DELETE"))
        {
            request.downloadHandler = new DownloadHandlerBuffer();
            request.timeout = 25;
            request.SetRequestHeader("apikey", UXPref.SupabaseKey);
            request.SetRequestHeader("Authorization", $"Bearer {UXPref.SupabaseKey}");
            request.SetRequestHeader("Prefer", "return=minimal");
            yield return request.SendWebRequest();

            bool success = request.result == UnityWebRequest.Result.Success;
            string body = request.downloadHandler != null ? request.downloadHandler.text : string.Empty;
            onDone?.Invoke(new RequestResult(success, body, request.error));
        }
    }

    private static List<object> ParseJsonArray(string json)
    {
        try
        {
            object parsed = Json.Deserialize(json);
            if (parsed is List<object> list) return list;
        }
        catch { }
        return new List<object>();
    }

    private static object GetValue(Dictionary<string, object> row, string key)
    {
        if (row == null || string.IsNullOrEmpty(key)) return null;
        row.TryGetValue(key, out object value);
        return value;
    }

    private static string GetString(Dictionary<string, object> row, string key)
    {
        object value = GetValue(row, key);
        return value?.ToString() ?? string.Empty;
    }

    private static int GetInt(Dictionary<string, object> row, string key, int fallback)
    {
        object value = GetValue(row, key);
        if (value == null) return fallback;
        if (value is long l) return (int)l;
        if (value is int i) return i;
        if (value is double d) return (int)d;
        if (int.TryParse(value.ToString(), out int parsed)) return parsed;
        return fallback;
    }

    private static bool GetBool(Dictionary<string, object> row, string key, bool fallback)
    {
        object value = GetValue(row, key);
        if (value == null) return fallback;
        if (value is bool b) return b;
        if (bool.TryParse(value.ToString(), out bool parsed)) return parsed;
        return fallback;
    }

    private static DateTime GetDateTime(Dictionary<string, object> row, string key)
    {
        object value = GetValue(row, key);
        if (value == null) return default(DateTime);
        if (value is DateTime dateTime) return dateTime;
        if (DateTime.TryParse(value.ToString(), CultureInfo.InvariantCulture, DateTimeStyles.RoundtripKind, out DateTime parsed))
            return parsed;
        if (DateTime.TryParse(value.ToString(), out parsed))
            return parsed;
        return default(DateTime);
    }

    private static int[] ToIntArray(object obj, int fallbackLength)
    {
        if (obj is List<object> list)
        {
            int[] arr = new int[list.Count];
            for (int i = 0; i < list.Count; i++)
            {
                object v = list[i];
                if (v is long l) arr[i] = (int)l;
                else if (v is int i32) arr[i] = i32;
                else if (v is double d) arr[i] = (int)d;
                else if (!int.TryParse(v?.ToString() ?? "0", out arr[i])) arr[i] = 0;
            }
            return arr;
        }
        return fallbackLength > 0 ? new int[fallbackLength] : Array.Empty<int>();
    }

    private static long[] ToLongArray(object obj, int fallbackLength)
    {
        if (obj is List<object> list)
        {
            long[] arr = new long[list.Count];
            for (int i = 0; i < list.Count; i++)
            {
                object v = list[i];
                if (v is long l) arr[i] = l;
                else if (v is int i32) arr[i] = i32;
                else if (v is double d) arr[i] = (long)d;
                else if (!long.TryParse(v?.ToString() ?? "0", out arr[i])) arr[i] = 0L;
                if (arr[i] < 0L) arr[i] = 0L;
            }
            return arr;
        }
        return fallbackLength > 0 ? new long[fallbackLength] : Array.Empty<long>();
    }

    private static bool[] ToBoolArray(object obj, int fallbackLength)
    {
        if (obj is List<object> list)
        {
            bool[] arr = new bool[list.Count];
            for (int i = 0; i < list.Count; i++)
            {
                object v = list[i];
                if (v is bool b) arr[i] = b;
                else if (!bool.TryParse(v?.ToString() ?? "false", out arr[i])) arr[i] = false;
            }
            return arr;
        }
        return fallbackLength > 0 ? new bool[fallbackLength] : Array.Empty<bool>();
    }

    private static string[] ToStringArray(object obj, int expectedSize)
    {
        string[] arr = new string[expectedSize];
        if (obj is List<object> list)
        {
            for (int i = 0; i < Mathf.Min(expectedSize, list.Count); i++)
                arr[i] = list[i]?.ToString() ?? string.Empty;
        }
        else
        {
            for (int i = 0; i < expectedSize; i++) arr[i] = string.Empty;
        }
        return arr;
    }

    private static int[,] ToInt2D(object obj)
    {
        if (!(obj is List<object> rows)) return new int[0, 0];
        int rowCount = rows.Count;
        int colCount = rowCount > 0 && rows[0] is List<object> first ? first.Count : 0;
        int[,] data = new int[rowCount, colCount];
        for (int r = 0; r < rowCount; r++)
        {
            List<object> cols = rows[r] as List<object>;
            for (int c = 0; c < colCount; c++)
            {
                object v = cols != null && c < cols.Count ? cols[c] : 0;
                if (v is long l) data[r, c] = (int)l;
                else if (v is int i) data[r, c] = i;
                else if (v is double d) data[r, c] = (int)d;
                else if (!int.TryParse(v?.ToString() ?? "0", out data[r, c])) data[r, c] = 0;
            }
        }
        return data;
    }

    private static string[,] ToString2D(object obj)
    {
        if (!(obj is List<object> rows)) return new string[0, 0];
        int rowCount = rows.Count;
        int colCount = rowCount > 0 && rows[0] is List<object> first ? first.Count : 0;
        string[,] data = new string[rowCount, colCount];
        for (int r = 0; r < rowCount; r++)
        {
            List<object> cols = rows[r] as List<object>;
            for (int c = 0; c < colCount; c++)
                data[r, c] = cols != null && c < cols.Count ? cols[c]?.ToString() ?? string.Empty : string.Empty;
        }
        return data;
    }

    private static bool ValidatePid(string pid, out string error)
    {
        if (string.IsNullOrWhiteSpace(pid))
        {
            error = "User ID cannot be empty.";
            return false;
        }
        if (pid.Length != 8)
        {
            error = "User ID must be exactly 8 characters.";
            return false;
        }
        for (int i = 0; i < pid.Length; i++)
        {
            if (!char.IsLetterOrDigit(pid[i]))
            {
                error = "User ID supports only English letters and digits.";
                return false;
            }
        }
        error = null;
        return true;
    }

    private static bool ValidateTransferCode(string code, out string error)
    {
        return TransferCodeRules.Validate(code, out error);
    }

    private void SetInfo(string message)
    {
        if (infoText != null) infoText.text = message ?? string.Empty;
    }

    private void SetButtonsInteractable(bool interactable)
    {
        if (cancelButton != null) cancelButton.interactable = interactable;
        if (restoreButton != null) restoreButton.interactable = interactable;
        if (confirmNoButton != null) confirmNoButton.interactable = interactable;
        if (confirmYesButton != null) confirmYesButton.interactable = interactable;
        if (successOkButton != null) successOkButton.interactable = interactable;
    }

    private void StartLoading(List<LoadingTask> tasks, Action<bool> onComplete)
    {
        GameObject prefab = Resources.Load<GameObject>(LoadingPagePath);
        if (prefab == null)
        {
            onComplete?.Invoke(false);
            return;
        }

        GameObject obj = Instantiate(prefab);
        loadingPage = obj.GetComponent<LoadingPage>();
        if (loadingPage == null)
        {
            Destroy(obj);
            onComplete?.Invoke(false);
            return;
        }
        loadingPage.Initialize(tasks, onComplete);
    }

    private void CleanupLoading()
    {
        if (loadingPage == null) return;
        Destroy(loadingPage.gameObject);
        loadingPage = null;
    }

    private static string JsonEscape(string s)
    {
        if (string.IsNullOrEmpty(s)) return string.Empty;
        return s.Replace("\\", "\\\\").Replace("\"", "\\\"");
    }

    private static string[] BuildDefaultTeamNames()
    {
        string[] names = new string[SelectionsSave.TeamNum];
        for (int i = 0; i < names.Length; i++) names[i] = $"Team {i + 1}";
        return names;
    }

    private void ReturnToLoginCheckPage()
    {
        GameObject prefab = Resources.Load<GameObject>(LoginCheckPagePath);
        if (prefab != null) Instantiate(prefab);
        Destroy(gameObject);
    }

    private readonly struct RequestResult
    {
        public readonly bool success;
        public readonly string body;
        public readonly string error;

        public RequestResult(bool success, string body, string error)
        {
            this.success = success;
            this.body = body ?? string.Empty;
            this.error = error ?? string.Empty;
        }
    }

    private class CachedRestoreData
    {
        public Dictionary<string, CharacterUpgradeSave.UpgradeDetails> characterUpgrades;
        public GameProgressSave.ChapterClearList[] gameProgress;
        public int[] rewardInventory;
        public string[,] teamSelections;
        public string[] teamNames;
        public bool[] enemyMeet;
        public List<BontiquePurchaseEntry> bontiquePurchases;
    }
}
