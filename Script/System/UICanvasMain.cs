using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public abstract class UICanvasMain : MonoBehaviour
{
    [Header("FrameUI")]
    [SerializeField] private List<int> extraCurrencyIds = new List<int>();
    [Header("Audio")]
    [SerializeField] private AudioClip pageBgmClip;

    public FrameUIDisplayer FrameUI { get; private set; }
    public IReadOnlyList<int> ExtraCurrencyIds => extraCurrencyIds;
    public bool IsPageInProgress { get; private set; }

    public virtual void Initialize(FrameUIDisplayer frameUI)
    {
        FrameUI = frameUI;
    }

    public virtual string GetPageBgmName()
    {
        return pageBgmClip != null ? pageBgmClip.name : string.Empty;
    }

    public void SetPageInProgress(bool inProgress)
    {
        IsPageInProgress = inProgress;
        FrameUI?.RefreshBackButtonState();
    }

    protected virtual void OnDestroy()
    {
        if (IsPageInProgress) SetPageInProgress(false);
    }

    public abstract IEnumerator OnEnter();
    public abstract IEnumerator OnExit();
}
