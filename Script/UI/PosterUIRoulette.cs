using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems;

public class PosterUIRoulette : MonoBehaviour, IBeginDragHandler, IDragHandler, IEndDragHandler
{
    [Header("References")]
    [SerializeField] private RectTransform postersRoot;
    [SerializeField] private KiButton posterButtonPrefab;

    [Header("Layout")]
    [SerializeField] private float radius = 100f;
    [SerializeField] private float angleStep = 25f;
    [SerializeField] private float dragSensitivity = 0.2f;
    [SerializeField] private float smoothSpeed = 12f;
    [SerializeField] private float scaleSmoothSpeed = 14f;
    [SerializeField] private float unselectedAlpha = 0.45f;
    [SerializeField] private float lowerScale = 0.33f;

    private readonly List<KiButton> posterButtons = new List<KiButton>();
    private readonly List<Sprite> posterSprites = new List<Sprite>();
    private readonly List<float> currentScales = new List<float>();

    private Action<int> onSelectionChanged;
    private Action<int> onSelectedPosterClicked;
    private Action<int> onDragEnded;

    private float currentRotation;
    private float targetRotation;
    private bool dragging;
    private int selectedIndex = -1;

    public void Configure(RectTransform postersRoot, KiButton posterButtonPrefab)
    {
        if (postersRoot != null) this.postersRoot = postersRoot;
        if (posterButtonPrefab != null) this.posterButtonPrefab = posterButtonPrefab;
    }

    public void Initialize(Action<int> onSelectionChanged, Action<int> onSelectedPosterClicked)
    {
        this.onSelectionChanged = onSelectionChanged;
        this.onSelectedPosterClicked = onSelectedPosterClicked;
        if (postersRoot == null) postersRoot = transform as RectTransform;
    }

    public void SetOnDragEnded(Action<int> onDragEnded)
    {
        this.onDragEnded = onDragEnded;
    }

    public void SetPosters(IList<Sprite> posters, int defaultIndex = 0)
    {
        posterSprites.Clear();
        if (posters != null)
        {
            for (int i = 0; i < posters.Count; i++) posterSprites.Add(posters[i]);
        }

        RebuildPosterButtons();
        if (posterSprites.Count == 0)
        {
            selectedIndex = -1;
            currentRotation = 0f;
            targetRotation = 0f;
            return;
        }

        int clamped = Mathf.Clamp(defaultIndex, 0, posterSprites.Count - 1);
        currentRotation = clamped * angleStep;
        targetRotation = currentRotation;
        selectedIndex = clamped;
        for (int i = 0; i < currentScales.Count; i++)
        {
            currentScales[i] = GetTargetScale(i);
        }
        UpdateVisuals(true);
    }

    public int GetSelectedIndex()
    {
        return selectedIndex;
    }

    private void Update()
    {
        if (!dragging)
        {
            float t = 1f - Mathf.Exp(-smoothSpeed * Time.unscaledDeltaTime);
            currentRotation = Mathf.Lerp(currentRotation, targetRotation, t);
        }
        UpdateVisuals(false);
    }

    public void OnBeginDrag(PointerEventData eventData)
    {
        if (posterButtons.Count == 0) return;
        dragging = true;
    }

    public void OnDrag(PointerEventData eventData)
    {
        if (!dragging || posterButtons.Count == 0) return;
        currentRotation += eventData.delta.y * dragSensitivity;
        targetRotation = currentRotation;
    }

    public void OnEndDrag(PointerEventData eventData)
    {
        if (posterButtons.Count == 0) return;
        dragging = false;
        int nearest = GetNearestIndexByCurrentRotation();
        targetRotation = nearest * angleStep;
        onDragEnded?.Invoke(nearest);
    }

    private void RebuildPosterButtons()
    {
        if (postersRoot == null) postersRoot = transform as RectTransform;
        if (postersRoot == null || posterButtonPrefab == null) return;

        for (int i = posterButtons.Count - 1; i >= 0; i--)
        {
            if (posterButtons[i] != null) Destroy(posterButtons[i].gameObject);
        }
        posterButtons.Clear();
        currentScales.Clear();

        for (int i = 0; i < posterSprites.Count; i++)
        {
            int idx = i;
            KiButton btn = Instantiate(posterButtonPrefab, postersRoot);
            btn.transform.localScale = Vector3.one;
            btn.SetCover(posterSprites[i]);
            btn.onClick.RemoveAllListeners();
            btn.onClick.AddListener(() =>
            {
                if (idx != selectedIndex) return;
                onSelectedPosterClicked?.Invoke(idx);
            });
            posterButtons.Add(btn);
            currentScales.Add(0f);
        }
    }

    private void UpdateVisuals(bool forceNotify)
    {
        if (posterButtons.Count == 0) return;

        int nearest = GetNearestIndexByCurrentRotation();
        if (nearest != selectedIndex || forceNotify)
        {
            selectedIndex = nearest;
            onSelectionChanged?.Invoke(selectedIndex);
        }

        for (int i = 0; i < posterButtons.Count; i++)
        {
            var btn = posterButtons[i];
            if (btn == null) continue;

            float angle = currentRotation - i * angleStep;
            float rad = angle * Mathf.Deg2Rad;
            float x = radius * Mathf.Cos(rad);
            float y = radius * Mathf.Sin(rad);

            var rt = btn.GetComponent<RectTransform>();
            if (rt != null)
            {
                rt.anchoredPosition = new Vector2(x, y);
                rt.localEulerAngles = new Vector3(0f, 0f, angle);
                float targetScale = GetTargetScale(i);
                float t = 1f - Mathf.Exp(-scaleSmoothSpeed * Time.unscaledDeltaTime);
                currentScales[i] = Mathf.Lerp(currentScales[i], targetScale, t);
                rt.localScale = Vector3.one * currentScales[i];
            }

            bool selected = i == selectedIndex;
            btn.interactable = selected;
            btn.SetCoverColor(new Color(1f, 1f, 1f, selected ? 1f : unselectedAlpha));
        }
    }

    private int GetNearestIndexByCurrentRotation()
    {
        if (posterButtons.Count == 0) return -1;
        int nearest = 0;
        float minAbs = float.MaxValue;
        for (int i = 0; i < posterButtons.Count; i++)
        {
            float angle = currentRotation - i * angleStep;
            float abs = Mathf.Abs(angle);
            if (abs < minAbs)
            {
                minAbs = abs;
                nearest = i;
            }
        }
        return nearest;
    }

    private float GetTargetScale(int index)
    {
        if (index == selectedIndex) return 1f;
        float angle = currentRotation - index * angleStep;
        float absAngle = Mathf.Max(angleStep, Mathf.Abs(angle));
        float t = Mathf.InverseLerp(angleStep, 180f, absAngle);
        return Mathf.Lerp(lowerScale, 0f, t);
    }
}
