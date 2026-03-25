using System;
using UnityEngine;
using UnityEngine.EventSystems;

public class LevelDragSelector : MonoBehaviour, IBeginDragHandler, IDragHandler, IEndDragHandler
{
    [Header("Target")]
    [SerializeField] private RectTransform target;

    [Header("Drag")]
    [SerializeField] private float dragSensitivity = 1f;
    [SerializeField] private float moveSpeed = 5f;
    [SerializeField] private float settleThreshold = 100f;

    private bool dragging;
    private bool isConfigured;
    private bool lastSettledState = true;
    private int currentLevelIndex;
    private float minX;
    private float maxX;
    private float tileGap = 375f;
    private Vector2 desiredPosition;
    private Action<int> onLevelChanged;
    private Action<bool> onSettleStateChanged;

    public int CurrentLevelIndex => currentLevelIndex;
    public bool IsSettled => Mathf.Abs(desiredPosition.x - target.anchoredPosition.x) < settleThreshold;
    public float DesiredX => desiredPosition.x;

    private void Update()
    {
        if (!isConfigured || target == null) return;

        target.anchoredPosition = Vector2.Lerp(target.anchoredPosition, desiredPosition, Time.deltaTime * moveSpeed);

        int idx = PositionToLevelIndex(desiredPosition.x);
        if (idx != currentLevelIndex)
        {
            currentLevelIndex = idx;
            onLevelChanged?.Invoke(currentLevelIndex);
        }

        bool settled = IsSettled;
        if (settled != lastSettledState)
        {
            lastSettledState = settled;
            onSettleStateChanged?.Invoke(settled);
        }
    }

    public void Configure(
        RectTransform target,
        float minX,
        float maxX,
        float tileGap,
        float moveSpeed,
        Action<int> onLevelChanged,
        Action<bool> onSettleStateChanged = null)
    {
        this.target = target;
        this.minX = Mathf.Min(minX, maxX);
        this.maxX = Mathf.Max(minX, maxX);
        this.tileGap = Mathf.Max(1f, tileGap);
        this.moveSpeed = Mathf.Max(0.01f, moveSpeed);
        this.onLevelChanged = onLevelChanged;
        this.onSettleStateChanged = onSettleStateChanged;

        if (this.target != null)
        {
            desiredPosition = this.target.anchoredPosition;
            desiredPosition.x = ClampAndSnapX(desiredPosition.x);
            this.target.anchoredPosition = desiredPosition;
            currentLevelIndex = PositionToLevelIndex(desiredPosition.x);
            onLevelChanged?.Invoke(currentLevelIndex);
            lastSettledState = true;
            onSettleStateChanged?.Invoke(true);
        }
        else
        {
            desiredPosition = Vector2.zero;
            currentLevelIndex = 0;
            lastSettledState = true;
        }

        isConfigured = true;
    }

    public void SetBounds(float minX, float maxX)
    {
        this.minX = Mathf.Min(minX, maxX);
        this.maxX = Mathf.Max(minX, maxX);
        desiredPosition.x = ClampAndSnapX(desiredPosition.x);
        if (target != null) target.anchoredPosition = new Vector2(Mathf.Clamp(target.anchoredPosition.x, this.minX, this.maxX), target.anchoredPosition.y);
        UpdateLevelImmediate();
    }

    public void MoveToLevel(int levelIndex, bool immediate = false)
    {
        float x = -Mathf.Max(0, levelIndex) * tileGap;
        desiredPosition.x = ClampAndSnapX(x);
        if (target != null && immediate)
        {
            target.anchoredPosition = new Vector2(desiredPosition.x, target.anchoredPosition.y);
            UpdateLevelImmediate();
        }
    }

    private void UpdateLevelImmediate()
    {
        int idx = PositionToLevelIndex(desiredPosition.x);
        if (idx != currentLevelIndex)
        {
            currentLevelIndex = idx;
            onLevelChanged?.Invoke(currentLevelIndex);
        }
        bool settled = IsSettled;
        if (settled != lastSettledState)
        {
            lastSettledState = settled;
            onSettleStateChanged?.Invoke(settled);
        }
    }

    private float ClampAndSnapX(float x)
    {
        float clamped = Mathf.Clamp(x, minX, maxX);
        return Mathf.Round(clamped / tileGap) * tileGap;
    }

    private int PositionToLevelIndex(float x)
    {
        return Mathf.Max(0, Mathf.RoundToInt(-x / tileGap));
    }

    public void OnBeginDrag(PointerEventData eventData)
    {
        if (!isConfigured || target == null) return;
        dragging = true;
    }

    public void OnDrag(PointerEventData eventData)
    {
        if (!dragging || !isConfigured || target == null) return;
        desiredPosition = target.anchoredPosition + new Vector2(eventData.delta.x * dragSensitivity, 0f);
        desiredPosition.x = Mathf.Clamp(desiredPosition.x, minX, maxX);
    }

    public void OnEndDrag(PointerEventData eventData)
    {
        if (!isConfigured || target == null) return;
        dragging = false;
        desiredPosition.x = ClampAndSnapX(desiredPosition.x);
    }
}
