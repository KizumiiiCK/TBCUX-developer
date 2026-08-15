using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.AddressableAssets;
using UnityEngine.ResourceManagement.AsyncOperations;

/// <summary>
/// BGM playback via Addressables + the scene AudioSource named "BGM".
/// </summary>
public static class BGMTool
{
    private static AsyncOperationHandle<AudioClip> currentHandle;
    private static bool hasHandle;
    private static int loadVersion;

    /// <summary>
    /// Change BGM with optional fade.
    /// </summary>
    /// <param name="bgmName">Logical name (e.g. "002", "silent_love").</param>
    /// <param name="instant">If true, skip fading on AudioSource path</param>
    public static void ChangeBGM(string bgmName, bool instant = false)
    {
        bgmName = NormalizeBgmAddress(bgmName);
        if (string.IsNullOrEmpty(bgmName)) return;

        GameObject bgmGo = GameObject.Find("BGM");
        if (bgmGo == null)
        {
            Debug.LogError("BGM AudioSource not found! Make sure there is a GameObject named 'BGM'.");
            return;
        }

        AudioSource source = bgmGo.GetComponent<AudioSource>();
        if (source == null)
        {
            Debug.LogError("BGM AudioSource not found! Make sure there is a GameObject named 'BGM'.");
            return;
        }

        if (source.clip != null && source.clip.name == bgmName && source.isPlaying)
            return;

        MonoBehaviour host = bgmGo.GetComponent<MonoBehaviour>();
        if (host == null)
        {
            Debug.LogError("BGM GameObject needs a MonoBehaviour to run coroutines.");
            return;
        }

        int version = ++loadVersion;
        host.StartCoroutine(ChangeBgmRoutine(source, bgmName, instant, version));
    }

    /// <summary>Stop current BGM and release Addressables handle.</summary>
    public static void StopBGM()
    {
        loadVersion++;

        GameObject bgmGo = GameObject.Find("BGM");
        if (bgmGo != null)
        {
            AudioSource source = bgmGo.GetComponent<AudioSource>();
            if (source != null)
            {
                source.Stop();
                source.clip = null;
            }
        }
        BundledAddressables.Release(ref currentHandle, ref hasHandle);
    }

    /// <summary>
    /// Normalize legacy Resources paths / extensions to logical BGM names.
    /// </summary>
    public static string NormalizeBgmAddress(string bgmName)
    {
        if (string.IsNullOrWhiteSpace(bgmName)) return string.Empty;
        string name = bgmName.Trim().Replace('\\', '/');
        const string resourcesPrefix = "Music/BGM/";
        if (name.StartsWith(resourcesPrefix, System.StringComparison.OrdinalIgnoreCase))
            name = name.Substring(resourcesPrefix.Length);
        if (name.StartsWith("BGM/", System.StringComparison.OrdinalIgnoreCase))
            name = name.Substring(4);
        if (name.StartsWith("audio/bgm/", System.StringComparison.OrdinalIgnoreCase))
            name = name.Substring("audio/bgm/".Length);
        int slash = name.LastIndexOf('/');
        if (slash >= 0) name = name.Substring(slash + 1);
        int dot = name.LastIndexOf('.');
        if (dot > 0) name = name.Substring(0, dot);
        return name;
    }

    private static IEnumerator ChangeBgmRoutine(AudioSource source, string bgmName, bool instant, int version)
    {
        AsyncOperationHandle<AudioClip> pending = default;
        bool pendingValid = false;

        yield return BundledAddressables.Load<AudioClip>(bgmName, handle =>
        {
            pending = handle;
            pendingValid = handle.IsValid();
        });

        if (version != loadVersion)
        {
            if (pendingValid) Addressables.Release(pending);
            yield break;
        }

        if (!BundledAddressables.TryGetResult(pending, out AudioClip newClip))
        {
            if (pendingValid) Addressables.Release(pending);
            Debug.LogError($"BGM clip not found in Addressables: '{bgmName}'");
            yield break;
        }

        BundledAddressables.Release(ref currentHandle, ref hasHandle);
        currentHandle = pending;
        hasHandle = true;

        if (source.clip == null || instant)
        {
            source.clip = newClip;
            source.Play();
            yield break;
        }

        yield return FadeAndSwitch(source, newClip);
    }

    private static IEnumerator FadeAndSwitch(AudioSource source, AudioClip newClip)
    {
        float originalVolume = source.volume;
        float t = 0f;

        while (t < 1f)
        {
            t += Time.deltaTime;
            source.volume = Mathf.Lerp(originalVolume, 0f, t);
            yield return null;
        }

        source.clip = newClip;
        source.Play();
        source.volume = originalVolume;
    }

    public static void BGM_By_BaseMap(string cptName) => ChangeBGM(baseMap[cptName]);

    public static string ResolveBaseMapBgmName(string cptName)
    {
        if (string.IsNullOrEmpty(cptName)) return string.Empty;
        if (baseMap.TryGetValue(cptName, out var bgmName)) return bgmName;
        return string.Empty;
    }

    private static readonly Dictionary<string, string> baseMap = new Dictionary<string, string>()
    {
        { "World_I", "002" },
        { "World_II", "002" },
        { "World_III", "002" },
        { "Future_I", "002" },
        { "Future_II", "002" },
        { "Future_III", "002" },
        { "LEGEND", "000" },
        { "Dream_Pre", "lilytales-title" },
        { "Dungeon", "HazeReverb-43-antinova" },
    };
}
