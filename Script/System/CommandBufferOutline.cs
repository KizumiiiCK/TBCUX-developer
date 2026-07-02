using UnityEngine;
using UnityEngine.Rendering;

[DisallowMultipleComponent]
public class SimpleCommandBufferOutline : MonoBehaviour
{
    [Header("描边设置")]
    public Color outlineColor = new Color(0.196f, 0.933f, 1.000f, 1.000f);
    public Color highlightColor = Color.white;
    [Range(1, 12)] public int outlineWidth = 7;
    [Range(0f, 1f)] public float highlightStrength = 0.9f;
    public float scrollSpeed = 2.5f;
    public float waveFrequency = 28f;
    [Header("渲染过滤")]
    [Range(0f, 1f)] public float spriteAlphaThreshold = 0.15f;
    [Header("动态闪烁")]
    public bool enablePulse = true;
    public float pulseSpeed = 4f;
    [Range(0f, 2f)] public float pulseMin = 0.4f;
    [Range(0f, 2f)] public float pulseMax = 1.2f;

    private CommandBuffer cb;
    private Material mat;
    private Camera cam;
    private Renderer[] targetRenderers = System.Array.Empty<Renderer>();

    // RT 名字
    private readonly int _Silhouette = Shader.PropertyToID("_Silhouette");

    void OnEnable()
    {
        CollectTargetRenderers();
        cam = Camera.main;
        if (cam == null)
        {
            Debug.LogWarning("SimpleCommandBufferOutline: Camera.main 为空，无法创建描边命令缓冲。");
            return;
        }

        Shader shader = Shader.Find("Hidden/CharacterOutline");
        if (shader == null)
        {
            Debug.LogError("找不到 Hidden/CharacterOutline Shader");
            return;
        }

        mat = new Material(shader);
        cb = new CommandBuffer { name = "SimpleOutline" };

        cam.AddCommandBuffer(CameraEvent.AfterEverything, cb);
    }

    void OnDisable()
    {
        if (cam != null && cb != null) cam.RemoveCommandBuffer(CameraEvent.AfterEverything, cb);
        cb?.Release();
        if (mat != null) Destroy(mat);
    }

    void Update()
    {
        if (cb == null || cam == null || mat == null) return;
        CollectTargetRenderers();
        if (targetRenderers == null || targetRenderers.Length == 0) return;

        int w = Screen.width;
        int h = Screen.height;

        cb.Clear();

        // 1. 创建 RT
        cb.GetTemporaryRT(_Silhouette, w, h, 0, FilterMode.Bilinear, RenderTextureFormat.ARGB32);

        // 2. 渲染轮廓
        cb.SetRenderTarget(_Silhouette);
        cb.ClearRenderTarget(false, true, Color.clear);
        bool hasAnyRenderable = false;
        for (int i = 0; i < targetRenderers.Length; i++)
        {
            Renderer renderer = targetRenderers[i];
            if (!IsRendererEligible(renderer)) continue;
            cb.DrawRenderer(renderer, mat, 0, 0);
            hasAnyRenderable = true;
        }
        if (!hasAnyRenderable)
        {
            cb.ReleaseTemporaryRT(_Silhouette);
            return;
        }

        // 3. 设置参数并描边
        float pulse = 1f;
        if (enablePulse)
        {
            float t = 0.5f + 0.5f * Mathf.Sin(Time.unscaledTime * Mathf.Max(0.01f, pulseSpeed));
            float minV = Mathf.Min(pulseMin, pulseMax);
            float maxV = Mathf.Max(pulseMin, pulseMax);
            pulse = Mathf.Lerp(minV, maxV, t);
        }

        mat.SetColor("_OutlineColor", outlineColor);
        mat.SetColor("_HighlightColor", highlightColor);
        mat.SetFloat("_OutlineWidth", outlineWidth);
        mat.SetFloat("_HighlightStrength", highlightStrength);
        mat.SetFloat("_ScrollSpeed", scrollSpeed);
        mat.SetFloat("_WaveFrequency", waveFrequency);
        mat.SetFloat("_PulseIntensity", pulse);
        // 直接叠加到相机目标，使用 shader pass 的 Blend，避免黑底覆盖整屏
        cb.Blit(_Silhouette, BuiltinRenderTextureType.CameraTarget, mat, 1);

        // 4. 清理
        cb.ReleaseTemporaryRT(_Silhouette);
    }

    private void CollectTargetRenderers()
    {
        Renderer[] allRenderers = GetComponentsInChildren<Renderer>(true);
        if (allRenderers == null || allRenderers.Length == 0)
        {
            targetRenderers = System.Array.Empty<Renderer>();
            return;
        }

        System.Collections.Generic.List<Renderer> filtered = new System.Collections.Generic.List<Renderer>(allRenderers.Length);
        for (int i = 0; i < allRenderers.Length; i++)
        {
            Renderer r = allRenderers[i];
            if (IsRendererEligible(r))
            {
                filtered.Add(r);
            }
        }

        targetRenderers = filtered.Count > 0 ? filtered.ToArray() : System.Array.Empty<Renderer>();
    }

    public void RefreshTargets() => CollectTargetRenderers();
    private bool IsRendererEligible(Renderer r)
    {
        if (r == null || !r.enabled || !r.gameObject.activeInHierarchy) return false;

        bool isSupportedType = r is SpriteRenderer || r is MeshRenderer || r is SkinnedMeshRenderer;
        if (!isSupportedType) return false;

        if (r is SpriteRenderer sr)
        {
            if (sr.sprite == null) return false;
            if (sr.color.a < spriteAlphaThreshold) return false;
        }

        return true;
    }

    public void SetColor(Color c) => outlineColor = c;
    public void SetHighlightColor(Color c) => highlightColor = c;
    public void SetOutlineWidth(int width) => outlineWidth = Mathf.Clamp(width, 1, 12);
    public void SetStyle(Color baseColor, Color hiColor, int width)
    {
        outlineColor = baseColor;
        highlightColor = hiColor;
        outlineWidth = Mathf.Clamp(width, 1, 12);
    }
    public void SetPulse(bool enabled, float speed = 4f, float min = 0.65f, float max = 1.2f)
    {
        enablePulse = enabled;
        pulseSpeed = speed;
        pulseMin = min;
        pulseMax = max;
    }
    public void SetActive(bool a) => enabled = a;
}