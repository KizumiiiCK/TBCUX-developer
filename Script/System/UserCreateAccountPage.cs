using System;
using System.Collections;
using System.Collections.Generic;
using System.Text;
using TMPro;
using UnityEngine;
using UnityEngine.Networking;
using UnityEngine.UI;

public class UserCreateAccountPage : MonoBehaviour
{
    private const string UserTable = "user_accounts";
    private const int MaxNameChars = 16;
    private const int MaxPidAttempts = 20;

    [Header("UI")]
    [SerializeField] private TMP_InputField userNameInput;
    [SerializeField] private TMP_Text hintText;
    [SerializeField] private TMP_Text pidText;
    [SerializeField] private Button createButton;
    [SerializeField] private Button retakeButton;
    [SerializeField] private Button backButton;
    private static string loadingPagePath = "UI/Pages/loading";
    private static string loginCheckPagePath = "UI/Pages/user/UserLoginCheckPage";

    private LoadingPage loadingPage;
    private string generatedPid;
    private bool usernameDuplicated;
    private bool pidGenerating;

    private void Awake()
    {
        if (createButton != null) createButton.onClick.AddListener(OnCreateClicked);
        if (retakeButton != null) retakeButton.onClick.AddListener(OnRetakeClicked);
        if (backButton != null) backButton.onClick.AddListener(OnBackClicked);
    }

    private void Start()
    {
        if (!UXPref.HasSupabaseConfig)
        {
            SetHint(SupabaseSettings.MissingConfigHint);
            if (createButton != null) createButton.interactable = false;
            if (retakeButton != null) retakeButton.interactable = false;
            return;
        }

        SetHint("Preparing account...");
        StartCoroutine(PrepareUniquePid());
    }

    private IEnumerator PrepareUniquePid()
    {
        if (pidGenerating) yield break;
        pidGenerating = true;
        if (retakeButton != null) retakeButton.interactable = false;

        bool done = false;
        bool success = false;
        StartLoading(
            new List<LoadingTask>
            {
                new LoadingTask("Generating account ID...", ExecuteGeneratePidTask)
            },
            ok =>
            {
                success = ok;
                done = true;
            }
        );

        while (!done) yield return null;
        CleanupLoading();

        if (!success || string.IsNullOrWhiteSpace(generatedPid))
        {
            SetHint("Failed to generate account ID.");
            UpdatePidDisplay(null);
            pidGenerating = false;
            if (retakeButton != null) retakeButton.interactable = true;
            yield break;
        }

        UpdatePidDisplay(generatedPid);
        SetHint("Please enter your username.");
        pidGenerating = false;
        if (retakeButton != null) retakeButton.interactable = true;
    }

    private IEnumerator ExecuteGeneratePidTask(LoadingTask task)
    {
        loadingPage?.SetDetail("Preparing random pid generation...");
        if (Application.internetReachability == NetworkReachability.NotReachable)
        {
            task.Success = false;
            loadingPage?.NotifyFailure("No network connection.");
            yield break;
        }

        for (int i = 0; i < MaxPidAttempts; i++)
        {
            string candidate = GenerateRandomPid();
            bool exists = false;
            bool reqDone = false;
            loadingPage?.SetDetail($"Validating candidate pid ({i + 1}/{MaxPidAttempts}): {candidate}");

            yield return StartCoroutine(CheckPidExists(candidate, value =>
            {
                exists = value;
                reqDone = true;
            }));

            while (!reqDone) yield return null;

            if (!exists)
            {
                generatedPid = candidate;
                task.Success = true;
                task.Result = generatedPid;
                loadingPage?.SetDetail($"Pid assigned: {generatedPid}");
                yield break;
            }
        }

        task.Success = false;
        task.Result = null;
        loadingPage?.SetDetail("Could not find a valid unique pid.");
    }

    private void OnCreateClicked()
    {
        if (pidGenerating)
        {
            SetHint("Generating account ID...");
            return;
        }

        if (string.IsNullOrWhiteSpace(generatedPid))
        {
            SetHint("Account ID not ready. Please try again later.");
            return;
        }

        string userName = userNameInput != null ? userNameInput.text : string.Empty;
        if (!ValidateUserName(userName, out string error))
        {
            SetHint(error);
            return;
        }

        StartCoroutine(CreateAccountFlow(userName.Trim()));
    }

    private void OnRetakeClicked()
    {
        if (pidGenerating) return;
        SetHint("Retaking account ID...");
        StartCoroutine(PrepareUniquePid());
    }

    private IEnumerator CreateAccountFlow(string userName)
    {
        bool done = false;
        bool success = false;
        StartLoading(
            new List<LoadingTask>
            {
                new LoadingTask("Checking username...", task => ExecuteCheckNameTask(task, userName)),
                new LoadingTask("Creating account...", task => ExecuteCreateAccountTask(task, userName))
            },
            ok =>
            {
                success = ok;
                done = true;
            }
        );

        while (!done) yield return null;
        CleanupLoading();

        if (!success)
        {
            if (usernameDuplicated) SetHint("Username already exists. Please pick another one.");
            else SetHint("Failed to create account. Please try again.");
            yield break;
        }

        UserInfoLocalStore.Save(new UserInfoLocalData
        {
            pid = generatedPid,
            user_name = userName,
            device_code = UserInfoLocalStore.GetDeviceCode()
        });

        PlayerPrefs.SetString(UXPref.UserPrefKey, generatedPid);
        PlayerPrefs.Save();

        if (createButton != null) createButton.interactable = false;
        if (retakeButton != null) retakeButton.interactable = false;
        SetHint("Account created successfully!");
    }

    private IEnumerator ExecuteCheckNameTask(LoadingTask task, string userName)
    {
        usernameDuplicated = false;
        loadingPage?.SetDetail($"Checking username availability: {userName}");
        if (Application.internetReachability == NetworkReachability.NotReachable)
        {
            task.Success = false;
            loadingPage?.NotifyFailure("No network connection.");
            yield break;
        }

        string encoded = UnityWebRequest.EscapeURL(userName);
        string url = $"{UXPref.SupabaseUrl}/rest/v1/{UserTable}?user_name=eq.{encoded}&select=pid&limit=1";
        using (UnityWebRequest request = UnityWebRequest.Get(url))
        {
            request.SetRequestHeader("apikey", UXPref.SupabaseKey);
            request.SetRequestHeader("Authorization", $"Bearer {UXPref.SupabaseKey}");
            yield return request.SendWebRequest();

            if (request.result != UnityWebRequest.Result.Success)
            {
                task.Success = false;
                loadingPage?.SetDetail(request.error);
                yield break;
            }

            string json = request.downloadHandler.text ?? "[]";
            var rows = JsonArrayHelper.FromJsonArray<NameLookupRow>(json);
            usernameDuplicated = rows.Length > 0;
            task.Success = !usernameDuplicated;
            task.Result = null;
            loadingPage?.SetDetail(task.Success
                ? "Username is available."
                : "Username is already taken.");
        }
    }

    private IEnumerator ExecuteCreateAccountTask(LoadingTask task, string userName)
    {
        if (usernameDuplicated)
        {
            task.Success = false;
            task.Result = null;
            yield break;
        }

        if (Application.internetReachability == NetworkReachability.NotReachable)
        {
            task.Success = false;
            loadingPage?.NotifyFailure("No network connection.");
            yield break;
        }
        loadingPage?.SetDetail($"Creating account record for pid: {generatedPid}");

        DateTime now = DateTime.UtcNow;
        DateTime yesterday = now.Date.AddDays(-1);
        string payload = "[{"
                         + $"\"pid\":\"{JsonEscape(generatedPid)}\","
                         + $"\"user_name\":\"{JsonEscape(userName)}\","
                         + $"\"device_code\":\"{JsonEscape(UserInfoLocalStore.GetDeviceCode())}\","
                         + "\"transfer_code\":null,"
                         + $"\"last_update\":\"{now.ToString("o")}\","
                         + $"\"last_checkin_date\":\"{yesterday.ToString("o")}\","
                         + "\"consecutive_days\":0"
                         + "}]";

        string url = $"{UXPref.SupabaseUrl}/rest/v1/{UserTable}";
        using (UnityWebRequest request = new UnityWebRequest(url, "POST"))
        {
            request.uploadHandler = new UploadHandlerRaw(Encoding.UTF8.GetBytes(payload));
            request.downloadHandler = new DownloadHandlerBuffer();
            request.SetRequestHeader("Content-Type", "application/json");
            request.SetRequestHeader("apikey", UXPref.SupabaseKey);
            request.SetRequestHeader("Authorization", $"Bearer {UXPref.SupabaseKey}");
            request.SetRequestHeader("Prefer", "resolution=merge-duplicates,return=minimal");
            yield return request.SendWebRequest();

            if (request.result != UnityWebRequest.Result.Success)
            {
                task.Success = false;
                loadingPage?.SetDetail($"{request.error} - {request.downloadHandler.text}");
                yield break;
            }

            task.Success = true;
            task.Result = null;
            loadingPage?.SetDetail("Account record uploaded successfully.");
        }
    }

    private void OnBackClicked()
    {
        GameObject prefab = Resources.Load<GameObject>(loginCheckPagePath);
        if (prefab != null) Instantiate(prefab);
        Destroy(gameObject);
    }

    private IEnumerator CheckPidExists(string pid, Action<bool> onDone)
    {
        string encoded = UnityWebRequest.EscapeURL(pid);
        string url = $"{UXPref.SupabaseUrl}/rest/v1/{UserTable}?pid=eq.{encoded}&select=pid&limit=1";
        using (UnityWebRequest request = UnityWebRequest.Get(url))
        {
            request.SetRequestHeader("apikey", UXPref.SupabaseKey);
            request.SetRequestHeader("Authorization", $"Bearer {UXPref.SupabaseKey}");
            yield return request.SendWebRequest();

            if (request.result != UnityWebRequest.Result.Success)
            {
                onDone?.Invoke(true);
                yield break;
            }

            string json = request.downloadHandler.text ?? "[]";
            var rows = JsonArrayHelper.FromJsonArray<PidLookupRow>(json);
            onDone?.Invoke(rows.Length > 0);
        }
    }

    private static string GenerateRandomPid()
    {
        // Lowercase + digits only, excluding confusing chars: 0/o, 1/l/i.
        const string chars = "abcdefghijkmnopqrstuvwxyz0123456789";
        char[] pid = new char[8];
        for (int i = 0; i < pid.Length; i++)
            pid[i] = chars[UnityEngine.Random.Range(0, chars.Length)];
        return new string(pid);
    }

    private static bool ValidateUserName(string value, out string error)
    {
        string text = value == null ? string.Empty : value.Trim();
        if (string.IsNullOrWhiteSpace(text))
        {
            error = "Please enter your username.";
            return false;
        }
        if (text.Length > MaxNameChars)
        {
            error = "Username cannot exceed 16 characters.";
            return false;
        }
        for (int i = 0; i < text.Length; i++)
        {
            char c = text[i];
            if (char.IsWhiteSpace(c))
            {
                error = "Username cannot contain spaces.";
                return false;
            }
            if (char.IsPunctuation(c) || char.IsSymbol(c))
            {
                error = "Username cannot contain punctuation or symbols.";
                return false;
            }
        }

        error = null;
        return true;
    }

    private static string JsonEscape(string s)
    {
        if (string.IsNullOrEmpty(s)) return string.Empty;
        return s.Replace("\\", "\\\\").Replace("\"", "\\\"");
    }

    private void SetHint(string text)
    {
        if (hintText != null) hintText.text = text ?? string.Empty;
    }

    private void UpdatePidDisplay(string pid)
    {
        if (pidText == null) return;
        if (string.IsNullOrWhiteSpace(pid))
        {
            pidText.text = string.Empty;
            return;
        }

        pidText.text = $"Your new user id: <color=#007bff>{pid}</color>.";
    }

    private void StartLoading(List<LoadingTask> tasks, Action<bool> onComplete)
    {
        GameObject prefab = Resources.Load<GameObject>(loadingPagePath);
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

    [Serializable]
    private class NameLookupRow
    {
        public string pid;
    }

    [Serializable]
    private class PidLookupRow
    {
        public string pid;
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
            Wrapper<T> result = JsonUtility.FromJson<Wrapper<T>>(wrapped);
            return result?.items ?? new T[0];
        }
    }
}
