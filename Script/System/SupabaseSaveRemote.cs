using System;
using System.Collections;
using System.Collections.Generic;
using System.Text;
using UnityEngine;
using UnityEngine.Networking;

public static class SupabaseSaveRemote
{
    private const float RequestRetryWindowSeconds = 30f;
    private const float RequestRetryDelaySeconds = 1.2f;

    private static string supabaseUrl;
    private static string supabaseKey;
    private static string pid;
    private static bool initialized;

    private static readonly Dictionary<string, object> cache = new Dictionary<string, object>();

    private class CoroutineRunner : MonoBehaviour
    {
        private static CoroutineRunner instance;

        public static CoroutineRunner Instance
        {
            get
            {
                if (instance == null)
                {
                    var go = new GameObject("[SupabaseSaveRemoteRunner]");
                    UnityEngine.Object.DontDestroyOnLoad(go);
                    instance = go.AddComponent<CoroutineRunner>();
                }
                return instance;
            }
        }
    }

    public static void Initialize(string url, string key, string playerId)
    {
        if (string.IsNullOrWhiteSpace(url) || string.IsNullOrWhiteSpace(key) || string.IsNullOrWhiteSpace(playerId))
        {
            supabaseUrl = null;
            supabaseKey = null;
            pid = null;
            initialized = false;
            Debug.LogWarning("[SupabaseSaveRemote] Cloud save is not configured. Local play continues without remote.");
            return;
        }

        supabaseUrl = url;
        supabaseKey = key;
        pid = playerId;
        initialized = true;
    }

    public static bool IsReady() => initialized && !string.IsNullOrWhiteSpace(pid);

    public static void Save<T>(string filename, T data) where T : class
    {
        if (!IsReady())
        {
            Debug.LogError("[SupabaseSaveRemote] Not initialized.");
            return;
        }
        cache[filename] = data;
        CoroutineRunner.Instance.StartCoroutine(UploadByFilename(filename));
    }

    public static T Load<T>(string filename) where T : class
    {
        if (!IsReady())
        {
            Debug.LogError("[SupabaseSaveRemote] Not initialized.");
            return null;
        }
        if (cache.TryGetValue(filename, out var obj))
            return obj as T;
        return null;
    }

    public static void DownloadAll(Action onComplete = null)
    {

        if (!IsReady())
        {
            Debug.LogError("[SupabaseSaveRemote] Not initialized.");
            return;
        }
        CoroutineRunner.Instance.StartCoroutine(DownloadAllCoroutine(onComplete));
    }

    private static IEnumerator DownloadAllCoroutine(Action onComplete)
    {
        // yield return DownloadPlayerPrefs();
        yield return DownloadGameProgress();
        yield return DownloadRewardInventory();
        yield return DownloadCharacterUpgrades();
        yield return DownloadTeamSelections();
        yield return DownloadEnemyMeet();
        onComplete?.Invoke();
    }

    private static IEnumerator UploadByFilename(string filename)
    {
        if (filename == GameProgressSave.filename)
        {
            yield return UploadGameProgress();
        }
        else if (filename == RewardingSystem.filename)
        {
            yield return UploadRewardInventory();
        }
        else if (filename == CharacterUpgradeSave.filename)
        {
            yield return UploadCharacterUpgrades();
        }
        else if (filename == SelectionsSave.filename)
        {
            yield return UploadTeamSelections();
        }
        else if (filename == EnemyMeetSave.filename)
        {
            yield return UploadEnemyMeet();
        }
    }

    // private static IEnumerator DownloadPlayerPrefs()
    // {
    //     string url = $"{supabaseUrl}/rest/v1/player_prefs?pid=eq.{pid}&limit=1";
    //     yield return GetJson(url, json =>
    //     {
    //         var rows = JsonHelper.FromJsonArray<PrefsRow>(json);
    //         if (rows.Length == 0) return;
    //         var r = rows[0];
    //         PlayerPrefs.SetString(UXPref.ChapterName, r.chapter_name ?? UXPref.DefaultChapterName);
    //         PlayerPrefs.SetString(UXPref.SectionName, r.section_name ?? "");
    //         PlayerPrefs.SetInt(UXPref.SectionNum, r.section_num);
    //         PlayerPrefs.SetInt(UXPref.Difficulty, r.difficulty);
    //         PlayerPrefs.SetInt(UXPref.LevelNum, r.level_num);
    //         PlayerPrefs.SetString(UXPref.DirectMark, r.direct_mark ?? "");
    //         PlayerPrefs.SetString(UXPref.LANG, r.lang ?? "");
    //         PlayerPrefs.SetString(UXPref.Localized_BGnum, r.bgnum_cat ?? "");
    //         PlayerPrefs.SetFloat(UXPref.BGM_PARAM, r.bgm_volume);
    //         PlayerPrefs.SetFloat(UXPref.SE_PARAM, r.se_volume);
    //         PlayerPrefs.SetInt(UXPref.BASE_CannonNum, r.base_cannon_num);
    //         PlayerPrefs.SetInt(UXPref.BASE_DecorationNum, r.base_decoration_num);
    //         PlayerPrefs.SetInt(UXPref.BASE_BaseNum, r.base_base_num);
    //         PlayerPrefs.SetString(UXPref.Login_Date, r.login_date ?? "");
    //         PlayerPrefs.SetInt(UXPref.RewardPenalty, r.reward_penalty);
    //     });
    // }

    private static IEnumerator DownloadGameProgress()
    {
        string url = $"{supabaseUrl}/rest/v1/game_progress_sections?pid=eq.{pid}";
        yield return GetJson(url, json =>
        {
            var rows = JsonHelper.FromJsonArray<GameProgressRow>(json);
            var chapters = new Dictionary<string, List<GameProgressRow>>();
            foreach (var row in rows)
            {
                if (!chapters.ContainsKey(row.chapter_name))
                    chapters[row.chapter_name] = new List<GameProgressRow>();
                chapters[row.chapter_name].Add(row);
            }

            var result = new List<GameProgressSave.ChapterClearList>();
            foreach (var kv in chapters)
            {
                var sectionList = new List<GameProgressSave.SectionClearList>();
                foreach (var row in kv.Value)
                {
                    var sec = new GameProgressSave.SectionClearList
                    {
                        SectionName = row.section_name,
                        cleared = row.cleared,
                        reward_gained = row.reward_gained ?? new bool[0],
                        clear_times = JsonConvert.ToInt2D(row.clear_times),
                        level_score = JsonConvert.ToInt2D(row.level_score),
                        cleared_teams = JsonConvert.ToString2D(row.cleared_teams),
                        cleared_cannon = row.cleared_cannon ?? new int[0]
                    };
                    sectionList.Add(sec);
                }
                result.Add(new GameProgressSave.ChapterClearList
                {
                    ChapterName = kv.Key,
                    SectionList = sectionList.ToArray()
                });
            }

            cache[GameProgressSave.filename] = result.ToArray();
        });
    }

    private static IEnumerator DownloadRewardInventory()
    {
        string url = $"{supabaseUrl}/rest/v1/reward_inventory?pid=eq.{pid}";
        yield return GetJson(url, json =>
        {
            var rows = JsonHelper.FromJsonArray<RewardRow>(json);
            int[] items = new int[RewardingSystem.ExpectedInventoryLength];
            foreach (var row in rows)
            {
                if (row.reward_id >= 0 && row.reward_id < items.Length)
                    items[row.reward_id] = row.amount;
            }
            cache[RewardingSystem.filename] = items;
        });
    }

    private static IEnumerator DownloadCharacterUpgrades()
    {
        string url = $"{supabaseUrl}/rest/v1/character_upgrades?pid=eq.{pid}";
        yield return GetJson(url, json =>
        {
            var rows = JsonHelper.FromJsonArray<CharacterUpgradeRow>(json);
            var dict = new Dictionary<string, CharacterUpgradeSave.UpgradeDetails>();
            foreach (var row in rows)
            {
                CharacterProficiency proficiency = new CharacterProficiency
                {
                    level = row.proficiency_level
                };
                proficiency.LoadFromLongProgressArray(row.proficiency_stack);
                proficiency.UpdateLevel();

                var ud = new CharacterUpgradeSave.UpgradeDetails
                {
                    tire_unlocked = row.tire_unlocked ?? new bool[4],
                    talent_unlocked = row.talent_unlocked,
                    upgraded_level = row.upgraded_level,
                    plus_level = row.plus_level,
                    proficiency = proficiency
                };
                dict[row.character_id] = ud;
            }
            cache[CharacterUpgradeSave.filename] = dict;
        });
    }

    private static IEnumerator DownloadTeamSelections()
    {
        string url = $"{supabaseUrl}/rest/v1/team_selections?pid=eq.{pid}";
        yield return GetJson(url, json =>
        {
            var rows = JsonHelper.FromJsonArray<TeamRow>(json);
            string[,] data = new string[SelectionsSave.TeamNum, SelectionsSave.SIZE];
            foreach (var row in rows)
            {
                if (row.team_index < 0 || row.team_index >= SelectionsSave.TeamNum) continue;
                var slots = row.slots ?? new string[SelectionsSave.SIZE];
                for (int i = 0; i < SelectionsSave.SIZE; i++)
                    data[row.team_index, i] = i < slots.Length ? slots[i] : "";
            }
            cache[SelectionsSave.filename] = data;
        });
    }

    private static IEnumerator DownloadEnemyMeet()
    {
        string url = $"{supabaseUrl}/rest/v1/enemy_meet?pid=eq.{pid}";
        yield return GetJson(url, json =>
        {
            var rows = JsonHelper.FromJsonArray<EnemyMeetRow>(json);
            bool[] data = new bool[EnemyMeetSave.SIZE];
            foreach (var row in rows)
            {
                if (row.enemy_code >= 0 && row.enemy_code < data.Length)
                    data[row.enemy_code] = row.met;
            }
            cache[EnemyMeetSave.filename] = data;
        });
    }

    private static IEnumerator UploadGameProgress()
    {
        var data = Load<GameProgressSave.ChapterClearList[]>(GameProgressSave.filename) ?? Array.Empty<GameProgressSave.ChapterClearList>();
        var rows = new List<string>();
        foreach (var chapter in data)
        {
            if (chapter?.SectionList == null) continue;
            foreach (var sec in chapter.SectionList)
            {
                string row = "{"
                    + $"\"pid\":\"{pid}\","
                    + $"\"chapter_name\":\"{JsonEscape(chapter.ChapterName)}\","
                    + $"\"section_name\":\"{JsonEscape(sec.SectionName)}\","
                    + $"\"cleared\":{BoolJson(sec.cleared)},"
                    + $"\"reward_gained\":{BoolArrayJson(sec.reward_gained)},"
                    + $"\"clear_times\":{Int2DJson(sec.clear_times)},"
                    + $"\"level_score\":{Int2DJson(sec.level_score)},"
                    + $"\"cleared_teams\":{String2DJson(sec.cleared_teams)},"
                    + $"\"cleared_cannon\":{IntArrayJson(sec.cleared_cannon)}"
                    + "}";
                rows.Add(row);
            }
        }
        if (rows.Count > 0) yield return Upsert("game_progress_sections", $"[{string.Join(",", rows)}]");
    }

    private static IEnumerator UploadRewardInventory()
    {
        var items = Load<int[]>(RewardingSystem.filename) ?? new int[RewardingSystem.ExpectedInventoryLength];
        var rows = new List<string>();
        for (int i = 0; i < items.Length; i++)
        {
            string row = "{"
                + $"\"pid\":\"{pid}\","
                + $"\"reward_id\":{i},"
                + $"\"amount\":{items[i]}"
                + "}";
            rows.Add(row);
        }
        if (rows.Count > 0) yield return Upsert("reward_inventory", $"[{string.Join(",", rows)}]");
    }

    private static IEnumerator UploadCharacterUpgrades()
    {
        var dict = Load<Dictionary<string, CharacterUpgradeSave.UpgradeDetails>>(CharacterUpgradeSave.filename)
                   ?? new Dictionary<string, CharacterUpgradeSave.UpgradeDetails>();
        var rows = new List<string>();
        foreach (var kv in dict)
        {
            var ud = kv.Value ?? new CharacterUpgradeSave.UpgradeDetails();
            if (ud.proficiency != null)
            {
                ud.proficiency.NormalizeProgress();
                ud.proficiency.UpdateLevel();
            }
            long[] proficiencyStack = ud.proficiency != null ? ud.proficiency.ToLongProgressArray() : new long[4];
            string row = "{"
                + $"\"pid\":\"{pid}\","
                + $"\"character_id\":\"{JsonEscape(kv.Key)}\","
                + $"\"tire_unlocked\":{BoolArrayJson(ud.tire_unlocked)},"
                + $"\"talent_unlocked\":{BoolJson(ud.talent_unlocked)},"
                + $"\"upgraded_level\":{ud.upgraded_level},"
                + $"\"plus_level\":{ud.plus_level},"
                + $"\"proficiency_level\":{(ud.proficiency != null ? ud.proficiency.level : 0)},"
                + $"\"proficiency_stack\":{LongArrayJson(proficiencyStack)}"
                + "}";
            rows.Add(row);
        }
        if (rows.Count > 0) yield return Upsert("character_upgrades", $"[{string.Join(",", rows)}]");
    }

    private static IEnumerator UploadTeamSelections()
    {
        string[,] data = Load<string[,]>(SelectionsSave.filename) ?? new string[SelectionsSave.TeamNum, SelectionsSave.SIZE];
        var rows = new List<string>();
        for (int i = 0; i < SelectionsSave.TeamNum; i++)
        {
            var slots = new string[SelectionsSave.SIZE];
            for (int j = 0; j < SelectionsSave.SIZE; j++)
                slots[j] = data[i, j];
            string row = "{"
                + $"\"pid\":\"{pid}\","
                + $"\"team_index\":{i},"
                + $"\"slots\":{StringArrayJson(slots)}"
                + "}";
            rows.Add(row);
        }
        if (rows.Count > 0) yield return Upsert("team_selections", $"[{string.Join(",", rows)}]");
    }

    private static IEnumerator UploadEnemyMeet()
    {
        bool[] data = Load<bool[]>(EnemyMeetSave.filename) ?? new bool[EnemyMeetSave.SIZE];
        var rows = new List<string>();
        for (int i = 0; i < data.Length; i++)
        {
            string row = "{"
                + $"\"pid\":\"{pid}\","
                + $"\"enemy_code\":{i},"
                + $"\"met\":{BoolJson(data[i])}"
                + "}";
            rows.Add(row);
        }
        if (rows.Count > 0) yield return Upsert("enemy_meet", $"[{string.Join(",", rows)}]");
    }

    public static IEnumerator GetUserCheckInData(Action<DateTime?, int> onComplete)
    {

        if (!IsReady())
        {
            Debug.LogError("[SupabaseSaveRemote] Not initialized.");
            onComplete?.Invoke(null, 0);
            yield break;
        }

        string url = $"{supabaseUrl}/rest/v1/user_accounts?pid=eq.{pid}&select=last_checkin_date,consecutive_days";
        yield return GetJson(url, json =>
        {
            var rows = JsonHelper.FromJsonArray<UserAccountCheckInRow>(json);
            if (rows.Length == 0)
            {
                onComplete?.Invoke(null, 0);
                return;
            }

            DateTime? lastDate = null;
            if (!string.IsNullOrWhiteSpace(rows[0].last_checkin_date)
                && DateTime.TryParse(rows[0].last_checkin_date, out DateTime parsed))
            {
                lastDate = parsed;
            }
            onComplete?.Invoke(lastDate, rows[0].consecutive_days);
        });
    }

    public static IEnumerator UpdateUserCheckInData(DateTime date, int consecutive, Action<bool> onComplete = null)
    {

        if (!IsReady())
        {
            Debug.LogError("[SupabaseSaveRemote] Not initialized.");
            onComplete?.Invoke(false);
            yield break;
        }

        string url = $"{supabaseUrl}/rest/v1/user_accounts?pid=eq.{pid}";
        string payload = "{"
                         + $"\"last_checkin_date\":\"{date.ToString("o")}\","
                         + $"\"consecutive_days\":{consecutive}"
                         + "}";
        byte[] bodyRaw = Encoding.UTF8.GetBytes(payload);
        float start = Time.realtimeSinceStartup;
        int attempt = 0;
        string lastError = string.Empty;
        while (Time.realtimeSinceStartup - start < RequestRetryWindowSeconds)
        {
            attempt++;
            using (UnityWebRequest request = new UnityWebRequest(url, "PATCH"))
            {
                request.uploadHandler = new UploadHandlerRaw(bodyRaw);
                request.downloadHandler = new DownloadHandlerBuffer();
                request.SetRequestHeader("Content-Type", "application/json");
                request.SetRequestHeader("apikey", supabaseKey);
                request.SetRequestHeader("Authorization", $"Bearer {supabaseKey}");
                request.SetRequestHeader("Prefer", "return=minimal");
                yield return request.SendWebRequest();
                if (request.result == UnityWebRequest.Result.Success)
                {
                    onComplete?.Invoke(true);
                    yield break;
                }

                lastError = $"{request.error} - {request.downloadHandler.text}";
                yield return new WaitForSecondsRealtime(RequestRetryDelaySeconds);
            }
        }

        Debug.LogError($"[SupabaseSaveRemote] Update check-in failed after retries: {lastError}");
        onComplete?.Invoke(false);
    }

    private static IEnumerator GetJson(string url, Action<string> onComplete)
    {

        float start = Time.realtimeSinceStartup;
        int attempt = 0;
        string lastError = string.Empty;
        while (Time.realtimeSinceStartup - start < RequestRetryWindowSeconds)
        {
            attempt++;
            using (UnityWebRequest request = UnityWebRequest.Get(url))
            {
                request.SetRequestHeader("apikey", supabaseKey);
                request.SetRequestHeader("Authorization", $"Bearer {supabaseKey}");
                yield return request.SendWebRequest();
                if (request.result == UnityWebRequest.Result.Success)
                {
                    onComplete?.Invoke(request.downloadHandler.text ?? "[]");
                    yield break;
                }

                lastError = $"{request.error} - {request.downloadHandler.text}";
                yield return new WaitForSecondsRealtime(RequestRetryDelaySeconds);
            }
        }

        Debug.LogError($"[SupabaseSaveRemote] Download failed after retries: {lastError}");
        onComplete?.Invoke("[]");
    }

    private static IEnumerator Upsert(string table, string jsonArray)
    {
        string url = $"{supabaseUrl}/rest/v1/{table}";
        byte[] bodyRaw = Encoding.UTF8.GetBytes(jsonArray);
        using (UnityWebRequest request = new UnityWebRequest(url, "POST"))
        {
            request.uploadHandler = new UploadHandlerRaw(bodyRaw);
            request.downloadHandler = new DownloadHandlerBuffer();
            request.SetRequestHeader("Content-Type", "application/json");
            request.SetRequestHeader("apikey", supabaseKey);
            request.SetRequestHeader("Authorization", $"Bearer {supabaseKey}");
            request.SetRequestHeader("Prefer", "resolution=merge-duplicates");
            yield return request.SendWebRequest();
            if (request.result != UnityWebRequest.Result.Success)
            {
                Debug.LogError($"[SupabaseSaveRemote] Upload failed ({table}): {request.error} - {request.downloadHandler.text}");
            }
        }
    }

    private static string BoolJson(bool v) => v ? "true" : "false";

    private static string BoolArrayJson(bool[] arr)
    {
        if (arr == null) return "[]";
        var sb = new StringBuilder("[");
        for (int i = 0; i < arr.Length; i++)
        {
            if (i > 0) sb.Append(',');
            sb.Append(arr[i] ? "true" : "false");
        }
        sb.Append(']');
        return sb.ToString();
    }

    private static string IntArrayJson(int[] arr)
    {
        if (arr == null) return "[]";
        var sb = new StringBuilder("[");
        for (int i = 0; i < arr.Length; i++)
        {
            if (i > 0) sb.Append(',');
            sb.Append(arr[i]);
        }
        sb.Append(']');
        return sb.ToString();
    }

    private static string LongArrayJson(long[] arr)
    {
        if (arr == null) return "[]";
        var sb = new StringBuilder("[");
        for (int i = 0; i < arr.Length; i++)
        {
            if (i > 0) sb.Append(',');
            sb.Append(arr[i]);
        }
        sb.Append(']');
        return sb.ToString();
    }

    private static string StringArrayJson(string[] arr)
    {
        if (arr == null) return "[]";
        var sb = new StringBuilder("[");
        for (int i = 0; i < arr.Length; i++)
        {
            if (i > 0) sb.Append(',');
            sb.Append('"').Append(JsonEscape(arr[i] ?? string.Empty)).Append('"');
        }
        sb.Append(']');
        return sb.ToString();
    }

    private static string Int2DJson(int[,] arr)
    {
        if (arr == null) return "[]";
        int d0 = arr.GetLength(0);
        int d1 = arr.GetLength(1);
        var sb = new StringBuilder("[");
        for (int i = 0; i < d0; i++)
        {
            if (i > 0) sb.Append(',');
            sb.Append('[');
            for (int j = 0; j < d1; j++)
            {
                if (j > 0) sb.Append(',');
                sb.Append(arr[i, j]);
            }
            sb.Append(']');
        }
        sb.Append(']');
        return sb.ToString();
    }

    private static string String2DJson(string[,] arr)
    {
        if (arr == null) return "[]";
        int d0 = arr.GetLength(0);
        int d1 = arr.GetLength(1);
        var sb = new StringBuilder("[");
        for (int i = 0; i < d0; i++)
        {
            if (i > 0) sb.Append(',');
            sb.Append('[');
            for (int j = 0; j < d1; j++)
            {
                if (j > 0) sb.Append(',');
                sb.Append('"').Append(JsonEscape(arr[i, j] ?? string.Empty)).Append('"');
            }
            sb.Append(']');
        }
        sb.Append(']');
        return sb.ToString();
    }

    private static string JsonEscape(string s)
    {
        if (string.IsNullOrEmpty(s)) return "";
        return s.Replace("\\", "\\\\").Replace("\"", "\\\"");
    }

    [Serializable]
    private class PrefsRow
    {
        public string pid;
        public string chapter_name;
        public string section_name;
        public int section_num;
        public int difficulty;
        public int level_num;
        public string direct_mark;
        public string lang;
        public string bgnum_cat;
        public float bgm_volume;
        public float se_volume;
        public int base_cannon_num;
        public int base_decoration_num;
        public int base_base_num;
        public string login_date;
        public int reward_penalty;
    }

    [Serializable]
    private class GameProgressRow
    {
        public string pid;
        public string chapter_name;
        public string section_name;
        public bool cleared;
        public bool[] reward_gained;
        public object clear_times;
        public object level_score;
        public object cleared_teams;
        public int[] cleared_cannon;
    }

    [Serializable]
    private class RewardRow
    {
        public string pid;
        public int reward_id;
        public int amount;
    }

    [Serializable]
    private class CharacterUpgradeRow
    {
        public string pid;
        public string character_id;
        public bool[] tire_unlocked;
        public bool talent_unlocked;
        public int upgraded_level;
        public int plus_level;
        public int proficiency_level;
        public long[] proficiency_stack;
    }

    [Serializable]
    private class TeamRow
    {
        public string pid;
        public int team_index;
        public string[] slots;
    }

    [Serializable]
    private class EnemyMeetRow
    {
        public string pid;
        public int enemy_code;
        public bool met;
    }

    [Serializable]
    private class UserAccountCheckInRow
    {
        public string last_checkin_date;
        public int consecutive_days;
    }

    private static class JsonHelper
    {
        [Serializable]
        private class Wrapper<T>
        {
            public T[] items;
        }

        public static T[] FromJsonArray<T>(string json)
        {
            if (string.IsNullOrWhiteSpace(json)) return new T[0];
            string wrapped = "{\"items\":" + json + "}";
            return JsonUtility.FromJson<Wrapper<T>>(wrapped)?.items ?? new T[0];
        }
    }

    private static class JsonConvert
    {
        public static int[,] ToInt2D(object obj)
        {
            var list = MiniJson.Deserialize(obj) as List<object>;
            if (list == null) return new int[0, 0];
            int rows = list.Count;
            int cols = rows > 0 ? (list[0] as List<object>)?.Count ?? 0 : 0;
            int[,] result = new int[rows, cols];
            for (int i = 0; i < rows; i++)
            {
                var row = list[i] as List<object>;
                for (int j = 0; j < cols; j++)
                    result[i, j] = row != null && j < row.Count ? Convert.ToInt32(row[j]) : 0;
            }
            return result;
        }

        public static string[,] ToString2D(object obj)
        {
            var list = MiniJson.Deserialize(obj) as List<object>;
            if (list == null) return new string[0, 0];
            int rows = list.Count;
            int cols = rows > 0 ? (list[0] as List<object>)?.Count ?? 0 : 0;
            string[,] result = new string[rows, cols];
            for (int i = 0; i < rows; i++)
            {
                var row = list[i] as List<object>;
                for (int j = 0; j < cols; j++)
                    result[i, j] = row != null && j < row.Count ? row[j]?.ToString() ?? "" : "";
            }
            return result;
        }
    }

    private static class MiniJson
    {
        public static object Deserialize(object obj)
        {
            if (obj == null) return null;
            if (obj is string s) return Deserialize(s);
            return obj;
        }

        public static object Deserialize(string json)
        {
            if (string.IsNullOrEmpty(json)) return null;
            return Json.Deserialize(json);
        }
    }
}

internal static class Json
{
    public static object Deserialize(string json)
    {
        if (json == null) return null;
        int index = 0;
        return ParseValue(json, ref index);
    }

    private static object ParseValue(string json, ref int index)
    {
        EatWhitespace(json, ref index);
        if (index >= json.Length) return null;
        char c = json[index];
        if (c == '"') return ParseString(json, ref index);
        if (c == '{') return ParseObject(json, ref index);
        if (c == '[') return ParseArray(json, ref index);
        if (char.IsDigit(c) || c == '-') return ParseNumber(json, ref index);
        if (json.Substring(index).StartsWith("true")) { index += 4; return true; }
        if (json.Substring(index).StartsWith("false")) { index += 5; return false; }
        if (json.Substring(index).StartsWith("null")) { index += 4; return null; }
        return null;
    }

    private static Dictionary<string, object> ParseObject(string json, ref int index)
    {
        var dict = new Dictionary<string, object>();
        index++; // {
        while (true)
        {
            EatWhitespace(json, ref index);
            if (index >= json.Length) break;
            if (json[index] == '}') { index++; break; }
            string key = ParseString(json, ref index);
            EatWhitespace(json, ref index);
            if (json[index] == ':') index++;
            object val = ParseValue(json, ref index);
            dict[key] = val;
            EatWhitespace(json, ref index);
            if (json[index] == ',') { index++; continue; }
            if (json[index] == '}') { index++; break; }
        }
        return dict;
    }

    private static List<object> ParseArray(string json, ref int index)
    {
        var list = new List<object>();
        index++; // [
        while (true)
        {
            EatWhitespace(json, ref index);
            if (index >= json.Length) break;
            if (json[index] == ']') { index++; break; }
            list.Add(ParseValue(json, ref index));
            EatWhitespace(json, ref index);
            if (json[index] == ',') { index++; continue; }
            if (json[index] == ']') { index++; break; }
        }
        return list;
    }

    private static string ParseString(string json, ref int index)
    {
        var sb = new StringBuilder();
        index++; // "
        while (index < json.Length)
        {
            char c = json[index++];
            if (c == '"') break;
            if (c == '\\' && index < json.Length)
            {
                char next = json[index++];
                if (next == '"' || next == '\\' || next == '/') sb.Append(next);
                else if (next == 'b') sb.Append('\b');
                else if (next == 'f') sb.Append('\f');
                else if (next == 'n') sb.Append('\n');
                else if (next == 'r') sb.Append('\r');
                else if (next == 't') sb.Append('\t');
            }
            else sb.Append(c);
        }
        return sb.ToString();
    }

    private static object ParseNumber(string json, ref int index)
    {
        int start = index;
        while (index < json.Length && "0123456789+-.eE".IndexOf(json[index]) != -1) index++;
        string s = json.Substring(start, index - start);
        if (s.Contains(".") || s.Contains("e") || s.Contains("E"))
            return Convert.ToDouble(s);
        return Convert.ToInt64(s);
    }

    private static void EatWhitespace(string json, ref int index)
    {
        while (index < json.Length && char.IsWhiteSpace(json[index])) index++;
    }
}
