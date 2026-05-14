using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Audio;

public class EffectManager : MonoBehaviour
{
    [SerializeField] public GameObject EffectObjectNull;
    private readonly Dictionary<string, AnimDecryptPack> packCache = new Dictionary<string, AnimDecryptPack>();
    private readonly Dictionary<string, Queue<AnimationDisplayer>> displayPool = new Dictionary<string, Queue<AnimationDisplayer>>();
    private readonly Dictionary<string, AudioClip[]> effectSoundCache = new Dictionary<string, AudioClip[]>();
    private readonly Dictionary<string, AudioSource> effectSoundPlayers = new Dictionary<string, AudioSource>();
    private const string EffectSourceRoot = "Effects/source file/";
    private const string AudioMixerResourcePath = "Music/AudioMixer";
    private AudioMixerGroup seMixerGroup;
    private bool seMixerResolved;
    public AnimationDisplayer InstantiateBattleObject(SEnums sename,float posX, float posY, bool playSound=true)
    {
        string effectName = sename.ToString();
        float finalY = posY + Ydeviation[sename];
        AnimationDisplayer ad = InstantiateNamedBattleObjectInternal(
            effectName,
            new Vector3(posX, finalY, 0),
            null,
            true,
            posY,
            playSound,
            SEExistT[sename]);
        return ad;
    }

    public AnimationDisplayer InstantiateBattleObject(string effectName, float posX, float posY, bool playSound = true, int lifetimeFrames = 0)
    {
        return InstantiateNamedBattleObjectInternal(
            effectName,
            new Vector3(posX, posY, 0),
            null,
            true,
            posY,
            playSound,
            lifetimeFrames);
    }

    public AnimationDisplayer InstantiateAttachedBattleObject(
        string effectName,
        Vector3 worldPosition,
        Transform parent,
        bool worldPositionStays = true,
        bool playSound = true,
        int lifetimeFrames = 0)
    {
        return InstantiateNamedBattleObjectInternal(
            effectName,
            worldPosition,
            parent,
            worldPositionStays,
            worldPosition.y,
            playSound,
            lifetimeFrames);
    }

    public AnimationDisplayer PlayReusableAttachedEffect(
        ref AnimationDisplayer cachedDisplay,
        string effectName,
        Transform parent,
        Vector3 worldPosition,
        int animIndex,
        bool worldPositionStays = true)
    {
        if (string.IsNullOrEmpty(effectName) || parent == null) return null;
        if (cachedDisplay == null)
        {
            cachedDisplay = InstantiateAttachedBattleObject(
                effectName,
                worldPosition,
                parent,
                worldPositionStays,
                playSound: false,
                lifetimeFrames: 0);
        }
        if (cachedDisplay == null) return null;
        cachedDisplay.gameObject.SetActive(true);
        cachedDisplay.SetMaanimPointer(animIndex);
        PlayEffectSound(effectName, animIndex);
        return cachedDisplay;
    }

    public void ReleaseReusableAttachedEffect(ref AnimationDisplayer cachedDisplay, string effectName)
    {
        if (cachedDisplay == null) return;
        RecycleBattleObject(cachedDisplay, effectName);
        cachedDisplay = null;
    }

    private AnimationDisplayer InstantiateNamedBattleObjectInternal(
        string effectName,
        Vector3 worldPosition,
        Transform parent,
        bool worldPositionStays,
        float sortY,
        bool playSound,
        int lifetimeFrames)
    {
        if (string.IsNullOrEmpty(effectName)) return null;
        string packKey = GetEffectPackKey(effectName);
        AnimDecryptPack pack = GetOrCreateEffectPack(effectName);
        if (pack == null) return null;
        AnimationDisplayer ad = SpawnDisplayFromPool(
            packKey,
            pack,
            worldPosition,
            parent,
            worldPositionStays,
            sortY);
        if (ad == null) return null;
        if (string.Equals(effectName, "corpse", StringComparison.OrdinalIgnoreCase))
        {
            CharacterSummoner.ResetAnimationOrderLayer(ad, 0);
        }
        if (lifetimeFrames > 0)
        {
            PooledEffectLifetime lifetime = ad.GetComponent<PooledEffectLifetime>();
            if (lifetime == null) lifetime = ad.gameObject.AddComponent<PooledEffectLifetime>();
            lifetime.Activate(this, packKey, lifetimeFrames, true);
        }
        if (playSound) PlayEffectSound(effectName, 0);
        return ad;
    }

    public void PlayEffectSound(string effectName, int animIndex = 0)
    {
        string soundKey = GetEffectSoundKey(effectName);
        if (!effectSoundCache.TryGetValue(soundKey, out AudioClip[] clips))
        {
            clips = Resources.LoadAll<AudioClip>(GetEffectResourceFolder(effectName));
            if (clips == null) clips = Array.Empty<AudioClip>();
            effectSoundCache[soundKey] = clips;
        }
        if (clips.Length == 0) return;

        AudioSource source = GetOrCreateEffectSoundPlayer(soundKey, effectName);
        if (source == null) return;
        int clipIndex = Mathf.Clamp(animIndex, 0, clips.Length - 1);
        AudioClip clip = clips[clipIndex];
        source.Stop();
        source.clip = clip;
        source.time = 0f;
        source.Play();
    }

    private AudioSource GetOrCreateEffectSoundPlayer(string soundKey, string effectName)
    {
        if (effectSoundPlayers.TryGetValue(soundKey, out AudioSource existing) && existing != null) return existing;

        GameObject go = new GameObject($"SE_{effectName}");
        go.transform.SetParent(transform, false);
        AudioSource src = go.AddComponent<AudioSource>();
        src.playOnAwake = false;
        src.loop = false;
        src.spatialBlend = 0f;
        src.outputAudioMixerGroup = ResolveSEMixerGroup();
        effectSoundPlayers[soundKey] = src;
        return src;
    }

    private AudioMixerGroup ResolveSEMixerGroup()
    {
        if (seMixerResolved) return seMixerGroup;
        seMixerResolved = true;

        AudioMixer mixer = Resources.Load<AudioMixer>(AudioMixerResourcePath);
        if (mixer == null)
        {
            Debug.LogWarning($"[EffectManager] AudioMixer not found at Resources/{AudioMixerResourcePath}");
            return null;
        }
        AudioMixerGroup[] groups = mixer.FindMatchingGroups("SE");
        if (groups == null || groups.Length == 0)
        {
            Debug.LogWarning("[EffectManager] SE mixer group not found.");
            return null;
        }
        seMixerGroup = groups[0];
        return seMixerGroup;
    }
    private static Dictionary<SEnums, int> SEExistT = new Dictionary<SEnums, int>() {
        { SEnums.soul,         90 },
        { SEnums.bite,         30 },
        { SEnums.critical,     30 },
        { SEnums.savage,       30 },
        { SEnums.zombieKiller,   30 },
        { SEnums.wave_invalid, 30 },
        { SEnums.invalid,      30 },
        { SEnums.wave,         30 },
        { SEnums.wave_e,       30 },
        { SEnums.heal,         60 },
        { SEnums.heal_e,       60 },
        { SEnums.surge,        -1 },
        { SEnums.surge_e,      -1 }
    };
    private static Dictionary<SEnums, int> Ydeviation = new Dictionary<SEnums, int>() {
        { SEnums.soul,         1 },
        { SEnums.bite,         1 },
        { SEnums.critical,     1 },
        { SEnums.savage,       1 },
        { SEnums.zombieKiller,   1 },
        { SEnums.wave_invalid, 1 },
        { SEnums.invalid,      1 },
        { SEnums.wave,         -1 },
        { SEnums.wave_e,       -1 },
        { SEnums.heal,         1 },
        { SEnums.heal_e,       1 },
        { SEnums.surge,        -1 },
        { SEnums.surge_e,      -1 }
    };
    private AnimDecryptPack GetOrCreateEffectPack(string effectName)
    {
        string key = GetEffectPackKey(effectName);
        if (packCache.TryGetValue(key, out AnimDecryptPack cached) && cached != null) return cached;

        string folder = GetEffectResourceFolder(effectName);
        AnimDecryptPack pack = BuildPackFromFolder(folder, effectName);
        if (pack == null) return null;
        packCache[key] = pack;
        return pack;
    }

    public AnimationDisplayer InstantiateRuntimeBattleObject(
        string resourceRoot,
        string[] maanimNames,
        Vector3 worldPosition,
        Transform parent = null,
        bool worldPositionStays = true)
    {
        if (EffectObjectNull == null || string.IsNullOrEmpty(resourceRoot) || maanimNames == null || maanimNames.Length == 0) return null;

        string packKey = GetRuntimePackKey(resourceRoot, maanimNames);
        AnimDecryptPack pack = GetOrCreateRuntimePack(resourceRoot, maanimNames);
        if (pack == null) return null;

        return SpawnDisplayFromPool(packKey, pack, worldPosition, parent, worldPositionStays, worldPosition.y);
    }

    private AnimDecryptPack GetOrCreateRuntimePack(string resourceRoot, string[] maanimNames)
    {
        string key = GetRuntimePackKey(resourceRoot, maanimNames);
        if (packCache.TryGetValue(key, out AnimDecryptPack cached) && cached != null)
        {
            return cached;
        }

        Texture2D sprite = Resources.Load<Texture2D>(resourceRoot + "sprite");
        TextAsset imgcut = Resources.Load<TextAsset>(resourceRoot + "imgcut");
        TextAsset mamodel = Resources.Load<TextAsset>(resourceRoot + "mamodel");
        if (sprite == null || imgcut == null || mamodel == null) return null;

        TextAsset[] maanims = new TextAsset[maanimNames.Length];
        for (int i = 0; i < maanimNames.Length; i++)
        {
            maanims[i] = Resources.Load<TextAsset>(resourceRoot + maanimNames[i]);
            if (maanims[i] == null) return null;
        }

        AnimEncryptPack animEncryptPack = new AnimEncryptPack(sprite, imgcut, mamodel, maanims);
        AnimDecryptPack pack = AnimFileDecrypter.DecryptEncryptPack(animEncryptPack);
        packCache[key] = pack;
        return pack;
    }

    private AnimDecryptPack BuildPackFromFolder(string resourceFolder, string effectName)
    {
        // Strict naming rule:
        // folder: Effects/source file/{effect}
        // files : sprite, imgcut, mamodel, maanim(_0.._3) (continuous from 0)
        Texture2D sprite = Resources.Load<Texture2D>($"{resourceFolder}/sprite");
        TextAsset imgcut = Resources.Load<TextAsset>($"{resourceFolder}/imgcut");
        TextAsset mamodel = Resources.Load<TextAsset>($"{resourceFolder}/mamodel");
        TextAsset[] maanims = LoadMaanimSequence(resourceFolder, effectName);

        if (sprite == null || imgcut == null || mamodel == null || maanims == null || maanims.Length == 0)
        {
            Debug.LogError($"[EffectManager] Effect resource naming mismatch for '{effectName}' under '{resourceFolder}'.");
            return null;
        }

        AnimEncryptPack animEncryptPack = new AnimEncryptPack(sprite, imgcut, mamodel, maanims);
        return AnimFileDecrypter.DecryptEncryptPack(animEncryptPack);
    }

    private TextAsset[] LoadMaanimSequence(string resourceFolder, string effectName)
    {
        List<TextAsset> list = new List<TextAsset>();
        TextAsset maanim_single = Resources.Load<TextAsset>($"{resourceFolder}/maanim");
        if (maanim_single != null) list.Add(maanim_single);
        else for (int i = 0; i <= 3; i++)
        {
            TextAsset maanim = Resources.Load<TextAsset>($"{resourceFolder}/maanim_{i}");
            if (maanim == null)
            {
                if (i == 0)
                {
                    Debug.LogError($"[EffectManager] Missing required file: {resourceFolder}/maanim(_n) for effect '{effectName}'.");
                    return null;
                }
                break;
            }
            list.Add(maanim);
        }
        return list.ToArray();
    }

    private AnimationDisplayer SpawnDisplayFromPool(
        string poolKey,
        AnimDecryptPack pack,
        Vector3 worldPosition,
        Transform parent,
        bool worldPositionStays,
        float sortY)
    {
        AnimationDisplayer ad = TryTakeFromPool(poolKey);
        if (ad == null)
        {
            GameObject go = Instantiate(EffectObjectNull, worldPosition, Quaternion.identity);
            ad = go.GetComponent<AnimationDisplayer>();
            if (ad == null) ad = go.AddComponent<AnimationDisplayer>();
            ad.Initialization(pack);
            PooledEffectLifetime lifetime = ad.GetComponent<PooledEffectLifetime>();
            if (lifetime == null) lifetime = ad.gameObject.AddComponent<PooledEffectLifetime>();
        }
        else
        {
            ad.gameObject.SetActive(true);
            ad.SetAnimationSpeed(1f);
            ad.PlayAnimation(0);
        }

        if (parent != null) ad.transform.SetParent(parent, worldPositionStays);
        else ad.transform.SetParent(null, true);
        ad.transform.position = worldPosition;

        int resetlayer = ResolveEffectOrderLayer(parent, sortY);
        CharacterSummoner.ResetAnimationOrderLayer(ad, resetlayer);
        return ad;
    }

    private int ResolveEffectOrderLayer(Transform parent, float sortY)
    {
        int resetlayer = 0;
        if (parent != null) resetlayer= (int)(20000 * (1.1f - parent.position.y));
        else
        {
            resetlayer = (int)(20000 * (1 - sortY));
            if (Mathf.Abs(resetlayer) > 21000) resetlayer = 21000;
        }
        return resetlayer;
    }

    private AnimationDisplayer TryTakeFromPool(string poolKey)
    {
        if (!displayPool.TryGetValue(poolKey, out Queue<AnimationDisplayer> queue) || queue == null) return null;
        while (queue.Count > 0)
        {
            AnimationDisplayer ad = queue.Dequeue();
            if (ad == null) continue;
            if (ad.gameObject == null) continue;
            return ad;
        }
        return null;
    }

    public void RecycleDisplay(AnimationDisplayer ad, string poolKey)
    {
        if (ad == null || string.IsNullOrEmpty(poolKey)) return;
        if (ad.gameObject == null) return;
        if (!displayPool.TryGetValue(poolKey, out Queue<AnimationDisplayer> queue) || queue == null)
        {
            queue = new Queue<AnimationDisplayer>();
            displayPool[poolKey] = queue;
        }
        ad.transform.SetParent(transform, true);
        ad.gameObject.SetActive(false);
        queue.Enqueue(ad);
    }

    public void RecycleRuntimeBattleObject(AnimationDisplayer ad, string resourceRoot, string[] maanimNames)
    {
        if (ad == null || string.IsNullOrEmpty(resourceRoot) || maanimNames == null || maanimNames.Length == 0) return;
        RecycleDisplay(ad, GetRuntimePackKey(resourceRoot, maanimNames));
    }

    public void RecycleBattleObject(AnimationDisplayer ad, string effectName)
    {
        if (ad == null || string.IsNullOrEmpty(effectName)) return;
        RecycleDisplay(ad, GetEffectPackKey(effectName));
    }

    private string GetEffectResourceFolder(string effectName) => $"{EffectSourceRoot}{effectName}";
    private static string GetEffectPackKey(string effectName) => $"e:{effectName}";
    private static string GetEffectSoundKey(string effectName) => $"s:{effectName}";
    private static string GetRuntimePackKey(string root, string[] maanimNames) => $"runtime:{root}|{string.Join(",", maanimNames)}";
    //public AnimDecryptPack GetCatWave() { return wave_decrypt; }
    //public AnimDecryptPack GetEnemyWave() { return wave_e_decrypt; }
}
public enum SEnums
{
    soul, bite, critical, savage, zombieKiller, wave_invalid, invalid, wave, wave_e, heal, heal_e, surge, surge_e
}

public class PooledEffectLifetime : MonoBehaviour
{
    private EffectManager manager;
    private AnimationDisplayer ad;
    private string poolKey;
    private int remainingFrames;
    private bool recycleOnExpire;
    private bool active;

    public void Activate(EffectManager effectManager, string key, int durationFrames, bool recycle)
    {
        manager = effectManager;
        poolKey = key;
        remainingFrames = durationFrames;
        recycleOnExpire = recycle;
        active = durationFrames > 0;
        if (ad == null) ad = GetComponent<AnimationDisplayer>();
    }

    private void FixedUpdate()
    {
        if (!active) return;
        remainingFrames--;
        if (remainingFrames > 0) return;
        active = false;
        if (recycleOnExpire && manager != null)
        {
            manager.RecycleDisplay(ad, poolKey);
            return;
        }
        Destroy(gameObject);
    }
}

