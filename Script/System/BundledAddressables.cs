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
///
/// WebGL contract
/// --------------
/// A browser cannot block the main thread, so <see cref="AsyncOperationHandle.WaitForCompletion"/>
/// deadlocks under WebGL. The sync API (<see cref="LoadSync{T}"/> and friends) therefore no longer
/// performs I/O: it only reads an in-memory cache that must be filled ahead of time by
/// <see cref="PrewarmRoutine"/> (normally driven by LoadingPage before a scene is entered).
///
/// A sync call that is not prewarmed is a bug, not a fallback. It is reported as
/// "[PREWARM MISS]" so the missing address can be added to the scene's prewarm list.
/// In the Editor the legacy blocking load still runs after the warning, so the game stays
/// playable while you collect the miss list. Use <see cref="GetMissReport"/> to dump it.
/// </summary>
public static class BundledAddressables
{
    private static bool initialized;
    private static bool initializing;

    // Cached load handles, keyed by address + requested type. Keying by address alone would let a
    // Sprite request and a Texture2D request for the same file evict each other.
    private static readonly Dictionary<string, AsyncOperationHandle> handleCache =
        new Dictionary<string, AsyncOperationHandle>();
    private static readonly Dictionary<string, string> resolvedAddressCache =
        new Dictionary<string, string>();
    private static readonly Dictionary<string, Sprite[]> spriteSheetCache =
        new Dictionary<string, Sprite[]>();

    // Addresses that were requested synchronously without being prewarmed.
    private static readonly HashSet<string> missedAddresses = new HashSet<string>();

    /// <summary>True once the catalog is loaded and sync cache reads are meaningful.</summary>
    public static bool IsReady => initialized;

    /// <summary>Addresses hit synchronously without a prewarm, in first-seen order.</summary>
    public static IEnumerable<string> Misses => missedAddresses;

    public static int MissCount => missedAddresses.Count;

    private static string CacheKey(string address, Type type)
        => type == null ? address : address + "|" + type.FullName;

    #region Initialization

    /// <summary>
    /// Boot-time catalog initialization. Yield this from LoadingPage before loading any content.
    /// </summary>
    public static IEnumerator InitializeRoutine(Action<bool> onComplete = null)
    {
        if (initialized)
        {
            onComplete?.Invoke(true);
            yield break;
        }

        // Another routine is already initializing - just wait for it.
        while (initializing && !initialized) yield return null;
        if (initialized)
        {
            onComplete?.Invoke(true);
            yield break;
        }

        initializing = true;

        // Non-generic handle + parameterless overload: the only form present in every Addressables
        // version. The init handle is intentionally not released - Addressables owns the catalog.
        AsyncOperationHandle init = default;
        bool started = false;
        try
        {
            init = Addressables.InitializeAsync();
            started = true;
        }
        catch (Exception e)
        {
            Debug.LogError($"[BundledAddressables] InitializeAsync threw: {e.Message}");
        }

        if (started)
        {
            yield return init;

            if (init.IsValid() && init.Status != AsyncOperationStatus.Succeeded)
            {
                string reason = init.OperationException != null
                    ? init.OperationException.Message
                    : "unknown error";
                Debug.LogError($"[BundledAddressables] Catalog initialization failed: {reason}");
            }
            else
            {
                initialized = true;
            }
        }

        initializing = false;
        onComplete?.Invoke(initialized);
    }

    /// <summary>
    /// Legacy guard. On WebGL this can only verify that <see cref="InitializeRoutine"/> already ran;
    /// it must never block. On other platforms it keeps the original blocking behaviour so Editor
    /// play mode and standalone builds work without a boot gate.
    /// </summary>
    public static void EnsureInitialized()
    {
        if (initialized) return;

#if UNITY_WEBGL && !UNITY_EDITOR
        // Addressables lazily initializes itself on the first async load, so this is not fatal on
        // its own - but every sync read before the catalog exists will miss.
        Debug.LogError("[BundledAddressables] Used before InitializeRoutine() completed. " +
                       "Run the boot LoadingPage gate first.");
#else
        AsyncOperationHandle init = Addressables.InitializeAsync();
        init.WaitForCompletion();
        initialized = true;
#endif
    }

    #endregion

    #region Async load (preferred)

    public static IEnumerator Load<T>(string address, System.Action<AsyncOperationHandle<T>> onCompleted)
        where T : UnityEngine.Object
    {
        if (onCompleted == null) yield break;

        if (string.IsNullOrEmpty(address))
        {
            onCompleted(default);
            yield break;
        }

        if (!initialized) yield return InitializeRoutine();

        string key = CacheKey(address, typeof(T));
        if (handleCache.TryGetValue(key, out AsyncOperationHandle existing)
            && existing.IsValid()
            && existing.Status == AsyncOperationStatus.Succeeded)
        {
            onCompleted(existing.Convert<T>());
            yield break;
        }

        string resolved = ResolveAddress(address, typeof(T));
        if (resolved == null)
        {
            onCompleted(default);
            yield break;
        }

        AsyncOperationHandle<T> handle = Addressables.LoadAssetAsync<T>(resolved);
        yield return handle;
        if (handle.Status == AsyncOperationStatus.Succeeded && handle.Result != null)
        {
            handleCache[key] = handle;
            missedAddresses.Remove(key);
        }
        onCompleted(handle);
    }

    #endregion

    #region Prewarm

    /// <summary>
    /// A batch of addresses to resolve and load before entering a scene. Build one with the typed
    /// Add* helpers, then yield <see cref="PrewarmRoutine"/>.
    /// </summary>
    public class PrewarmList
    {
        internal readonly List<Entry> Entries = new List<Entry>();
        private readonly HashSet<string> seen = new HashSet<string>();

        internal struct Entry
        {
            public string Label;
            // The heartbeat is threaded in explicitly (rather than kept in a static) so that
            // concurrent prewarms cannot clobber each other's progress reporting.
            public Func<Action, IEnumerator> Run;
        }

        public int Count => Entries.Count;

        /// <summary>Queue a single asset. Safe to call with duplicates or null/empty addresses.</summary>
        public PrewarmList Add<T>(string address) where T : UnityEngine.Object
        {
            if (string.IsNullOrEmpty(address)) return this;
            string key = CacheKey(address, typeof(T));
            if (!seen.Add(key)) return this;
            Entries.Add(new Entry { Label = address, Run = hb => PrewarmSingle<T>(address, hb) });
            return this;
        }

        /// <summary>Queue every sprite of a sprite sheet (see <see cref="LoadSpriteSheetSync"/>).</summary>
        public PrewarmList AddSpriteSheet(string address)
        {
            if (string.IsNullOrEmpty(address)) return this;
            if (!seen.Add("sheet|" + address)) return this;
            Entries.Add(new Entry { Label = address, Run = hb => PrewarmSpriteSheet(address, hb) });
            return this;
        }

        /// <summary>Queue {folder}/0, {folder}/1, ... (see <see cref="LoadNumberedSync{T}"/>).</summary>
        public PrewarmList AddNumbered<T>(string folderAddress, int maxCount = 64) where T : UnityEngine.Object
        {
            if (string.IsNullOrEmpty(folderAddress)) return this;
            string root = folderAddress.TrimEnd('/');
            for (int i = 0; i < maxCount; i++) Add<T>($"{root}/{i}");
            return this;
        }

        /// <summary>Queue every direct child of a folder (see <see cref="LoadAllInFolderSync{T}"/>).</summary>
        public PrewarmList AddFolder<T>(string folderAddress) where T : UnityEngine.Object
        {
            if (string.IsNullOrEmpty(folderAddress)) return this;
            foreach (string key in EnumerateFolderKeys(folderAddress)) Add<T>(key);
            return this;
        }
    }

    /// <summary>
    /// Loads every queued entry, reporting progress as (0..1, currentLabel).
    /// Missing addresses are logged but do not abort the batch - a level should still start if one
    /// optional effect is absent.
    /// </summary>
    public static IEnumerator PrewarmRoutine(PrewarmList list, Action<float, string> onProgress = null)
    {
        if (list == null || list.Count == 0)
        {
            onProgress?.Invoke(1f, string.Empty);
            yield break;
        }

        if (!initialized) yield return InitializeRoutine();

        int total = list.Count;
        for (int i = 0; i < total; i++)
        {
            PrewarmList.Entry entry = list.Entries[i];
            float baseProgress = i / (float)total;
            onProgress?.Invoke(baseProgress, entry.Label);
            // Heartbeat during the download too, not just between entries: one entry can pull a
            // whole bundle, which on a slow connection outlasts any single stall budget.
            yield return entry.Run(() => onProgress?.Invoke(baseProgress, entry.Label));
        }
        onProgress?.Invoke(1f, string.Empty);
    }

    /// <summary>
    /// Yields until <paramref name="handle"/> finishes, invoking <paramref name="heartbeat"/> only
    /// when the byte count actually advances.
    ///
    /// Firing every frame instead would defeat the caller's stall watchdog entirely: a connection
    /// that opens and then hangs would look healthy forever. Tying the heartbeat to received bytes
    /// keeps a slow-but-progressing download alive while still letting a dead one time out.
    /// </summary>
    private static IEnumerator AwaitWithHeartbeat(AsyncOperationHandle handle, Action heartbeat)
    {
        if (heartbeat == null)
        {
            yield return handle;
            yield break;
        }

        long lastBytes = -1;
        while (handle.IsValid() && !handle.IsDone)
        {
            long bytes = handle.GetDownloadStatus().DownloadedBytes;
            if (bytes != lastBytes)
            {
                lastBytes = bytes;
                heartbeat();
            }
            yield return null;
        }
    }

    private static IEnumerator PrewarmSingle<T>(string address, Action heartbeat = null) where T : UnityEngine.Object
    {
        string key = CacheKey(address, typeof(T));
        if (handleCache.TryGetValue(key, out AsyncOperationHandle existing)
            && existing.IsValid()
            && existing.Status == AsyncOperationStatus.Succeeded)
        {
            yield break;
        }

        string resolved = ResolveAddress(address, typeof(T));
        if (resolved == null)
        {
            // Not an error: callers probe optional addresses (icon_deploy vs enemy_icon).
            yield break;
        }

        AsyncOperationHandle<T> handle;
        try
        {
            handle = Addressables.LoadAssetAsync<T>(resolved);
        }
        catch (Exception e)
        {
            Debug.LogWarning($"[BundledAddressables] Prewarm could not start '{address}': {e.Message}");
            yield break;
        }

        yield return AwaitWithHeartbeat(handle, heartbeat);

        if (handle.Status == AsyncOperationStatus.Succeeded && handle.Result != null)
        {
            handleCache[key] = handle;
            // Already satisfied - drop it from the miss list if a previous frame reported it.
            missedAddresses.Remove(key);
        }
        else
        {
            if (handle.IsValid()) Addressables.Release(handle);
            Debug.LogWarning($"[BundledAddressables] Prewarm failed for '{address}' as {typeof(T).Name}.");
        }
    }

    private static IEnumerator PrewarmSpriteSheet(string address, Action heartbeat = null)
    {
        if (spriteSheetCache.ContainsKey(address)) yield break;

        string resolved = ResolveAddress(address, typeof(Sprite))
            ?? ResolveAddress(address, typeof(Texture2D))
            ?? ResolveAddress(address, null);
        if (resolved == null) yield break;

        AsyncOperationHandle<IList<IResourceLocation>> locHandle =
            Addressables.LoadResourceLocationsAsync(resolved, typeof(Sprite));
        yield return AwaitWithHeartbeat(locHandle, heartbeat);

        var sprites = new List<Sprite>(4);
        if (locHandle.Status == AsyncOperationStatus.Succeeded && locHandle.Result != null)
        {
            IList<IResourceLocation> locations = locHandle.Result;
            for (int i = 0; i < locations.Count; i++)
            {
                AsyncOperationHandle<Sprite> h = Addressables.LoadAssetAsync<Sprite>(locations[i]);
                yield return AwaitWithHeartbeat(h, heartbeat);
                if (h.Status == AsyncOperationStatus.Succeeded && h.Result != null) sprites.Add(h.Result);
                else if (h.IsValid()) Addressables.Release(h);
            }
        }
        if (locHandle.IsValid()) Addressables.Release(locHandle);

        if (sprites.Count == 0)
        {
            // Single-sprite texture: fall back to the plain asset load.
            yield return PrewarmSingle<Sprite>(address);
            Sprite one = ReadCache<Sprite>(address);
            if (one != null) sprites.Add(one);
        }

        sprites.Sort((a, b) => string.CompareOrdinal(a.name, b.name));
        spriteSheetCache[address] = sprites.ToArray();
        missedAddresses.Remove("sheet|" + address);
    }

    #endregion

    #region Sync reads (cache only on WebGL)

    private static T ReadCache<T>(string address) where T : UnityEngine.Object
    {
        if (handleCache.TryGetValue(CacheKey(address, typeof(T)), out AsyncOperationHandle cached)
            && cached.IsValid()
            && cached.Status == AsyncOperationStatus.Succeeded
            && cached.Result is T typed)
        {
            return typed;
        }
        return null;
    }

    private static void ReportMiss(string cacheKey, string address, string typeName)
    {
        if (!missedAddresses.Add(cacheKey)) return;
        Debug.LogError($"[PREWARM MISS] {address} ({typeName}) - add it to this scene's prewarm list.");
    }

    /// <summary>
    /// Distinguishes "not prewarmed" (a bug worth reporting) from "not in the catalog at all"
    /// (a legitimate probe - several callers try an optional address then fall back).
    /// </summary>
    private static bool ExistsInCatalog(string address, Type type) => ResolveAddress(address, type) != null;

    /// <summary>
    /// Reads a prewarmed asset. Does not perform I/O on WebGL; see the class remarks.
    /// Returns null when the address was never prewarmed.
    /// </summary>
    public static T LoadSync<T>(string address) where T : UnityEngine.Object
    {
        if (string.IsNullOrEmpty(address)) return null;

        T cached = ReadCache<T>(address);
        if (cached != null) return cached;

        // Only a real prewarm gap is worth reporting; a probe for an address that does not exist
        // in the catalog is expected behaviour and stays quiet.
        if (ExistsInCatalog(address, typeof(T)))
            ReportMiss(CacheKey(address, typeof(T)), address, typeof(T).Name);

#if UNITY_WEBGL && !UNITY_EDITOR
        return null;
#else
        return LegacyBlockingLoad<T>(address);
#endif
    }

    /// <summary>
    /// Loads numbered children under a folder address: {folder}/0, {folder}/1, ... until a miss.
    /// Replaces Resources.LoadAll for folders that use integer file names.
    /// </summary>
    public static T[] LoadNumberedSync<T>(string folderAddress, int maxCount = 64) where T : UnityEngine.Object
    {
        if (string.IsNullOrEmpty(folderAddress) || maxCount <= 0) return Array.Empty<T>();

        string root = folderAddress.TrimEnd('/');
        var list = new List<T>(Mathf.Min(maxCount, 8));
        for (int i = 0; i < maxCount; i++)
        {
            string address = $"{root}/{i}";
            T asset = ReadCache<T>(address);
#if !UNITY_WEBGL || UNITY_EDITOR
            if (asset == null)
            {
                // Editor/standalone: probe so the sequence still terminates naturally.
                asset = LegacyBlockingLoad<T>(address);
            }
#endif
            if (asset == null) break;
            list.Add(asset);
        }

        // The loop stops at the first gap. If the catalog actually has the next index, the gap is a
        // prewarm hole rather than the end of the sequence - report it instead of silently truncating.
        string nextAddress = $"{root}/{list.Count}";
        if (ExistsInCatalog(nextAddress, typeof(T)))
        {
            ReportMiss(CacheKey(nextAddress, typeof(T)), nextAddress, typeof(T).Name + " (numbered)");
        }
        return list.ToArray();
    }

    /// <summary>
    /// Loads all direct child assets under a folder address (non-recursive).
    /// Replaces Resources.LoadAll for a folder path.
    /// </summary>
    public static T[] LoadAllInFolderSync<T>(string folderAddress) where T : UnityEngine.Object
    {
        if (string.IsNullOrEmpty(folderAddress)) return Array.Empty<T>();

        Type requested = typeof(T);
        var list = new List<T>();
        bool anyCompatibleKey = false;
        foreach (string key in EnumerateFolderKeys(folderAddress))
        {
            // Skip keys that only exist as an incompatible type (e.g. DialoguePortraitSettings
            // when loading Sprite[] from DialogueImage/).
            if (ResolveAddress(key, requested) == null) continue;
            anyCompatibleKey = true;

            T asset = ReadCache<T>(key);
#if !UNITY_WEBGL || UNITY_EDITOR
            if (asset == null) asset = LegacyBlockingLoad<T>(key);
#endif
            if (asset != null) list.Add(asset);
        }

        if (list.Count == 0 && anyCompatibleKey)
        {
            ReportMiss("folder|" + folderAddress + "|" + requested.FullName,
                folderAddress + "/*", requested.Name + "[] (folder)");
        }
        return list.ToArray();
    }

    /// <summary>
    /// Loads all Sprite sub-assets for one texture address (sprite sheet).
    /// Replaces Resources.LoadAll&lt;Sprite&gt;(singleTexturePath).
    /// </summary>
    public static Sprite[] LoadSpriteSheetSync(string address)
    {
        if (string.IsNullOrEmpty(address)) return Array.Empty<Sprite>();

        if (spriteSheetCache.TryGetValue(address, out Sprite[] cached)) return cached;

        if (ExistsInCatalog(address, typeof(Sprite)) || ExistsInCatalog(address, typeof(Texture2D)))
            ReportMiss("sheet|" + address, address, "Sprite[] (sheet)");

#if UNITY_WEBGL && !UNITY_EDITOR
        return Array.Empty<Sprite>();
#else
        return LegacyBlockingSpriteSheet(address);
#endif
    }

    #endregion

    #region Catalog queries (in-memory, safe on WebGL once initialized)

    public static bool Exists(string address, Type type = null)
    {
        if (string.IsNullOrEmpty(address)) return false;
        return ResolveAddress(address, type) != null;
    }

    /// <summary>
    /// Maps a Resources-style address (no extension) to the catalog key that actually exists.
    /// Operates on the in-memory catalog, so it does not block.
    /// </summary>
    public static string ResolveAddress(string address, Type type = null)
    {
        if (string.IsNullOrEmpty(address)) return null;

        string cacheKey = CacheKey(address, type);
        if (resolvedAddressCache.TryGetValue(cacheKey, out string cached))
            return string.IsNullOrEmpty(cached) ? null : cached;

#if !UNITY_WEBGL || UNITY_EDITOR
        EnsureInitialized();
#endif

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

    /// <summary>Direct child keys of a folder address, normalized to Resources-style (no extension).</summary>
    private static IEnumerable<string> EnumerateFolderKeys(string folderAddress)
    {
        string prefixSlash = folderAddress.TrimEnd('/') + "/";
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

                string noExt = StripKnownExtension(prefixSlash + remainder);
                if (!seen.Add(noExt)) continue;
                keys.Add(noExt);
            }
        }

        keys.Sort(StringComparer.Ordinal);
        return keys;
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

    private static string StripKnownExtension(string address)
    {
        foreach (string suffix in GetCandidateSuffixes(null))
        {
            if (address.EndsWith(suffix, StringComparison.OrdinalIgnoreCase))
                return address.Substring(0, address.Length - suffix.Length);
        }
        return address;
    }

    #endregion

    #region Handle helpers

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

    #endregion

    #region Diagnostics

    /// <summary>
    /// Newline-separated list of addresses that were read synchronously without a prewarm.
    /// Paste this into the scene's prewarm list to close the gaps.
    /// </summary>
    public static string GetMissReport()
    {
        if (missedAddresses.Count == 0) return "[BundledAddressables] No prewarm misses.";
        var sb = new System.Text.StringBuilder();
        sb.AppendLine($"[BundledAddressables] {missedAddresses.Count} prewarm miss(es):");
        foreach (string key in missedAddresses) sb.AppendLine("  " + key);
        return sb.ToString();
    }

    public static void ClearMisses() => missedAddresses.Clear();

    #endregion

    #region Legacy blocking path (Editor / standalone only)

#if !UNITY_WEBGL || UNITY_EDITOR
    private static T LegacyBlockingLoad<T>(string address) where T : UnityEngine.Object
    {
        EnsureInitialized();
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

            handleCache[CacheKey(address, typeof(T))] = handle;
            return result;
        }
        catch (InvalidKeyException)
        {
            return null;
        }
        catch (Exception e)
        {
            Debug.LogWarning($"BundledAddressables blocking load failed for '{address}': {e.Message}");
            return null;
        }
    }

    private static Sprite[] LegacyBlockingSpriteSheet(string address)
    {
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
                Debug.LogWarning($"BundledAddressables sprite sheet load failed for '{address}': {e.Message}");
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
            Sprite one = LegacyBlockingLoad<Sprite>(address);
            if (one != null) sprites.Add(one);
        }

        sprites.Sort((a, b) => string.CompareOrdinal(a.name, b.name));
        spriteSheetCache[address] = sprites.ToArray();
        return spriteSheetCache[address];
    }
#endif

    #endregion
}
