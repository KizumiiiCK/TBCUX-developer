using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
#if UNITY_WEBGL && !UNITY_EDITOR
using Builda;
#endif

/// <summary>
/// Owns the "download before you show" gate for WebGL.
///
/// On WebGL nothing may block, so any scene that loads content synchronously must have that content
/// resolved *before* it is entered. This runner drives a <see cref="LoadingPage"/> through the
/// required async work, then performs the scene switch only once every task succeeded.
///
/// It survives scene loads (<see cref="Object.DontDestroyOnLoad"/>) so the loading UI stays visible
/// across the transition instead of dying with the outgoing scene.
/// </summary>
public class PrewarmGate : MonoBehaviour
{
    private const string LoadingPagePath = "UI/Pages/loading";

    private static PrewarmGate instance;
    private LoadingPage loadingPage;

    /// <summary>True while a gate is running, so callers can avoid double-triggering.</summary>
    public static bool IsRunning => instance != null;

    /// <summary>
    /// Platform display name from the last boot <c>whoami</c>. Empty when the host did not
    /// return one; never null.
    /// </summary>
    public static string PlayerDisplayName { get; private set; } = string.Empty;

    private static PrewarmGate EnsureInstance()
    {
        if (instance != null) return instance;
        var host = new GameObject("[PrewarmGate]");
        DontDestroyOnLoad(host);
        instance = host.AddComponent<PrewarmGate>();
        return instance;
    }

    /// <summary>
    /// Shows the loading page, runs <paramref name="tasks"/>, then invokes
    /// <paramref name="onSuccess"/>. If the player abandons or a task fails unrecoverably,
    /// <paramref name="onAbandon"/> runs instead and the scene switch never happens.
    /// </summary>
    public static void Run(List<LoadingTask> tasks, Action onSuccess, Action onAbandon = null)
    {
        PrewarmGate gate = EnsureInstance();
        gate.StartCoroutine(gate.RunRoutine(tasks, onSuccess, onAbandon));
    }

    /// <summary>
    /// First-scene gate for the Builda WebGL player: Addressables catalog, privateKV hydrate,
    /// then <c>whoami</c>. The host <c>runtime.ready</c> call is fired on success, before
    /// <paramref name="onSuccess"/>, so the menu is not interactive until the host is told.
    /// </summary>
    public static void RunBoot(Action onSuccess, Action onAbandon = null)
    {
        PlayerDisplayName = string.Empty;
        var tasks = new List<LoadingTask>
        {
            new LoadingTask("Connecting to content...", task => InitCatalogTask(task)),
            new LoadingTask("Loading save data...", PullSaveTask),
            new LoadingTask("Signing in...", WhoamiTask),
        };
        Run(tasks, () =>
        {
            NotifyHostReady();
            onSuccess?.Invoke();
        }, onAbandon);
    }

    /// <summary>
    /// The battle gate: catalog + every asset the pending level needs, then the scene switch.
    /// </summary>
    public static void RunBattle(string sceneName, Action onAbandon = null)
    {
        LoadingPage page = null;

        var tasks = new List<LoadingTask>
        {
            new LoadingTask("Connecting to content...", task => InitCatalogTask(task)),
            new LoadingTask("Downloading battle assets...", task => PrewarmBattleTask(task, () => page)),
        };

        PrewarmGate gate = EnsureInstance();
        gate.StartCoroutine(gate.RunRoutine(
            tasks,
            () => gate.StartCoroutine(gate.SwitchSceneRoutine(sceneName)),
            onAbandon,
            p => page = p,
            cleanupOnSuccess: false));
    }

    private static IEnumerator InitCatalogTask(LoadingTask task)
    {
        if (BundledAddressables.IsReady)
        {
            task.Success = true;
            yield break;
        }

        // No heartbeat here on purpose. This is one opaque round-trip for a small catalog file with
        // no sub-steps to report, so the plain timeout is the correct watchdog: pumping it every
        // frame would mean a hung connection never fails.
        bool ok = false;
        yield return BundledAddressables.InitializeRoutine(result => ok = result);
        task.Success = ok;
    }

    private static IEnumerator PullSaveTask(LoadingTask task)
    {
        if (BuildaSaveBackend.IsLoaded)
        {
            task.Success = true;
            yield break;
        }

        bool ok = false;
        yield return BuildaSaveBackend.PullAllRoutine(
            SaveKeys.AllKeys(),
            result => ok = result,
            () => task.ReportProgress?.Invoke());
        task.Success = ok;
    }

    private static IEnumerator WhoamiTask(LoadingTask task)
    {
        PlayerDisplayName = string.Empty;

#if UNITY_WEBGL && !UNITY_EDITOR
        bool done = false;
        BuildaResult response = null;
        BuildaSDK.Whoami(r => { response = r; done = true; });
        while (!done) yield return null;

        if (response != null && response.Ok)
        {
            var map = response.DataMap;
            if (map != null && map.TryGetValue("name", out object raw) && raw is string name)
                PlayerDisplayName = name.Trim();
            task.Success = true;
            yield break;
        }

        // A missing display name is not worth blocking entry: identity is already the host
        // session, and the save pull has succeeded. The welcome line just falls back.
        Debug.LogWarning($"[PrewarmGate] whoami failed: {Describe(response)}");
        task.Success = true;
        yield break;
#else
        task.Success = true;
        yield break;
#endif
    }

    private static void NotifyHostReady()
    {
#if UNITY_WEBGL && !UNITY_EDITOR
        BuildaSDK.RuntimeReady(r =>
        {
            if (r != null && r.Ok) return;
            Debug.LogWarning($"[PrewarmGate] RuntimeReady failed: {Describe(r)}");
        });
#endif
    }

#if UNITY_WEBGL && !UNITY_EDITOR
    private static string Describe(BuildaResult r)
    {
        if (r == null) return "no result";
        if (r.Error == null) return "unknown error";
        return $"{r.Error.Code} {r.Error.Message}";
    }
#endif

    private static IEnumerator PrewarmBattleTask(LoadingTask task, Func<LoadingPage> pageAccessor)
    {
        LevelData resolved = null;
        LoadingPage page = pageAccessor?.Invoke();

        yield return BattlePrewarm.PrewarmCurrentBattleRoutine(
            (progress, label) =>
            {
                // Every completed asset is proof the download is alive, so the stall watchdog is
                // reset here. A big battle can legitimately take much longer than one asset's
                // timeout budget; only a genuine stall should fail it.
                task.ReportProgress?.Invoke();

                if (page == null) page = pageAccessor?.Invoke();
                if (page == null) return;
                page.SetDetail(string.IsNullOrEmpty(label)
                    ? $"{Mathf.RoundToInt(progress * 100f)}%"
                    : $"{Mathf.RoundToInt(progress * 100f)}%  {label}");
            },
            ld => resolved = ld);

        // A missing LevelData is a content bug, not a network problem - but it still has to stop the
        // transition, otherwise the battle scene starts with a null level and throws on Start().
        task.Success = resolved != null;
    }

    private IEnumerator RunRoutine(
        List<LoadingTask> tasks,
        Action onSuccess,
        Action onAbandon,
        Action<LoadingPage> onPageReady = null,
        bool cleanupOnSuccess = true)
    {
        if (!ShowLoadingPage())
        {
            // No loading UI available - fail loudly rather than silently entering a broken scene.
            Debug.LogError($"[PrewarmGate] Missing loading prefab at '{LoadingPagePath}'.");
            onAbandon?.Invoke();
            Cleanup();
            yield break;
        }

        onPageReady?.Invoke(loadingPage);

        bool done = false;
        bool succeeded = false;
        loadingPage.Initialize(tasks, ok => { succeeded = ok; done = true; });
        while (!done) yield return null;

        if (!succeeded)
        {
            onAbandon?.Invoke();
            Cleanup();
            yield break;
        }

        onSuccess?.Invoke();

        // Scene-switching callers keep the page alive so the player never sees a half-built scene;
        // they call Cleanup() themselves once the switch finishes.
        if (cleanupOnSuccess) Cleanup();
    }

    private IEnumerator SwitchSceneRoutine(string sceneName)
    {
        if (loadingPage != null) loadingPage.SetDetail("Entering battle...");

        AsyncOperation op = UnityEngine.SceneManagement.SceneManager.LoadSceneAsync(sceneName);
        if (op != null)
        {
            while (!op.isDone)
            {
                if (loadingPage != null)
                    loadingPage.SetDetail($"Entering battle... {Mathf.RoundToInt(op.progress * 100f)}%");
                yield return null;
            }
        }

        Cleanup();
    }

    private bool ShowLoadingPage()
    {
        if (loadingPage != null) return true;

        GameObject prefab = Resources.Load<GameObject>(LoadingPagePath);
        if (prefab == null) return false;

        GameObject obj = Instantiate(prefab);
        DontDestroyOnLoad(obj);
        loadingPage = obj.GetComponent<LoadingPage>();
        if (loadingPage == null)
        {
            Destroy(obj);
            return false;
        }
        return true;
    }

    private void Cleanup()
    {
        // LoadingPage.AbandonAll already destroys itself; the null check keeps this idempotent.
        if (loadingPage != null)
        {
            Destroy(loadingPage.gameObject);
            loadingPage = null;
        }
        if (instance == this) instance = null;
        Destroy(gameObject);
    }
}
