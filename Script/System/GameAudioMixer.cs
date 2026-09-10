using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Audio;
using UnityEngine.ResourceManagement.AsyncOperations;

/// <summary>
/// Single owner of the project AudioMixer.
///
/// The mixer used to live in <c>Assets/Resources/Music/AudioMixer.mixer</c>, which gave it two
/// separate lives in a player build: one copy inside <c>resources.assets</c> (for
/// <c>Resources.Load</c>) and one copy inside every AssetBundle whose prefabs route an AudioSource
/// through it. Two copies are two distinct runtime instances, so a <c>SetFloat</c> on the volume
/// parameters only reached the sources wired to that particular instance. Moving the asset to
/// <c>Assets/Bundled/System/audio</c> and going through Addressables collapses it to one instance
/// that every consumer shares.
///
/// Callers must not hold their own <c>[SerializeField] AudioMixer</c>: a serialized reference from a
/// built-in scene re-embeds the asset in that scene's data and reintroduces the duplicate.
/// </summary>
public static class GameAudioMixer
{
    /// <summary>Addressable key. Registered by VisualsAddressablesRegistrar's System/audio folder.</summary>
    public const string Address = "System/audio/AudioMixer";

    private const string SEGroupName = "SE";

    private static AudioMixer mixer;
    private static AudioMixerGroup seGroup;
    private static bool seGroupResolved;

    /// <summary>
    /// Volume values requested before the mixer was resident. On WebGL a sync read only succeeds
    /// after the address has been prewarmed, so an early <see cref="SetVolumeDb"/> would otherwise
    /// be dropped and the player would keep the mixer's authored volume instead of their own.
    /// </summary>
    private static readonly Dictionary<string, float> pendingVolumes = new Dictionary<string, float>();

    /// <summary>The shared mixer, or null when it is not resident yet (WebGL, before prewarm).</summary>
    public static AudioMixer Mixer
    {
        get
        {
            if (mixer != null) return mixer;

            mixer = BundledAddressables.LoadSync<AudioMixer>(Address);
            if (mixer != null) FlushPendingVolumes();
            return mixer;
        }
    }

    public static bool IsReady => Mixer != null;

    /// <summary>
    /// The group SE AudioSources route through, or null when unavailable. Resolution is retried
    /// until it succeeds so a call made before prewarm does not permanently disable SE routing.
    /// </summary>
    public static AudioMixerGroup SEGroup
    {
        get
        {
            if (seGroupResolved) return seGroup;

            AudioMixer m = Mixer;
            if (m == null) return null;

            AudioMixerGroup[] groups = m.FindMatchingGroups(SEGroupName);
            if (groups == null || groups.Length == 0)
            {
                // The asset is loaded and simply has no such group - that is authoring data, not a
                // timing problem, so latch it and stop looking.
                seGroupResolved = true;
                Debug.LogWarning($"[GameAudioMixer] Mixer group '{SEGroupName}' not found.");
                return null;
            }

            seGroup = groups[0];
            seGroupResolved = true;
            return seGroup;
        }
    }

    /// <summary>
    /// Sets an exposed mixer parameter. Remembers the value when the mixer is not resident yet and
    /// replays it on the first successful load.
    /// </summary>
    public static void SetVolumeDb(string parameterName, float decibels)
    {
        if (string.IsNullOrEmpty(parameterName)) return;

        AudioMixer m = Mixer;
        if (m == null)
        {
            pendingVolumes[parameterName] = decibels;
            return;
        }
        m.SetFloat(parameterName, decibels);
    }

    /// <summary>
    /// Loads the mixer asynchronously and leaves it in the BundledAddressables cache, so later
    /// <see cref="Mixer"/> reads are sync cache hits. Safe to call more than once.
    /// </summary>
    public static IEnumerator EnsureLoadedRoutine()
    {
        if (mixer != null) yield break;

        AudioMixer loaded = null;
        yield return BundledAddressables.Load<AudioMixer>(Address, handle =>
        {
            if (handle.IsValid() && handle.Status == AsyncOperationStatus.Succeeded) loaded = handle.Result;
        });

        if (loaded == null)
        {
            Debug.LogWarning($"[GameAudioMixer] Could not load '{Address}'.");
            yield break;
        }
        mixer = loaded;
        FlushPendingVolumes();
    }

    private static void FlushPendingVolumes()
    {
        if (pendingVolumes.Count == 0) return;

        foreach (KeyValuePair<string, float> pending in pendingVolumes)
            mixer.SetFloat(pending.Key, pending.Value);
        pendingVolumes.Clear();
    }
}
