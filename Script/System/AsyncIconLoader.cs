using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// On-demand async sprite loading for virtualized lists.
///
/// Why this exists: image lists (unit index, enemy index) contain hundreds of icons. Prewarming all
/// of them would mean downloading the whole unit catalog just to open a menu, which defeats the
/// point of streaming content in a browser. Instead the list is *structured* immediately from the
/// catalog (see <see cref="BundledAddressables.Exists"/>, which needs no download) and each icon is
/// fetched only while its cell is actually on screen.
///
/// The hard part is recycling. A virtualized cell can be reused for a different row before its
/// request finishes, so a naive "assign on completion" writes the wrong sprite into the wrong cell.
/// Each cell's currently-wanted address is tracked, and a finished download is applied only if the
/// cell still wants that same address.
/// </summary>
public class AsyncIconLoader : MonoBehaviour
{
    private class Waiter
    {
        public object Owner;
        public string Address;
        public Action<Sprite> Apply;
    }

    private static AsyncIconLoader instance;

    /// <summary>Owner -> the address it currently wants. Rebinding overwrites, invalidating older requests.</summary>
    private readonly Dictionary<object, string> wanted = new Dictionary<object, string>();

    /// <summary>Address -> sprite, so scrolling back over a row does not re-download.</summary>
    private readonly Dictionary<string, Sprite> cache = new Dictionary<string, Sprite>();

    /// <summary>Addresses being downloaded, so N cells sharing an icon issue one request.</summary>
    private readonly Dictionary<string, List<Waiter>> inFlight = new Dictionary<string, List<Waiter>>();

    public static AsyncIconLoader Instance
    {
        get
        {
            if (instance != null) return instance;
            var host = new GameObject("[AsyncIconLoader]");
            DontDestroyOnLoad(host);
            instance = host.AddComponent<AsyncIconLoader>();
            return instance;
        }
    }

    /// <summary>
    /// Requests <paramref name="address"/> for <paramref name="owner"/> (normally the cell
    /// GameObject). <paramref name="apply"/> runs only if the owner still wants this address.
    /// A cached sprite is applied immediately, so already-seen rows never flicker.
    /// </summary>
    public void Load(object owner, string address, Action<Sprite> apply)
    {
        if (owner == null || apply == null) return;

        if (string.IsNullOrEmpty(address))
        {
            wanted.Remove(owner);
            apply(null);
            return;
        }

        // Record intent first: this is what makes a later completion verifiable.
        wanted[owner] = address;

        // Already downloaded - apply synchronously, no frame delay, no flicker.
        if (cache.TryGetValue(address, out Sprite cached))
        {
            wanted.Remove(owner);
            apply(cached);
            return;
        }

        // Blank the cell so a sprite left over from the row this cell previously showed is not
        // mistaken for this row's icon. This is the "empty cell while loading" behaviour.
        apply(null);

        var waiter = new Waiter { Owner = owner, Address = address, Apply = apply };

        if (inFlight.TryGetValue(address, out List<Waiter> waiters))
        {
            // Someone else already asked for this icon; ride along instead of downloading twice.
            waiters.Add(waiter);
            return;
        }

        inFlight[address] = new List<Waiter> { waiter };
        StartCoroutine(LoadRoutine(address));
    }

    /// <summary>
    /// Cancels whatever <paramref name="owner"/> was waiting for. Call when a cell is recycled.
    /// </summary>
    public void Cancel(object owner)
    {
        if (owner != null) wanted.Remove(owner);
    }

    /// <summary>Drops cached sprites. Handles stay owned by BundledAddressables.</summary>
    public void ClearCache() => cache.Clear();

    private IEnumerator LoadRoutine(string address)
    {
        Sprite result = null;

        // Skip the download entirely if the address is not in the catalog: LoadAssetAsync throws
        // InvalidKeyException, and callers legitimately probe optional icon paths.
        if (BundledAddressables.Exists(address, typeof(Sprite)))
        {
            yield return BundledAddressables.Load<Sprite>(address, handle =>
            {
                if (BundledAddressables.TryGetResult(handle, out Sprite sprite)) result = sprite;
            });
        }

        if (result != null) cache[address] = result;

        if (!inFlight.TryGetValue(address, out List<Waiter> waiters)) yield break;
        inFlight.Remove(address);

        for (int i = 0; i < waiters.Count; i++)
        {
            Waiter waiter = waiters[i];
            // Deliver only if this owner still wants this exact address; otherwise the cell was
            // recycled onto a different row while the download was in flight.
            if (!wanted.TryGetValue(waiter.Owner, out string current)) continue;
            if (!string.Equals(current, waiter.Address, StringComparison.Ordinal)) continue;

            wanted.Remove(waiter.Owner);
            waiter.Apply?.Invoke(result);
        }
    }

    private void OnDestroy()
    {
        if (instance == this) instance = null;
    }
}
