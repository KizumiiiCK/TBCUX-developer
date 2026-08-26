using System;
using System.Collections;

/// <summary>
/// The game's clock.
///
/// On the Builda platform every non-platform network call is blocked, so there is no time server to
/// ask. The platform runs its own clock-tampering defences on the host side, which is what the old
/// network fetch was really guarding against - so the correct client behaviour here is simply to
/// trust the local clock and let the host police it.
///
/// <see cref="FetchUtc8DateTime"/> keeps its coroutine shape on purpose: callers
/// (<c>CheckInSystem</c>, <c>BontiqueCanvas</c>) drive it through a <c>LoadingPage</c> task and
/// handle a null result as "connection failed". Changing the signature would mean rewriting those
/// flows for no behavioural gain, and the coroutine now simply completes on the first yield.
/// </summary>
public static class PlatformTimeSystem
{
    private static readonly TimeSpan Utc8Offset = TimeSpan.FromHours(8);

    public static DateTime Now => DateTime.Now;

    public static DateTime Today => Now.Date;

    /// <summary>
    /// UTC+8 wall-clock time, derived from the device clock rather than a time server.
    ///
    /// Computed from <see cref="DateTime.UtcNow"/> plus a fixed offset instead of local time, so a
    /// player in another timezone still gets the UTC+8 calendar day the daily-reset and shop-window
    /// logic is written against.
    /// </summary>
    public static DateTime Utc8Now => DateTime.UtcNow.Add(Utc8Offset);

    /// <summary>
    /// Yields once and reports <see cref="Utc8Now"/>. Never fails, so the "Connection Failed"
    /// branches in the callers are now unreachable rather than removed - deliberately, since
    /// those callers also guard against other failures.
    /// </summary>
    public static IEnumerator FetchUtc8DateTime(
        Action<DateTime?> onComplete,
        Action<string> onProgress = null)
    {
        onProgress?.Invoke("Reading local clock...");
        yield return null;
        onComplete?.Invoke(Utc8Now);
    }
}
