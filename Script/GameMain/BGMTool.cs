using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public static class BGMTool
{
    /// <summary>
    /// Change BGM with optional fade.
    /// </summary>
    /// <param name="bgmName">Name of the BGM clip under Resources/BGM/xxx</param>
    /// <param name="instant">If true, skip fading</param>
    public static void ChangeBGM(string bgmName, bool instant = false)
    {
        if (bgmName == null) return;
        if (bgmName == string.Empty) return;
        AudioSource source = GameObject.Find("BGM").GetComponent<AudioSource>();
        if (source == null)
        {
            Debug.LogError("BGM AudioSource not found! Make sure there is a GameObject named 'BGM'.");
            return;
        }
        AudioClip newClip = Resources.Load<AudioClip>("Music/BGM/" + bgmName);
        // If nothing was playing ¡ú play instantly
        if (source.clip == null || instant)
        {
            source.clip = newClip;
            source.Play();
            return;
        }
        else if (source.clip.name == bgmName) return;

        if (newClip == null)
        {
            Debug.LogError($"BGM clip not found: /Music/BGM/{bgmName}");
            return;
        }

        // Otherwise: fade out & switch
        source.gameObject.GetComponent<MonoBehaviour>().StartCoroutine(
            FadeAndSwitch(source, newClip)
        );
    }

    private static IEnumerator FadeAndSwitch(AudioSource source, AudioClip newClip)
    {
        float originalVolume = source.volume;
        float t = 0f;

        // fade out in 1 sec
        while (t < 1f)
        {
            t += Time.deltaTime;
            source.volume = Mathf.Lerp(originalVolume, 0f, t);
            yield return null;
        }

        // switch BGM
        source.clip = newClip;
        source.Play();

        // fade in instantly to original volume
        source.volume = originalVolume;
    }
    //
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
