using System;
using System.Collections;

/// <summary>
/// Local clock plus optional UTC+8 world-time fetch for Editor / Windows / Android.
/// </summary>
public static class PlatformTimeSystem
{
    public static DateTime Now => DateTime.Now;

    public static DateTime Today => Now.Date;

    public static IEnumerator FetchUtc8DateTime(
        Action<DateTime?> onComplete,
        Action<string> onProgress = null)
    {
        yield return WorldTimeService.FetchUtc8DateTime(onComplete, onProgress);
    }
}
