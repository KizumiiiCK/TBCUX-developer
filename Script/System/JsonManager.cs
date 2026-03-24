using System.IO;
using UnityEngine;
using Newtonsoft.Json;

public static class JsonManager
{
    // 读取指定路径下的JSON文件，并将其内容转换为TextAsset，并保存为textasset.txt
    public static TextAsset LoadJsonFileAsTextAsset(string filePath, string filename)
    {
        string fullPath = Path.Combine(filePath, filename);
        if (!File.Exists(fullPath))
        {
            Debug.LogError("File does not exist: " + fullPath);
            return null;
        }

        try
        {
            string jsonContent = File.ReadAllText(fullPath);
            TextAsset textAsset = new TextAsset(jsonContent);

            // 保存TextAsset内容到textasset.txt
            string textAssetFilePath = Path.Combine(Path.GetDirectoryName(filePath), $"{filename}.txt");
            File.WriteAllText(textAssetFilePath, jsonContent);

            Debug.Log("TextAsset saved to: " + textAssetFilePath);
            return textAsset;
        }
        catch (System.Exception e)
        {
            Debug.LogError("Failed to load JSON file: " + e.Message);
            return null;
        }
    }

    // 读取TextAsset中的JSON数据，并将其解析为指定的数据类型
    public static T LoadDataFromTextAsset<T>(TextAsset textAsset)
    {
        if (textAsset == null)
        {
            Debug.LogError("TextAsset is null");
            return default(T);
        }

        try
        {
            T data = JsonConvert.DeserializeObject<T>(textAsset.text);
            return data;
        }
        catch (System.Exception e)
        {
            Debug.LogError("Failed to deserialize JSON: " + e.Message);
            return default(T);
        }
    }

    // 保存数据到指定路径的JSON文件
    public static void SaveDataToJsonFile<T>(T data, string filePath)
    {
        filePath += ".txt";
        try
        {
            string jsonContent = JsonConvert.SerializeObject(data, Formatting.Indented);
            File.WriteAllText(filePath, jsonContent);
            Debug.Log("Data saved to JSON file: " + filePath);
        }
        catch (System.Exception e)
        {
            Debug.LogError("Failed to save JSON file: " + e.Message);
        }
    }
}