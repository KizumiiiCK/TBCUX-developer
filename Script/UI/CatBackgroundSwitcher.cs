using System;
using System.Collections;
using UnityEngine;
using UnityEngine.UI;

public class CatBackgroundSwitcher : MonoBehaviour
{
    [SerializeField] private float autoBackgroundInterval = 15f;
    [SerializeField] private float manualPauseDuration = 60f;
    [SerializeField] private float switchFxDuration = 1f;

    private Image backgroundImage;
    private Material ghostMaterialTemplate;
    private Material runtimeGhostMaterial;
    private Material cachedOriginalMaterial;
    private Action onBackgroundApplied;

    private Coroutine autoRoutine;
    private Coroutine transitionRoutine;
    private float nextAutoSwitchTime;

    public void Initialize(Image targetBackground, Material ghostTemplate, Action onApplied)
    {
        backgroundImage = targetBackground;
        ghostMaterialTemplate = ghostTemplate;
        onBackgroundApplied = onApplied;
        StartAutoSwitching();
    }

    public void ApplyCurrentBackgroundImmediate()
    {
        int bgNum = PlayerPrefs.GetInt(UXPref.Localized_BGnum, 0);
        ApplyBackgroundImmediate(bgNum);
    }

    public void StartAutoSwitching()
    {
        nextAutoSwitchTime = Time.unscaledTime + autoBackgroundInterval;
        if (autoRoutine != null) StopCoroutine(autoRoutine);
        autoRoutine = StartCoroutine(AutoRandomBackgroundRoutine());
    }

    private void OnEnable()
    {
        ChangeBGPage.OnBackgroundSelected -= OnManualBackgroundSelected;
        ChangeBGPage.OnBackgroundSelected += OnManualBackgroundSelected;
    }

    private void OnDisable()
    {
        ChangeBGPage.OnBackgroundSelected -= OnManualBackgroundSelected;
        StopAllSwitchRoutines();
    }

    private void OnDestroy()
    {
        if (runtimeGhostMaterial != null) Destroy(runtimeGhostMaterial);
    }

    private IEnumerator AutoRandomBackgroundRoutine()
    {
        while (true)
        {
            if (Time.unscaledTime >= nextAutoSwitchTime)
            {
                int randomBg = GetRandomBackgroundDifferentFromCurrent();
                StartBackgroundTransition(randomBg);
                nextAutoSwitchTime = Time.unscaledTime + autoBackgroundInterval;
            }
            yield return null;
        }
    }

    private void OnManualBackgroundSelected(int bgNum)
    {
        StartBackgroundTransition(bgNum);
        nextAutoSwitchTime = Time.unscaledTime + manualPauseDuration;
    }

    private int GetRandomBackgroundDifferentFromCurrent()
    {
        int current = PlayerPrefs.GetInt(UXPref.Localized_BGnum, 0);
        int random = ChangeBGPage.GetRandomBackgroundNumber();
        if (ChangeBGPage.BG_nums == null || ChangeBGPage.BG_nums.Length <= 1) return random;

        int guard = 0;
        while (random == current && guard < 10)
        {
            random = ChangeBGPage.GetRandomBackgroundNumber();
            guard++;
        }
        return random;
    }

    private void StartBackgroundTransition(int bgNum)
    {
        if (backgroundImage == null)
        {
            ApplyBackgroundImmediate(bgNum);
            return;
        }

        RestoreBackgroundMaterialIfNeeded();
        if (transitionRoutine != null) StopCoroutine(transitionRoutine);
        transitionRoutine = StartCoroutine(BackgroundTransitionRoutine(bgNum));
    }

    private IEnumerator BackgroundTransitionRoutine(int bgNum)
    {
        Material ghost = GetRuntimeGhostMaterial();
        if (ghost == null)
        {
            ApplyBackgroundImmediate(bgNum);
            yield break;
        }

        cachedOriginalMaterial = backgroundImage.material;
        backgroundImage.material = ghost;

        float t = 0f;
        bool switched = false;
        while (t < switchFxDuration)
        {
            t += Time.unscaledDeltaTime;
            float p = Mathf.Clamp01(t / switchFxDuration);
            ApplyGhostFlicker(ghost, t);

            if (!switched && p >= 0.5f)
            {
                ApplyBackgroundImmediate(bgNum);
                switched = true;
            }

            yield return null;
        }

        if (!switched) ApplyBackgroundImmediate(bgNum);
        RestoreBackgroundMaterialIfNeeded();
        transitionRoutine = null;
    }

    private void ApplyGhostFlicker(Material mat, float time)
    {
        if (mat == null) return;

        float blend = 0.55f + Mathf.PingPong(time * 8f, 0.45f);
        float boost = 0.1f + Mathf.PingPong(time * 6f, 1.2f);
        float transparency = 0.15f + Mathf.PingPong(time * 7f, 0.35f);

        if (mat.HasProperty("_GhostBlend")) mat.SetFloat("_GhostBlend", blend);
        if (mat.HasProperty("_GhostColorBoost")) mat.SetFloat("_GhostColorBoost", boost);
        if (mat.HasProperty("_GhostTransparency")) mat.SetFloat("_GhostTransparency", transparency);
    }

    private Material GetRuntimeGhostMaterial()
    {
        if (runtimeGhostMaterial != null) return runtimeGhostMaterial;

        if (ghostMaterialTemplate != null)
        {
            runtimeGhostMaterial = new Material(ghostMaterialTemplate);
            return runtimeGhostMaterial;
        }

        Shader ghostShader = Shader.Find("Hidden/Ghost");
        if (ghostShader == null) return null;

        runtimeGhostMaterial = new Material(ghostShader);
        if (runtimeGhostMaterial.HasProperty("_GhostBlend")) runtimeGhostMaterial.SetFloat("_GhostBlend", 1f);
        if (runtimeGhostMaterial.HasProperty("_GhostColorBoost")) runtimeGhostMaterial.SetFloat("_GhostColorBoost", 0f);
        if (runtimeGhostMaterial.HasProperty("_GhostTransparency")) runtimeGhostMaterial.SetFloat("_GhostTransparency", 0.25f);
        return runtimeGhostMaterial;
    }

    private void ApplyBackgroundImmediate(int bgNum)
    {
        PlayerPrefs.SetInt(UXPref.Localized_BGnum, bgNum);
        if (backgroundImage != null)
        {
            backgroundImage.sprite = Resources.Load<Sprite>($"Background/Maps/{bgNum}");
        }
        onBackgroundApplied?.Invoke();
    }

    private void RestoreBackgroundMaterialIfNeeded()
    {
        if (backgroundImage == null) return;
        if (cachedOriginalMaterial == null) return;
        backgroundImage.material = cachedOriginalMaterial;
        cachedOriginalMaterial = null;
    }

    private void StopAllSwitchRoutines()
    {
        RestoreBackgroundMaterialIfNeeded();
        if (autoRoutine != null) StopCoroutine(autoRoutine);
        if (transitionRoutine != null) StopCoroutine(transitionRoutine);
        autoRoutine = null;
        transitionRoutine = null;
    }
}
