using Spine.Unity;
using UnityEngine;

//[RequireComponent(typeof(SkeletonAnimation))]
public class SpineAnimationEventController : MonoBehaviour
{
    [Header("基础配置")]
    [SerializeField] private SkeletonAnimation _skeletonAnimation;

    [Header("过渡设置")]
    [Tooltip("切换动画时的平滑过渡时间（秒）")]
    [SerializeField] private float _defaultTransitionDuration = 0.1f;

    private void Awake()
    {
        // 自动获取组件（如果未手动赋值）
        if (_skeletonAnimation == null)
        {
            _skeletonAnimation = GetComponent<SkeletonAnimation>();
        }

        if (_skeletonAnimation == null)
        {
            Debug.LogError("未找到 SkeletonAnimation 组件！请确保物体上挂载了 Spine SkeletonAnimation。", this);
        }
    }

    /// <summary>
    /// 【Unity 动画事件调用】切换到目标动画，并指定是否循环
    /// </summary>
    /// <param name="animData">格式："动画名,是否循环"（例："walk,true" 或 "attack,false"）</param>
    public void PlaySpineAnimation(string animData)
    {
        string[] parts = animData.Split(',');
        string animName = parts[0];
        bool loop = parts.Length > 1 && bool.Parse(parts[1]);

        if (_skeletonAnimation == null || string.IsNullOrEmpty(animName)) return;

        // 清空当前轨道，播放新动画
        _skeletonAnimation.AnimationState.ClearTracks();
        var track = _skeletonAnimation.AnimationState.SetAnimation(0, animName, loop);
        track.MixDuration = _defaultTransitionDuration; // 应用平滑过渡
    }

    /// <summary>
    /// 【Unity 动画事件调用】在当前动画结束后，追加播放下一个动画
    /// </summary>
    /// <param name="animData">格式："动画名,是否循环"（例："idle,true"）</param>
    public void AddSpineAnimation(string animData)
    {
        string[] parts = animData.Split(',');
        string animName = parts[0];
        bool loop = parts.Length > 1 && bool.Parse(parts[1]);

        if (_skeletonAnimation == null || string.IsNullOrEmpty(animName)) return;

        // 在当前动画结束后追加播放
        _skeletonAnimation.AnimationState.AddAnimation(0, animName, loop, 0f);
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