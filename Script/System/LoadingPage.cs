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
    [SerializeField] private float timeoutSeconds = 30f;

    private readonly List<LoadingTask> tasks = new List<LoadingTask>();
    private int currentIndex = 0;
    private Coroutine taskRoutine;
    private Coroutine timeoutRoutine;
    private bool waitingChoice = false;
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
        float t = 0f;
        while (t < timeoutSeconds)
        {
            t += Time.unscaledDeltaTime;
            yield return null;
        }
        FailCurrent("Connection Failed...");
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
