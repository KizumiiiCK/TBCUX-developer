using System;
using System.IO;
using UnityEngine;

[Serializable]
public class UserInfoLocalData
{
    public string pid;
    public string user_name;
    public string device_code;

    /// <summary>UTC+8 日期 token（yyyy-MM-dd），本机最后一次成功签到的日期；空字符串表示从未签到。</summary>
    public string last_checkin_date;

    /// <summary>本机记录的连续签到天数。</summary>
    public int consecutive_days;
}

public static class UserInfoLocalStore
{
    public const string FileName = "userinfo";

    public static string FilePath => Path.Combine(Application.persistentDataPath, FileName);

    public static bool Exists()
    {
        return File.Exists(FilePath);
    }

    public static bool TryLoad(out UserInfoLocalData data)
    {
        data = null;
        try
        {
            if (!File.Exists(FilePath)) return false;
            string json = File.ReadAllText(FilePath);
            if (string.IsNullOrWhiteSpace(json)) return false;
            data = JsonUtility.FromJson<UserInfoLocalData>(json);
            return data != null
                && !string.IsNullOrWhiteSpace(data.user_name)
                && !string.IsNullOrWhiteSpace(data.device_code);
        }
        catch (Exception e)
        {
            Debug.LogError($"[UserInfoLocalStore] Load failed: {e.Message}");
            return false;
        }
    }

    public static bool HasOnlinePid(UserInfoLocalData data)
    {
        return data != null && !string.IsNullOrWhiteSpace(data.pid);
    }

    public static bool Save(UserInfoLocalData data)
    {
        if (data == null) return false;
        try
        {
            string dir = Path.GetDirectoryName(FilePath);
            if (!string.IsNullOrWhiteSpace(dir) && !Directory.Exists(dir))
                Directory.CreateDirectory(dir);

            string json = JsonUtility.ToJson(data, true);
            File.WriteAllText(FilePath, json);
            return true;
        }
        catch (Exception e)
        {
            Debug.LogError($"[UserInfoLocalStore] Save failed: {e.Message}");
            return false;
        }
    }

    public static string GetDeviceCode()
    {
        string code = SystemInfo.deviceUniqueIdentifier;
        if (string.IsNullOrWhiteSpace(code)) code = SystemInfo.deviceName;
        return string.IsNullOrWhiteSpace(code) ? "unknown-device" : code;
    }
}
