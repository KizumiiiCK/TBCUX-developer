using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Runtime.Serialization.Formatters.Binary;
using System.Text;
using UnityEngine;
using UnityEngine.Networking;

public class SupabaseSaveUploader : MonoBehaviour
{
    public event Action<string> OnPidReserved;
    private const string SupabaseUrl = "https://udnacihdrcqwjbnavyau.supabase.co";
    private const string SupabaseKey = "sb_publishable_icguQw1JHoIlGDk8TBxECw_Mj_-ABem";
    private const string PidReserveTable = "player_ids";
    private const string UserTable = "user_accounts";
    private const string UserPrefKey = "USER";
    private const string UserNamePrefKey = "USER_NAME";

    private const string WelcomeUiPath = "UI/user_welcome_page";
    private const string LocalSaveUiPath = "UI/localsave_upload";

    private const int PidGenerateAttempts = 10;
    private const bool TestOnly = true;

    private string pid = "";
    private string userName = "";
    private string transferCode = "";

    private GameObject welcomeUiInstance;
    private GameObject localSaveUiInstance;
    private bool waitingLocalDecision = false;
    private bool localDecisionUpload = false;

    private void Start()
    {
        StartCoroutine(Bootstrap());
    }

    public void StartUpload()
    {
        if (!IsValidPid(pid))
        {
            Debug.LogError($"[SupabaseSaveUploader] pid is invalid: \"{pid}\". Please set a valid 8-char alphanumeric pid.");
            return;
        }
        if (TestOnly)
        {
            Debug.Log("[SupabaseSaveUploader] Test-only mode. Upload skipped.");
            return;
        }
        StartCoroutine(UploadAll());
    }

    /// <summary>
    /// UI callback: create new account with provided user name.
    /// </summary>
    public void OnWelcomeCreateNew(string inputUserName)
    {
        if (string.IsNullOrWhiteSpace(inputUserName))
        {
            Debug.LogError("[SupabaseSaveUploader] userName is empty.");
            return;
        }
        userName = inputUserName;
        DestroyWelcomeUi();
        StartCoroutine(BeginAccountFlow());
    }

    /// <summary>
    /// UI callback: restore account with pid + transfer code.
    /// </summary>
    public void OnWelcomeRestore(string inputPid, string inputTransferCode, string inputUserName)
    {
        if (!IsValidPid(inputPid))
        {
            Debug.LogError($"[SupabaseSaveUploader] pid is invalid: \"{inputPid}\".");
            return;
        }
        pid = inputPid;
        transferCode = inputTransferCode ?? string.Empty;
        userName = inputUserName ?? string.Empty;
        DestroyWelcomeUi();
        StartCoroutine(BeginAccountFlow());
    }

    /// <summary>
    /// UI callback: local save decision.
    /// </summary>
    public void OnLocalSaveDecision(bool uploadLocalSave)
    {
        DestroyLocalSaveUi();
        StartCoroutine(HandleLocalSaveDecision(uploadLocalSave));
    }

    public IEnumerator UploadAll()
    {
        if (!IsValidPid(pid))
        {
            Debug.LogError($"[SupabaseSaveUploader] pid is invalid: \"{pid}\". Upload aborted.");
            yield break;
        }
        if (TestOnly)
        {
            Debug.Log("[SupabaseSaveUploader] Test-only mode. Upload aborted.");
            yield break;
        }
        yield return UpdateUserLastUpdate();
        yield return UploadPlayerPrefs();
        yield return UploadGameProgress();
        yield return UploadRewardInventory();
        yield return UploadCharacterUpgrades();
        yield return UploadTeamSelections();
        yield return UploadEnemyMeet();
    }

    /// <summary>
    /// Reserve a new transfer code for account migration.
    /// </summary>
    public void ReserveTransferCode()
    {
        if (TestOnly)
        {
            Debug.Log("[SupabaseSaveUploader] Test-only mode. ReserveTransferCode skipped.");
            return;
        }
        StartCoroutine(ReserveTransferCodeCoroutine());
    }

    private IEnumerator Bootstrap()
    {
        if (Application.internetReachability == NetworkReachability.NotReachable)
        {
            Debug.LogWarning("[SupabaseSaveUploader] No internet connection.");
            yield break;
        }

        string storedPid = PlayerPrefs.GetString(UserPrefKey, string.Empty);
        string deviceCode = GetDeviceCode();

        if (string.IsNullOrWhiteSpace(storedPid))
        {
            pid = GenerateRandomPid();
            Debug.Log($"Create new account, random id: {pid}, unique device name or code: {deviceCode}");
            InstantiateWelcomeUi();
            yield break;
        }

        pid = storedPid;
        userName = PlayerPrefs.GetString(UserNamePrefKey, string.Empty);
        Debug.Log($"pid: {pid}, user name: {userName}, device code: {deviceCode}");

        if (TestOnly) yield break;

        SupabaseSaveRemote.Initialize(SupabaseUrl, SupabaseKey, pid);
        yield return CheckDeviceMismatch(deviceCode);
        yield return BeginAccountFlow();
    }

    private IEnumerator BeginAccountFlow()
    {
        if (TestOnly)
        {
            Debug.Log("[SupabaseSaveUploader] Test-only mode. No upload.");
            yield break;
        }

        if (!IsValidPid(pid))
        {
            Debug.LogError($"[SupabaseSaveUploader] pid is invalid: \"{pid}\".");
            yield break;
        }

        SupabaseSaveRemote.Initialize(SupabaseUrl, SupabaseKey, pid);
        yield return CheckDeviceMismatch(GetDeviceCode());

        yield return EnsureUserAccount();

        if (DetectLocalSaveExists())
        {
            InstantiateLocalSaveUi();
            waitingLocalDecision = true;
            while (waitingLocalDecision)
                yield return null;
        }
        else
        {
            FinalizeAccount();
        }
    }

    private IEnumerator HandleLocalSaveDecision(bool uploadLocalSave)
    {
        waitingLocalDecision = false;
        localDecisionUpload = uploadLocalSave;

        if (TestOnly)
        {
            Debug.Log("[SupabaseSaveUploader] Test-only mode. Local save decision logged.");
            yield break;
        }

        if (localDecisionUpload)
        {
            yield return UploadLegacyLocalSave();
        }

        DeleteLocalSaves();
        FinalizeAccount();
    }

    private void FinalizeAccount()
    {
        PlayerPrefs.SetString(UserPrefKey, pid);
        if (!string.IsNullOrWhiteSpace(userName))
            PlayerPrefs.SetString(UserNamePrefKey, userName);
        PlayerPrefs.Save();
    }

    private void InstantiateWelcomeUi()
    {
        if (welcomeUiInstance != null) return;
        GameObject prefab = Resources.Load<GameObject>(WelcomeUiPath);
        if (prefab == null)
        {
            Debug.LogError($"[SupabaseSaveUploader] Missing UI prefab at {WelcomeUiPath}");
            return;
        }
        welcomeUiInstance = Instantiate(prefab);
    }

    private void InstantiateLocalSaveUi()
    {
        if (localSaveUiInstance != null) return;
        GameObject prefab = Resources.Load<GameObject>(LocalSaveUiPath);
        if (prefab == null)
        {
            Debug.LogError($"[SupabaseSaveUploader] Missing UI prefab at {LocalSaveUiPath}");
            return;
        }
        localSaveUiInstance = Instantiate(prefab);
    }

    private void DestroyWelcomeUi()
    {
        if (welcomeUiInstance == null) return;
        Destroy(welcomeUiInstance);
        welcomeUiInstance = null;
    }

    private void DestroyLocalSaveUi()
    {
        if (localSaveUiInstance == null) return;
        Destroy(localSaveUiInstance);
        localSaveUiInstance = null;
    }

    private bool DetectLocalSaveExists()
    {
        string dir = Application.persistentDataPath;
        if (!Directory.Exists(dir)) return false;
        string[] files = Directory.GetFiles(dir, $"*{GenericSaveSystem.FirmEnding}");
        return files != null && files.Length > 0;
    }

    private void DeleteLocalSaves()
    {
        string dir = Application.persistentDataPath;
        if (!Directory.Exists(dir)) return;
        string[] files = Directory.GetFiles(dir, $"*{GenericSaveSystem.FirmEnding}");
        foreach (var f in files)
        {
            try { File.Delete(f); }
            catch (Exception e) { Debug.LogError($"[SupabaseSaveUploader] Delete failed: {e.Message}"); }
        }
    }

    private IEnumerator UploadLegacyLocalSave()
    {
        // load local files directly and push to remote
        object gp = LoadLegacy(GameProgressSave.filename);
        if (gp is GameProgressSave.ChapterClearList[] gpData)
            SupabaseSaveRemote.Save(GameProgressSave.filename, gpData);

        object items = LoadLegacy(RewardingSystem.filename);
        if (items is int[] itemsData)
            SupabaseSaveRemote.Save(RewardingSystem.filename, itemsData);

        object cu = LoadLegacy(CharacterUpgradeSave.filename);
        if (cu is Dictionary<string, CharacterUpgradeSave.UpgradeDetails> cuData)
            SupabaseSaveRemote.Save(CharacterUpgradeSave.filename, cuData);

        object teams = LoadLegacy(SelectionsSave.filename);
        if (teams is string[,] teamsData)
            SupabaseSaveRemote.Save(SelectionsSave.filename, teamsData);

        object em = LoadLegacy(EnemyMeetSave.filename);
        if (em is bool[] emData)
            SupabaseSaveRemote.Save(EnemyMeetSave.filename, emData);

        yield return UploadPlayerPrefs();
    }

    private object LoadLegacy(string filename)
    {
        try
        {
            string path = Path.Combine(Application.persistentDataPath, filename + GenericSaveSystem.FirmEnding);
            if (!File.Exists(path)) return null;
            BinaryFormatter formatter = new BinaryFormatter();
            using (FileStream fs = new FileStream(path, FileMode.Open))
            {
                return formatter.Deserialize(fs);
            }
        }
        catch (Exception e)
        {
            Debug.LogError($"[SupabaseSaveUploader] Load legacy failed: {e.Message}");
            return null;
        }
    }

    private IEnumerator UploadPlayerPrefs()
    {
        string json = "{"
            + $"\"pid\":\"{JsonEscape(pid)}\","
            + $"\"chapter_name\":\"{JsonEscape(PlayerPrefs.GetString(UXPref.ChapterName, UXPref.DefaultChapterName))}\","
            + $"\"section_name\":\"{JsonEscape(PlayerPrefs.GetString(UXPref.SectionName))}\","
            + $"\"section_num\":{PlayerPrefs.GetInt(UXPref.SectionNum, 0)},"
            + $"\"difficulty\":{PlayerPrefs.GetInt(UXPref.Difficulty, 0)},"
            + $"\"level_num\":{PlayerPrefs.GetInt(UXPref.LevelNum, 0)},"
            + $"\"direct_mark\":\"{JsonEscape(PlayerPrefs.GetString(UXPref.DirectMark))}\","
            + $"\"lang\":\"{JsonEscape(PlayerPrefs.GetString(UXPref.LANG))}\","
            + $"\"bgnum_cat\":\"{JsonEscape(PlayerPrefs.GetString(UXPref.Localized_BGnum))}\","
            + $"\"bgm_volume\":{PlayerPrefs.GetFloat(UXPref.BGM_PARAM, 1f).ToString(System.Globalization.CultureInfo.InvariantCulture)},"
            + $"\"se_volume\":{PlayerPrefs.GetFloat(UXPref.SE_PARAM, 1f).ToString(System.Globalization.CultureInfo.InvariantCulture)},"
            + $"\"base_cannon_num\":{PlayerPrefs.GetInt(UXPref.BASE_CannonNum, 0)},"
            + $"\"base_decoration_num\":{PlayerPrefs.GetInt(UXPref.BASE_DecorationNum, 0)},"
            + $"\"base_base_num\":{PlayerPrefs.GetInt(UXPref.BASE_BaseNum, 0)},"
            + $"\"login_date\":\"{JsonEscape(PlayerPrefs.GetString(UXPref.Login_Date))}\","
            + $"\"reward_penalty\":{PlayerPrefs.GetInt(UXPref.RewardPenalty, 0)}"
            + "}";

        yield return Upsert("player_prefs", $"[{json}]");
    }

    private IEnumerator UploadGameProgress()
    {
        GameProgressSave.ChapterClearList[] chapters = GenericSaveSystem.LoadData<GameProgressSave.ChapterClearList[]>(GameProgressSave.filename)
                                                   ?? Array.Empty<GameProgressSave.ChapterClearList>();
        var rows = new List<string>();
        foreach (var chapter in chapters)
        {
            if (chapter?.SectionList == null) continue;
            foreach (var sec in chapter.SectionList)
            {
                if (sec == null) continue;
                string row = "{"
                    + $"\"pid\":\"{JsonEscape(pid)}\","
                    + $"\"chapter_name\":\"{JsonEscape(chapter.ChapterName)}\","
                    + $"\"section_name\":\"{JsonEscape(sec.SectionName)}\","
                    + $"\"cleared\":{BoolJson(sec.cleared)},"
                    + $"\"reward_gained\":{BoolArrayJson(sec.reward_gained)},"
                    + $"\"clear_times\":{Int2DJson(sec.clear_times)},"
                    + $"\"level_score\":{Int2DJson(sec.level_score)},"
                    + $"\"cleared_teams\":{String2DJson(sec.cleared_teams)},"
                    + $"\"cleared_cannon\":{IntArrayJson(sec.cleared_cannon)}"
                    + "}";
                rows.Add(row);
            }
        }
        if (rows.Count == 0) yield break;
        yield return Upsert("game_progress_sections", $"[{string.Join(",", rows)}]");
    }

    private IEnumerator UploadRewardInventory()
    {
        var rows = new List<string>();
        foreach (var kv in RewardingSystem.RewardNumMap)
        {
            int amount = RewardingSystem.GetAmount(kv.Key);
            string row = "{"
                + $"\"pid\":\"{JsonEscape(pid)}\","
                + $"\"reward_id\":{kv.Value},"
                + $"\"amount\":{amount}"
                + "}";
            rows.Add(row);
        }
        if (rows.Count == 0) yield break;
        yield return Upsert("reward_inventory", $"[{string.Join(",", rows)}]");
    }

    private IEnumerator UploadCharacterUpgrades()
    {
        var dict = GenericSaveSystem.LoadData<Dictionary<string, CharacterUpgradeSave.UpgradeDetails>>(CharacterUpgradeSave.filename)
                   ?? new Dictionary<string, CharacterUpgradeSave.UpgradeDetails>();
        var rows = new List<string>();
        foreach (var kv in dict)
        {
            var ud = kv.Value ?? new CharacterUpgradeSave.UpgradeDetails();
            string row = "{"
                + $"\"pid\":\"{JsonEscape(pid)}\","
                + $"\"character_id\":\"{JsonEscape(kv.Key)}\","
                + $"\"tire_unlocked\":{BoolArrayJson(ud.tire_unlocked)},"
                + $"\"talent_unlocked\":{BoolJson(ud.talent_unlocked)},"
                + $"\"upgraded_level\":{ud.upgraded_level},"
                + $"\"plus_level\":{ud.plus_level},"
                + $"\"proficiency_level\":{(ud.proficiency != null ? ud.proficiency.level : 0)},"
                + $"\"proficiency_stack\":{IntArrayJson(ud.proficiency != null ? ud.proficiency.pro_stack : new int[4])}"
                + "}";
            rows.Add(row);
        }
        if (rows.Count == 0) yield break;
        yield return Upsert("character_upgrades", $"[{string.Join(",", rows)}]");
    }

    private IEnumerator UploadTeamSelections()
    {
        string[,] data = SelectionsSave.GetAll();
        var rows = new List<string>();
        for (int i = 0; i < SelectionsSave.TeamNum; i++)
        {
            var slots = new string[SelectionsSave.SIZE];
            for (int j = 0; j < SelectionsSave.SIZE; j++)
                slots[j] = data[i, j];
            string row = "{"
                + $"\"pid\":\"{JsonEscape(pid)}\","
                + $"\"team_index\":{i},"
                + $"\"slots\":{StringArrayJson(slots)}"
                + "}";
            rows.Add(row);
        }
        if (rows.Count == 0) yield break;
        yield return Upsert("team_selections", $"[{string.Join(",", rows)}]");
    }

    private IEnumerator UploadEnemyMeet()
    {
        bool[] data = EnemyMeetSave.GetData();
        var rows = new List<string>();
        for (int i = 0; i < data.Length; i++)
        {
            string row = "{"
                + $"\"pid\":\"{JsonEscape(pid)}\","
                + $"\"enemy_code\":{i},"
                + $"\"met\":{BoolJson(data[i])}"
                + "}";
            rows.Add(row);
        }
        if (rows.Count == 0) yield break;
        yield return Upsert("enemy_meet", $"[{string.Join(",", rows)}]");
    }

    private IEnumerator Upsert(string table, string jsonArray)
    {
        string url = $"{SupabaseUrl}/rest/v1/{table}";
        byte[] bodyRaw = Encoding.UTF8.GetBytes(jsonArray);

        using (UnityWebRequest request = new UnityWebRequest(url, "POST"))
        {
            request.uploadHandler = new UploadHandlerRaw(bodyRaw);
            request.downloadHandler = new DownloadHandlerBuffer();
            request.SetRequestHeader("Content-Type", "application/json");
            request.SetRequestHeader("apikey", SupabaseKey);
            request.SetRequestHeader("Authorization", $"Bearer {SupabaseKey}");
            request.SetRequestHeader("Prefer", "resolution=merge-duplicates");

            yield return request.SendWebRequest();
            if (request.result != UnityWebRequest.Result.Success)
            {
                Debug.LogError($"[SupabaseSaveUploader] Upload failed ({table}): {request.error} - {request.downloadHandler.text}");
            }
        }
    }

    private IEnumerator EnsureUserAccount()
    {
        if (!IsValidPid(pid))
        {
            bool created = false;
            yield return CreateNewPidInternal(success => created = success);
            if (!created) yield break;
        }

        if (string.IsNullOrWhiteSpace(userName))
        {
            Debug.LogError("[SupabaseSaveUploader] userName is empty. Please set a user name before creating account.");
            yield break;
        }

        yield return UpsertUser();
    }

    private IEnumerator CreateNewPidInternal(Action<bool> onComplete)
    {
        for (int i = 0; i < PidGenerateAttempts; i++)
        {
            string candidate = GenerateRandomPid();
            bool reserved = false;
            yield return ReservePid(candidate, success => reserved = success);
            if (reserved)
            {
                pid = candidate;
                Debug.Log($"[SupabaseSaveUploader] Reserved new pid: {pid}");
                OnPidReserved?.Invoke(pid);
                onComplete?.Invoke(true);
                yield break;
            }
        }

        Debug.LogError("[SupabaseSaveUploader] Failed to reserve a new pid. Try again.");
        onComplete?.Invoke(false);
    }

    private IEnumerator UpsertUser()
    {
        string deviceCode = GetDeviceCode();
        string json = "{"
            + $"\"pid\":\"{JsonEscape(pid)}\","
            + $"\"user_name\":\"{JsonEscape(userName)}\","
            + $"\"transfer_code\":\"{JsonEscape(transferCode)}\","
            + $"\"device_code\":\"{JsonEscape(deviceCode)}\","
            + $"\"last_update\":\"{JsonEscape(DateTime.UtcNow.ToString("o"))}\""
            + "}";

        yield return Upsert(UserTable, $"[{json}]");
    }

    private IEnumerator UpdateUserLastUpdate()
    {
        if (!IsValidPid(pid)) yield break;

        bool exists = false;
        yield return CheckUserExists(pid, success => exists = success);

        if (!exists)
        {
            if (string.IsNullOrWhiteSpace(userName))
            {
                Debug.LogError("[SupabaseSaveUploader] userName is empty. Cannot create user_accounts row for last_update.");
                yield break;
            }
            yield return UpsertUser();
            yield break;
        }

        yield return PatchUserLastUpdate();
    }

    private IEnumerator ReserveTransferCodeCoroutine()
    {
        if (!IsValidPid(pid))
        {
            Debug.LogError("[SupabaseSaveUploader] pid is invalid. Please create account first.");
            yield break;
        }

        for (int i = 0; i < PidGenerateAttempts; i++)
        {
            string candidate = GenerateRandomPid();
            bool available = false;
            yield return CheckTransferCodeAvailable(candidate, success => available = success);
            if (!available) continue;

            transferCode = candidate;
            yield return UpsertUser();
            Debug.Log($"[SupabaseSaveUploader] Reserved transfer code: {transferCode}");
            yield break;
        }

        Debug.LogError("[SupabaseSaveUploader] Failed to reserve a transfer code. Try again.");
    }

    private IEnumerator CheckTransferCodeAvailable(string candidate, Action<bool> onComplete)
    {
        string url = $"{SupabaseUrl}/rest/v1/{UserTable}?transfer_code=eq.{candidate}&select=pid&limit=1";
        using (UnityWebRequest request = UnityWebRequest.Get(url))
        {
            request.SetRequestHeader("apikey", SupabaseKey);
            request.SetRequestHeader("Authorization", $"Bearer {SupabaseKey}");
            yield return request.SendWebRequest();
            if (request.result != UnityWebRequest.Result.Success)
            {
                Debug.LogError($"[SupabaseSaveUploader] transfer code check failed: {request.error} - {request.downloadHandler.text}");
                onComplete?.Invoke(false);
                yield break;
            }

            string body = request.downloadHandler.text ?? "[]";
            onComplete?.Invoke(!body.Contains("\"pid\""));
        }
    }

    private IEnumerator CheckUserExists(string candidatePid, Action<bool> onComplete)
    {
        string url = $"{SupabaseUrl}/rest/v1/{UserTable}?pid=eq.{candidatePid}&select=pid&limit=1";
        using (UnityWebRequest request = UnityWebRequest.Get(url))
        {
            request.SetRequestHeader("apikey", SupabaseKey);
            request.SetRequestHeader("Authorization", $"Bearer {SupabaseKey}");
            yield return request.SendWebRequest();
            if (request.result != UnityWebRequest.Result.Success)
            {
                Debug.LogError($"[SupabaseSaveUploader] user check failed: {request.error} - {request.downloadHandler.text}");
                onComplete?.Invoke(false);
                yield break;
            }

            string body = request.downloadHandler.text ?? "[]";
            onComplete?.Invoke(body.Contains("\"pid\""));
        }
    }

    private IEnumerator PatchUserLastUpdate()
    {
        string deviceCode = GetDeviceCode();
        string url = $"{SupabaseUrl}/rest/v1/{UserTable}?pid=eq.{pid}";
        string json = "{"
            + $"\"device_code\":\"{JsonEscape(deviceCode)}\","
            + $"\"last_update\":\"{JsonEscape(DateTime.UtcNow.ToString("o"))}\""
            + "}";
        byte[] bodyRaw = Encoding.UTF8.GetBytes(json);

        using (UnityWebRequest request = new UnityWebRequest(url, "PATCH"))
        {
            request.uploadHandler = new UploadHandlerRaw(bodyRaw);
            request.downloadHandler = new DownloadHandlerBuffer();
            request.SetRequestHeader("Content-Type", "application/json");
            request.SetRequestHeader("apikey", SupabaseKey);
            request.SetRequestHeader("Authorization", $"Bearer {SupabaseKey}");
            request.SetRequestHeader("Prefer", "return=representation");

            yield return request.SendWebRequest();
            if (request.result != UnityWebRequest.Result.Success)
            {
                Debug.LogError($"[SupabaseSaveUploader] last_update patch failed: {request.error} - {request.downloadHandler.text}");
            }
        }
    }

    private IEnumerator CreateNewPidCoroutine()
    {
        for (int i = 0; i < PidGenerateAttempts; i++)
        {
            string candidate = GenerateRandomPid();
            bool reserved = false;
            yield return ReservePid(candidate, success => reserved = success);
            if (reserved)
            {
                pid = candidate;
                Debug.Log($"[SupabaseSaveUploader] Reserved new pid: {pid}");
                OnPidReserved?.Invoke(pid);
                yield break;
            }
        }

        Debug.LogError("[SupabaseSaveUploader] Failed to reserve a new pid. Try again.");
    }

    private IEnumerator ReservePid(string candidate, Action<bool> onComplete)
    {
        if (!IsValidPid(candidate))
        {
            onComplete?.Invoke(false);
            yield break;
        }

        // Check existence
        string checkUrl = $"{SupabaseUrl}/rest/v1/{PidReserveTable}?pid=eq.{candidate}&select=pid&limit=1";
        using (UnityWebRequest check = UnityWebRequest.Get(checkUrl))
        {
            check.SetRequestHeader("apikey", SupabaseKey);
            check.SetRequestHeader("Authorization", $"Bearer {SupabaseKey}");
            yield return check.SendWebRequest();
            if (check.result != UnityWebRequest.Result.Success)
            {
                Debug.LogError($"[SupabaseSaveUploader] pid check failed: {check.error} - {check.downloadHandler.text}");
                onComplete?.Invoke(false);
                yield break;
            }

            string body = check.downloadHandler.text ?? "[]";
            if (body.Contains("\"pid\""))
            {
                onComplete?.Invoke(false);
                yield break;
            }
        }

        // Insert reservation
        string insertUrl = $"{SupabaseUrl}/rest/v1/{PidReserveTable}";
        string json = $"{{\"pid\":\"{JsonEscape(candidate)}\"}}";
        byte[] bodyRaw = Encoding.UTF8.GetBytes(json);
        using (UnityWebRequest insert = new UnityWebRequest(insertUrl, "POST"))
        {
            insert.uploadHandler = new UploadHandlerRaw(bodyRaw);
            insert.downloadHandler = new DownloadHandlerBuffer();
            insert.SetRequestHeader("Content-Type", "application/json");
            insert.SetRequestHeader("apikey", SupabaseKey);
            insert.SetRequestHeader("Authorization", $"Bearer {SupabaseKey}");
            insert.SetRequestHeader("Prefer", "return=representation");

            yield return insert.SendWebRequest();
            if (insert.result != UnityWebRequest.Result.Success)
            {
                Debug.LogError($"[SupabaseSaveUploader] pid reserve failed: {insert.error} - {insert.downloadHandler.text}");
                onComplete?.Invoke(false);
                yield break;
            }
        }

        onComplete?.Invoke(true);
    }

    private static string GenerateRandomPid()
    {
        const string chars = "ABCDEFGHIJKLMNOPQRSTUVWXYZ0123456789";
        System.Text.StringBuilder sb = new System.Text.StringBuilder(8);
        for (int i = 0; i < 8; i++)
        {
            int idx = UnityEngine.Random.Range(0, chars.Length);
            sb.Append(chars[idx]);
        }
        return sb.ToString();
    }

    private static bool IsValidPid(string value)
    {
        if (string.IsNullOrWhiteSpace(value)) return false;
        if (value.Length != 8) return false;
        for (int i = 0; i < value.Length; i++)
        {
            char c = value[i];
            if (!char.IsLetterOrDigit(c)) return false;
        }
        return true;
    }

    private static string BoolJson(bool v) => v ? "true" : "false";

    private static string BoolArrayJson(bool[] arr)
    {
        if (arr == null) return "[]";
        var sb = new StringBuilder("[");
        for (int i = 0; i < arr.Length; i++)
        {
            if (i > 0) sb.Append(',');
            sb.Append(arr[i] ? "true" : "false");
        }
        sb.Append(']');
        return sb.ToString();
    }

    private static string IntArrayJson(int[] arr)
    {
        if (arr == null) return "[]";
        var sb = new StringBuilder("[");
        for (int i = 0; i < arr.Length; i++)
        {
            if (i > 0) sb.Append(',');
            sb.Append(arr[i]);
        }
        sb.Append(']');
        return sb.ToString();
    }

    private static string StringArrayJson(string[] arr)
    {
        if (arr == null) return "[]";
        var sb = new StringBuilder("[");
        for (int i = 0; i < arr.Length; i++)
        {
            if (i > 0) sb.Append(',');
            sb.Append('"').Append(JsonEscape(arr[i] ?? string.Empty)).Append('"');
        }
        sb.Append(']');
        return sb.ToString();
    }

    private static string Int2DJson(int[,] arr)
    {
        if (arr == null) return "[]";
        int d0 = arr.GetLength(0);
        int d1 = arr.GetLength(1);
        var sb = new StringBuilder("[");
        for (int i = 0; i < d0; i++)
        {
            if (i > 0) sb.Append(',');
            sb.Append('[');
            for (int j = 0; j < d1; j++)
            {
                if (j > 0) sb.Append(',');
                sb.Append(arr[i, j]);
            }
            sb.Append(']');
        }
        sb.Append(']');
        return sb.ToString();
    }

    private static string String2DJson(string[,] arr)
    {
        if (arr == null) return "[]";
        int d0 = arr.GetLength(0);
        int d1 = arr.GetLength(1);
        var sb = new StringBuilder("[");
        for (int i = 0; i < d0; i++)
        {
            if (i > 0) sb.Append(',');
            sb.Append('[');
            for (int j = 0; j < d1; j++)
            {
                if (j > 0) sb.Append(',');
                sb.Append('"').Append(JsonEscape(arr[i, j] ?? string.Empty)).Append('"');
            }
            sb.Append(']');
        }
        sb.Append(']');
        return sb.ToString();
    }

    private static string JsonEscape(string s)
    {
        if (string.IsNullOrEmpty(s)) return "";
        return s.Replace("\\", "\\\\").Replace("\"", "\\\"");
    }

    private static string GetDeviceCode()
    {
        string code = SystemInfo.deviceUniqueIdentifier;
        if (string.IsNullOrWhiteSpace(code)) code = SystemInfo.deviceName;
        return string.IsNullOrWhiteSpace(code) ? "unknown-device" : code;
    }

    [Serializable]
    private class UserAccountRow
    {
        public string pid;
        public string user_name;
        public string device_code;
    }

    private IEnumerator CheckDeviceMismatch(string currentDeviceCode)
    {
        string url = $"{SupabaseUrl}/rest/v1/{UserTable}?pid=eq.{pid}&select=pid,user_name,device_code&limit=1";
        using (UnityWebRequest request = UnityWebRequest.Get(url))
        {
            request.SetRequestHeader("apikey", SupabaseKey);
            request.SetRequestHeader("Authorization", $"Bearer {SupabaseKey}");
            yield return request.SendWebRequest();
            if (request.result != UnityWebRequest.Result.Success)
            {
                Debug.LogError($"[SupabaseSaveUploader] user lookup failed: {request.error} - {request.downloadHandler.text}");
                yield break;
            }

            string body = request.downloadHandler.text ?? "[]";
            var rows = JsonArrayHelper.FromJsonArray<UserAccountRow>(body);
            if (rows.Length == 0) yield break;

            var row = rows[0];
            if (!string.IsNullOrWhiteSpace(row.device_code) && row.device_code != currentDeviceCode)
            {
                Debug.Log("Player at 2 devices!");
            }
        }
    }

    private static class JsonArrayHelper
    {
        [Serializable]
        private class Wrapper<T>
        {
            public T[] items;
        }

        public static T[] FromJsonArray<T>(string json)
        {
            if (string.IsNullOrWhiteSpace(json)) return new T[0];
            string wrapped = "{\"items\":" + json + "}";
            return JsonUtility.FromJson<Wrapper<T>>(wrapped)?.items ?? new T[0];
        }
    }
}
