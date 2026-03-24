using TMPro;
using UnityEngine;
using UnityEngine.UI;

[ExecuteAlways]
public class KiPanel : MonoBehaviour
{
    [Header("Structure")]
    [SerializeField] private Transform frameRoot;
    [SerializeField] private TMP_Text label;
    [SerializeField] private Image cover;

    private static int FIXED_BORDER_WIDTH = 100;
    private static float RESIZE_SCALER = 0.5f;

    [Header("Outfit")]
    [SerializeField] private KiOutfit initialOutfit = KiOutfit.Border;
    [SerializeField] private int initialType = 0;
    [SerializeField] private Color initialColor = Color.white;
    [SerializeField] private Vector2 initialSize = new Vector2(400, 400);
    [SerializeField] private bool screenSaveScaler = false;
    [SerializeField] private bool rotateToRhombus = false;

    private Image[] frameImages;
    private Color[] frameOriginalColors;
    private Color labelOriginalColor;
    private float labelOriginalSize;

    private void Awake()
    {
        CacheRefs();
        InitializeOutfit();
    }

    private void OnEnable()
    {
        CacheRefs();
        InitializeOutfit();
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
        Vector2 size = initialSize;
        if (screenSaveScaler)
        {
            float aspect = Camera.main != null ? Camera.main.aspect : 2f;
            size = new Vector2(size.x * (aspect / 2f), size.y);
        }
        SetSize((int)size.x, (int)size.y);
    }

    private void ApplyInitialRotation()
    {
        if (frameRoot != null)
            frameRoot.localRotation = rotateToRhombus ? Quaternion.Euler(0f, 0f, -45f) : Quaternion.identity;
        // if (cover != null)
        //     cover.transform.localRotation = rotateToRhombus ? Quaternion.Euler(0f, 0f, -45f) : Quaternion.identity;
    }

    public void SetSize(int width, int height)
    {
        if (width < 0 || height < 0) return;
        CacheRefs();
        int count = frameImages.Length;
        RectTransform[] rt = new RectTransform[count];
        for (int i = 0; i < count; i++) rt[i] = frameImages[i].GetComponent<RectTransform>();
        rt[0].anchoredPosition = new Vector2(-width, height);
        rt[1].anchoredPosition = new Vector2(0, height);
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

    public void SetCover(Sprite s)
    {
        CacheRefs();
        if (cover == null) return;
        cover.sprite = s;
        cover.enabled = s != null;
    }

    public void ApplyFrameColor(Color c)
    {
        if (frameImages == null) return;
        for (int i = 0; i < frameImages.Length; i++)
            frameImages[i].color = c;
    }

    private void ApplyOutfitSprites(KiOutfit outfit, int type)
    {
        if (frameImages == null || frameImages.Length == 0) return;
        string path = $"kennyui/{outfit}/{type}";
        Sprite[] sprites = Resources.LoadAll<Sprite>(path);
        if (sprites == null || sprites.Length == 0) return;

        System.Array.Sort(sprites, (a, b) => string.Compare(a.name, b.name, System.StringComparison.Ordinal));
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