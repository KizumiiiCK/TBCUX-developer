using System.Collections;
using TMPro;
using Unity.VisualScripting;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

public enum KiOutfit
{
    Border,
    Panel,
    TransparentBorder,
    TransparentCenter
}

[ExecuteAlways]
[RequireComponent(typeof(RectTransform))]
public class KiButton : Button
{
    [Header("Structure")]
    [SerializeField] private Transform frameRoot;
    [SerializeField] private TMP_Text label;
    [SerializeField] private Image cover;

    private static float PRESS_SCALE = 0.9f;
    //private static float PRESS_DURATION = 0.1f;
    private static float PRESS_FADE = 0.75f;
    private static int FIXED_BORDER_WIDTH = 100;
    private static float RESIZE_SCALER = 0.5f;

    [Header("Outfit")]
    [SerializeField] private KiOutfit initialOutfit = KiOutfit.Border;
    [SerializeField] private int initialType = 0;
    [SerializeField] private Color initialColor = Color.white;
    [SerializeField] private Vector2 initialSize = new Vector2(400, 60);
    [SerializeField] private bool rotateToRhombus = false;

    private Image[] frameImages;
    private Color[] frameOriginalColors;
    private Color labelOriginalColor;
    private float labelOriginalSize;
    private Coroutine notifyCoroutine;
    private RectTransform cachedRectTransform;
    private Vector3 cachedNormalScale = Vector3.one;
    private Vector3 pressedOriginalScale = Vector3.one;
    private bool hasPressedOriginalScale;

    protected override void Awake()
    {
        base.Awake();
        CacheRefs();
        CacheRectTransform();
        InitializeOutfit();
        if (cachedRectTransform != null) cachedNormalScale = cachedRectTransform.localScale;
    }

    protected override void OnEnable()
    {
        base.OnEnable();
        CacheRefs();
        CacheRectTransform();
        EnsureNormalVisualState();
    }

    protected override void OnDisable()
    {
        EnsureNormalVisualState();
        base.OnDisable();
    }
    private void OnValidate()
    {
        if (Application.isPlaying) return;
        CacheRefs();
        InitializeOutfit();
    }
    private void CacheRefs()
    {
        if (frameRoot == null)
        {
            var t = transform.Find("Frame");
            if (t != null) frameRoot = t;
        }
        if (label == null)
        {
            label = GetComponentInChildren<TMP_Text>(true);
        }
        if (cover == null)
        {
            var t = transform.Find("Cover");
            if (t != null) cover = t.GetComponent<Image>();
        }

        if (frameRoot != null)
        {
            frameImages = GetFrameImagesOrdered(frameRoot);
            frameOriginalColors = new Color[frameImages.Length];
            for (int i = 0; i < frameImages.Length; i++)
                frameOriginalColors[i] = frameImages[i].color;
        }
        else
        {
            frameImages = new Image[0];
            frameOriginalColors = new Color[0];
        }
    }

    private void CacheRectTransform()
    {
        if (cachedRectTransform == null) cachedRectTransform = GetComponent<RectTransform>();
    }

    public override void OnPointerClick(PointerEventData eventData)
    {
        Onclick_Notify();
        base.OnPointerClick(eventData);
    }

    // Appearance
    public void SetOutfit(KiOutfit outfit, int type)
    {
        CacheRefs();
        ApplyOutfitSprites(outfit, type);
    }

    private void InitializeOutfit()
    {
        if (frameImages == null || frameImages.Length == 0) return;
        SetOutfit(initialOutfit, initialType);
        ApplyInitialColor();
        ApplyInitialSize();
        ApplyInitialRotation();
        if (label != null)
        {
            labelOriginalColor = label.color;
            labelOriginalSize = label.fontSize;
        }
    }

    private void ApplyInitialColor()
    {
        ApplyFrameColor(initialColor);
    }

    private void ApplyInitialSize()
    {
        SetSize((int)initialSize.x, (int)initialSize.y);
    }

    private void ApplyInitialRotation()
    {
        if (frameRoot != null)
            frameRoot.localRotation = rotateToRhombus ? Quaternion.Euler(0f, 0f, -45f) : Quaternion.identity;
        if (cover != null)
            cover.transform.localRotation = rotateToRhombus ? Quaternion.Euler(0f, 0f, -45f) : Quaternion.identity;
    }

    public void SetSize(int width, int height)
    {
        if (width < 0 || height < 0) return;
        CacheRefs();
        int count=frameImages.Length;
        RectTransform[] rt = new RectTransform[count];
        for(int i = 0; i <count; i++) rt[i] = frameImages[i].GetComponent<RectTransform>();
        rt[0].anchoredPosition = new Vector2(-width, height);
        rt[1].anchoredPosition= new Vector2(0, height);
        rt[1].sizeDelta = new Vector2(width / RESIZE_SCALER, FIXED_BORDER_WIDTH);
        rt[2].anchoredPosition = new Vector2(width, height);
        rt[3].anchoredPosition = new Vector2(-width, 0);
        rt[3].sizeDelta = new Vector2(FIXED_BORDER_WIDTH, height / RESIZE_SCALER);
        rt[4].sizeDelta = new Vector2(width, height);
        rt[5].anchoredPosition = new Vector2(width, 0);
        rt[5].sizeDelta = new Vector2(FIXED_BORDER_WIDTH, height / RESIZE_SCALER);
        rt[6].anchoredPosition = new Vector2(-width, -height);
        rt[7].anchoredPosition = new Vector2(0, -height);
        rt[7].sizeDelta = new Vector2(width / RESIZE_SCALER, FIXED_BORDER_WIDTH);
        rt[8].anchoredPosition = new Vector2(width, -height);
        if(cover!=null){
            cover.GetComponent<RectTransform>().sizeDelta = new Vector2(width+FIXED_BORDER_WIDTH, height+FIXED_BORDER_WIDTH);
        }
    }

    public void SetText(string text, int size = -1, Color? c = null)
    {
        CacheRefs();
        if (label == null) return;
        label.text = text ?? string.Empty;
        label.fontSize = size > 0 ? size : labelOriginalSize;
        if (c.HasValue) label.color = c.Value;
    }

    public string GetText()
    {
        CacheRefs();
        return label != null ? label.text : string.Empty;
    }

    public Color GetInitialColor()
    {
        return initialColor;
    }

    public void SetFrameColorPersistent(Color c)
    {
        CacheRefs();
        initialColor = c;
        ApplyFrameColor(c);
        if (frameImages == null) return;
        if (frameOriginalColors == null || frameOriginalColors.Length != frameImages.Length)
            frameOriginalColors = new Color[frameImages.Length];
        for (int i = 0; i < frameImages.Length; i++)
            frameOriginalColors[i] = frameImages[i].color;
    }

    public void SetCover(Sprite s)
    {
        CacheRefs();
        cover.sprite = s;
        cover.gameObject.SetActive(s != null);
    }

    public void SetCoverColor(Color color)
    {
        CacheRefs();
        if (cover == null) return;
        cover.color = color;
    }

    // Event
    public void Onclick_Notify()
    {
        EnsureNormalVisualState();
        notifyCoroutine = StartCoroutine(NotifyRoutine());
    }

    public void Onclick_Event()
    {
        onClick.Invoke();
    }

    private IEnumerator NotifyRoutine()
    {
        CacheRectTransform();
        if (cachedRectTransform == null)
        {
            notifyCoroutine = null;
            yield break;
        }

        Vector3 originalScale = cachedRectTransform.localScale;
        pressedOriginalScale = originalScale;
        hasPressedOriginalScale = true;
        cachedNormalScale = originalScale;

        ApplyFrameFade(PRESS_FADE);

        cachedRectTransform.localScale = originalScale * PRESS_SCALE;
        yield return new WaitForFixedUpdate();
        cachedRectTransform.localScale = originalScale * PRESS_SCALE;
        yield return new WaitForFixedUpdate();

        RestoreFrameColors();
        cachedRectTransform.localScale = originalScale;
        hasPressedOriginalScale = false;
        notifyCoroutine = null;
    }

    private void EnsureNormalVisualState()
    {
        if (notifyCoroutine != null)
        {
            StopCoroutine(notifyCoroutine);
            notifyCoroutine = null;
        }

        CacheRectTransform();
        RestoreFrameColors();

        if (cachedRectTransform != null)
        {
            cachedRectTransform.localScale = hasPressedOriginalScale ? pressedOriginalScale : cachedNormalScale;
            cachedNormalScale = cachedRectTransform.localScale;
        }

        hasPressedOriginalScale = false;
    }

    public void ApplyFrameColor(Color c)
    {
        if (frameImages == null) return;
        for (int i = 0; i < frameImages.Length; i++)
            frameImages[i].color = c;
    }

    private void ApplyFrameFade(float fade)
    {
        if (frameImages == null || frameOriginalColors == null) return;
        float t = Mathf.Clamp01(fade);
        for (int i = 0; i < frameImages.Length && i < frameOriginalColors.Length; i++)
        {
            Color original = frameOriginalColors[i];
            frameImages[i].color = new Color(original.r * t, original.g * t, original.b * t, original.a);
        }
    }

    private void RestoreFrameColors()
    {
        if (frameImages == null || frameOriginalColors == null) return;
        for (int i = 0; i < frameImages.Length && i < frameOriginalColors.Length; i++)
            frameImages[i].color = frameOriginalColors[i];
    }

    private void ApplyOutfitSprites(KiOutfit outfit, int type)
    {
        if (frameImages == null || frameImages.Length == 0) return;
        if(type < 0 || type > 31) type = 0;
        string path = $"kennyui/{outfit}/{type}";
        Sprite[] sprites = Resources.LoadAll<Sprite>(path);
        if (sprites == null || sprites.Length == 0) return;

        // Array.Sort(sprites, (a, b) => string.Compare(a.name, b.name, StringComparison.Ordinal));
        int count = Mathf.Min(frameImages.Length, sprites.Length);
        for (int i = 0; i < count; i++)
        {
            frameImages[i].sprite = sprites[i];
            frameImages[i].enabled = sprites[i] != null;
        }
    }

    private Image[] GetFrameImagesOrdered(Transform root)
    {
        int childCount = root.childCount;
        var images = new Image[childCount];
        for (int i = 0; i < childCount; i++)
        {
            var child = root.GetChild(i);
            images[i] = child.GetComponent<Image>();
        }
        return images;
    }
}
