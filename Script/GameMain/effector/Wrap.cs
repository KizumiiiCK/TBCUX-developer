using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class Wrap : E
{
    private const int TransitionFrames = 30;
    private const float HiddenPosY = -1000f;
    private const float WrapLiftDistance = 8f;

    private Character character;
    private Transform visualRoot;
    private Animator visualAnimator;
    private bool originalAnimatorEnabled;
    private Vector3 originalVisualScale = Vector3.one;
    private Vector3 originalVisualLocalPosition = Vector3.zero;
    private Vector3 originalWorldPosition = Vector3.zero;
    private int originalAnimationSpeed = 1;
    private int originalFrameStep = 1;
    private int originalSpeed = 0;
    private int holdDurationFrames = 0;
    private AnimationDisplayer wrapDisplay;
    private string wrapEffectName;

    public override void EffectInitializer()
    {
        effectName = EffectName.wrap;
        character = etarget;
        if (character == null)
        {
            Destroy(this);
            return;
        }

        originalWorldPosition = character.transform.position;
        holdDurationFrames = Mathf.Max(0, duration);
        duration = holdDurationFrames + TransitionFrames * 2 + 2;
        originalFrameStep = Mathf.Max(1, character.GetFrameStep());
        originalAnimationSpeed = character.GetFrameStep() > 0 ? 1 : 0;
        originalSpeed = character.GetRealSpeed();
        wrapEffectName = character.IsCat() ? "wrap" : "wrap_e";

        visualRoot = ResolveVisualRoot(character);
        if (visualRoot != null)
        {
            originalVisualScale = visualRoot.localScale;
            originalVisualLocalPosition = visualRoot.localPosition;
        }

        StartCoroutine(Wrapping());
    }

    public override void RemoveEffect()
    {
        if (character == null) return;

        RestoreVisualAnimator();
        character.SetAnimationSpeed(originalAnimationSpeed);
        character.SetFrameStep(originalFrameStep);
        character.ChangeSpeed(originalSpeed);
        CharacterTargetManager.Instance.SetCharacterUndetectable(character, false);

        RestoreVisualPose();
        CleanupWrapAnim();
    }

    private IEnumerator Wrapping()
    {
        // 开始时等待1帧：若处于KB，直接移除
        yield return new WaitForFixedUpdate();
        if (character == null || character.IsOnKB())
        {
            Destroy(this);
            yield break;
        }

        // Interrupt current attack timeline before freezing frame step,
        // avoiding repeated attack checks while wrapped.
        character.AbortCurrentAttackForControl();

        character.SetAnimationSpeed(0);
        character.SetFrameStep(0);
        character.ChangeSpeed(0);
        FreezeVisualAnimator();
        CharacterTargetManager.Instance.SetCharacterUndetectable(character, true);

        // wrap_out：首帧播放0，30帧内缩小到0，并隐藏到不可检测位置
        PlayWrapAnim(0);
        
        for (int i = 0; i < TransitionFrames; i++)
        {
            if (character == null) yield break;
            float t = (i + 1f) / TransitionFrames;
            ApplyWrapVisual(visualRoot, originalVisualScale, originalVisualLocalPosition, 1f - t, t);
            yield return new WaitForFixedUpdate();
        }

        // 隐藏持续 duration 帧
        // character.transform.position = new Vector3(character.transform.position.x, HiddenPosY, character.transform.position.z);
        int holdFrames = holdDurationFrames;
        for (int i = 0; i < holdFrames; i++)
        {
            if (character == null) yield break;
            yield return new WaitForFixedUpdate();
        }

        // wrap_in：首帧播放1，移动 intensity/100 距离，30帧恢复缩放
        float dx = intensity / 100f;
        Vector3 targetPos = new Vector3(originalWorldPosition.x + dx, originalWorldPosition.y, originalWorldPosition.z);
        character.transform.position = targetPos;
        PlayWrapAnim(1);
        for (int i = 0; i < TransitionFrames; i++)
        {
            if (character == null) yield break;
            float t = (i + 1f) / TransitionFrames;
            ApplyWrapVisual(visualRoot, originalVisualScale, originalVisualLocalPosition, t, 1f - t);
            yield return new WaitForFixedUpdate();
        }

        RestoreVisualPose();
        RestoreVisualAnimator();
        character.SetAnimationSpeed(originalAnimationSpeed);
        character.SetFrameStep(originalFrameStep);
        character.ChangeSpeed(originalSpeed);
        CharacterTargetManager.Instance.SetCharacterUndetectable(character, false);
        character.AbortCurrentAttackForControl();
        Destroy(this);
    }

    /// <summary>
    /// 只播传送出现：特效 anim 1 + 30 帧从抬高处放大并落下，期间不可被选中。
    /// 给召唤物用，不要走完整 Wrap（那会先缩小藏起来）。
    /// </summary>
    public static void PlayAppear(Character character)
    {
        if (character == null) return;
        Transform visualRoot = ResolveVisualRoot(character);
        Vector3 originalScale = visualRoot != null ? visualRoot.localScale : Vector3.one;
        Vector3 originalLocalPos = visualRoot != null ? visualRoot.localPosition : Vector3.zero;
        ApplyWrapVisual(visualRoot, originalScale, originalLocalPos, 0f, 1f);
        if (CharacterTargetManager.Instance != null)
            CharacterTargetManager.Instance.SetCharacterUndetectable(character, true);
        character.StartCoroutine(AppearRoutine(character, visualRoot, originalScale, originalLocalPos));
    }

    private static IEnumerator AppearRoutine(Character character, Transform visualRoot, Vector3 originalScale, Vector3 originalLocalPos)
    {
        yield return new WaitForFixedUpdate();
        if (character == null) yield break;

        int originalFrameStep = Mathf.Max(1, character.GetFrameStep());
        int originalAnimationSpeed = character.GetFrameStep() > 0 ? 1 : 0;
        int originalSpeed = character.GetRealSpeed();
        string wrapEffectName = character.IsCat() ? "wrap" : "wrap_e";
        AnimationDisplayer wrapDisplay = null;
        Animator visualAnimator = visualRoot != null ? visualRoot.GetComponent<Animator>() : null;
        bool originalAnimatorEnabled = visualAnimator != null && visualAnimator.enabled;

        try
        {
            character.SetAnimationSpeed(0);
            character.SetFrameStep(0);
            character.ChangeSpeed(0);
            if (visualAnimator != null) visualAnimator.enabled = false;
            PlayAppearAnim(character, ref wrapDisplay, wrapEffectName);

            for (int i = 0; i < TransitionFrames; i++)
            {
                if (character == null) yield break;
                float t = (i + 1f) / TransitionFrames;
                ApplyWrapVisual(visualRoot, originalScale, originalLocalPos, t, 1f - t);
                yield return new WaitForFixedUpdate();
            }

            if (character == null) yield break;
            ApplyWrapVisual(visualRoot, originalScale, originalLocalPos, 1f, 0f);
            if (visualAnimator != null) visualAnimator.enabled = originalAnimatorEnabled;
            character.SetAnimationSpeed(originalAnimationSpeed);
            character.SetFrameStep(originalFrameStep);
            character.ChangeSpeed(originalSpeed);
            if (CharacterTargetManager.Instance != null)
                CharacterTargetManager.Instance.SetCharacterUndetectable(character, false);
        }
        finally
        {
            if (visualAnimator != null) visualAnimator.enabled = originalAnimatorEnabled;
            if (wrapDisplay != null)
            {
                if (character != null && character.EM != null)
                    character.EM.ReleaseReusableAttachedEffect(ref wrapDisplay, wrapEffectName);
                else
                {
                    UnityEngine.Object.Destroy(wrapDisplay.gameObject);
                    wrapDisplay = null;
                }
            }
        }
    }

    private static void PlayAppearAnim(Character character, ref AnimationDisplayer wrapDisplay, string wrapEffectName)
    {
        if (character == null || character.EM == null) return;
        character.EM.PlayReusableAttachedEffect(
            ref wrapDisplay,
            wrapEffectName,
            character.transform,
            character.transform.position + new Vector3(0, 6, 0),
            1,
            worldPositionStays: true,
            essential: true);
    }

    private void PlayWrapAnim(int animIndex)
    {
        if (character == null || character.EM == null) return;
        character.EM.PlayReusableAttachedEffect(
            ref wrapDisplay,
            wrapEffectName,
            character.transform,
            character.transform.position+new Vector3(0,6,0),
            animIndex,
            worldPositionStays: true,
            // 捆绑是持续状态标记，开了「不生成战斗特效」也要留着。
            essential: true);
    }

    private void CleanupWrapAnim()
    {
        if (wrapDisplay == null) return;
        if (character != null && character.EM != null)
        {
            character.EM.ReleaseReusableAttachedEffect(ref wrapDisplay, wrapEffectName);
        }
        else
        {
            Destroy(wrapDisplay.gameObject);
            wrapDisplay = null;
        }
    }

    /// <summary>
    /// BCU 外形在 AnimationDisplayer.Target（main）；UA/Spine 在后来挂上的 uaunit。
    /// 宿主上那个空 main 不能当视觉根。
    /// </summary>
    private static Transform ResolveVisualRoot(Character character)
    {
        if (character == null) return null;
        Transform host = character.transform;

        if (character.UNITYAnimated)
        {
            Animator animator = character.GetComponentInChildren<Animator>(true);
            if (animator != null && animator.transform != host)
                return animator.transform;

            for (int i = 0; i < host.childCount; i++)
            {
                Transform child = host.GetChild(i);
                if (child != null && child.name == "uaunit")
                    return child;
            }
        }

        AnimationDisplayer ad = character.GetComponent<AnimationDisplayer>();
        if (ad != null && ad.Target != null)
            return ad.Target.transform;

        return host.childCount > 0 ? host.GetChild(0) : host;
    }

    private static void ApplyWrapVisual(Transform visualRoot, Vector3 originalScale, Vector3 originalLocalPos, float scaleRatio, float liftRatio)
    {
        if (visualRoot == null) return;
        visualRoot.localScale = originalScale * Mathf.Clamp01(scaleRatio);
        Vector3 localPos = visualRoot.localPosition;
        localPos.y = originalLocalPos.y + WrapLiftDistance * liftRatio;
        visualRoot.localPosition = localPos;
    }

    private void FreezeVisualAnimator()
    {
        visualAnimator = visualRoot != null ? visualRoot.GetComponent<Animator>() : null;
        if (visualAnimator == null) return;
        originalAnimatorEnabled = visualAnimator.enabled;
        visualAnimator.enabled = false;
    }

    private void RestoreVisualAnimator()
    {
        if (visualAnimator == null) return;
        visualAnimator.enabled = originalAnimatorEnabled;
    }

    private void RestoreVisualPose()
    {
        ApplyWrapVisual(visualRoot, originalVisualScale, originalVisualLocalPosition, 1f, 0f);
    }
}
