using System;
using System.Collections;
using UnityEngine;
#if UNITY_WEBGL && !UNITY_EDITOR
using Builda;
#endif

/// <summary>
/// The game's clock.
///
/// On the Builda player, <see cref="FetchUtc8DateTime"/> asks the platform for the time
/// (<c>BuildaSDK.GetServerTime</c>), not the device. That call is a platform bridge call, so it is
/// not affected by the block on non-platform network access. Everything that grants something once
/// per day - check-in, the anniversary window, the shop's daily rotation - is settled against that
/// answer, so moving the device clock cannot buy a second reward or reopen an expired activity.
///
/// A failed fetch reports null, and every caller already treats null as "do not proceed". That is
/// the deliberate stance: without a trusted date we grant nothing rather than fall back to the
/// device clock, which is exactly what an attacker controls.
///
/// <see cref="Now"/>, <see cref="Today"/> and <see cref="Utc8Now"/> stay on the device clock. They
/// back display and cosmetic decisions only; nothing that grants a reward may read them.
/// </summary>
public static class PlatformTimeSystem
{
    private static readonly TimeSpan Utc8Offset = TimeSpan.FromHours(8);
    private static readonly DateTime UnixEpochUtc = new DateTime(1970, 1, 1, 0, 0, 0, DateTimeKind.Utc);

    /// <summary>
    /// How long to wait for the platform's time reply before giving up. The SDK dispatches by
    /// request id and has no timeout of its own, so a reply that never arrives would otherwise
    /// leave the caller's LoadingPage spinning forever.
    /// </summary>
    private const float ServerTimeTimeoutSeconds = 10f;

    /// <summary>Device local time. Display only - never gate a reward on this.</summary>
    public static DateTime Now => DateTime.Now;

    /// <summary>Device local date. Display only - never gate a reward on this.</summary>
    public static DateTime Today => Now.Date;

    /// <summary>
    /// UTC+8 wall-clock time from the device clock.
    ///
    /// Computed from <see cref="DateTime.UtcNow"/> plus a fixed offset instead of local time, so a
    /// player in another timezone still reads the UTC+8 calendar day the daily-reset and
    /// shop-window logic is written against.
    ///
    /// The device owns this value, so it is only as trustworthy as the player. On the Builda player
    /// use <see cref="FetchUtc8DateTime"/> for anything that grants something.
    /// </summary>
    public static DateTime Utc8Now => DateTime.UtcNow.Add(Utc8Offset);

    /// <summary>Converts the platform's Unix epoch milliseconds into a UTC+8 wall-clock time.</summary>
    public static DateTime Utc8FromUnixMs(double unixMs) =>
        UnixEpochUtc.AddMilliseconds(unixMs).Add(Utc8Offset);

    /// <summary>
    /// Reports the current UTC+8 time, or null when it could not be established.
    ///
    /// On the Builda player this is the platform's clock; elsewhere (editor, native builds) there is
    /// no platform to ask, so it falls back to the device clock and cannot fail.
    /// </summary>
    public static IEnumerator FetchUtc8DateTime(
        Action<DateTime?> onComplete,
        Action<string> onProgress = null)
    {
#if UNITY_WEBGL && !UNITY_EDITOR
        onProgress?.Invoke("Getting world time...");

        bool done = false;
        BuildaResult response = null;
        BuildaSDK.GetServerTime(r => { response = r; done = true; });

        float waited = 0f;
        while (!done && waited < ServerTimeTimeoutSeconds)
        {
            waited += Time.unscaledDeltaTime;
            yield return null;
        }

        if (!done)
        {
            Debug.LogWarning("[PlatformTimeSystem] getServerTime timed out.");
            onProgress?.Invoke("World time timed out.");
            onComplete?.Invoke(null);
            yield break;
        }

        if (!TryReadServerTimeMs(response, out double unixMs))
        {
            Debug.LogWarning($"[PlatformTimeSystem] getServerTime failed: {Describe(response)}");
            onProgress?.Invoke("Could not read world time.");
            onComplete?.Invoke(null);
            yield break;
        }

        onComplete?.Invoke(Utc8FromUnixMs(unixMs));
#else
        onProgress?.Invoke("Reading local clock...");
        yield return null;
        onComplete?.Invoke(Utc8Now);
#endif
    }

#if UNITY_WEBGL && !UNITY_EDITOR
    /// <summary>
    /// Pulls <c>serverTimeMs</c> out of the bridge result.
    ///
    /// The JSON bridge hands numbers back as <see cref="double"/>, but a host that ever sends the
    /// value as a string would otherwise read as a hard failure, so both forms are accepted. A
    /// non-positive value is rejected: it means the field was absent or zero-filled, and treating
    /// it as a date would put the player in 1970 - outside every activity window, and behind any
    /// date already recorded.
    /// </summary>
    private static bool TryReadServerTimeMs(BuildaResult result, out double unixMs)
    {
        unixMs = 0d;
        if (result == null || !result.Ok) return false;

        var map = result.DataMap;
        if (map == null || !map.TryGetValue("serverTimeMs", out object raw) || raw == null) return false;

        if (raw is double d) unixMs = d;
        else if (raw is long l) unixMs = l;
        else if (raw is int i) unixMs = i;
        else if (!(raw is string s) || !double.TryParse(s, out unixMs)) return false;

        return unixMs > 0d;
    }

    private static string Describe(BuildaResult r)
    {
        if (r == null) return "no result";
        if (r.Error == null) return "unknown error";
        return $"{r.Error.Code} {r.Error.Message}";
    }
#endif
}
