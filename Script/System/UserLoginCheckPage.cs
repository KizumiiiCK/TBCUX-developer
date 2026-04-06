using System;
using System.Collections;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.Networking;
using UnityEngine.UI;

public class UserLoginCheckPage : MonoBehaviour
{
    private const string UserTable = "user_accounts";

    [Header("UI")]
    [SerializeField] private TMP_Text messageText;
    [SerializeField] private Button createNewButton;
    [SerializeField] private Button inheritButton;
    [SerializeField] private MainMenu mainMenu;

    private static string loadingPagePath = "UI/Pages/loading";
    private static string createAccountPagePath = "UI/Pages/user/CreateAccount";
    private static string restoreAccountPagePath = "UI/Pages/user/RestoreAccount";

    private LoadingPage loadingPage;
    private UserInfoLocalData localInfo;
    private UserAccountRow remoteRow;

    private void Awake()
    {
        if (createNewButton != null) createNewButton.onClick.AddListener(OnCreateNewAccount);
        if (inheritButton != null) inheritButton.onClick.AddListener(OnInheritAccount);
    }

    private void Start()
    {
        ShowChoice(false);
        StartCoroutine(BootstrapCheck());
    }

    private IEnumerator BootstrapCheck()
    {
        if (!UserInfoLocalStore.TryLoad(out localInfo))
        {
            ShowNewUserPanel();
            yield break;
        }

        if (string.IsNullOrWhiteSpace(localInfo.pid))
        {
            ShowNewUserPanel();
            yield break;
        }

        bool done = false;
        bool success = false;
        StartLoading(
            new List<LoadingTask>
            {
                new LoadingTask("Checking account information...", ExecuteCheckPidTask)
            },
            ok =>
            {
                success = ok;
                done = true;
            }
        );

        while (!done) yield return null;
        CleanupLoading();

        if (!success || remoteRow == null)
        {
            ShowNewUserPanel();
            yield break;
        }

        bool matched =
            string.Equals(localInfo.pid, remoteRow.pid, StringComparison.Ordinal) &&
            string.Equals(localInfo.user_name, remoteRow.user_name, StringComparison.Ordinal) &&
            string.Equals(localInfo.device_code, remoteRow.device_code, StringComparison.Ordinal);

        if (matched)
        {
            if (mainMenu != null)
            {
                string nickname = string.IsNullOrWhiteSpace(remoteRow.user_name) ? localInfo.user_name : remoteRow.user_name;
                mainMenu.SetWelcomeBackMessage($"Welcome back, {nickname}!");
            }
            Destroy(gameObject);
            yield break;
        }

        ShowNewUserPanel();
    }

    private IEnumerator ExecuteCheckPidTask(LoadingTask task)
    {
        loadingPage?.SetDetail("Preparing request for account validation...");
        if (Application.internetReachability == NetworkReachability.NotReachable)
        {
            task.Success = false;
            task.Result = null;
            loadingPage?.NotifyFailure("No network connection.");
            yield break;
        }

        string encodedPid = UnityWebRequest.EscapeURL(localInfo.pid);
        string url = $"{UXPref.SupabaseUrl}/rest/v1/{UserTable}?pid=eq.{encodedPid}&select=pid,user_name,device_code&limit=1";
        loadingPage?.SetDetail($"Querying user_accounts by pid: {localInfo.pid}");

        using (UnityWebRequest request = UnityWebRequest.Get(url))
        {
            request.SetRequestHeader("apikey", UXPref.SupabaseKey);
            request.SetRequestHeader("Authorization", $"Bearer {UXPref.SupabaseKey}");
            yield return request.SendWebRequest();

            if (request.result != UnityWebRequest.Result.Success)
            {
                task.Success = false;
                task.Result = null;
                loadingPage?.SetDetail($"Account check failed: {request.error}");
                yield break;
            }

            string json = request.downloadHandler.text ?? "[]";
            UserAccountRow[] rows = JsonArrayHelper.FromJsonArray<UserAccountRow>(json);
            remoteRow = rows.Length > 0 ? rows[0] : null;
            task.Success = remoteRow != null;
            task.Result = remoteRow;
            loadingPage?.SetDetail(task.Success
                ? "Account record found."
                : "No matching account record found.");
        }
    }

    private void OnCreateNewAccount()
    {
        GameObject prefab = Resources.Load<GameObject>(createAccountPagePath);
        if (prefab == null)
        {
            SetMessage($"缺少页面：{createAccountPagePath}");
            return;
        }
        Instantiate(prefab);
        Destroy(gameObject);
    }

    private void OnInheritAccount()
    {
        GameObject prefab = Resources.Load<GameObject>(restoreAccountPagePath);
        if (prefab == null)
        {
            SetMessage($"Missing page: {restoreAccountPagePath}");
            return;
        }
        Instantiate(prefab);
        Destroy(gameObject);
    }

    private void ShowNewUserPanel()
    {
        ShowChoice(true);
    }

    private void SetMessage(string text)
    {
        if (messageText != null) messageText.text = text ?? string.Empty;
    }

    private void ShowChoice(bool show)
    {
        if (createNewButton != null) createNewButton.gameObject.SetActive(show);
        if (inheritButton != null) inheritButton.gameObject.SetActive(show);
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
    private class UserAccountRow
    {
        public string pid;
        public string user_name;
        public string device_code;
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
