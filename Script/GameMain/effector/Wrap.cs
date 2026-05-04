using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class Wrap : E
{
    private const int TransitionFrames = 30;
    private const float HiddenPosY = -1000f;

    private Character character;
    private Transform visualRoot;
    private Vector3 originalVisualScale = Vector3.one;
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

        if (character.transform.childCount > 0)
        {
            visualRoot = character.transform.GetChild(0);
            originalVisualScale = visualRoot.localScale;
        }

        StartCoroutine(Wrapping());
    }

    public override void RemoveEffect()
    {
        if (character == null) return;

        character.SetAnimationSpeed(originalAnimationSpeed);
        character.SetFrameStep(originalFrameStep);
        character.ChangeSpeed(originalSpeed);
        CharacterTargetManager.Instance.SetCharacterUndetectable(character, false);

        if (visualRoot != null)
        {
            visualRoot.localScale = originalVisualScale;
        }
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

        character.SetAnimationSpeed(0);
        character.SetFrameStep(0);
        character.ChangeSpeed(0);
        CharacterTargetManager.Instance.SetCharacterUndetectable(character, true);

        // wrap_out：首帧播放0，30帧内缩小到0，并隐藏到不可检测位置
        PlayWrapAnim(0);
        character.transform.position = new Vector3(character.transform.position.x, HiddenPosY, character.transform.position.z);
        for (int i = 0; i < TransitionFrames; i++)
        {
            if (character == null) yield break;
            if (visualRoot != null)
            {
                float ratio = 1f - ((i + 1f) / TransitionFrames);
                visualRoot.localScale = originalVisualScale * Mathf.Clamp01(ratio);
            }
            yield return new WaitForFixedUpdate();
        }

        // 隐藏持续 duration 帧
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
            if (visualRoot != null)
            {
                float ratio = (i + 1f) / TransitionFrames;
                visualRoot.localScale = originalVisualScale * Mathf.Clamp01(ratio);
            }
            yield return new WaitForFixedUpdate();
        }

        character.SetAnimationSpeed(originalAnimationSpeed);
        character.SetFrameStep(originalFrameStep);
        character.ChangeSpeed(originalSpeed);
        CharacterTargetManager.Instance.SetCharacterUndetectable(character, false);
        Destroy(this);
    }

    private void PlayWrapAnim(int animIndex)
    {
        if (character == null || character.EM == null) return;
        character.EM.PlayReusableAttachedEffect(
            ref wrapDisplay,
            wrapEffectName,
            character.transform,
            character.transform.position,
            animIndex,
            worldPositionStays: true);
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
}
