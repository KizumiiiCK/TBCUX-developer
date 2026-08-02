using System;
using Spine.Unity;
using UnityEngine;

[RequireComponent(typeof(SkeletonAnimation))]
public class SpineAnimationEventController : MonoBehaviour
{
    [Serializable]
    private struct MixSetting
    {
        public string From;
        public string To;
        [Min(0f)] public float Duration;
    }

    [Header("基础配置")]
    [SerializeField] private SkeletonAnimation _skeletonAnimation;

    [Header("过渡设置")]
    [Tooltip("未命中自定义规则时的默认过渡时长（秒）")]
    [Min(0f)]
    [SerializeField] private float _defaultTransitionDuration = 0.1f;
    [Tooltip("同动画同循环参数时，是否跳过重复播放")]
    [SerializeField] private bool _skipIfSameAnimation = true;
    [Tooltip("可选：按 动画A->动画B 指定单独过渡时长")]
    [SerializeField] private MixSetting[] _customMixes = Array.Empty<MixSetting>();

    private Spine.AnimationState _animationState;
    private bool ShouldLogWarnings => Application.isPlaying;

    private void Awake()
    {
        CacheReferences();
        ApplyMixSettings();
    }

    /// <summary>
    /// 【Unity 动画事件调用】切换到目标动画，并指定是否循环
    /// </summary>
    /// <param name="animData">格式："动画名,是否循环"（例："walk,true" 或 "attack,false"）</param>
    public void PlaySpineAnimation(string animData)
    {
        if (!TryParseAnimationData(animData, out string animName, out bool loop)) return;
        if (!HasAnimation(animName)) return;
        if (_animationState == null) return;

        Spine.TrackEntry current = _animationState.GetCurrent(0);
        if (_skipIfSameAnimation &&
            current != null &&
            current.Animation != null &&
            current.Animation.Name == animName &&
            current.Loop == loop)
        {
            return;
        }

        Spine.TrackEntry next = _animationState.SetAnimation(0, animName, loop);
        next.MixDuration = ResolveMixDuration(current?.Animation?.Name, animName);
    }

    /// <summary>
    /// 【Unity 动画事件调用】在当前动画结束后，追加播放下一个动画
    /// </summary>
    /// <param name="animData">格式："动画名,是否循环"（例："idle,true"）</param>
    public void AddSpineAnimation(string animData)
    {
        if (!TryParseAnimationData(animData, out string animName, out bool loop)) return;
        if (!HasAnimation(animName)) return;
        if (_animationState == null) return;

        string fromName = _animationState.GetCurrent(0)?.Animation?.Name;
        Spine.TrackEntry next = _animationState.AddAnimation(0, animName, loop, 0f);
        next.MixDuration = ResolveMixDuration(fromName, animName);
    }

    private void CacheReferences()
    {
        if (_skeletonAnimation == null)
        {
            _skeletonAnimation = GetComponent<SkeletonAnimation>();
        }

        if (_skeletonAnimation == null)
        {
            Debug.LogError("未找到 SkeletonAnimation 组件！请确保物体上挂载了 Spine SkeletonAnimation。", this);
            return;
        }

        _animationState = _skeletonAnimation.AnimationState;
    }

    private void ApplyMixSettings()
    {
        if (_animationState == null) return;

        Spine.AnimationStateData stateData = _animationState.Data;
        stateData.DefaultMix = Mathf.Max(0f, _defaultTransitionDuration);

        if (_customMixes == null) return;
        for (int i = 0; i < _customMixes.Length; i++)
        {
            MixSetting setting = _customMixes[i];
            if (string.IsNullOrWhiteSpace(setting.From) || string.IsNullOrWhiteSpace(setting.To))
            {
                continue;
            }

            stateData.SetMix(setting.From.Trim(), setting.To.Trim(), Mathf.Max(0f, setting.Duration));
        }
    }

    private bool TryParseAnimationData(string animData, out string animName, out bool loop)
    {
        animName = string.Empty;
        loop = false;

        if (string.IsNullOrWhiteSpace(animData))
        {
            return false;
        }

        string[] parts = animData.Split(',');
        animName = parts[0].Trim();
        if (string.IsNullOrEmpty(animName))
        {
            return false;
        }

        if (parts.Length > 1)
        {
            string loopText = parts[1].Trim();
            bool.TryParse(loopText, out loop);
        }

        return true;
    }

    private bool HasAnimation(string animName)
    {
        if (_skeletonAnimation == null || _skeletonAnimation.Skeleton == null) return false;

        Spine.Animation animation = _skeletonAnimation.Skeleton.Data?.FindAnimation(animName);
        if (animation != null) return true;

        if (ShouldLogWarnings)
        {
            Debug.LogWarning($"Spine 动画不存在：{animName}", this);
        }
        return false;
    }

    private float ResolveMixDuration(string from, string to)
    {
        if (!string.IsNullOrEmpty(from) && !string.IsNullOrEmpty(to) && _customMixes != null)
        {
            for (int i = 0; i < _customMixes.Length; i++)
            {
                MixSetting setting = _customMixes[i];
                if (string.Equals(setting.From?.Trim(), from, StringComparison.Ordinal) &&
                    string.Equals(setting.To?.Trim(), to, StringComparison.Ordinal))
                {
                    return Mathf.Max(0f, setting.Duration);
                }
            }
        }

        return Mathf.Max(0f, _defaultTransitionDuration);
    }

    /// <summary>
    /// 【调试用】在 Inspector 右键快速测试动画切换
    /// </summary>
    [ContextMenu("测试播放行走动画")]
    private void TestPlayWalk()
    {
        PlaySpineAnimation("walk,true");
    }

    [ContextMenu("测试播放待机动画")]
    private void TestPlayIdle()
    {
        PlaySpineAnimation("idle,true");
    }
}