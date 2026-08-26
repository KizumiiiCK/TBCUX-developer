using System;
using System.Collections;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class LoadingPage : MonoBehaviour
{
    [Header("UI")]
    [SerializeField] private TMP_Text statusText;
    [SerializeField] private TMP_Text detailText;
    [SerializeField] private Button retryButton;
    [SerializeField] private Button abandonButton;

    [Header("Timing")]
    [Tooltip("Fail a task after this many seconds with no progress at all. This is a stall timeout, " +
             "not a total time limit - a task that keeps reporting progress may run indefinitely.")]
    [SerializeField] private float timeoutSeconds = 30f;

    private readonly List<LoadingTask> tasks = new List<LoadingTask>();
    private int currentIndex = 0;
    private Coroutine taskRoutine;
    private Coroutine timeoutRoutine;
    private bool waitingChoice = false;
    private float stallSeconds;
    private Action<bool> onAllComplete;

    private void Awake()
    {
        if (retryButton != null) retryButton.onClick.AddListener(RetryCurrent);
        if (abandonButton != null) abandonButton.onClick.AddListener(AbandonAll);
        SetButtonsVisible(false);
    }

    public void Initialize(List<LoadingTask> loadingTasks, Action<bool> onComplete = null)
    {
        tasks.Clear();
        if (loadingTasks != null) tasks.AddRange(loadingTasks);
        onAllComplete = onComplete;
        currentIndex = 0;
        SetDetail(string.Empty);
        StartTasks();
    }

    public void AddTask(LoadingTask task)
    {
        if (task != null) tasks.Add(task);
    }

    public void SetTimeoutSeconds(float seconds)
    {
        timeoutSeconds = Mathf.Max(5f, seconds);
    }

    private void StartTasks()
    {
        if (taskRoutine != null) StopCoroutine(taskRoutine);
        taskRoutine = StartCoroutine(RunTasks());
    }

    private IEnumerator RunTasks()
    {
        while (currentIndex < tasks.Count)
        {
            var task = tasks[currentIndex];
            if (task == null || task.Routine == null)
            {
                FailCurrent("UNKNOWN TASK...");
                yield break;
            }

            UpdateStatus(task.Message);
            task.Reset();
            // Let the routine restart its own stall countdown as it advances, without needing a
            // reference back to this page.
            task.ReportProgress = NotifyProgress;

            StartTimeout();
            yield return StartCoroutine(task.Routine(task));
            StopTimeout();

            if (!task.Success)
            {
                FailCurrent("Connection Failed...");
                yield break;
            }

            task.OnComplete?.Invoke(true, task.Result);
            currentIndex++;
        }

        UpdateStatus("Success!");
        SetDetail("Success!");
        onAllComplete?.Invoke(true);
    }

    private void StartTimeout()
    {
        if (timeoutRoutine != null) StopCoroutine(timeoutRoutine);
        timeoutRoutine = StartCoroutine(TimeoutRoutine());
    }

    private void StopTimeout()
    {
        if (timeoutRoutine != null) StopCoroutine(timeoutRoutine);
        timeoutRoutine = null;
    }

    private IEnumerator TimeoutRoutine()
    {
        stallSeconds = 0f;
        while (stallSeconds < timeoutSeconds)
        {
            stallSeconds += Time.unscaledDeltaTime;
            yield return null;
        }
        FailCurrent("Connection Failed...");
    }

    /// <summary>
    /// Reports that the current task is still making progress, restarting its stall countdown.
    /// A battle prewarm downloads hundreds of assets under one task, so the watchdog has to measure
    /// "nothing happened for N seconds" rather than total elapsed time - otherwise a slow but
    /// perfectly healthy download fails and the player is asked to restart it from scratch.
    /// </summary>
    public void NotifyProgress()
    {
        stallSeconds = 0f;
    }

    private void RetryCurrent()
    {
        if (!waitingChoice) return;
        waitingChoice = false;
        SetButtonsVisible(false);
        UpdateStatus("Retrying...");
        StartTasks();
    }

    private void AbandonAll()
    {
        if (!waitingChoice) return;
        waitingChoice = false;
        SetButtonsVisible(false);

        for (int i = currentIndex; i < tasks.Count; i++)
        {
            tasks[i]?.OnComplete?.Invoke(false, null);
        }

        onAllComplete?.Invoke(false);
        Destroy(gameObject);
    }

    public void NotifyFailure(string message = "Connection Failed...")
    {
        FailCurrent(message);
    }

    private void FailCurrent(string message)
    {
        if (waitingChoice) return;
        waitingChoice = true;
        StopTimeout();
        if (taskRoutine != null)
        {
            StopCoroutine(taskRoutine);
            taskRoutine = null;
        }

        var task = currentIndex < tasks.Count ? tasks[currentIndex] : null;
        task?.OnCancel?.Invoke();
        task?.OnComplete?.Invoke(false, null);

        UpdateStatus(message);
        SetDetail(message);
        SetButtonsVisible(true);
    }

    public void SetDetail(string text)
    {
        if (detailText != null) detailText.text = text ?? string.Empty;
    }

    private void UpdateStatus(string text)
    {
        if (statusText != null) statusText.text = text ?? string.Empty;
    }

    private void SetButtonsVisible(bool visible)
    {
        if (retryButton != null) retryButton.GetComponent<RectTransform>().anchoredPosition = new Vector2(-200, visible?-300:-1000);
        if (abandonButton != null) abandonButton.GetComponent<RectTransform>().anchoredPosition = new Vector2(200, visible?-300:-1000);
    }
}

[Serializable]
public class LoadingTask
{
    public string Message;
    public Func<LoadingTask, IEnumerator> Routine;
    public Action<bool, object> OnComplete;
    public Action OnCancel;

    [NonSerialized] public bool Success;
    [NonSerialized] public object Result;

    /// <summary>
    /// Assigned by <see cref="LoadingPage"/> before the routine runs. Long tasks should call this
    /// each time a unit of work finishes so the stall watchdog knows the task is alive.
    /// Safe to invoke via <c>?.</c> - it is null if the task is run outside a LoadingPage.
    /// </summary>
    [NonSerialized] public Action ReportProgress;

    public LoadingTask(string message, Func<LoadingTask, IEnumerator> routine, Action<bool, object> onComplete = null)
    {
        Message = message;
        Routine = routine;
        OnComplete = onComplete;
    }

    public void Reset()
    {
        Success = false;
        Result = null;
    }
}
