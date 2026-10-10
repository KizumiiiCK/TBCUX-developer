using UnityEngine;
using UnityEngine.Audio;

/// <summary>
/// Unity AudioSource playback helpers for Editor / Windows / Android.
/// </summary>
public static class PlatformAudio
{
    private const string AudioMixerResourcePath = "Music/AudioMixer";
    private static AudioMixerGroup seGroup;
    private static bool seGroupResolved;

    /// <summary>
    /// Resources 那份 AudioMixer 的 SE 组 —— 设置页 SetFloat("SEVolume") 改的就是这一份。
    /// </summary>
    public static AudioMixerGroup SEGroup
    {
        get
        {
            if (seGroupResolved) return seGroup;
            seGroupResolved = true;

            AudioMixer mixer = Resources.Load<AudioMixer>(AudioMixerResourcePath);
            if (mixer == null)
            {
                Debug.LogWarning($"[PlatformAudio] AudioMixer not found at Resources/{AudioMixerResourcePath}");
                return null;
            }
            AudioMixerGroup[] groups = mixer.FindMatchingGroups("SE");
            if (groups == null || groups.Length == 0)
            {
                Debug.LogWarning("[PlatformAudio] SE mixer group not found.");
                return null;
            }
            seGroup = groups[0];
            return seGroup;
        }
    }

    /// <summary>
    /// 把 root 及其子物体上的 AudioSource 全部重新绑到 <see cref="SEGroup"/>。
    ///
    /// Addressables 会把非 Addressable 的依赖整份拷进 bundle，而 AudioMixer 躺在 Resources 里
    /// 没被标记成 Addressable，于是 bundle 里夹带了一份它的副本。从 bundle 加载出来的预制体上的
    /// AudioSource 指向的是那份副本，设置页的 SetFloat 永远碰不到，音量滑条对它完全无效
    /// （表现为原声播放）。生成后重绑一次就回到受控的那一份。
    ///
    /// 已经在播的（playOnAwake 的预制体在 Instantiate 返回时就响了）重起一次，
    /// 免得开头漏出一两帧原声。
    /// </summary>
    public static void BindToSEGroup(GameObject root)
    {
        if (root == null) return;
        AudioMixerGroup group = SEGroup;
        if (group == null) return;

        AudioSource[] sources = root.GetComponentsInChildren<AudioSource>(true);
        for (int i = 0; i < sources.Length; i++)
        {
            AudioSource src = sources[i];
            if (src == null || src.outputAudioMixerGroup == group) continue;
            bool wasPlaying = src.isPlaying;
            src.outputAudioMixerGroup = group;
            if (!wasPlaying) continue;
            src.Stop();
            src.time = 0f;
            src.Play();
        }
    }

    public static void PlaySfx(AudioSource source, string logicalName = null)
    {
        if (source == null) return;
        source.Play();
    }

    public static void PlayOneShot(AudioSource source, AudioClip clip, float volumeScale = 1f, string logicalName = null)
    {
        if (source == null || clip == null) return;
        source.PlayOneShot(clip, volumeScale);
    }

    public static void PlayClipAtPoint(AudioClip clip, Vector3 position, float volume = 1f, string logicalName = null)
    {
        if (clip == null) return;
        AudioSource.PlayClipAtPoint(clip, position, volume);
    }
}
