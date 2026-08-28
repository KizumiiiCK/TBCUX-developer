using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using TMPro;
using UnityEngine;
using UnityEngine.Networking;
using UnityEngine.UI;

public class UserDeleteAccountPage : MonoBehaviour
{
    private const string UserTable = "user_accounts";
    private const string LoadingPagePath = "UI/Pages/loading";

    [Header("Messages")]
    [SerializeField] private TMP_Text secondMessageText;

    [Header("Buttons")]
    [SerializeField] private Button confirmDeleteButton;
    [SerializeField] private Button cancelButton;

    [Header("Result")]
    [SerializeField] private GameObject completePage;

    private LoadingPage loadingPage;
    private UserInfoLocalData localUser;
    private bool secondConfirmRequired;
    private bool isDeleting;

    private void Awake()
    {

        if (confirmDeleteButton != null) confirmDeleteButton.onClick.AddListener(OnConfirmDeleteClicked);
        if (cancelButton != null) cancelButton.onClick.AddListener(OnCancelClicked);

        if (secondMessageText != null) secondMessageText.gameObject.SetActive(false);
        if (completePage != null) completePage.SetActive(false);
    }

    private void Start()
    {
        secondConfirmRequired = true;
        UserInfoLocalStore.TryLoad(out localUser);
    }

    private void OnCancelClicked()
    {
        // Requirement: cancel should always destroy this page.
        Destroy(gameObject);
    }

    private void OnConfirmDeleteClicked()
    {
        if (isDeleting) return;
        if (!UXPref.HasSupabaseConfig)
        {
            Debug.LogWarning("[UserDeleteAccountPage] " + SupabaseSettings.MissingConfigHint);
            return;
        }

        if (secondConfirmRequired)
        {
            secondConfirmRequired = false;
            if (secondMessageText != null) secondMessageText.gameObject.SetActive(true);
            return;
        }

        StartCoroutine(DeleteFlow());
    }

    private IEnumerator DeleteFlow()
    {
        isDeleting = true;
        if (confirmDeleteButton != null) confirmDeleteButton.interactable = false;

        bool done = false;
        bool success = false;
        StartLoading(
            new List<LoadingTask>
            {
                new LoadingTask("Deleting account row from user_accounts...", ExecuteDeleteRemoteUserTask),
                new LoadingTask("Deleting all local save files...", ExecuteDeleteLocalTask),
            },
            ok =>
            {
                success = ok;
                done = true;
            });

        while (!done) yield return null;
        CleanupLoading();

        isDeleting = false;
        if (!success)
        {
            if (confirmDeleteButton != null) confirmDeleteButton.interactable = true;
            if (secondMessageText != null) secondMessageText.gameObject.SetActive(false);
            secondConfirmRequired = true;
            yield break;
        }

        if (completePage != null) completePage.SetActive(true);
    }

    private IEnumerator ExecuteDeleteRemoteUserTask(LoadingTask task)
    {
        if (Application.internetReachability == NetworkReachability.NotReachable)
        {
            task.Success = false;
            if (loadingPage != null) loadingPage.NotifyFailure("No network connection.");
            yield break;
        }

        if (localUser == null || string.IsNullOrWhiteSpace(localUser.pid))
        {
            task.Success = false;
            if (loadingPage != null) loadingPage.NotifyFailure("Local user ID is missing.");
            yield break;
        }

        string url = $"{UXPref.SupabaseUrl}/rest/v1/{UserTable}?pid=eq.{UnityWebRequest.EscapeURL(localUser.pid)}";
        using (UnityWebRequest request = new UnityWebRequest(url, "DELETE"))
        {
            request.downloadHandler = new DownloadHandlerBuffer();
            request.SetRequestHeader("apikey", UXPref.SupabaseKey);
            request.SetRequestHeader("Authorization", $"Bearer {UXPref.SupabaseKey}");
            request.SetRequestHeader("Prefer", "return=minimal");

            yield return request.SendWebRequest();
            if (request.result != UnityWebRequest.Result.Success)
            {
                task.Success = false;
                if (loadingPage != null) loadingPage.NotifyFailure("Failed to delete user_accounts row.");
                yield break;
            }
        }

        task.Success = true;
    }

    private IEnumerator ExecuteDeleteLocalTask(LoadingTask task)
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
                        Debug.LogError($"[UserDeleteAccountPage] Failed to delete save file: {e.Message}");
                    }
                }
            }
        }
        catch (Exception e)
        {
            ok = false;
            Debug.LogError($"[UserDeleteAccountPage] Failed to enumerate save files: {e.Message}");
        }

        try
        {
            if (File.Exists(UserInfoLocalStore.FilePath))
                File.Delete(UserInfoLocalStore.FilePath);
        }
        catch (Exception e)
        {
            ok = false;
            Debug.LogError($"[UserDeleteAccountPage] Failed to delete user info file: {e.Message}");
        }

        task.Success = ok;
        if (!ok && loadingPage != null) loadingPage.NotifyFailure("Failed to delete local files.");
        yield break;
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
