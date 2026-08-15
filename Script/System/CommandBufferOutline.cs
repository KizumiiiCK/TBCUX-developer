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
    [SerializeField] private Shader outlineShader;

    private CommandBuffer cb;
    private Material mat;
    private Camera cam;
    private Renderer[] targetRenderers = System.Array.Empty<Renderer>();

    // AfterEverything 在编辑器 Game 视图还能画到屏幕上，但 Win/安卓实机此时已经提交，Blit 不会显示。
    private const CameraEvent OutlineEvent = CameraEvent.AfterImageEffects;

    // RT 名字
    private readonly int _Silhouette = Shader.PropertyToID("_Silhouette");

    void OnEnable()
    {
        CollectTargetRenderers();
        TryInitCommandBuffer();
    }

    void OnDisable()
    {
        if (cam != null && cb != null) cam.RemoveCommandBuffer(OutlineEvent, cb);
        cb?.Release();
        cb = null;
        if (mat != null) Destroy(mat);
        mat = null;
        cam = null;
    }

    void TryInitCommandBuffer()
    {
        if (cb != null && mat != null && cam != null) return;

        cam = Camera.main;
        if (cam == null) return;

        Shader shader = outlineShader != null ? outlineShader : Shader.Find("Hidden/CharacterOutline");
        if (shader == null)
        {
            Material resourceMat = Resources.Load<Material>("Effects/CharacterOutline");
            if (resourceMat != null) shader = resourceMat.shader;
        }
        if (shader == null)
        {
            Debug.LogError("找不到 Hidden/CharacterOutline Shader");
            return;
        }

        if (mat == null) mat = new Material(shader);
        if (cb == null) cb = new CommandBuffer { name = "SimpleOutline" };
        cam.AddCommandBuffer(OutlineEvent, cb);
    }

    void Update()
    {
        if (cb == null || cam == null || mat == null)
        {
            TryInitCommandBuffer();
            if (cb == null || cam == null || mat == null) return;
        }
        CollectTargetRenderers();
        if (targetRenderers == null || targetRenderers.Length == 0) return;

        int w = Mathf.Max(1, cam.pixelWidth);
        int h = Mathf.Max(1, cam.pixelHeight);

        cb.Clear();

        // 1. 创建 RT
        cb.GetTemporaryRT(_Silhouette, w, h, 0, FilterMode.Bilinear, RenderTextureFormat.ARGB32);

        // 2. 渲染轮廓
        cb.SetRenderTarget(_Silhouette);
        cb.ClearRenderTarget(false, true, Color.clear);
        bool hasAnyRenderable = false;
        Texture silhouetteTex = null;
        for (int i = 0; i < targetRenderers.Length; i++)
        {
            Renderer renderer = targetRenderers[i];
            if (!IsRendererEligible(renderer)) continue;
            if (silhouetteTex == null) silhouetteTex = GetRendererMainTexture(renderer);
            cb.DrawRenderer(renderer, mat, 0, 0);
            hasAnyRenderable = true;
        }
        if (silhouetteTex != null) mat.SetTexture("_MainTex", silhouetteTex);
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

    private static Texture GetRendererMainTexture(Renderer r)
    {
        if (r is SpriteRenderer sr && sr.sprite != null) return sr.sprite.texture;
        Material src = r.sharedMaterial;
        return src != null ? src.mainTexture : null;
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