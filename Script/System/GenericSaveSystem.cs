using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Runtime.Serialization.Formatters.Binary;
using UnityEngine;
using UnityEngine.Localization.Settings;
using UnityEngine.ResourceManagement.AsyncOperations;
using static CharacterUpgradeSave;

public static class UXPref
{
    //Player Pref.
    public const string UserPrefKey = "USER";
    public const string ChapterName = "CN";
    public const string SectionName = "SN";
    public const string SectionNum = "SNN";
    public const string Difficulty = "DF";
    public const string LevelNum = "LN";
    public const string DirectMark = "DR";
    public const string DefaultChapterName = "World_I";
    public const string LANG = "Lang";
    public const string TIREUPUNLOCKMARK = "TireUp_{0}";
    public const string Localized_BGnum = "BGnum_cat";
    public const string BGM_PARAM = "BGMVolume";
    public const string SE_PARAM = "SEVolume";
    public const string BASE_CannonNum = "BCN";
    public const string BASE_DecorationNum = "BDN";
    public const string BASE_BaseNum = "BBN";
    public const string Login_Date= "LDate";
    //Localization Table
    public const string Localized_LevelNames = "LevelNames";
    public const string Localized_UI = "UI Elements";
    public const string Localized_Dialogue = "Dialogues";
    public const string Localized_DialogueNames = "DialogueNames";
    public const string Localized_Descriptions = "Descriptions";
    public const string Localized_UnitNames = "UnitNames";
    public const string Localized_Bontiques = "BontiqueItems";
    public const string Localized_CS = "ChapterSections";
    public const string Localized_BM = "BaseMessages";
    public const string Localized_InsDailyClear = "insClear";
    //public static readonly DateTime REASONABLEDATE_LATE = new DateTime(year: 2026, month: 12, day: 11);
    //public static readonly DateTime REASONABLEDATE_EARLY = new DateTime(year: 2025, month: 11, day: 15);
    // Specials
    public const string RewardPenalty = "reward-p";

    // Supabase credentials are intentionally BLANK, not merely unused.
    //
    // Every string literal in a WebGL build is readable from the browser, so shipping the real URL
    // and anon key would publish this project's backend to anyone who opens devtools - for a
    // backend the Builda platform blocks the game from reaching anyway. Persistence now goes
    // through BuildaSaveBackend (privateKV) and identity through BuildaSDK.Whoami().
    //
    // The names survive only so the account-transfer pages still compile; those pages are
    // unreachable on WebGL (their entry points are disabled in MMOption and UserLoginCheckPage).
    // Do not restore real values here - inject them at runtime if the Windows/Android builds ever
    // need them again.
    public const string SupabaseUrl = "";
    public const string SupabaseKey = "";

    // UI rarity frame colors
    public static readonly Color FrameColorDefault = Color.white;
    public static readonly Color FrameColorRarity1 = new Color(0.40f, 1.00f, 0.40f); // light green
    public static readonly Color FrameColorRarity2 = new Color(0.20f, 0.70f, 1.00f); // light blue
    public static readonly Color FrameColorRarity3 = new Color(0.82f, 0.42f, 0.95f); // purple
    public static readonly Color FrameColorRarity4 = new Color(1.00f, 0.85f, 0.35f); // gold
    public static readonly Color FrameColorRarity5 = new Color(1.000f, 0.40f, 0.75f); // pink
    public static readonly Color FrameColorRarity6 = new Color(1.000f, 0.20f, 0.20f); // red
    public static readonly Color FrameColorRarity10 = new Color(0.302f, 0.000f, 0.490f, 1.000f); // dark purple

    public static Color GetRarityFrameColor(int rarity)
    {
        switch (rarity)
        {
            case 1: return FrameColorRarity1;
            case 2: return FrameColorRarity2;
            case 3: return FrameColorRarity3;
            case 4: return FrameColorRarity4;
            case 5: return FrameColorRarity5;
            case 6: return FrameColorRarity6;
            case 10: return FrameColorRarity10;
            default: return FrameColorDefault;
        }
    }
}
public static class GenericSaveSystem
{
    // 保存数据
    public static readonly string FirmFilePath = Application.persistentDataPath;
    public static readonly string FirmEnding = ".datux";
    //public static readonly string debugEnding = "";
    public static void SaveData<T>(T data, string filename) where T : class
    {
        string fullpath = Path.Combine(FirmFilePath, filename+FirmEnding);
        //string debugpath = Path.Combine(FirmFilePath, filename+debugEnding);
        BinaryFormatter formatter = new BinaryFormatter();
        using (FileStream fileStream = new FileStream(fullpath, FileMode.Create))
        {
            try
            {
                formatter.Serialize(fileStream, data);
                Debug.Log($"Succesfully saved: {filename}");
                //SaveSystem.DataUpdate(debugpath, data);
            }
            catch (Exception e)
            {
                Debug.LogError("Save error: " + e.Message);
            }
        }
        // SupabaseSaveRemote.Save(filename, data);
    }

    public static T LoadData<T>(string filename) where T : class
    {
        //Debug.Log($"{filename}.dat");
        string fullpath = Path.Combine(FirmFilePath, filename+FirmEnding);
        if (File.Exists(fullpath))
        {
            BinaryFormatter formatter = new BinaryFormatter();
            using (FileStream fileStream = new FileStream($"{fullpath}", FileMode.Open))
            {
                try
                {
                    T data = formatter.Deserialize(fileStream) as T;
                    //Debug.Log("Data Loaded.");
                    return data;
                }
                catch (Exception e)
                {
                    Debug.LogError("Load Error: " + e.Message);
                    return null;
                }
            }
        }
        else
        {
            Debug.LogWarning($"No such file named {filename}.");
            return null;
        }
        // return SupabaseSaveRemote.Load<T>(filename);
    }

    public static void DeleteData(string filename)
    {
        string fullpath = Path.Combine(FirmFilePath, filename + FirmEnding);
        if (!File.Exists(fullpath)) return;

        try
        {
            File.Delete(fullpath);
            Debug.Log($"Succesfully deleted: {filename}");
        }
        catch (Exception e)
        {
            Debug.LogError("Delete Error: " + e.Message);
        }
    }
}

[System.Serializable]
public class DailyMapClearRecord
{
    public string dateToken = string.Empty;
    public List<string> clearedSectionNames = new List<string>();
}

public static class DailyMapChallengeSave
{
    public static readonly string filename = "B6977A5A57A64B3A9C4D1F6B8F0A1D27";

    public static void ResetIfNewDay(string currentDateToken)
    {
        if (string.IsNullOrEmpty(currentDateToken)) return;

        DailyMapClearRecord save = Load();
        if (save == null) return;
        if (save.dateToken == currentDateToken) return;

        BuildaSaveBackend.Remove(SaveKeys.DailyMapClear);
    }

    public static bool HasSectionClearRecordToday(string currentDateToken, string sectionName)
    {
        if (string.IsNullOrEmpty(currentDateToken) || string.IsNullOrEmpty(sectionName)) return false;

        ResetIfNewDay(currentDateToken);
        DailyMapClearRecord save = Load();
        if (save == null) return false;
        if (save.dateToken != currentDateToken) return false;
        return save.clearedSectionNames != null && save.clearedSectionNames.Contains(sectionName);
    }

    public static void RecordSectionClear(string currentDateToken, string sectionName)
    {
        if (string.IsNullOrEmpty(currentDateToken) || string.IsNullOrEmpty(sectionName)) return;

        DailyMapClearRecord save = Load();
        if (save == null || save.dateToken != currentDateToken)
        {
            save = new DailyMapClearRecord
            {
                dateToken = currentDateToken,
                clearedSectionNames = new List<string>()
            };
        }
        else if (save.clearedSectionNames == null)
        {
            save.clearedSectionNames = new List<string>();
        }

        if (!save.clearedSectionNames.Contains(sectionName))
        {
            save.clearedSectionNames.Add(sectionName);
        }

        BuildaSaveBackend.Set(SaveKeys.DailyMapClear, SaveCodec.EncodeDailyMapClear(save));
    }

    private static DailyMapClearRecord Load() =>
        SaveCodec.DecodeDailyMapClear(BuildaSaveBackend.Get(SaveKeys.DailyMapClear));
}

[System.Serializable]
public class GameProgressSave
{
    public static readonly string filename = "2F3030275C7621A21A17ED8641B57605";// gameprogress
    private const int teamSize = 13;
    // Data Structure
    [System.Serializable]
    public class ChapterClearList
    {
        public string ChapterName;
        public SectionClearList[] SectionList;
    }
    [System.Serializable]
    public class SectionClearList
    {
        public string SectionName;
        public bool cleared;
        public bool[] reward_gained; //[levelIndex]
        public int[,] clear_times; // [hardness, levelIndex]
        public int[,] level_score; // [hardness, levelIndex]
        public string[,] cleared_teams; // [levelIndex, 13 strings (With guests)]
        public int[] cleared_cannon; // [levelIndex]
    }

    // Reader and Writer
    public static ChapterClearList LoadChapterProgress(string chapterName)
    {
        ChapterClearList exist = LoadChapter(chapterName);
        if (exist != null) return UpdateCCL(exist);

        // First Save
        MapInfo[] mapInfos = LoadChapterMapInfos(chapterName);
        if (mapInfos == null || mapInfos.Length == 0)
        {
            Debug.LogError($"No Such Chapter: {chapterName}");
            return null;
        }
        var newChapter = BuildNewChapter(chapterName, mapInfos);
        SaveChapter(newChapter);
        return newChapter;
    }
    //public static SectionClearList LoadSectionProgress(string chapterName, int sectionNum)
    //{
    //    ChapterClearList[] CCL = GenericSaveSystem.LoadData<ChapterClearList[]>(filename) ?? Array.Empty<ChapterClearList>();
    //    var exist = CCL.FirstOrDefault(c => c.ChapterName == chapterName);
    //    if (exist != null) return UpdateCCL(exist).SectionList[sectionNum];

    //    // First Save
    //    MapInfo[] mapInfos = Resources.LoadAll<MapInfo>($"LevelData/Chapters/{chapterName}");
    //    if (mapInfos == null || mapInfos.Length == 0)
    //    {
    //        Debug.LogError($"No Such Chapter: {chapterName}");
    //        return null;
    //    }
    //    var newChapter = BuildNewChapter(chapterName, mapInfos);
    //    var newCCL = CCL.Append(newChapter).ToArray();
    //    GenericSaveSystem.SaveData(newCCL, filename);
    //    return newChapter.SectionList[sectionNum];
    //}
    public static SectionClearList LoadSectionProgress(string chapterName, string sectionName)
    {
        ChapterClearList exist = LoadChapter(chapterName);
        if (exist != null)
        {
            var section = exist.SectionList?.FirstOrDefault(s => s.SectionName == sectionName);
            if (section != null) return UpdateCCL(exist).SectionList.FirstOrDefault(s => s.SectionName == sectionName);
        }

        // First Save
        MapInfo[] mapInfos = LoadChapterMapInfos(chapterName);
        if (mapInfos == null || mapInfos.Length == 0)
        {
            Debug.LogError($"No Such Chapter: {chapterName}");
            return null;
        }
        var newChapter = BuildNewChapter(chapterName, mapInfos);
        SaveChapter(newChapter);
        return newChapter.SectionList.FirstOrDefault(s => s.SectionName == sectionName);
    }

    /*========== 分片存取（每章节一个 privateKV key） ==========*/

    /// <summary>
    /// Loads one chapter's shard. Returns null when the player has no save for it yet, which the
    /// callers above turn into a freshly built chapter.
    /// </summary>
    public static ChapterClearList LoadChapter(string chapterName)
    {
        if (string.IsNullOrEmpty(chapterName)) return null;
        byte[] bytes = BuildaSaveBackend.Get(SaveKeys.Progress(chapterName));
        if (bytes == null) return null;
        try
        {
            return SaveCodec.DecodeChapter(bytes);
        }
        catch (Exception e)
        {
            // A corrupt shard must not be silently treated as "no progress": that would rebuild an
            // empty chapter and the next write would overwrite the player's real save.
            Debug.LogError($"[GameProgressSave] Chapter '{chapterName}' failed to decode: {e.Message}");
            return null;
        }
    }

    public static void SaveChapter(ChapterClearList chapter)
    {
        if (chapter == null || string.IsNullOrEmpty(chapter.ChapterName)) return;
        BuildaSaveBackend.Set(SaveKeys.Progress(chapter.ChapterName), SaveCodec.EncodeChapter(chapter));
    }

    /// <summary>
    /// Every chapter the player has a save for. Used by account transfer, which needs the whole
    /// picture rather than one chapter.
    /// </summary>
    public static ChapterClearList[] LoadAllChapters()
    {
        var list = new List<ChapterClearList>(SaveKeys.Chapters.Length);
        for (int i = 0; i < SaveKeys.Chapters.Length; i++)
        {
            var chapter = LoadChapter(SaveKeys.Chapters[i]);
            if (chapter != null) list.Add(chapter);
        }
        return list.ToArray();
    }

    /// <summary>
    /// Writes several chapters at once. Batched through setMany so a restore cannot leave the
    /// player with only some chapters written.
    /// </summary>
    public static void SaveAllChapters(ChapterClearList[] chapters)
    {
        if (chapters == null || chapters.Length == 0) return;
        var entries = new Dictionary<string, byte[]>(chapters.Length);
        for (int i = 0; i < chapters.Length; i++)
        {
            var c = chapters[i];
            if (c == null || string.IsNullOrEmpty(c.ChapterName)) continue;
            entries[SaveKeys.Progress(c.ChapterName)] = SaveCodec.EncodeChapter(c);
        }
        BuildaSaveBackend.SetMany(entries);
    }

    /// <summary>
    /// Resolves a chapter's MapInfo assets. Centralized because the same Resources path was
    /// written three different ways, one of which ("/Chapters/{name}") is wrong and silently
    /// returns nothing - it is missing the "LevelData" segment and has a leading slash.
    /// </summary>
    private static MapInfo[] LoadChapterMapInfos(string chapterName)
    {
        return Resources.LoadAll<MapInfo>($"LevelData/Chapters/{chapterName}");
    }

    public static ChapterClearList BuildNewChapter(string chapterName, MapInfo[] mapInfos)
    {
        var sectionList = new SectionClearList[mapInfos.Length];
        for (int i = 0; i < mapInfos.Length; i++)
        {
            var mi = mapInfos[i];
            sectionList[i] = new SectionClearList
            {
                SectionName = mi.sectionName,
                cleared = false,
                reward_gained = new bool[mi.levelsOnMap.Length],
                clear_times = new int[mi.hardness, mi.levelsOnMap.Length],
                level_score = new int[mi.hardness, mi.levelsOnMap.Length],
                cleared_teams=new string[mi.levelsOnMap.Length,teamSize],
                cleared_cannon=new int[mi.levelsOnMap.Length]
            };
        }
        return new ChapterClearList
        {
            ChapterName = chapterName,
            SectionList = sectionList
        };
    }

    /*========== 更新存档结构（资源新增 Section / 关卡） ==========*/
    public static ChapterClearList UpdateCCL(ChapterClearList old)
    {
        MapInfo[] mapInfos = Resources.LoadAll<MapInfo>($"LevelData/Chapters/{old.ChapterName}");
        if (mapInfos == null) return old;

        var fresh = BuildNewChapter(old.ChapterName, mapInfos);

        for (int secIdx = 0; secIdx < fresh.SectionList.Length; secIdx++)
        {
            var newSec = fresh.SectionList[secIdx];
            var oldSec = old.SectionList?.FirstOrDefault(s => s.SectionName == newSec.SectionName);
            if (oldSec == null) continue;

            int oldD = oldSec.clear_times?.GetLength(0) ?? 0;
            int oldL = oldSec.clear_times?.GetLength(1) ?? 0;
            int newD = newSec.clear_times.GetLength(0);
            int newL = newSec.clear_times.GetLength(1);

            for (int d = 0; d < Mathf.Min(oldD, newD); d++)
                for (int l = 0; l < Mathf.Min(oldL, newL); l++)
                {
                    newSec.clear_times[d, l] = oldSec.clear_times[d, l];
                    newSec.level_score[d, l] = oldSec.level_score[d, l];
                }
            //clear mark
            if (newD > 0 && newL > 0 && newSec.clear_times[0, newL - 1] > 0)
                newSec.cleared = true;
            for (int l = 0; l < Mathf.Min(oldL, newL); l++)
            {
                newSec.reward_gained[l] = oldSec.reward_gained[l];
                for (int j = 0; j < teamSize; j++)
                {
                    newSec.cleared_teams[l,j] = oldSec.cleared_teams[l,j];
                }
                newSec.cleared_cannon[l]=oldSec.cleared_cannon[l];
            }
        }

        // Only this chapter's shard is rewritten. The pre-shard version loaded the whole
        // ChapterClearList[] and wrote it back, which under privateKV would mean 9 writes per
        // level clear instead of 1.
        SaveChapter(fresh);
        return fresh;
    }

    public static void SaveProgress(string chapterName, string sectionName, int diff, int levelNum, int score, bool gain_reward, string[] team, int cannon)
    {
        var ccl = LoadChapterProgress(chapterName);
        if (ccl == null) return;
        var sec = ccl.SectionList.FirstOrDefault(s => s.SectionName == sectionName);
        if (sec == null) return;

        if (levelNum >= 0 && levelNum < sec.clear_times.GetLength(1))
        {
            sec.clear_times[diff, levelNum]++;
            sec.level_score[diff, levelNum] = Mathf.Max(score, sec.level_score[diff, levelNum]);
            if (!sec.reward_gained[levelNum]) sec.reward_gained[levelNum] = gain_reward;
            if(team!=null) for(int i = 0; i < teamSize; i++)
            {
                sec.cleared_teams[levelNum,i]=team[i];
            }
            sec.cleared_cannon[levelNum] = cannon;
        }
        Debug.Log($"{levelNum} / {sec.clear_times.GetLength(1) - 1}");
        if (levelNum == sec.clear_times.GetLength(1)-1 && !sec.cleared)
        {
            Debug.Log($"Cleared section {sectionName}!");
            sec.cleared = true;
            RewardingSystem.GainReward(RewardName.CANs, 300);
            RewardingSystem.GainReward(RewardName.XP, 10000);
        }
        Debug.Log($"cleared: {sec.cleared}");
        SaveChapter(ccl);
    }
}
public static class LocalizationHelper
{
    /// <summary>
    /// 异步获取本地化文本，保证 Localization 已初始化
    /// </summary>
    /// <param name="tableName">本地化表的名称</param>
    /// <param name="id">文本 ID</param>
    /// <param name="callback">获取文本后的回调</param>
    public static void GetLocalizedText(string tableName, string id, System.Action<string> callback)
    {
        // 如果已在主线程协程里调用，可直接启动内部协程
        CoroutineRunner.Instance.StartCoroutine(GetTextCoroutine(tableName, id, callback));
    }

    private static IEnumerator GetTextCoroutine(string tableName, string id, System.Action<string> callback)
    {
        // 等待 Localization 系统初始化完毕
        yield return LocalizationSettings.InitializationOperation;

        var handle = LocalizationSettings.StringDatabase.GetLocalizedStringAsync(tableName, id);
        yield return handle;

        if (handle.Status == AsyncOperationStatus.Succeeded)
            callback?.Invoke(handle.Result);
        else
        {
            Debug.LogError($"Failed to load localized text: {id}@{tableName}");
            callback?.Invoke(null);
        }
    }

    /* -------- 协程运行器（单例） -------- */
    private class CoroutineRunner : MonoBehaviour
    {
        public static CoroutineRunner Instance
        {
            get
            {
                if (_instance == null)
                {
                    var go = new GameObject("[LocalizationRunner]");
                    _instance = go.AddComponent<CoroutineRunner>();
                    DontDestroyOnLoad(go);
                }
                return _instance;
            }
        }
        private static CoroutineRunner _instance;
    }
}
[System.Serializable]
public class Reward
{
    public RewardType type;
    public int id;
    public int drawtimes = 1;
    public int droprate;
    public bool onlyOnce;
}
[System.Serializable]
public enum RewardType
{
    character,
    item,
    treasure,
    maxlevel,
    highscore,
    UnlockTire
}

public static class RewardIconHelper
{
    public static void ParseUnlockTireId(int rewardId, out string characterId, out int tire)
    {
        tire = rewardId % 10;
        characterId = (rewardId / 10).ToString("0000");
    }

    public static string GetCatDeployIconPath(string characterId4, int tire)
    {
        return $"Units/Cat Units/{characterId4[0]}/{characterId4.Substring(1, 3)}/{tire}/icon_deploy";
    }

    public static string GetUnlockTireIconPath(int rewardId)
    {
        ParseUnlockTireId(rewardId, out string characterId, out int tire);
        return GetCatDeployIconPath(characterId, tire);
    }
}
[System.Serializable]
public enum RewardName
{
    Cateye_EX=0, Cateye_Rare=1, Cateye_SuperRare=2, Cateye_UberRare=3, Cateye_Legend=4, Cateye_Dark=5,
    Ticket_Normal=6, Ticket_Gold=7, Ticket_Platinum=8, Ticket_PlatinumShard=9, Ticket_Legend=10,
    XP=11, CANs=12, NP=13,
    //Catamin_A=14, Catamin_B=15, Catamin_C=16,
    //Prop_Treasure=17, Prop_max=18, Prop_Sniper=19,
    //Base_Atk=20, Base_Charge=21, Base_Worker=22, Base_Health=23, Base_Cooldown=24, Base_Accounting=25,
    Base_Study=26,
    CatfruitSeed_Purple=27, CatfruitSeed_Red=28, CatfruitSeed_Blue=29, CatfruitSeed_Green=30, CatfruitSeed_Yellow=31, CatfruitSeed_Rainbow=32, CatfruitSeed_Relic=33, CatfruitSeed_Gold=34, CatfruitSeed_Aku=35,
    Catfruit_Purple=36, Catfruit_Red=37, Catfruit_Blue=38, Catfruit_Green=39, Catfruit_Yellow=40, Catfruit_Rainbow=41, Catfruit_Relic=42, Catfruit_Gold=43, Catfruit_Aku=44,
    BS_Purple=45, BS_Red=46, BS_Blue=47, BS_Green=48, BS_Yellow=49, BS_Rainbow=50,
    //Material_Brick=51, Material_Feather=52, Material_Coal=53, Material_Gear=54, Material_Gold=55, Material_Meteorite=56, Material_Bone=57,
    //EmptySlot1=58, EmptySlot2=59,
    WorldTreasures=60,
    UpgradeMax_N=61, UpgradeMax_EX=62, UpgradeMax_R=63, UpgradeMax_SR=64, UpgradeMax_UR=65, UpgradeMax_LR=66, UpgradeMax_G=67,
    DrawMax_N=68, DrawMax_EX=69, DrawMax_R=70, DrawMax_SR=71, DrawMax_UR=72, DrawMax_LR=73, DrawMax_G=74,
    Bottle_Water=75, Bottle_Soul=76,
    SecMed_Purple=77, SecMed_Red=78, SecMed_Blue=79, SecMed_Green=80,
    GF_Core=81
}
[System.Serializable]
public class Dialogue
{
    public string DialoguerName;
    public string DialoguerImage;
    public bool faceToRight;
    public bool clearImage;
    public string cg;
}
[System.Serializable]
public static class RewardingSystem
{
    public static readonly string filename = "9A82026168246A52E87DDBCA143010DF"; //items
    public static readonly Dictionary<RewardName, int> RewardNumMap = new Dictionary<RewardName, int>
    {
        {RewardName.Cateye_EX,0 },
        {RewardName.Cateye_Rare,1 },
        {RewardName.Cateye_SuperRare,2 },
        {RewardName.Cateye_UberRare,3 },
        {RewardName.Cateye_Legend,4 },
        {RewardName.Cateye_Dark,5 },
        {RewardName.Ticket_Normal,6 },
        {RewardName.Ticket_Gold,7 },
        {RewardName.Ticket_Platinum,8 },
        {RewardName.Ticket_PlatinumShard,9 },
        {RewardName.Ticket_Legend,10 },
        {RewardName.XP,11 },
        {RewardName.CANs,12 },
        //{RewardName.Catamin_A,13 },
        //{RewardName.Catamin_B,14 },
        //{RewardName.Catamin_C,15 },
        //{RewardName.Prop_Treasure,16},
        //{RewardName.Prop_max,17},
        //{RewardName.Prop_Sniper,18},
        //{RewardName.Base_Atk,19},
        //{RewardName.Base_Charge,20},
        //{RewardName.Base_Worker,21},
        //{RewardName.Base_Health,22},
        //{RewardName.Base_Cooldown,23},
        //{RewardName.Base_Accounting,24},
        {RewardName.Base_Study,25},
        {RewardName.CatfruitSeed_Purple,26},
        {RewardName.CatfruitSeed_Red,27},
        {RewardName.CatfruitSeed_Blue,28},
        {RewardName.CatfruitSeed_Green,29},
        {RewardName.CatfruitSeed_Yellow,30},
        {RewardName.CatfruitSeed_Rainbow,31},
        {RewardName.CatfruitSeed_Relic,32},
        {RewardName.CatfruitSeed_Gold,33},
        {RewardName.Catfruit_Purple,34},
        {RewardName.Catfruit_Red,35},
        {RewardName.Catfruit_Blue,36},
        {RewardName.Catfruit_Green,37},
        {RewardName.Catfruit_Yellow,38},
        {RewardName.Catfruit_Rainbow,39},
        {RewardName.Catfruit_Relic,40},
        {RewardName.Catfruit_Gold,41},
        {RewardName.BS_Purple,42},
        {RewardName.BS_Red,43},
        {RewardName.BS_Blue,44},
        {RewardName.BS_Green,45},
        {RewardName.BS_Yellow,46},
        {RewardName.BS_Rainbow,47},
        //{RewardName.Material_Brick,48},
        //{RewardName.Material_Feather,49},
        //{RewardName.Material_Coal,50},
        //{RewardName.Material_Gear,51},
        //{RewardName.Material_Gold,52},
        //{RewardName.Material_Meteorite,53},
        //{RewardName.Material_Bone,54},
        //{RewardName.EmptySlot1,55},
        {RewardName.GF_Core,56},
        {RewardName.NP,57},
        {RewardName.CatfruitSeed_Aku,58},
        {RewardName.Catfruit_Aku,59},
        {RewardName.WorldTreasures,60},
        {RewardName.UpgradeMax_N,61},
        {RewardName.UpgradeMax_EX,62},
        {RewardName.UpgradeMax_R,63},
        {RewardName.UpgradeMax_SR,64},
        {RewardName.UpgradeMax_UR,65},
        {RewardName.UpgradeMax_LR,66},
        {RewardName.UpgradeMax_G,67},
        {RewardName.DrawMax_N,68},
        {RewardName.DrawMax_EX,69},
        {RewardName.DrawMax_R,70},
        {RewardName.DrawMax_SR,71},
        {RewardName.DrawMax_UR,72},
        {RewardName.DrawMax_LR,73},
        {RewardName.DrawMax_G,74},
        {RewardName.Bottle_Water,110},
        {RewardName.Bottle_Soul,111},
        {RewardName.SecMed_Purple,120},
        {RewardName.SecMed_Red,121},
        {RewardName.SecMed_Blue,122},
        {RewardName.SecMed_Green,123},
    };
    static RewardingSystem()
    {
        ValidateRewardNumMap();
    }

    public static int ExpectedInventoryLength => RewardNumMap.Values.Max() + 1;

    private static void ValidateRewardNumMap()
    {
        if (RewardNumMap == null || RewardNumMap.Count == 0)
        {
            Debug.LogError("[RewardingSystem] RewardNumMap is empty.");
            return;
        }

        HashSet<int> seenIds = new HashSet<int>();
        foreach (var kv in RewardNumMap)
        {
            if (kv.Value < 0)
            {
                Debug.LogError($"[RewardingSystem] Negative reward id for {kv.Key}: {kv.Value}");
                continue;
            }
            if (!seenIds.Add(kv.Value))
            {
                Debug.LogError($"[RewardingSystem] Duplicate reward id detected: {kv.Value}");
            }
        }

        RewardName[] allNames = (RewardName[])Enum.GetValues(typeof(RewardName));
        for (int i = 0; i < allNames.Length; i++)
        {
            RewardName rewardName = allNames[i];
            if (!RewardNumMap.ContainsKey(rewardName))
            {
                Debug.LogWarning($"[RewardingSystem] Missing RewardNumMap entry for {rewardName}.");
            }
        }
    }
    /// <summary>
    /// 加载存档；若不存在或长度不符，自动重建并拷贝旧数据
    /// </summary>
    private static int[] LoadOrResize()
    {
        int[] old = SaveCodec.DecodeIntArray(BuildaSaveBackend.Get(SaveKeys.Inventory));
        int expectedLength = ExpectedInventoryLength;

        if (old == null || old.Length != expectedLength)
        {
            int[] neo = new int[expectedLength];
            if (old != null) Array.Copy(old, 0, neo, 0, Math.Min(old.Length, expectedLength));
            SaveInventory(neo);
            return neo;
        }
        return old;
    }

    private static void SaveInventory(int[] items)
    {
        BuildaSaveBackend.Set(SaveKeys.Inventory, SaveCodec.EncodeIntArray(items));
    }
    public static int GetAmount(RewardName reward_name)
    {
        int[] items = LoadOrResize();
        return items[RewardNumMap[reward_name]];
    }
    public static int GetAmount(int reward_num)
    {
        int[] items = LoadOrResize();
        return items[reward_num];
    }
    public static void GainReward(RewardName reward_name, int count)
    {
        if (count < 0) return;

        int[] items = LoadOrResize();
        int idx = RewardNumMap[reward_name];
        items[idx] = Mathf.Clamp(items[idx] + count, 0, 99999999);
        SaveInventory(items);
        Debug.Log($"Gained {reward_name} x {count}");
    }
    public static void GainRewardByOrder(int reward_num, int count)
    {
        if (count < 0) return;

        int[] items = LoadOrResize();
        items[reward_num] = Mathf.Clamp(items[reward_num] + count, 0, 99999999);
        SaveInventory(items);
        Debug.Log($"Gained {reward_num} x {count}");
    }
    public static bool ConsumeItem(RewardName RN, int count)
    {
        if (count < 0) return false;

        int[] items = LoadOrResize();
        int idx = RewardNumMap[RN];
        if (items[idx] < count) return false;

        items[idx] -= count;
        SaveInventory(items);
        return true;
    }
    public static bool CheckItemIsEnough(RewardName reward_name, int count)
    {
        if (count <= 0) return true;
        int[] items = LoadOrResize();
        return items[RewardNumMap[reward_name]] >= count;
    }
    public static bool CheckItemIsEnough(int reward_num, int count)
    {
        if (count <= 0) return true;
        int[] items = LoadOrResize();
        return items[reward_num] >= count;
    }
}
[System.Serializable]
public static class CharacterUpgradeSave
{
    public static readonly string filename = "461F39959FF203CF8DA28F3EEA8A9035"; //characters

    #region Data  Structure
    [System.Serializable]
    public class UpgradeDetails
    {
        public bool[] tire_unlocked = new bool[4];
        public bool talent_unlocked = false;
        public int upgraded_level = 0;
        public int plus_level = 0;
        public CharacterProficiency proficiency = new CharacterProficiency();
        public int TotalLevel() => upgraded_level+plus_level;
    }
    #endregion

    #region Functions
    /// <summary>生成所有 Unit ID 列表（按 rality+000 规则）</summary>
    private static IEnumerable<string> EnumerateAllIds()
    {
        for (int r = 0; r < 7; r++)
        {
            for(int code=0;code<1000;code++)
            {
                string id = $"{r}{code:000}";
                if (!BundledAddressables.Exists($"Units/Cat Units/{r}/{code:000}/0/data", typeof(CharacterData))) continue;
                yield return id;
            }
        }
    }
    private static Dictionary<string, UpgradeDetails> Load()
    {
        var dict = LoadSharded();
        if (dict == null) return Rebuild();

        bool needsUpdate = false;
        
        // 检查并修复现有条目的 proficiency
        foreach (var kv in dict)
        {
            var ud = kv.Value;
            if (ud.proficiency == null)
            {
                needsUpdate = true;
                ud.proficiency = new CharacterProficiency();
                Debug.Log($"[Save Upgrade] Added proficiency to {kv.Key}");
            }
            else if (ud.proficiency.NormalizeProgress())
            {
                needsUpdate = true;
                Debug.Log($"[Save Upgrade] Normalized overflowed proficiency for {kv.Key}");
            }
        }
        
        // 确保默认角色存在且正确初始化（只检查这一个角色，不遍历所有角色）
        if (dict.ContainsKey("0000"))
        {
            if (!dict["0000"].tire_unlocked[0])
            {
                needsUpdate = true;
                dict["0000"].tire_unlocked[0] = true;
            }
            if (dict["0000"].upgraded_level < 1)
            {
                needsUpdate = true;
                dict["0000"].upgraded_level = 1;
            }
        }
        else
        {
            // 只检查默认角色是否存在（单个资源加载，性能影响可忽略）
            if (BundledAddressables.Exists("Units/Cat Units/0/000/0/data", typeof(CharacterData)))
            {
                needsUpdate = true;
                dict["0000"] = new UpgradeDetails
                {
                    tire_unlocked = new bool[4],
                    upgraded_level = 1
                };
                dict["0000"].tire_unlocked[0] = true;
            }
        }

        if (needsUpdate) Save(dict);
        return dict;
    }
    private static Dictionary<string, UpgradeDetails> Rebuild()
    {
        var oldDict = LoadSharded() ?? new Dictionary<string, UpgradeDetails>();

        var allIds = EnumerateAllIds().ToHashSet();
        var newDict = new Dictionary<string, UpgradeDetails>(oldDict);
        foreach (var id in allIds)
        {
            if (!newDict.ContainsKey(id))
            {
                newDict[id] = new UpgradeDetails();
                Debug.Log($"new ID: {id}");
            }
        }
        // The default cat may legitimately be absent when its asset is missing from the catalog;
        // indexing it unconditionally would throw during Rebuild and take the whole boot with it.
        if (newDict.TryGetValue("0000", out var starter))
        {
            starter.tire_unlocked[0] = true;
            if (starter.upgraded_level < 1) starter.upgraded_level = 1;
        }
        Save(newDict);
        return newDict;
    }

    /*========== 分片存取（按稀有度首位数字，7 个 key） ==========*/

    /// <summary>
    /// Reassembles the upgrade dictionary from its per-rarity shards. Returns null only when no
    /// shard exists at all, so a returning player is never mistaken for a new one.
    /// </summary>
    private static Dictionary<string, UpgradeDetails> LoadSharded()
    {
        Dictionary<string, UpgradeDetails> merged = null;

        for (int r = 0; r < SaveKeys.RarityCount; r++)
        {
            byte[] bytes = BuildaSaveBackend.Get(SaveKeys.Upgrades(r));
            if (bytes == null) continue;

            Dictionary<string, UpgradeDetails> shard;
            try
            {
                shard = SaveCodec.DecodeUpgrades(bytes);
            }
            catch (Exception e)
            {
                Debug.LogError($"[CharacterUpgradeSave] Rarity {r} shard failed to decode: {e.Message}");
                continue;
            }
            if (shard == null) continue;

            if (merged == null) merged = new Dictionary<string, UpgradeDetails>(shard.Count * SaveKeys.RarityCount);
            foreach (var kv in shard) merged[kv.Key] = kv.Value;
        }

        return merged;
    }

    /// <summary>
    /// Splits the dictionary by the leading rarity digit of each unit ID and writes all shards as
    /// one batch, so a partial write cannot leave two rarities disagreeing about the same player.
    /// </summary>
    private static void SaveSharded(Dictionary<string, UpgradeDetails> dict)
    {
        if (dict == null) return;

        var buckets = new Dictionary<string, UpgradeDetails>[SaveKeys.RarityCount];
        for (int r = 0; r < SaveKeys.RarityCount; r++)
            buckets[r] = new Dictionary<string, UpgradeDetails>();

        foreach (var kv in dict)
        {
            int rarity = RarityOf(kv.Key);
            if (rarity < 0)
            {
                Debug.LogWarning($"[CharacterUpgradeSave] Unit ID '{kv.Key}' has no valid rarity digit; not saved.");
                continue;
            }
            buckets[rarity][kv.Key] = kv.Value;
        }

        var entries = new Dictionary<string, byte[]>(SaveKeys.RarityCount);
        for (int r = 0; r < SaveKeys.RarityCount; r++)
            entries[SaveKeys.Upgrades(r)] = SaveCodec.EncodeUpgrades(buckets[r]);

        BuildaSaveBackend.SetMany(entries);
    }

    /// <summary>
    /// Rarity is the first character of the ID, which is generated as <c>$"{r}{code:000}"</c>.
    /// Returns -1 for anything outside 0..<see cref="SaveKeys.RarityCount"/>-1 so a malformed ID is
    /// reported rather than silently landing in the wrong bucket.
    /// </summary>
    private static int RarityOf(string unitId)
    {
        if (string.IsNullOrEmpty(unitId)) return -1;
        int r = unitId[0] - '0';
        return (r >= 0 && r < SaveKeys.RarityCount) ? r : -1;
    }

    /// <summary>统一保存</summary>
    private static void Save(Dictionary<string, UpgradeDetails> dict) => SaveSharded(dict);
    #endregion

    #region API

    /// <summary>重建空存档（供调试或重置）</summary>
    public static void RebuildSave() => Save(Load());

    /// <summary>解锁指定 tier</summary>
    public static void UnlockCharacterTire(string id, int tier)
    {
        if (tier < 0 || tier > 3) return;
        var dict = Load();
        if (!dict.TryGetValue(id, out var ud))
        {
            // 如果角色不存在，创建新条目（防止游戏更新后新角色无法解锁）
            ud = new UpgradeDetails
            {
                tire_unlocked = new bool[4],
                upgraded_level = 0,
                plus_level = 0
            };
            dict[id] = ud;
            Debug.LogWarning($"[CharacterUpgradeSave] Character {id} not found in save, created new entry for tier unlock");
        }

        if (!ud.tire_unlocked[tier])
        {
            ud.tire_unlocked[tier] = true;
            Save(dict);
        }
    }

    /// <summary>经验升级</summary>
    public static void UpgradeCharacterByXP(string id)
    {
        var dict = Load();
        if (!dict.TryGetValue(id, out var ud))
        {
            // 如果角色不存在，创建新条目（防止游戏更新后新角色无法升级）
            ud = new UpgradeDetails();
            dict[id] = ud;
            Debug.LogWarning($"[CharacterUpgradeSave] Character {id} not found in save, created new entry for XP upgrade");
        }
        ud.upgraded_level++;
        Save(dict);
    }

    /// <summary>抽卡升级</summary>
    public static void UpgradeCharacterByDraw(string id)
    {
        var dict = Load();
        if (!dict.TryGetValue(id, out var ud))
        {
            // 如果角色不存在，创建新条目（防止游戏更新后新角色无法升级）
            ud = new UpgradeDetails();
            dict[id] = ud;
            Debug.LogWarning($"[CharacterUpgradeSave] Character {id} not found in save, created new entry for draw upgrade");
        }
        ud.plus_level++;
        Save(dict);
        UnlockCharacterTire(id, 0);
    }
    /// <summary>通关解锁</summary>
    public static void UpgradeCharacterByClear(string id)
    {
        var dict = Load();
        if (!dict.TryGetValue(id, out var ud))
        {
            // 如果角色不存在，创建新条目（防止游戏更新后新角色无法升级）
            ud = new UpgradeDetails();
            dict[id] = ud;
            Debug.LogWarning($"[CharacterUpgradeSave] Character {id} not found in save, created new entry for clear upgrade");
        }
        if (ud.upgraded_level > 0) return;
        ud.upgraded_level++;
        Save(dict);
        UnlockCharacterTire(id, 0);
    }

    /// <summary>解锁天赋</summary>
    public static void UnlockCharacterTalent(string id)
    {
        var dict = Load();
        if (!dict.TryGetValue(id, out var ud))
        {
            // 如果角色不存在，创建新条目（防止游戏更新后新角色无法解锁天赋）
            ud = new UpgradeDetails();
            dict[id] = ud;
            Debug.LogWarning($"[CharacterUpgradeSave] Character {id} not found in save, created new entry for talent unlock");
        }
        
        if (!ud.talent_unlocked)
        {
            ud.talent_unlocked = true;
            Save(dict);
        }
    }

    /// <summary>
    /// Batch update proficiency for multiple characters.  
    /// Adds (not replaces) each proficiency field.
    /// Called once after a level is completed.
    /// </summary>
    public static void BatchUpdateProficiency(string[] character_codes, CharacterProficiency[] pros)
    {
        if (character_codes == null || pros == null) return;
        if (character_codes.Length != pros.Length)
        {
            Debug.LogError("[BatchUpdateProficiency] Input array length mismatch.");
            return;
        }

        var dict = Load(); // Load ALL save data once

        for (int i = 0; i < character_codes.Length; i++)
        {
            string id = character_codes[i];
            CharacterProficiency incoming = pros[i];

            if (incoming == null) continue;
            incoming.NormalizeProgress();

            // Ensure entry exists
            if (!dict.TryGetValue(id, out UpgradeDetails ud))
            {
                ud = new UpgradeDetails();
                dict[id] = ud;
            }

            // Ensure proficiency exists
            if (ud.proficiency == null)
                ud.proficiency = new CharacterProficiency();
            // Persist full progress (base int + overflow counter) without changing save schema shape.
            ud.proficiency.LoadFromLongProgressArray(incoming.ToLongProgressArray());
            ud.proficiency.NormalizeProgress();
            ud.proficiency.UpdateLevel();
        }
        Save(dict);
    }
    public static void BatchUpdateProficiency(string character_codes, CharacterProficiency pros)
    {
        if (character_codes == null || pros == null) return;

        var dict = Load(); // Load ALL save data once
        string id = character_codes;

        if (pros == null) return;
        pros.NormalizeProgress();

        // Ensure entry exists
        if (!dict.TryGetValue(id, out UpgradeDetails ud))
        {
            ud = new UpgradeDetails();
            dict[id] = ud;
        }
        // Ensure proficiency exists
        if (ud.proficiency == null)
            ud.proficiency = new CharacterProficiency();
        // Persist full progress (base int + overflow counter) without changing save schema shape.
        ud.proficiency.LoadFromLongProgressArray(pros.ToLongProgressArray());
        ud.proficiency.NormalizeProgress();
        ud.proficiency.UpdateLevel();
        Save(dict);
    }

    /// <summary>读取某个角色的详情（便于 UI 显示）</summary>
    public static UpgradeDetails GetDetails(string id)
    {
        var dict = Load();
        if (!dict.TryGetValue(id, out var ud))
        {
            // 保底创建：新角色资源已存在但旧存档未包含该 ID 时，避免调用方拿到 null
            ud = new UpgradeDetails();
            dict[id] = ud;
            Save(dict);
            Debug.LogWarning($"[CharacterUpgradeSave] Missing save entry for {id}, created default UpgradeDetails.");
        }

        bool changed = false;
        if (ud.tire_unlocked == null || ud.tire_unlocked.Length != 4)
        {
            ud.tire_unlocked = new bool[4];
            changed = true;
        }
        if (ud.proficiency == null)
        {
            ud.proficiency = new CharacterProficiency();
            changed = true;
        }
        else if (ud.proficiency.NormalizeProgress())
        {
            changed = true;
        }
        if (changed)
        {
            dict[id] = ud;
            Save(dict);
        }
        return ud;
    }

    public static bool XPUpgradeAvailable(string id)
    {
        UpgradeDetails ud=GetDetails(id);
        int tire = id[0] - '0';
        RewardName maxRewardName = GetUpgradeMaxRewardName(tire);
        int ugmax = RewardingSystem.GetAmount(maxRewardName) + 10;
        if (ud.upgraded_level > ugmax)
        {
            ud.upgraded_level = ugmax;
            var dict = Load();
            dict[id] = ud;
            Save(dict);
        }
        return ud.upgraded_level < ugmax;
    }
    public static bool DrawUpgradeAvailable(string id)
    {
        UpgradeDetails ud = GetDetails(id);
        int tire = id[0] - '0';
        RewardName maxRewardName = GetDrawMaxRewardName(tire);
        int ugmax = RewardingSystem.GetAmount(maxRewardName) + 10;
        if (ud.plus_level > ugmax)
        {
            ud.plus_level = ugmax;
            var dict = Load();
            dict[id] = ud;
            Save(dict);
        }
        return ud.plus_level < ugmax;
    }

    private static RewardName GetUpgradeMaxRewardName(int tire)
    {
        switch (tire)
        {
            case 0: return RewardName.UpgradeMax_N;
            case 1: return RewardName.UpgradeMax_EX;
            case 2: return RewardName.UpgradeMax_R;
            case 3: return RewardName.UpgradeMax_SR;
            case 4: return RewardName.UpgradeMax_UR;
            case 5: return RewardName.UpgradeMax_LR;
            case 6: return RewardName.UpgradeMax_G;
            default:
                Debug.LogWarning($"[CharacterUpgradeSave] Invalid tire {tire}, fallback to UpgradeMax_N.");
                return RewardName.UpgradeMax_N;
        }
    }

    private static RewardName GetDrawMaxRewardName(int tire)
    {
        switch (tire)
        {
            case 0: return RewardName.DrawMax_N;
            case 1: return RewardName.DrawMax_EX;
            case 2: return RewardName.DrawMax_R;
            case 3: return RewardName.DrawMax_SR;
            case 4: return RewardName.DrawMax_UR;
            case 5: return RewardName.DrawMax_LR;
            case 6: return RewardName.DrawMax_G;
            default:
                Debug.LogWarning($"[CharacterUpgradeSave] Invalid tire {tire}, fallback to DrawMax_N.");
                return RewardName.DrawMax_N;
        }
    }
    #endregion
}
[System.Serializable]
public static class SelectionsSave
{
    public static readonly string filename = "8DB79DDA23AFA35252A5B2637AD78375"; //selections
    public static readonly string pref_teamnum = "currentteam"; //currentteam
    public const int TeamNum = 10;
    public const int SIZE = 13;

    #region === 工具方法 ===
    /// <summary>
    /// 读取存档；若不存在则自动新建 10×10 空数组
    /// </summary>
    private static string[,] LoadOrCreate()
    {
        var data = SaveCodec.DecodeStringArray2D(BuildaSaveBackend.Get(SaveKeys.TeamSelections));
        if (data == null || data.GetLength(0) != TeamNum || data.GetLength(1) != SIZE)
        {
            data = new string[TeamNum, SIZE];
            data[0, 0] = "00000";
            Save(data);
        }
        return data;
    }

    /// <summary>保存整个数组</summary>
    private static void Save(string[,] data) =>
        BuildaSaveBackend.Set(SaveKeys.TeamSelections, SaveCodec.EncodeStringArray2D(data));
    #endregion

    #region === 对外 API ===
    /// <summary>修改指定位置的字符串</summary>
    public static void SetRow(int x, string[] row)
    {
        if (x < 0 || x >= TeamNum || row == null || row.Length != SIZE) return;

        var data = LoadOrCreate();
        for (int y = 0; y < SIZE; y++)
            data[x, y] = row[y];

        Save(data);
    }
    /// <summary>读取指定位置的字符串</summary>
    public static string[] GetRow(int x)
    {
        if (x < 0 || x >= TeamNum) return new string[SIZE];

        var data = LoadOrCreate();
        var row = new string[SIZE];
        for (int y = 0; y < SIZE; y++)
            row[y] = data[x, y];

        return row;
    }
    /// <summary>一次性读取整表（只读）</summary>
    public static string[,] GetAll()
    {
        var data = LoadOrCreate();
        var clone = new string[TeamNum, SIZE];
        Array.Copy(data, clone, data.Length);
        return clone;
    }
    #endregion
}
[System.Serializable]
public static class TeamNameSave
{
    public static readonly string filename = "1F987152C4D04289944EF7DCCAA53F9A"; // teamnames

    private static string[] LoadOrCreate()
    {
        var data = SaveCodec.DecodeStringArray(BuildaSaveBackend.Get(SaveKeys.TeamNames));
        if (data == null || data.Length != SelectionsSave.TeamNum)
        {
            data = new string[SelectionsSave.TeamNum];
            for (int i = 0; i < data.Length; i++) data[i] = $"Team {i + 1}";
            Save(data);
        }
        return data;
    }

    private static void Save(string[] data) =>
        BuildaSaveBackend.Set(SaveKeys.TeamNames, SaveCodec.EncodeStringArray(data));

    public static string NormalizeTeamName(int teamIndex, string value)
    {
        string normalized = value == null ? string.Empty : value.Trim();
        if (string.IsNullOrEmpty(normalized)) normalized = $"Team {teamIndex + 1}";
        return normalized;
    }

    public static string GetTeamNameOrDefault(int teamIndex)
    {
        if (teamIndex < 0 || teamIndex >= SelectionsSave.TeamNum) return $"Team {teamIndex + 1}";
        var data = LoadOrCreate();
        return NormalizeTeamName(teamIndex, data[teamIndex]);
    }

    public static void SetTeamName(int teamIndex, string value)
    {
        if (teamIndex < 0 || teamIndex >= SelectionsSave.TeamNum) return;
        var data = LoadOrCreate();
        data[teamIndex] = NormalizeTeamName(teamIndex, value);
        Save(data);
    }
}
[System.Serializable]
public static class EnemyMeetSave
{
    public static readonly string filename = "57E3700D57C1F903F7F35C0BDBFB8808";//enemiesmeet
    public const int SIZE = 1000;
    private static bool[] LoadOrCreate()
    {
        var data = SaveCodec.DecodeBoolArray(BuildaSaveBackend.Get(SaveKeys.EnemyMeet));
        if (data == null || data.Length != SIZE)
        {
            data = new bool[SIZE];
            data[2] = true;
            Save(data);
        }
        return data;
    }
    private static void Save(bool[] data) =>
        BuildaSaveBackend.Set(SaveKeys.EnemyMeet, SaveCodec.EncodeBoolArray(data));
    public static bool[] GetData()
    {
        var data = LoadOrCreate();
        var clone = new bool[SIZE];
        Array.Copy(data, clone, data.Length);
        return clone;
    }
    public static bool GetUnlocked(int x)
    {
        var data = LoadOrCreate();
        var clone = new bool[SIZE];
        Array.Copy(data, clone, data.Length);
        return clone[x];
    }
    public static void SetMetEnemyCode(int x)
    {
        if (x < 0 || x >= SIZE) return;
        var data = LoadOrCreate();
        var clone = new bool[SIZE];
        Array.Copy(data, clone, data.Length);
        clone[x] = true;
        Save(clone);
    }
}

[System.Serializable]
public class BontiquePurchaseEntry
{
    public string bid;
    public DateTime firstPurchaseDate;
    public int purchaseCount;
}

[System.Serializable]
public class BontiquePurchaseData
{
    public List<BontiquePurchaseEntry> entries = new List<BontiquePurchaseEntry>();
}

[System.Serializable]
public static class BontiquePurchaseSave
{
    public static readonly string filename = "A1F1DB4C73A5434C9176FE5B2A9427E1"; // bontique_purchase

    private static BontiquePurchaseData LoadOrCreate(bool saveWhenMissing = false)
    {
        var data = SaveCodec.DecodeBontiquePurchases(BuildaSaveBackend.Get(SaveKeys.BontiquePurchases));
        if (data == null)
        {
            data = new BontiquePurchaseData();
            if (saveWhenMissing) Save(data);
        }
        if (data.entries == null) data.entries = new List<BontiquePurchaseEntry>();
        return data;
    }

    private static void Save(BontiquePurchaseData data) =>
        BuildaSaveBackend.Set(SaveKeys.BontiquePurchases, SaveCodec.EncodeBontiquePurchases(data));

    public static List<BontiquePurchaseEntry> GetAll()
    {
        var data = LoadOrCreate(false);
        var result = new List<BontiquePurchaseEntry>(data.entries.Count);
        for (int i = 0; i < data.entries.Count; i++)
        {
            var e = data.entries[i];
            if (e == null) continue;
            result.Add(new BontiquePurchaseEntry
            {
                bid = e.bid,
                firstPurchaseDate = e.firstPurchaseDate,
                purchaseCount = e.purchaseCount
            });
        }
        return result;
    }

    public static bool TryGet(string bid, out BontiquePurchaseEntry entry)
    {
        entry = null;
        if (string.IsNullOrEmpty(bid)) return false;
        var data = LoadOrCreate(false);
        for (int i = 0; i < data.entries.Count; i++)
        {
            var e = data.entries[i];
            if (e == null || string.IsNullOrEmpty(e.bid)) continue;
            if (!string.Equals(e.bid, bid, StringComparison.Ordinal)) continue;
            entry = new BontiquePurchaseEntry
            {
                bid = e.bid,
                firstPurchaseDate = e.firstPurchaseDate,
                purchaseCount = e.purchaseCount
            };
            return true;
        }
        return false;
    }

    public static int GetPurchaseCount(string bid)
    {
        return TryGet(bid, out var e) ? Mathf.Max(0, e.purchaseCount) : 0;
    }

    public static void AddPurchase(string bid, DateTime now)
    {
        if (string.IsNullOrEmpty(bid)) return;
        var data = LoadOrCreate(true);
        for (int i = 0; i < data.entries.Count; i++)
        {
            var e = data.entries[i];
            if (e == null || string.IsNullOrEmpty(e.bid)) continue;
            if (!string.Equals(e.bid, bid, StringComparison.Ordinal)) continue;
            e.purchaseCount = Mathf.Max(0, e.purchaseCount) + 1;
            Save(data);
            return;
        }
        data.entries.Add(new BontiquePurchaseEntry
        {
            bid = bid,
            firstPurchaseDate = now,
            purchaseCount = 1
        });
        Save(data);
    }

    public static bool RemoveBid(string bid)
    {
        if (string.IsNullOrEmpty(bid)) return false;
        var data = LoadOrCreate(false);
        int before = data.entries.Count;
        data.entries.RemoveAll(e => e != null && string.Equals(e.bid, bid, StringComparison.Ordinal));
        if (data.entries.Count == before) return false;
        Save(data);
        return true;
    }

    public static int RemoveBids(ICollection<string> bids)
    {
        if (bids == null || bids.Count == 0) return 0;
        var data = LoadOrCreate(false);
        int before = data.entries.Count;
        data.entries.RemoveAll(e => e != null && !string.IsNullOrEmpty(e.bid) && bids.Contains(e.bid));
        int removed = before - data.entries.Count;
        if (removed > 0) Save(data);
        return removed;
    }

    public static void ReplaceAll(IEnumerable<BontiquePurchaseEntry> entries)
    {
        var data = new BontiquePurchaseData();
        if (entries != null)
        {
            foreach (var entry in entries)
            {
                if (entry == null || string.IsNullOrEmpty(entry.bid)) continue;
                data.entries.Add(new BontiquePurchaseEntry
                {
                    bid = entry.bid,
                    firstPurchaseDate = entry.firstPurchaseDate,
                    purchaseCount = Mathf.Max(0, entry.purchaseCount)
                });
            }
        }
        Save(data);
    }
}

