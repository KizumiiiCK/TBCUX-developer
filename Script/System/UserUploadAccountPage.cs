using System;
using System.Collections;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Text;
using TMPro;
using UnityEngine;
using UnityEngine.Networking;
using UnityEngine.UI;

public class UserUploadAccountPage : MonoBehaviour
{
    private const string UserTable = "user_accounts";
    private const string LoadingPagePath = "UI/Pages/loading";

    [Header("Main UI")]
    [SerializeField] private TMP_Text userIdText;
    [SerializeField] private TMP_Text userNameText;
    [SerializeField] private TMP_InputField transferCodeInput;
    [SerializeField] private TMP_Text infoText;
    [SerializeField] private Button cancelButton;
    [SerializeField] private Button uploadButton;

    [Header("Confirm Popup (Hidden by default)")]
    [SerializeField] private GameObject confirmPopup;
    [SerializeField] private Button confirmYesButton;
    [SerializeField] private Button confirmNoButton;

    [Header("Result")]
    [SerializeField] private GameObject uploadSuccessPage;

    private LoadingPage loadingPage;
    private UserInfoLocalData localUser;
    private DateTime utc8Now;
    private bool isUploading;

    private void Awake()
    {
        if (cancelButton != null) cancelButton.onClick.AddListener(OnCancelClicked);
        if (uploadButton != null) uploadButton.onClick.AddListener(OnUploadClicked);
        if (confirmYesButton != null) confirmYesButton.onClick.AddListener(OnConfirmYesClicked);
        if (confirmNoButton != null) confirmNoButton.onClick.AddListener(OnConfirmNoClicked);

        if (confirmPopup != null) confirmPopup.SetActive(false);
        if (uploadSuccessPage != null) uploadSuccessPage.SetActive(false);
    }

    private void Start()
    {
        if (!UserInfoLocalStore.TryLoad(out localUser))
        {
            SetInfo("Failed to read local user save. Upload is unavailable.");
            SetUserDisplay("--------", "--------");
            if (uploadButton != null) uploadButton.interactable = false;
            return;
        }

        SetUserDisplay(localUser.pid, localUser.user_name);
        SetInfo("Enter your transfer key code to upload.");
    }

    private void OnCancelClicked()
    {
        if (isUploading) return;
        Destroy(gameObject);
    }

    private void OnUploadClicked()
    {
        if (isUploading) return;
        if (localUser == null)
        {
            SetInfo("Local user info is invalid. Upload is unavailable.");
            return;
        }

        string transferCode = transferCodeInput != null ? transferCodeInput.text : string.Empty;
        if (!ValidateTransferCode(transferCode, out string error))
        {
            SetInfo(error);
            return;
        }

        if (confirmPopup != null) confirmPopup.SetActive(true);
    }

    private void OnConfirmNoClicked()
    {
        if (confirmPopup != null) confirmPopup.SetActive(false);
    }

    private void OnConfirmYesClicked()
    {
        if (isUploading) return;
        if (confirmPopup != null) confirmPopup.SetActive(false);
        StartCoroutine(UploadFlow());
    }

    private IEnumerator UploadFlow()
    {
        isUploading = true;
        SetButtonsInteractable(false);
        SetInfo("Uploading local save data...");

        string transferCode = transferCodeInput != null ? transferCodeInput.text.Trim() : string.Empty;
        bool finished = false;
        bool allSuccess = false;

        var tasks = new List<LoadingTask>
        {
            new LoadingTask("Fetching world time (UTC+8)...", ExecuteFetchUtc8Task),
            new LoadingTask("Uploading character upgrades...", ExecuteUploadCharacterUpgradesTask),
            new LoadingTask("Uploading stage progress...", ExecuteUploadGameProgressTask),
            new LoadingTask("Uploading reward inventory...", ExecuteUploadRewardInventoryTask),
            new LoadingTask("Uploading enemy meet flags...", ExecuteUploadEnemyMeetTask),
            new LoadingTask("Uploading team selections...", ExecuteUploadTeamSelectionsTask),
            new LoadingTask("Updating account transfer metadata...", task => ExecuteUpdateUserAccountTask(task, transferCode)),
        };

        StartLoading(tasks, ok =>
        {
            allSuccess = ok;
            finished = true;
        });

        while (!finished) yield return null;
        CleanupLoading();

        if (!allSuccess)
        {
            SetInfo("Upload did not complete (network failed or canceled). Local data is preserved.");
            isUploading = false;
            SetButtonsInteractable(true);
            yield break;
        }

        if (!DeleteAllLocalSaveFiles())
        {
            SetInfo("Upload succeeded, but local save cleanup failed. Please verify manually.");
            isUploading = false;
            SetButtonsInteractable(true);
            yield break;
        }

        SetInfo("Upload succeeded. Local save data has been removed.");
        if (uploadSuccessPage != null) uploadSuccessPage.SetActive(true);
        isUploading = false;
    }

    private IEnumerator ExecuteFetchUtc8Task(LoadingTask task)
    {
        if (loadingPage != null) loadingPage.SetDetail("Requesting world time from network APIs...");

        DateTime? result = null;
        yield return GetNetworkDateTimeUtc8(value => result = value);
        if (!result.HasValue)
        {
            task.Success = false;
            if (loadingPage != null) loadingPage.NotifyFailure("Failed to fetch world time.");
            yield break;
        }

        utc8Now = result.Value;
        task.Success = true;
        task.Result = utc8Now;
        if (loadingPage != null) loadingPage.SetDetail($"World time: {utc8Now:yyyy-MM-dd HH:mm:ss}");
    }

    private IEnumerator ExecuteUploadCharacterUpgradesTask(LoadingTask task)
    {
        if (loadingPage != null) loadingPage.SetDetail("Reading and uploading character_upgrades...");

        var dict = GenericSaveSystem.LoadData<Dictionary<string, CharacterUpgradeSave.UpgradeDetails>>(CharacterUpgradeSave.filename)
                   ?? new Dictionary<string, CharacterUpgradeSave.UpgradeDetails>();

        var rows = new List<string>();
        foreach (var kv in dict)
        {
            CharacterUpgradeSave.UpgradeDetails ud = kv.Value ?? new CharacterUpgradeSave.UpgradeDetails();
            string row = "{"
                + $"\"pid\":\"{JsonEscape(localUser.pid)}\","
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

        if (rows.Count == 0)
        {
            task.Success = true;
            if (loadingPage != null) loadingPage.SetDetail("No local character_upgrades data. Skipped.");
            yield break;
        }

        bool ok = false;
        yield return Upsert("character_upgrades", $"[{string.Join(",", rows)}]", success => ok = success);
        task.Success = ok;
        if (!ok && loadingPage != null) loadingPage.NotifyFailure("Failed to upload character_upgrades.");
    }

    private IEnumerator ExecuteUploadGameProgressTask(LoadingTask task)
    {
        if (loadingPage != null) loadingPage.SetDetail("Reading and uploading game_progress_sections...");

        GameProgressSave.ChapterClearList[] chapters = GenericSaveSystem.LoadData<GameProgressSave.ChapterClearList[]>(GameProgressSave.filename)
                                                   ?? Array.Empty<GameProgressSave.ChapterClearList>();
        var rows = new List<string>();
        foreach (GameProgressSave.ChapterClearList chapter in chapters)
        {
            if (chapter?.SectionList == null) continue;
            foreach (GameProgressSave.SectionClearList sec in chapter.SectionList)
            {
                if (sec == null) continue;
                string row = "{"
                    + $"\"pid\":\"{JsonEscape(localUser.pid)}\","
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

        if (rows.Count == 0)
        {
            task.Success = true;
            if (loadingPage != null) loadingPage.SetDetail("No local game_progress_sections data. Skipped.");
            yield break;
        }

        bool ok = false;
        yield return Upsert("game_progress_sections", $"[{string.Join(",", rows)}]", success => ok = success);
        task.Success = ok;
        if (!ok && loadingPage != null) loadingPage.NotifyFailure("Failed to upload game_progress_sections.");
    }

    private IEnumerator ExecuteUploadRewardInventoryTask(LoadingTask task)
    {
        if (loadingPage != null) loadingPage.SetDetail("Reading and uploading reward_inventory as one int[] row...");

        int expectedLength = RewardingSystem.RewardNumMap.Count;
        int[] localAmounts = GenericSaveSystem.LoadData<int[]>(RewardingSystem.filename) ?? new int[expectedLength];
        int[] amounts;
        if (localAmounts.Length == expectedLength)
        {
            amounts = localAmounts;
        }
        else
        {
            // Normalize legacy/corrupted lengths to current reward table size.
            amounts = new int[expectedLength];
            Array.Copy(localAmounts, amounts, Mathf.Min(localAmounts.Length, expectedLength));
        }

        string row = "{"
            + $"\"pid\":\"{JsonEscape(localUser.pid)}\","
            + $"\"amounts\":{IntArrayJson(amounts)}"
            + "}";

        bool ok = false;
        yield return Upsert("reward_inventory", $"[{row}]", success => ok = success);
        task.Success = ok;
        if (!ok && loadingPage != null) loadingPage.NotifyFailure("Failed to upload reward_inventory.");
    }

    private IEnumerator ExecuteUploadEnemyMeetTask(LoadingTask task)
    {
        if (loadingPage != null) loadingPage.SetDetail("Reading and uploading enemy_meet as one bool[1000] row...");

        bool[] metFlags = EnemyMeetSave.GetData();
        if (metFlags == null || metFlags.Length == 0)
        {
            task.Success = true;
            if (loadingPage != null) loadingPage.SetDetail("No local enemy_meet data. Skipped.");
            yield break;
        }

        string row = "{"
            + $"\"pid\":\"{JsonEscape(localUser.pid)}\","
            + $"\"met_flags\":{BoolArrayJson(metFlags)}"
            + "}";

        bool ok = false;
        yield return Upsert("enemy_meet", $"[{row}]", success => ok = success);
        task.Success = ok;
        if (!ok && loadingPage != null) loadingPage.NotifyFailure("Failed to upload enemy_meet.");
    }

    private IEnumerator ExecuteUploadTeamSelectionsTask(LoadingTask task)
    {
        if (loadingPage != null) loadingPage.SetDetail("Reading and uploading team_selections (with team_name)...");

        string[,] selections = SelectionsSave.GetAll();
        var rows = new List<string>();

        for (int i = 0; i < SelectionsSave.TeamNum; i++)
        {
            var slots = new string[SelectionsSave.SIZE];
            for (int j = 0; j < SelectionsSave.SIZE; j++)
                slots[j] = selections[i, j];

            string teamName = TeamNameSave.GetTeamNameOrDefault(i);
            string row = "{"
                + $"\"pid\":\"{JsonEscape(localUser.pid)}\","
                + $"\"team_index\":{i},"
                + $"\"team_name\":\"{JsonEscape(teamName)}\","
                + $"\"slots\":{StringArrayJson(slots)}"
                + "}";
            rows.Add(row);
        }

        if (rows.Count == 0)
        {
            task.Success = true;
            if (loadingPage != null) loadingPage.SetDetail("No local team_selections data. Skipped.");
            yield break;
        }

        bool ok = false;
        yield return Upsert("team_selections", $"[{string.Join(",", rows)}]", success => ok = success);
        task.Success = ok;
        if (!ok && loadingPage != null) loadingPage.NotifyFailure("Failed to upload team_selections.");
    }

    private IEnumerator ExecuteUpdateUserAccountTask(LoadingTask task, string transferCode)
    {
        if (loadingPage != null) loadingPage.SetDetail("Updating user_accounts.transfer_code / device_code / last_update...");

        if (utc8Now == DateTime.MinValue)
        {
            task.Success = false;
            if (loadingPage != null) loadingPage.NotifyFailure("World time is empty. Cannot update user account.");
            yield break;
        }

        string patchUrl = $"{UXPref.SupabaseUrl}/rest/v1/{UserTable}?pid=eq.{UnityWebRequest.EscapeURL(localUser.pid)}";
        string json = "{"
            + $"\"transfer_code\":\"{JsonEscape(transferCode)}\","
            + "\"device_code\":\"\","
            + $"\"last_update\":\"{JsonEscape(utc8Now.ToString("o", CultureInfo.InvariantCulture))}\""
            + "}";

        byte[] bodyRaw = Encoding.UTF8.GetBytes(json);
        using (UnityWebRequest request = new UnityWebRequest(patchUrl, "PATCH"))
        {
            request.uploadHandler = new UploadHandlerRaw(bodyRaw);
            request.downloadHandler = new DownloadHandlerBuffer();
            request.SetRequestHeader("Content-Type", "application/json");
            request.SetRequestHeader("apikey", UXPref.SupabaseKey);
            request.SetRequestHeader("Authorization", $"Bearer {UXPref.SupabaseKey}");
            request.SetRequestHeader("Prefer", "return=representation");

            yield return request.SendWebRequest();
            if (request.result != UnityWebRequest.Result.Success)
            {
                task.Success = false;
                if (loadingPage != null)
                {
                    loadingPage.SetDetail(request.error + " - " + request.downloadHandler.text);
                    loadingPage.NotifyFailure("Failed to update user_accounts.");
                }
                yield break;
            }
        }

        task.Success = true;
    }

    private IEnumerator Upsert(string table, string jsonArray, Action<bool> onDone)
    {
        string url = $"{UXPref.SupabaseUrl}/rest/v1/{table}";
        byte[] bodyRaw = Encoding.UTF8.GetBytes(jsonArray);

        using (UnityWebRequest request = new UnityWebRequest(url, "POST"))
        {
            request.uploadHandler = new UploadHandlerRaw(bodyRaw);
            request.downloadHandler = new DownloadHandlerBuffer();
            request.timeout = 25;
            request.SetRequestHeader("Content-Type", "application/json");
            request.SetRequestHeader("apikey", UXPref.SupabaseKey);
            request.SetRequestHeader("Authorization", $"Bearer {UXPref.SupabaseKey}");
            request.SetRequestHeader("Prefer", "resolution=merge-duplicates");

            yield return request.SendWebRequest();
            bool success = request.result == UnityWebRequest.Result.Success;
            if (!success && loadingPage != null)
            {
                loadingPage.SetDetail($"[{table}] {request.error} - {request.downloadHandler.text}");
            }
            onDone?.Invoke(success);
        }
    }

    private IEnumerator GetNetworkDateTimeUtc8(Action<DateTime?> onComplete)
    {
        string[] apiUrls =
        {
            "https://timeapi.io/api/Time/current/zone?timeZone=Asia/Shanghai",
            "https://worldtimeapi.org/api/timezone/Asia/Shanghai",
        };

        for (int i = 0; i < apiUrls.Length; i++)
        {
            string url = apiUrls[i];
            using (UnityWebRequest req = UnityWebRequest.Get(url))
            {
                yield return req.SendWebRequest();
                if (req.result != UnityWebRequest.Result.Success) continue;

                string json = req.downloadHandler.text;
                string key = i == 0 ? "dateTime" : "datetime";
                string value = TryExtractJsonStringValue(json, key);
                if (!string.IsNullOrEmpty(value) && DateTime.TryParse(value, out DateTime parsed))
                {
                    onComplete?.Invoke(parsed);
                    yield break;
                }
            }
        }

        onComplete?.Invoke(null);
    }

    private static string TryExtractJsonStringValue(string json, string key)
    {
        if (string.IsNullOrWhiteSpace(json) || string.IsNullOrWhiteSpace(key)) return null;
        string token = "\"" + key + "\"";
        int keyPos = json.IndexOf(token, StringComparison.Ordinal);
        if (keyPos < 0) return null;
        int colon = json.IndexOf(':', keyPos + token.Length);
        if (colon < 0) return null;

        int firstQuote = json.IndexOf('"', colon + 1);
        if (firstQuote < 0) return null;
        int endQuote = json.IndexOf('"', firstQuote + 1);
        if (endQuote <= firstQuote) return null;
        return json.Substring(firstQuote + 1, endQuote - firstQuote - 1);
    }

    private static bool ValidateTransferCode(string code, out string error)
    {
        string text = code == null ? string.Empty : code.Trim();
        if (string.IsNullOrWhiteSpace(text))
        {
            error = "Transfer code cannot be empty.";
            return false;
        }
        if (text.Length < 6)
        {
            error = "Transfer code must be at least 6 characters.";
            return false;
        }
        if (text.Length > 16)
        {
            error = "Transfer code cannot exceed 16 characters.";
            return false;
        }

        for (int i = 0; i < text.Length; i++)
        {
            char c = text[i];
            bool isAscii = c >= 33 && c <= 126;
            bool isLetterOrDigit = char.IsLetterOrDigit(c);
            bool isPunctuation = char.IsPunctuation(c);

            if (!isAscii || (!isLetterOrDigit && !isPunctuation))
            {
                error = "Transfer code supports only English letters, digits, and ASCII punctuation.";
                return false;
            }
        }

        error = null;
        return true;
    }

    private bool DeleteAllLocalSaveFiles()
    {
        bool ok = true;
        string dir = Application.persistentDataPath;

        try
        {
            if (Directory.Exists(dir))
            {
                string[] files = Directory.GetFiles(dir, "*" + GenericSaveSystem.FirmEnding);
                for (int i = 0; i < files.Length; i++)
                {
                    try { File.Delete(files[i]); }
                    catch (Exception e)
                    {
                        ok = false;
                        Debug.LogError($"[UserUploadAccountPage] Failed to delete save file: {e.Message}");
                    }
                }
            }
        }
        catch (Exception e)
        {
            ok = false;
            Debug.LogError($"[UserUploadAccountPage] Failed to enumerate save files: {e.Message}");
        }

        try
        {
            if (File.Exists(UserInfoLocalStore.FilePath))
                File.Delete(UserInfoLocalStore.FilePath);
        }
        catch (Exception e)
        {
            ok = false;
            Debug.LogError($"[UserUploadAccountPage] Failed to delete user info file: {e.Message}");
        }

        return ok;
    }

    private void SetUserDisplay(string pid, string userName)
    {
        if (userIdText != null) userIdText.text = pid ?? string.Empty;
        if (userNameText != null) userNameText.text = userName ?? string.Empty;
    }

    private void SetInfo(string text)
    {
        if (infoText != null) infoText.text = text ?? string.Empty;
    }

    private void SetButtonsInteractable(bool interactable)
    {
        if (cancelButton != null) cancelButton.interactable = interactable;
        if (uploadButton != null) uploadButton.interactable = interactable;
        if (confirmYesButton != null) confirmYesButton.interactable = interactable;
        if (confirmNoButton != null) confirmNoButton.interactable = interactable;
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

    private static string JsonEscape(string value)
    {
        if (string.IsNullOrEmpty(value)) return string.Empty;
        return value.Replace("\\", "\\\\").Replace("\"", "\\\"");
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
}
