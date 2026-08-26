using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using Builda;

/// <summary>
/// The privateKV-backed save transport.
///
/// This is the only place that talks to <see cref="BuildaSDK"/> for persistence. It owns an
/// in-memory cache of every save key so the rest of the game can keep reading saves
/// *synchronously* - which it must, because WebGL cannot block and the existing call sites
/// (<c>GameProgressSave.SaveProgress</c>, <c>CharacterUpgradeSave</c>, ...) are ordinary
/// non-coroutine methods invoked mid-gameplay.
///
/// The contract that makes that safe: <see cref="PullAllRoutine"/> must complete during the boot
/// gate, before any gameplay code reads a save. After that, reads hit the cache and writes update
/// the cache immediately and push to the platform asynchronously.
///
/// Platform limits this class is built around (from the Builda skill doc):
/// <list type="bullet">
/// <item>single value ≤ 32KB - <see cref="MaxValueBytes"/>, checked before every write so an
/// oversized payload is reported loudly instead of silently rejected by the host.</item>
/// <item>≤ 32 keys per batch call - <see cref="MaxBatchKeys"/>; the SDK does NOT auto-split and
/// returns <c>BATCH_TOO_LARGE</c>, so batching is done here.</item>
/// <item>≤ 100 live keys per player per game.</item>
/// <item>writes are rate-managed on device: write on event boundaries, never per-frame.</item>
/// </list>
/// </summary>
public static class BuildaSaveBackend
{
    /// <summary>Platform ceiling for one privateKV value.</summary>
    public const int MaxValueBytes = 32 * 1024;

    /// <summary>Platform ceiling for keys in one getMany/setMany/removeMany call.</summary>
    public const int MaxBatchKeys = 32;

    /// <summary>
    /// Warn threshold. A payload this close to the ceiling still writes fine today but will break
    /// as content grows, and the failure would land on players rather than in development - so it
    /// is surfaced while there is still room to reshard.
    /// </summary>
    private const int WarnValueBytes = 24 * 1024;

    private static readonly Dictionary<string, byte[]> cache = new Dictionary<string, byte[]>();

    /// <summary>Keys whose cached value has not yet been confirmed written by the host.</summary>
    private static readonly HashSet<string> dirty = new HashSet<string>();

    private static bool loaded;

    /// <summary>
    /// True once <see cref="PullAllRoutine"/> has finished. Synchronous reads before this point
    /// cannot see cloud data, so they are a bug; <see cref="Get"/> logs when it happens.
    /// </summary>
    public static bool IsLoaded => loaded;

    /// <summary>Number of keys currently held, for diagnosing the 100-key budget.</summary>
    public static int KeyCount => cache.Count;

    // ---- boot: pull everything the game might read ----

    /// <summary>
    /// Fetches <paramref name="keys"/> into the cache in batches of <see cref="MaxBatchKeys"/>.
    /// Calls <paramref name="onDone"/> with false if any batch failed - a partial pull must not be
    /// mistaken for "this player has no save", which would overwrite real progress on first write.
    /// </summary>
    public static IEnumerator PullAllRoutine(IList<string> keys, Action<bool> onDone, Action heartbeat = null)
    {
        cache.Clear();
        dirty.Clear();
        loaded = false;

#if !UNITY_WEBGL || UNITY_EDITOR
        // This branch ships WebGL only. Editor Play Mode has no host KV, so the cache stays
        // empty and reads look like a new player. Persistence is verified with `builda dev`.
        loaded = true;
        onDone?.Invoke(true);
        yield break;
#endif

        if (keys == null || keys.Count == 0)
        {
            loaded = true;
            onDone?.Invoke(true);
            yield break;
        }

        for (int start = 0; start < keys.Count; start += MaxBatchKeys)
        {
            int count = Mathf.Min(MaxBatchKeys, keys.Count - start);
            var batch = new List<string>(count);
            for (int i = 0; i < count; i++) batch.Add(keys[start + i]);

            bool done = false;
            bool ok = false;
            BuildaResult response = null;

            BuildaSDK.KvGetMany(batch, r => { response = r; ok = r.Ok; done = true; });
            while (!done) yield return null;

            heartbeat?.Invoke();

            if (!ok)
            {
                Debug.LogError($"[BuildaSaveBackend] getMany failed: {Describe(response)}");
                loaded = false;
                onDone?.Invoke(false);
                yield break;
            }

            // data is { entries: { key: base64|null } }. A null value means the key is unset.
            var map = ExtractEntries(response);
            if (map == null)
            {
                Debug.LogError("[BuildaSaveBackend] getMany returned no entries map.");
                loaded = false;
                onDone?.Invoke(false);
                yield break;
            }

            foreach (var kv in map)
            {
                byte[] bytes = BuildaSDK.KvBytes(kv.Value);
                if (bytes != null) cache[kv.Key] = bytes;
            }
        }

        loaded = true;
        onDone?.Invoke(true);
    }

    // ---- synchronous cache access ----

    /// <summary>
    /// Reads a key from the cache. Returns null when the key has never been written, which callers
    /// use to mean "no save yet" and build defaults.
    /// </summary>
    public static byte[] Get(string key)
    {
        if (!loaded)
        {
            // Not fatal, but it means a save was read before the boot pull finished, so the value
            // is a false negative. Worth shouting about: the caller will build fresh defaults and
            // the next write will overwrite the player's real cloud save.
            Debug.LogError($"[BuildaSaveBackend] Read '{key}' before the boot pull completed. " +
                           "Run the boot gate first, or the player's progress will be reset.");
        }
        return cache.TryGetValue(key, out var bytes) ? bytes : null;
    }

    public static bool Has(string key) => cache.ContainsKey(key);

    /// <summary>
    /// Updates the cache and queues a push. The cache is authoritative for subsequent reads even
    /// if the push is still in flight, so gameplay never observes a stale value it just wrote.
    /// </summary>
    public static void Set(string key, byte[] value)
    {
        if (string.IsNullOrEmpty(key)) return;
        value = value ?? Array.Empty<byte>();

        if (!CheckSize(key, value)) return;

        cache[key] = value;
        dirty.Add(key);
        PushOne(key, value);
    }

    /// <summary>
    /// Writes several keys as one batch. Preferred when a single player action changes more than
    /// one key: setMany is validated as a unit by the host, so the game cannot end up with half a
    /// state change persisted.
    /// </summary>
    public static void SetMany(IDictionary<string, byte[]> entries)
    {
        if (entries == null || entries.Count == 0) return;

        var pending = new Dictionary<string, byte[]>(entries.Count);
        foreach (var kv in entries)
        {
            if (string.IsNullOrEmpty(kv.Key)) continue;
            byte[] value = kv.Value ?? Array.Empty<byte>();
            if (!CheckSize(kv.Key, value)) continue;
            cache[kv.Key] = value;
            dirty.Add(kv.Key);
            pending[kv.Key] = value;
        }

        // Split to respect the 32-key ceiling. Each chunk is atomic on its own; the whole set is
        // not, so callers that need all-or-nothing must stay within one chunk.
        var keys = new List<string>(pending.Keys);
        for (int start = 0; start < keys.Count; start += MaxBatchKeys)
        {
            int count = Mathf.Min(MaxBatchKeys, keys.Count - start);
            var chunk = new Dictionary<string, byte[]>(count);
            for (int i = 0; i < count; i++) chunk[keys[start + i]] = pending[keys[start + i]];

            var chunkKeys = new List<string>(chunk.Keys);
            BuildaSDK.KvSetMany(chunk, r => OnPushed(chunkKeys, r));
        }
    }

    public static void Remove(string key)
    {
        if (string.IsNullOrEmpty(key)) return;
        cache.Remove(key);
        dirty.Remove(key);
        BuildaSDK.KvRemove(key, r =>
        {
            if (!r.Ok) Debug.LogError($"[BuildaSaveBackend] remove '{key}' failed: {Describe(r)}");
        });
    }

    public static void RemoveMany(IList<string> keys)
    {
        if (keys == null || keys.Count == 0) return;

        for (int i = 0; i < keys.Count; i++)
        {
            cache.Remove(keys[i]);
            dirty.Remove(keys[i]);
        }

        for (int start = 0; start < keys.Count; start += MaxBatchKeys)
        {
            int count = Mathf.Min(MaxBatchKeys, keys.Count - start);
            var chunk = new List<string>(count);
            for (int i = 0; i < count; i++) chunk.Add(keys[start + i]);
            BuildaSDK.KvRemoveMany(chunk, r =>
            {
                if (!r.Ok) Debug.LogError($"[BuildaSaveBackend] removeMany failed: {Describe(r)}");
            });
        }
    }

    // ---- internals ----

    private static Dictionary<string, object> ExtractEntries(BuildaResult r)
    {
        var data = r != null ? r.DataMap : null;
        if (data == null) return null;
        if (data.TryGetValue("entries", out object nested))
        {
            // `entries: null` means none of the requested keys exist - a valid empty save.
            if (nested == null) return new Dictionary<string, object>();
            return nested as Dictionary<string, object>;
        }
        // Tolerate a flat key map if a future SDK drop unwraps `entries`.
        return data;
    }

    private static bool CheckSize(string key, byte[] value)
    {
        if (value.Length > MaxValueBytes)
        {
            // Refusing locally rather than letting the host reject it: this way the message names
            // the key and the actual size, which is what tells you where to shard.
            Debug.LogError($"[BuildaSaveBackend] '{key}' is {value.Length} bytes, over the " +
                           $"{MaxValueBytes}-byte platform limit. NOT saved - this key needs sharding.");
            return false;
        }
        if (value.Length > WarnValueBytes)
        {
            Debug.LogWarning($"[BuildaSaveBackend] '{key}' is {value.Length} bytes, approaching the " +
                             $"{MaxValueBytes}-byte limit. Plan a finer shard before content grows.");
        }
        return true;
    }

    private static void PushOne(string key, byte[] value)
    {
        BuildaSDK.KvSet(key, value, r =>
        {
            if (r.Ok) { dirty.Remove(key); return; }
            Debug.LogError($"[BuildaSaveBackend] set '{key}' failed: {Describe(r)}");
        });
    }

    private static void OnPushed(List<string> keys, BuildaResult r)
    {
        if (r.Ok)
        {
            for (int i = 0; i < keys.Count; i++) dirty.Remove(keys[i]);
            return;
        }
        Debug.LogError($"[BuildaSaveBackend] setMany of {keys.Count} keys failed: {Describe(r)}");
    }

    private static string Describe(BuildaResult r)
    {
        if (r == null) return "no result";
        if (r.Error == null) return "unknown error";
        return $"{r.Error.Code} {r.Error.Message}";
    }
}
