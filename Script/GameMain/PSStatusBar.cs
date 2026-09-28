using System.Collections.Generic;
using UnityEngine;

public class PSStatusBar : MonoBehaviour
{
    [SerializeField] private SpriteRenderer ps_icon;
    [SerializeField] private SpriteRenderer empty_bar;
    [SerializeField] private SpriteRenderer filler_bar;
    [SerializeField] private Transform BarParent;

    private Color ps_color = Color.white;
    private Color icon_color = Color.white;
    private Color empty_color = Color.white;
    private float ps_alpha;
    private float ps_progress;
    private const float fadeT = 1f;
    private const float fadeCutoff = 0.01f;
    private Transform filler_tr;
    private Vector3 filler_scale = Vector3.one;

    private static readonly Dictionary<string, Sprite> iconCache = new Dictionary<string, Sprite>();
    // 状态条图标复用能力自己的图标，不再单独维护一套（名字形如 a-41）。
    private const string IconResourcePath = "EAIcons/";

    private void Awake()
    {
        filler_tr = filler_bar.transform;
        filler_scale = filler_tr.localScale;
        icon_color = ps_icon.color;
        empty_color = empty_bar.color;
        ps_color = filler_bar.color;
        HideAndSleep();
    }

    public void SetupPS(string iconname, Color color)
    {
        ps_color.r = color.r;
        ps_color.g = color.g;
        ps_color.b = color.b;
        Sprite sprite = LoadIcon(iconname);
        if (sprite != null) ps_icon.sprite = sprite;
        ApplyAlpha(ps_alpha);
    }

    public void SetPSValue(float p)
    {
        if (p < 0f) p = 0f;
        else if (p > 1f) p = 1f;
        ps_progress = p;
        filler_scale.x = p;
        filler_tr.localScale = filler_scale;
        RefreshFade();
    }

    private void FixedUpdate()
    {
        if (ps_alpha > fadeCutoff) Fade();
        else HideAndSleep();
    }

    private void RefreshFade()
    {
        if (!enabled) enabled = true;
        if (!ps_icon.enabled)
        {
            ps_icon.enabled = true;
            empty_bar.enabled = true;
            filler_bar.enabled = true;
        }
        ApplyAlpha(1f);
    }

    private void Fade()
    {
        float next = ps_alpha - fadeT * Time.deltaTime;
        if (next > fadeCutoff) ApplyAlpha(next);
        else HideAndSleep();
    }

    public void ReverseDirection(bool isCat)
    {
        Vector3 s = BarParent.localScale;
        s.x = isCat ? -1f : 1f;
        BarParent.localScale = s;
    }

    private void HideAndSleep()
    {
        ApplyAlpha(0f);
        ps_icon.enabled = false;
        empty_bar.enabled = false;
        filler_bar.enabled = false;
        enabled = false;
    }

    private void ApplyAlpha(float a)
    {
        if (a < 0f) a = 0f;
        ps_alpha = a;
        icon_color.a = a;
        empty_color.a = a;
        ps_color.a = a;
        ps_icon.color = icon_color;
        empty_bar.color = empty_color;
        filler_bar.color = ps_color;
    }

    private static Sprite LoadIcon(string iconname)
    {
        if (string.IsNullOrEmpty(iconname)) return null;
        if (iconCache.TryGetValue(iconname, out Sprite cached)) return cached;
        Sprite sprite = Resources.Load<Sprite>(IconResourcePath + iconname);
        iconCache.Add(iconname, sprite);
        return sprite;
    }
}
