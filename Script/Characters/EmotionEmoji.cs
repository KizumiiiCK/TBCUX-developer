using System;
using System.Collections;
using UnityEngine;

/// <summary>
/// 战斗表情气泡：挂在角色父物体下，只改 positioner 的 Y，2 秒后回调回收。
/// emojiBG 底板不变，子物体 emoji 只换图标。
/// 前/后 0.4s 对 positioner 做 0↔1 的弹出缩放。
/// </summary>
public class EmotionEmoji : MonoBehaviour
{
    private const float ScaleEdgeSeconds = 0.25f;

    private Transform positioner;
    private SpriteRenderer icon;
    private Coroutine lifeRoutine;
    private Action<EmotionEmoji> onFinished;

    private void Awake()
    {
        CacheRefs();
    }

    public void Show(Sprite sprite, float positionerY, float duration, Action<EmotionEmoji> finished)
    {
        CacheRefs();
        if (lifeRoutine != null)
        {
            StopCoroutine(lifeRoutine);
            lifeRoutine = null;
        }

        if (icon != null) icon.sprite = sprite;
        if (positioner != null)
        {
            Vector3 p = positioner.localPosition;
            p.y = positionerY;
            positioner.localPosition = p;
        }
        SetPositionerScale(0f);

        onFinished = finished;
        gameObject.SetActive(true);
        lifeRoutine = StartCoroutine(LifeRoutine(duration));
    }

    public void RecycleNow()
    {
        if (lifeRoutine != null)
        {
            StopCoroutine(lifeRoutine);
            lifeRoutine = null;
        }
        onFinished = null;
        if (icon != null) icon.sprite = null;
        SetPositionerScale(1f);
        Vector3 scale = transform.localScale;
        scale.x = 1f;
        transform.localScale = scale;
        gameObject.SetActive(false);
    }

    private IEnumerator LifeRoutine(float duration)
    {
        duration = Mathf.Max(0f, duration);
        float edge = Mathf.Min(ScaleEdgeSeconds, duration * 0.5f);
        float elapsed = 0f;
        SetPositionerScale(0f);
        while (elapsed < duration)
        {
            elapsed += Time.deltaTime;
            SetPositionerScale(EvaluatePopScale(elapsed, duration, edge));
            yield return null;
        }
        SetPositionerScale(0f);

        lifeRoutine = null;
        Action<EmotionEmoji> callback = onFinished;
        onFinished = null;
        callback?.Invoke(this);
    }

    private static float EvaluatePopScale(float elapsed, float duration, float edge)
    {
        if (edge <= 0f) return 1f;
        if (elapsed <= edge) return Mathf.Lerp(0f, 1f, elapsed / edge);
        float outroStart = duration - edge;
        if (elapsed >= outroStart) return Mathf.Lerp(1f, 0f, (elapsed - outroStart) / edge);
        return 1f;
    }

    private void SetPositionerScale(float s)
    {
        if (positioner == null) return;
        positioner.localScale = new Vector3(s, s, s);
    }

    private void CacheRefs()
    {
        if (positioner == null)
        {
            Transform found = transform.Find("positioner");
            if (found != null) positioner = found;
        }

        if (icon == null)
        {
            Transform emoji = positioner != null
                ? positioner.Find("emojiBG/emoji")
                : transform.Find("positioner/emojiBG/emoji");
            if (emoji != null) icon = emoji.GetComponent<SpriteRenderer>();
        }
    }
}
