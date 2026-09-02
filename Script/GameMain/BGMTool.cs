using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.AddressableAssets;
using UnityEngine.ResourceManagement.AsyncOperations;
#if UNITY_WEBGL && !UNITY_EDITOR
using Builda;
#endif

/// <summary>
/// BGM playback.
///
/// Two backends, chosen at compile time:
/// <list type="bullet">
/// <item><b>Windows / Android / Editor</b> - Addressables clip into the scene AudioSource named
/// "BGM", with a volume crossfade.</item>
/// <item><b>WebGL (Builda platform)</b> - <c>BuildaSDK.AudioPlayBGM</c>, i.e. the host App plays the
/// file natively out of <c>assets.zip</c>. This keeps ~130MB of music out of the main bundle, which
/// matters because the platform's default app-size ceiling is 200MiB.</item>
/// </list>
///
/// The platform path cannot crossfade: <c>AudioPlayBGM</c> takes a volume only at start and there is
/// no "change the volume of what is currently playing" call, so fading would mean restarting
/// playback every frame. Track changes are therefore hard cuts on WebGL. This is a deliberate,
/// approved trade - not an oversight.
/// </summary>
public static class BGMTool
{
    private static AsyncOperationHandle<AudioClip> currentHandle;
    private static bool hasHandle;
    private static int loadVersion;

    /// <summary>
    /// Last address handed to the host, so a repeat request for the already-playing track is
    /// ignored. The Unity path gets this for free by inspecting the AudioSource; the platform path
    /// has no readable playback state, so it has to be tracked here.
    /// </summary>
    private static string platformCurrentAddress;

    /// <summary>
    /// Change BGM with optional fade.
    /// </summary>
    /// <param name="bgmName">Logical name (e.g. "002", "silent_love").</param>
    /// <param name="instant">If true, skip fading on AudioSource path</param>
    public static void ChangeBGM(string bgmName, bool instant = false)
    {
        bgmName = NormalizeBgmAddress(bgmName);
        if (string.IsNullOrEmpty(bgmName)) return;

#if UNITY_WEBGL && !UNITY_EDITOR
        PlatformChangeBGM(bgmName);
#else
        UnityChangeBGM(bgmName, instant);
#endif
    }

    /// <summary>Stop current BGM and release Addressables handle.</summary>
    public static void StopBGM()
    {
        loadVersion++;

#if UNITY_WEBGL && !UNITY_EDITOR
        platformCurrentAddress = null;
        BuildaSDK.AudioStopBGM();
        return;
#else
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
#endif
    }

#if UNITY_WEBGL && !UNITY_EDITOR
    private static void PlatformChangeBGM(string bgmName)
    {
        if (platformCurrentAddress == bgmName) return;

        string path = BuildaBgmCatalog.TryGetPath(bgmName);
        if (path == null)
        {
            // The track is in the Addressables group but absent from the catalog, which means it is
            // also missing from assets.zip. Fail loudly: silence is otherwise indistinguishable
            // from a track that is simply quiet.
            Debug.LogError($"[BGMTool] '{bgmName}' is not in BuildaBgmCatalog, so it was never " +
                           "staged into assets.zip. Add it to the table and re-run the audio staging script.");
            return;
        }

        platformCurrentAddress = bgmName;
        BuildaSDK.AudioPlayBGM(path, loop: true, volume: 1f, callback: result =>
        {
            if (result.Ok) return;
            // Clear the cached address so a later retry is not suppressed by the equality check.
            if (platformCurrentAddress == bgmName) platformCurrentAddress = null;
            Debug.LogError($"[BGMTool] Host refused to play '{path}': " +
                           $"{result.Error?.Code} {result.Error?.Message}");
        });
    }
#else
    private static void UnityChangeBGM(string bgmName, bool instant)
    {
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
#endif

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

#if !UNITY_WEBGL || UNITY_EDITOR
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
#endif

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
        { "Challenge", "062" },
        { "Dungeon", "HazeReverb-43-antinova" },
    };
}
