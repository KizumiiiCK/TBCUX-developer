using UnityEngine;

/// <summary>
/// Unity AudioSource playback helpers for Editor / Windows / Android.
/// </summary>
public static class PlatformAudio
{
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
