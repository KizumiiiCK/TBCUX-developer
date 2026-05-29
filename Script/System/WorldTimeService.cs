using System;
using System.Collections;
using System.Collections.Generic;
using System.Globalization;
using UnityEngine;
using UnityEngine.Networking;

/// <summary>
/// Centralized network world-time fetcher (normalized to UTC+8).
/// </summary>
public static class WorldTimeService
{
    private static readonly TimeSpan Utc8Offset = TimeSpan.FromHours(8);

    private const float DefaultRetryWindowSeconds = 30f;
    private const float DefaultRetryDelaySeconds = 1.0f;
    private const int RequestTimeoutSeconds = 8;

    private static readonly JsonSource[] JsonSources =
    {
        new JsonSource("timeapi.io", "https://timeapi.io/api/Time/current/zone?timeZone=Asia/Shanghai"),
        // Mainland-friendly timestamp source.
        new JsonSource("taobao-time", "https://api.m.taobao.com/rest/api3.do?api=mtop.common.getTimestamp"),
        new JsonSource("worldtimeapi", "https://worldtimeapi.org/api/timezone/Asia/Shanghai"),
    };

    private static readonly HeaderSource[] HeaderSources =
    {
        // Fallback to Date header from large providers (usually stable in mainland networks).
        new HeaderSource("baidu", "https://www.baidu.com"),
        new HeaderSource("qq", "https://www.qq.com"),
        new HeaderSource("aliyun", "https://www.aliyun.com"),
        new HeaderSource("cloudflare", "https://www.cloudflare.com"),
    };

    public static IEnumerator FetchUtc8DateTime(
        Action<DateTime?> onComplete,
        Action<string> onProgress = null,
        float retryWindowSeconds = DefaultRetryWindowSeconds,
        float retryDelaySeconds = DefaultRetryDelaySeconds)
    {
        float start = Time.realtimeSinceStartup;
        while (Time.realtimeSinceStartup - start < retryWindowSeconds)
        {
            for (int i = 0; i < JsonSources.Length; i++)
            {
                JsonSource source = JsonSources[i];
                onProgress?.Invoke($"Fetching world time via {source.Name}...");
                DateTime? value = null;
                bool done = false;
                yield return FetchFromJsonSource(source, result =>
                {
                    value = result;
                    done = true;
                });
                while (!done) yield return null;
                if (value.HasValue)
                {
                    onComplete?.Invoke(value.Value);
                    yield break;
                }
            }

            for (int i = 0; i < HeaderSources.Length; i++)
            {
                HeaderSource source = HeaderSources[i];
                onProgress?.Invoke($"Fetching world time via {source.Name} header...");
                DateTime? value = null;
                bool done = false;
                yield return FetchFromHeaderSource(source, result =>
                {
                    value = result;
                    done = true;
                });
                while (!done) yield return null;
                if (value.HasValue)
                {
                    onComplete?.Invoke(value.Value);
                    yield break;
                }
            }

            onProgress?.Invoke("Retrying world time sources...");
            yield return new WaitForSecondsRealtime(retryDelaySeconds);
        }

        onComplete?.Invoke(null);
    }

    private static IEnumerator FetchFromJsonSource(JsonSource source, Action<DateTime?> onDone)
    {
        using (UnityWebRequest request = UnityWebRequest.Get(source.Url))
        {
            request.timeout = RequestTimeoutSeconds;
            yield return request.SendWebRequest();
            if (request.result != UnityWebRequest.Result.Success)
            {
                onDone?.Invoke(null);
                yield break;
            }

            string body = request.downloadHandler != null ? request.downloadHandler.text : string.Empty;
            if (TryParseUtc8FromJson(body, out DateTime parsed))
            {
                onDone?.Invoke(parsed);
                yield break;
            }
        }

        onDone?.Invoke(null);
    }

    private static IEnumerator FetchFromHeaderSource(HeaderSource source, Action<DateTime?> onDone)
    {
        using (UnityWebRequest request = new UnityWebRequest(source.Url, "HEAD"))
        {
            request.timeout = RequestTimeoutSeconds;
            request.downloadHandler = new DownloadHandlerBuffer();
            yield return request.SendWebRequest();
            if (request.result != UnityWebRequest.Result.Success)
            {
                onDone?.Invoke(null);
                yield break;
            }

            Dictionary<string, string> headers = request.GetResponseHeaders();
            if (TryGetHeaderIgnoreCase(headers, "Date", out string dateHeader)
                && TryParseUtc8FromHttpDate(dateHeader, out DateTime parsed))
            {
                onDone?.Invoke(parsed);
                yield break;
            }
        }

        onDone?.Invoke(null);
    }

    private static bool TryParseUtc8FromJson(string json, out DateTime utc8)
    {
        utc8 = default;
        if (string.IsNullOrWhiteSpace(json)) return false;

        string[] dateKeys = { "datetime", "dateTime", "currentDateTime", "utc_datetime", "time" };
        for (int i = 0; i < dateKeys.Length; i++)
        {
            if (TryExtractJsonStringValue(json, dateKeys[i], out string value)
                && TryConvertToUtc8(value, out utc8))
            {
                return true;
            }
        }

        string[] unixKeys = { "unixtime", "timestamp", "ts", "timeStamp", "t" };
        for (int i = 0; i < unixKeys.Length; i++)
        {
            if (TryExtractJsonNumberValue(json, unixKeys[i], out long unixValue)
                && TryConvertUnixToUtc8(unixValue, out utc8))
            {
                return true;
            }
        }

        return false;
    }

    private static bool TryConvertToUtc8(string dateText, out DateTime utc8)
    {
        utc8 = default;
        if (string.IsNullOrWhiteSpace(dateText)) return false;

        string trimmed = dateText.Trim();
        if (!HasExplicitTimezoneSuffix(trimmed))
        {
            // Some providers (e.g. timeapi) return local wall-clock time without timezone.
            // Treat it directly as UTC+8 time instead of parsing it as UTC then shifting.
            if (!DateTime.TryParse(trimmed, CultureInfo.InvariantCulture, DateTimeStyles.None, out DateTime localUtc8))
                return false;
            utc8 = localUtc8;
            return true;
        }

        if (!DateTimeOffset.TryParse(trimmed, CultureInfo.InvariantCulture, DateTimeStyles.AssumeUniversal, out DateTimeOffset dto))
            return false;

        utc8 = dto.ToOffset(Utc8Offset).DateTime;
        return true;
    }

    private static bool HasExplicitTimezoneSuffix(string dateText)
    {
        if (string.IsNullOrWhiteSpace(dateText)) return false;
        string trimmed = dateText.Trim();
        if (trimmed.EndsWith("Z", StringComparison.OrdinalIgnoreCase)) return true;

        int plus = trimmed.LastIndexOf('+');
        int minus = trimmed.LastIndexOf('-');
        int signIndex = Math.Max(plus, minus);
        // Ignore date-only dashes (e.g. yyyy-MM-dd).
        if (signIndex <= 10) return false;

        int suffixLength = trimmed.Length - signIndex;
        // Supports +08, +0800, +08:00 and their negative variants.
        return suffixLength == 3 || suffixLength == 5 || suffixLength == 6;
    }

    private static bool TryConvertUnixToUtc8(long unixValue, out DateTime utc8)
    {
        utc8 = default;
        try
        {
            // Heuristic: large values are milliseconds.
            DateTimeOffset dto = unixValue > 100000000000
                ? DateTimeOffset.FromUnixTimeMilliseconds(unixValue)
                : DateTimeOffset.FromUnixTimeSeconds(unixValue);
            utc8 = dto.ToOffset(Utc8Offset).DateTime;
            return true;
        }
        catch
        {
            return false;
        }
    }

    private static bool TryParseUtc8FromHttpDate(string value, out DateTime utc8)
    {
        utc8 = default;
        if (string.IsNullOrWhiteSpace(value)) return false;

        if (DateTimeOffset.TryParseExact(value, "r", CultureInfo.InvariantCulture, DateTimeStyles.AssumeUniversal, out DateTimeOffset exact))
        {
            utc8 = exact.ToOffset(Utc8Offset).DateTime;
            return true;
        }

        if (DateTimeOffset.TryParse(value, CultureInfo.InvariantCulture, DateTimeStyles.AssumeUniversal, out DateTimeOffset fallback))
        {
            utc8 = fallback.ToOffset(Utc8Offset).DateTime;
            return true;
        }

        return false;
    }

    private static bool TryExtractJsonStringValue(string json, string key, out string value)
    {
        value = null;
        if (string.IsNullOrWhiteSpace(json) || string.IsNullOrWhiteSpace(key)) return false;

        string token = "\"" + key + "\"";
        int keyPos = json.IndexOf(token, StringComparison.Ordinal);
        if (keyPos < 0) return false;

        int colon = json.IndexOf(':', keyPos + token.Length);
        if (colon < 0) return false;

        int firstQuote = json.IndexOf('"', colon + 1);
        if (firstQuote < 0) return false;
        int endQuote = json.IndexOf('"', firstQuote + 1);
        if (endQuote <= firstQuote) return false;

        value = json.Substring(firstQuote + 1, endQuote - firstQuote - 1);
        return true;
    }

    private static bool TryExtractJsonNumberValue(string json, string key, out long value)
    {
        value = 0;
        if (string.IsNullOrWhiteSpace(json) || string.IsNullOrWhiteSpace(key)) return false;

        string token = "\"" + key + "\"";
        int keyPos = json.IndexOf(token, StringComparison.Ordinal);
        if (keyPos < 0) return false;

        int colon = json.IndexOf(':', keyPos + token.Length);
        if (colon < 0) return false;

        int i = colon + 1;
        while (i < json.Length && char.IsWhiteSpace(json[i])) i++;
        if (i >= json.Length) return false;

        if (json[i] == '"')
        {
            int endQuote = json.IndexOf('"', i + 1);
            if (endQuote <= i + 1) return false;
            string numText = json.Substring(i + 1, endQuote - i - 1);
            return long.TryParse(numText, NumberStyles.Integer, CultureInfo.InvariantCulture, out value);
        }

        int start = i;
        while (i < json.Length && (char.IsDigit(json[i]) || json[i] == '-')) i++;
        if (i <= start) return false;

        string raw = json.Substring(start, i - start);
        return long.TryParse(raw, NumberStyles.Integer, CultureInfo.InvariantCulture, out value);
    }

    private static bool TryGetHeaderIgnoreCase(Dictionary<string, string> headers, string key, out string value)
    {
        value = null;
        if (headers == null || string.IsNullOrEmpty(key)) return false;
        foreach (KeyValuePair<string, string> pair in headers)
        {
            if (string.Equals(pair.Key, key, StringComparison.OrdinalIgnoreCase))
            {
                value = pair.Value;
                return true;
            }
        }
        return false;
    }

    private struct JsonSource
    {
        public readonly string Name;
        public readonly string Url;

        public JsonSource(string name, string url)
        {
            Name = name;
            Url = url;
        }
    }

    private struct HeaderSource
    {
        public readonly string Name;
        public readonly string Url;

        public HeaderSource(string name, string url)
        {
            Name = name;
            Url = url;
        }
    }
}
