using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.AddressableAssets;
using UnityEngine.ResourceManagement.AsyncOperations;
using UnityEngine.ResourceManagement.ResourceLocations;

/// <summary>
/// Shared helpers for Bundled Addressables (BGM / Visuals / Units).
/// Enemy addresses match old Resources paths, e.g. Units/Enemy Units/e002/data.
/// Folder Addressable entries keep file extensions; this helper resolves both forms.
/// </summary>
public static class BundledAddressables
{
    private static bool initialized;
    private static readonly Dictionary<string, AsyncOperationHandle> handleCache =
        new Dictionary<string, AsyncOperationHandle>();
    private static readonly Dictionary<string, string> resolvedAddressCache =
        new Dictionary<string, string>();

    public static void EnsureInitialized()
    {
        if (initialized) return;
        AsyncOperationHandle init = Addressables.InitializeAsync();
        init.WaitForCompletion();
        initialized = true;
    }

    public static IEnumerator Load<T>(string address, System.Action<AsyncOperationHandle<T>> onCompleted)
        where T : UnityEngine.Object
    {
        if (onCompleted == null) yield break;

        if (string.IsNullOrEmpty(address))
        {
            onCompleted(default);
            yield break;
        }

        EnsureInitialized();
        string resolved = ResolveAddress(address, typeof(T));
        if (resolved == null)
        {
            onCompleted(default);
            yield break;
        }

        AsyncOperationHandle<T> handle = Addressables.LoadAssetAsync<T>(resolved);
        yield return handle;
        onCompleted(handle);
    }

    /// <summary>
    /// Synchronous load for gameplay/UI paths that cannot easily go async yet.
    /// Handles are cached by requested address so repeated loads reuse the same asset.
    /// </summary>
    public static T LoadSync<T>(string address) where T : UnityEngine.Object
    {
        if (string.IsNullOrEmpty(address)) return null;
        EnsureInitialized();

        if (handleCache.TryGetValue(address, out AsyncOperationHandle cached) && cached.IsValid())
        {
            if (cached.Status == AsyncOperationStatus.Succeeded && cached.Result is T typed)
                return typed;
        }

        string resolved = ResolveAddress(address, typeof(T));
        if (resolved == null) return null;

        try
        {
            AsyncOperationHandle<T> handle = Addressables.LoadAssetAsync<T>(resolved);
            T result = handle.WaitForCompletion();
            if (handle.Status != AsyncOperationStatus.Succeeded || result == null)
            {
                if (handle.IsValid()) Addressables.Release(handle);
                return null;
            }

            handleCache[address] = handle;
            return result;
        }
        catch (InvalidKeyException)
        {
            return null;
        }
        catch (Exception e)
        {
            Debug.LogWarning($"BundledAddressables.LoadSync failed for '{address}': {e.Message}");
            return null;
        }
    }


    public static bool Exists(string address, Type type = null)
    {
        if (string.IsNullOrEmpty(address)) return false;
        EnsureInitialized();
        return ResolveAddress(address, type) != null;
    }

    /// <summary>
    /// Maps a Resources-style address (no extension) to the catalog key that actually exists.
    /// </summary>
    public static string ResolveAddress(string address, Type type = null)
    {
        if (string.IsNullOrEmpty(address)) return null;
        EnsureInitialized();

        string cacheKey = type == null ? address : address + "|" + type.FullName;
        if (resolvedAddressCache.TryGetValue(cacheKey, out string cached))
            return string.IsNullOrEmpty(cached) ? null : cached;

        if (Locate(address, type))
        {
            resolvedAddressCache[cacheKey] = address;
            return address;
        }

        foreach (string suffix in GetCandidateSuffixes(type))
        {
            string candidate = address + suffix;
            if (Locate(candidate, type))
            {
                resolvedAddressCache[cacheKey] = candidate;
                return candidate;
            }
        }

        resolvedAddressCache[cacheKey] = string.Empty;
        return null;
    }

    private static bool Locate(string address, Type type)
    {
        if (LocateExact(address, type)) return true;
        if (type == null) return false;

        // Key may be registered as a related type (Texture2D for Sprite, SO base for subclass).
        // Do NOT accept unrelated types — that causes InvalidKeyException on LoadAssetAsync<T>.
        foreach (var locator in Addressables.ResourceLocators)
        {
            if (!locator.Locate(address, null, out IList<IResourceLocation> locations)
                || locations == null
                || locations.Count == 0)
            {
                continue;
            }

            for (int i = 0; i < locations.Count; i++)
            {
                if (IsCompatibleResourceType(type, locations[i].ResourceType))
                    return true;
            }
        }
        return false;
    }

    private static bool IsCompatibleResourceType(Type requested, Type available)
    {
        if (requested == null || available == null) return false;
        if (requested == available) return true;
        if (requested.IsAssignableFrom(available) || available.IsAssignableFrom(requested))
            return true;

        // Addressables often catalogs PNG as Texture2D while gameplay loads Sprite.
        bool requestedSpriteOrTex = requested == typeof(Sprite)
            || typeof(Texture).IsAssignableFrom(requested);
        bool availableSpriteOrTex = available == typeof(Sprite)
            || typeof(Texture).IsAssignableFrom(available);
        return requestedSpriteOrTex && availableSpriteOrTex;
    }

    private static bool LocateExact(string address, Type type)
    {
        foreach (var locator in Addressables.ResourceLocators)
        {
            if (locator.Locate(address, type, out IList<IResourceLocation> locations)
                && locations != null
                && locations.Count > 0)
            {
                return true;
            }
        }
        return false;
    }

    private static IEnumerable<string> GetCandidateSuffixes(Type type)
    {
        if (type == null || type == typeof(UnityEngine.Object))
        {
            yield return ".asset";
            yield return ".prefab";
            yield return ".png";
            yield return ".jpg";
            yield return ".jpeg";
            yield return ".tga";
            yield return ".txt";
            yield return ".bytes";
            yield return ".json";
            yield return ".csv";
            yield return ".controller";
            yield return ".anim";
            yield return ".mat";
            yield return ".wav";
            yield return ".ogg";
            yield return ".mp3";
            yield break;
        }

        if (typeof(ScriptableObject).IsAssignableFrom(type))
        {
            yield return ".asset";
            yield break;
        }

        if (type == typeof(GameObject))
        {
            yield return ".prefab";
            yield break;
        }

        if (type == typeof(Sprite) || type == typeof(Texture2D) || type == typeof(Texture))
        {
            yield return ".png";
            yield return ".PNG";
            yield return ".jpg";
            yield return ".jpeg";
            yield return ".tga";
            yield break;
        }

        if (type == typeof(TextAsset))
        {
            yield return ".txt";
            yield return ".bytes";
            yield return ".json";
            yield return ".csv";
            yield break;
        }

        if (type == typeof(AudioClip))
        {
            yield return ".wav";
            yield return ".ogg";
            yield return ".mp3";
            yield break;
        }

        if (typeof(RuntimeAnimatorController).IsAssignableFrom(type))
        {
            yield return ".controller";
            yield break;
        }

        if (type == typeof(AnimationClip))
        {
            yield return ".anim";
            yield break;
        }

        if (type == typeof(Material))
        {
            yield return ".mat";
            yield break;
        }

        yield return ".asset";
        yield return ".prefab";
        yield return ".png";
        yield return ".txt";
    }

    /// <summary>
    /// Loads numbered children under a folder address: {folder}/0, {folder}/1, ... until a miss.
    /// Replaces Resources.LoadAll for folders that use integer file names.
    /// </summary>
    public static T[] LoadNumberedSync<T>(string folderAddress, int maxCount = 64) where T : UnityEngine.Object
    {
        if (string.IsNullOrEmpty(folderAddress) || maxCount <= 0) return System.Array.Empty<T>();
        EnsureInitialized();

        string root = folderAddress.TrimEnd('/');
        var list = new List<T>(Mathf.Min(maxCount, 8));
        for (int i = 0; i < maxCount; i++)
        {
            T asset = LoadSync<T>($"{root}/{i}");
            if (asset == null) break;
            list.Add(asset);
        }
        return list.ToArray();
    }

    /// <summary>
    /// Loads all direct child assets under a folder address (non-recursive).
    /// Replaces Resources.LoadAll for a folder path.
    /// </summary>
    public static T[] LoadAllInFolderSync<T>(string folderAddress) where T : UnityEngine.Object
    {
        if (string.IsNullOrEmpty(folderAddress)) return System.Array.Empty<T>();
        EnsureInitialized();

        string prefix = folderAddress.TrimEnd('/');
        string prefixSlash = prefix + "/";
        var keys = new List<string>();
        var seen = new HashSet<string>();

        foreach (var locator in Addressables.ResourceLocators)
        {
            foreach (object keyObj in locator.Keys)
            {
                if (!(keyObj is string key) || string.IsNullOrEmpty(key)) continue;
                if (!key.StartsWith(prefixSlash, StringComparison.Ordinal)) continue;

                string remainder = key.Substring(prefixSlash.Length);
                int slash = remainder.IndexOf('/');
                int bracket = remainder.IndexOf('[');
                if (slash >= 0) continue; // nested folder
                if (bracket >= 0) remainder = remainder.Substring(0, bracket);

                // Normalize extension variants to the Resources-style key.
                string noExt = StripKnownExtension(prefixSlash + remainder);
                if (!seen.Add(noExt)) continue;
                keys.Add(noExt);
            }
        }

        keys.Sort(StringComparer.Ordinal);
        var list = new List<T>(keys.Count);
        Type requested = typeof(T);
        for (int i = 0; i < keys.Count; i++)
        {
            // Skip keys that only exist as an incompatible type (e.g. DialoguePortraitSettings
            // when loading Sprite[] from DialogueImage/).
            if (ResolveAddress(keys[i], requested) == null) continue;

            T asset = LoadSync<T>(keys[i]);
            if (asset != null) list.Add(asset);
        }
        return list.ToArray();
    }

    /// <summary>
    /// Loads all Sprite sub-assets for one texture address (sprite sheet).
    /// Replaces Resources.LoadAll&lt;Sprite&gt;(singleTexturePath).
    /// </summary>
    public static Sprite[] LoadSpriteSheetSync(string address)
    {
        if (string.IsNullOrEmpty(address)) return System.Array.Empty<Sprite>();
        EnsureInitialized();

        var sprites = new List<Sprite>(4);
        string resolved = ResolveAddress(address, typeof(Sprite))
            ?? ResolveAddress(address, typeof(Texture2D))
            ?? ResolveAddress(address, null);

        if (resolved != null)
        {
            try
            {
                AsyncOperationHandle<IList<IResourceLocation>> locHandle =
                    Addressables.LoadResourceLocationsAsync(resolved, typeof(Sprite));
                IList<IResourceLocation> locations = locHandle.WaitForCompletion();
                if (locHandle.IsValid()) Addressables.Release(locHandle);

                if (locations != null)
                {
                    for (int i = 0; i < locations.Count; i++)
                    {
                        AsyncOperationHandle<Sprite> handle = Addressables.LoadAssetAsync<Sprite>(locations[i]);
                        Sprite sprite = handle.WaitForCompletion();
                        if (handle.Status == AsyncOperationStatus.Succeeded && sprite != null)
                            sprites.Add(sprite);
                        else if (handle.IsValid())
                            Addressables.Release(handle);
                    }
                }
            }
            catch (Exception e)
            {
                Debug.LogWarning($"BundledAddressables.LoadSpriteSheetSync locations failed for '{address}': {e.Message}");
            }
        }

#if UNITY_EDITOR
        if (sprites.Count <= 1)
        {
            string[] candidates =
            {
                $"Assets/Bundled/{address}.png",
                $"Assets/Bundled/{address}.PNG",
                $"Assets/Bundled/{address}.jpg",
                $"Assets/Bundled/{address}.jpeg",
                $"Assets/Bundled/{address}.tga",
            };
            for (int i = 0; i < candidates.Length; i++)
            {
                string path = candidates[i];
                if (!System.IO.File.Exists(path)) continue;
                UnityEngine.Object[] all = UnityEditor.AssetDatabase.LoadAllAssetsAtPath(path);
                sprites.Clear();
                for (int j = 0; j < all.Length; j++)
                {
                    if (all[j] is Sprite sp) sprites.Add(sp);
                }
                if (sprites.Count > 0) break;
            }
        }
#endif

        if (sprites.Count == 0)
        {
            Sprite one = LoadSync<Sprite>(address);
            if (one != null) sprites.Add(one);
        }

        sprites.Sort((a, b) => string.CompareOrdinal(a.name, b.name));
        return sprites.ToArray();
    }

    private static string StripKnownExtension(string address)
    {
        foreach (string suffix in GetCandidateSuffixes(null))
        {
            if (address.EndsWith(suffix, StringComparison.OrdinalIgnoreCase))
                return address.Substring(0, address.Length - suffix.Length);
        }
        return address;
    }

    public static bool TryGetResult<T>(AsyncOperationHandle<T> handle, out T asset) where T : UnityEngine.Object
    {
        asset = null;
        if (!handle.IsValid() || handle.Status != AsyncOperationStatus.Succeeded || handle.Result == null)
            return false;
        asset = handle.Result;
        return true;
    }

    public static void Release<T>(ref AsyncOperationHandle<T> handle, ref bool hasHandle) where T : UnityEngine.Object
    {
        if (!hasHandle) return;
        if (handle.IsValid()) Addressables.Release(handle);
        hasHandle = false;
        handle = default;
    }
}
