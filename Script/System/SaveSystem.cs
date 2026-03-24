using UnityEngine;
using System.IO;
using System.Security.Cryptography;
using System.Text;

public static class SaveSystem
{
    public static void DataUpdate(string filename, object data)
    {
        var json = JsonUtility.ToJson(data,true);
        //var path = Path.Combine(Application.persistentDataPath, $"{filename}.json");
        var path = Path.Combine(Application.streamingAssetsPath, $"{filename}.json");
        try
        {
            File.WriteAllText(path, json);
            Debug.Log($"Successfully saved data to {path}.");
        }
        catch (System.Exception ex)
        {
            Debug.LogError($"Failed to save data.\n{ex}");
        }
    }
    public static void TestSave(string filepath, object data)
    {
        var json = JsonUtility.ToJson(data, true);
        try
        {
            File.WriteAllText(filepath, json);
            Debug.Log($"Successfully saved data to {filepath}.");
        }
        catch (System.Exception ex)
        {
            Debug.LogError($"Failed to save data.\n{ex}");
        }
    }

    public static T DataLoad<T>(string filename)
    {
        //Debug.Log(Application.persistentDataPath);
        var path = Path.Combine(Application.streamingAssetsPath, $"{filename}.json");
        try
        {
            var json = File.ReadAllText(path);
            var data = JsonUtility.FromJson<T>(json);
            return data;
        }
        catch (System.Exception ex)
        {
            Debug.LogError($"Failed to load data.\n{ex}");
            return default;
        }
    }

    private static string GetMd5Hash(string input)
    {
        using (MD5 md5Hash = MD5.Create())
        {
            byte[] data = md5Hash.ComputeHash(Encoding.UTF8.GetBytes(input));
            StringBuilder sBuilder = new StringBuilder();
            for (int i = 0; i < data.Length; i++)
            {
                sBuilder.Append(data[i].ToString("x2"));
            }
            return sBuilder.ToString();
        }
    }

    public static void DeleteFile(string filename)
    {
        var path = Path.Combine(Application.persistentDataPath, $"{filename}.json");
        try
        {
            File.Delete(path);
        }
        catch (System.Exception ex)
        {
            Debug.LogError($"Failed to delete data from {path}.\n{ex}");
        }
    }

    public static bool CheckFileExistence(string filename)
    {
        string latestpath = Path.Combine(Application.persistentDataPath, $"{filename}.json");
        if (File.Exists(latestpath)) return true;
        else return false;
    }

    public static void IllegalQuit()
    {
        Application.Quit();
    }

}
