using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 1st Anniversary 8x8 board save.
/// The whole board is four longs used as bitmasks, so the file stays at 32 bytes of payload:
/// [0] lit cells, [1] duplicate-hit flags of the latest draw, [2] claimed lines, [3] claimed milestones.
/// </summary>
[System.Serializable]
public static class FirstAnniversarySave
{
    private const string BaseFilename = "1st-Anniversary";
    private const string TestFilenamePrefix = "test-";

    /// <summary>
    /// Development switch, owned by <see cref="AnniversaryBoardCanvas"/>. While set, every read and
    /// write is redirected to a separate "test-" file so test play never touches the player's board.
    /// </summary>
    public static bool TestMode { get; set; }

    public static string filename => TestMode ? TestFilenamePrefix + BaseFilename : BaseFilename;

    public const int Width = 8;
    public const int Size = Width * Width;  // 64 cells -> exactly one long
    public const int LineCount = 18;        // 8 rows + 8 columns + 2 diagonals
    public const int MaxMilestones = 32;

    private const int SlotLit = 0;
    private const int SlotDuplicate = 1;
    private const int SlotLines = 2;
    private const int SlotMilestones = 3;
    private const int SlotCount = 4;

    /// <summary>Cell indices of every line, ordered rows -> columns -> main diagonal -> anti diagonal.</summary>
    public static readonly int[][] LineCells;
    private static readonly long[] LineMasks;

    static FirstAnniversarySave()
    {
        LineCells = new int[LineCount][];
        LineMasks = new long[LineCount];

        for (int r = 0; r < Width; r++)
        {
            var cells = new int[Width];
            for (int c = 0; c < Width; c++) cells[c] = r * Width + c;
            LineCells[r] = cells;
        }
        for (int c = 0; c < Width; c++)
        {
            var cells = new int[Width];
            for (int r = 0; r < Width; r++) cells[r] = r * Width + c;
            LineCells[Width + c] = cells;
        }
        var main = new int[Width];
        var anti = new int[Width];
        for (int i = 0; i < Width; i++)
        {
            main[i] = i * Width + i;
            anti[i] = i * Width + (Width - 1 - i);
        }
        LineCells[Width * 2] = main;
        LineCells[Width * 2 + 1] = anti;

        for (int line = 0; line < LineCount; line++)
        {
            long mask = 0L;
            int[] cells = LineCells[line];
            for (int i = 0; i < cells.Length; i++) mask |= 1L << cells[i];
            LineMasks[line] = mask;
        }
    }

    #region Storage

    private static long[] LoadOrCreate()
    {
        long[] data = GenericSaveSystem.LoadData<long[]>(filename);
        if (data == null || data.Length != SlotCount)
        {
            long[] neo = new long[SlotCount];
            if (data != null) Array.Copy(data, neo, Math.Min(data.Length, SlotCount));
            Save(neo);
            return neo;
        }
        return data;
    }

    private static void Save(long[] data) => GenericSaveSystem.SaveData(data, filename);

    /// <summary>Wipes the board. Only for debug / activity teardown.</summary>
    public static void ResetAll() => Save(new long[SlotCount]);

    public static bool HasSave() => GenericSaveSystem.HasData(filename);

    /// <summary>
    /// 账号转移用的原始槽位数据。没有正式存档时返回 null，让上传方直接跳过。
    /// <para>
    /// 刻意读 <see cref="BaseFilename"/> 而不是 <see cref="filename"/>：<see cref="TestMode"/> 是个
    /// 活过场景切换的静态开关，测试时打开过就会让转移流程把 test- 档当成玩家存档传上去。
    /// 账号转移不认测试档，测试档只留在开发机上。
    /// </para>
    /// </summary>
    public static long[] GetRawData()
    {
        if (!GenericSaveSystem.HasData(BaseFilename)) return null;
        long[] data = GenericSaveSystem.LoadData<long[]>(BaseFilename);
        return Normalize(data);
    }

    /// <summary>
    /// 账号继承用的整盘覆写，同样只认正式存档。长度不足或超出都按 SlotCount 归一，
    /// 所以远端表结构改了也不会越界。
    /// 传 null 就是写回一盘空棋盘——继承必须把上一个账号的进度清掉，不能因为新账号没有
    /// 棋盘数据就把本机残留的棋盘留给它。
    /// </summary>
    public static void ReplaceAll(long[] data) =>
        GenericSaveSystem.SaveData(Normalize(data), BaseFilename);

    private static long[] Normalize(long[] data)
    {
        long[] neo = new long[SlotCount];
        if (data != null) Array.Copy(data, neo, Math.Min(data.Length, SlotCount));
        return neo;
    }

    /// <summary>
    /// Drops the board file when the verified world date falls outside the activity window.
    /// The activity does not carry over, so leftover data must not survive into the next one.
    /// </summary>
    public static bool DeleteIfOutsideWindow(DateTime worldDate)
    {
        if (FirstAnniversarySchedule.IsWithinWindow(worldDate)) return false;
        if (!HasSave()) return false;
        GenericSaveSystem.DeleteData(filename);
        Debug.Log($"[FirstAnniversary] Save deleted: {worldDate:yyyy-MM-dd} is outside the activity window.");
        return true;
    }

    #endregion

    #region Board Reads

    public static long GetLitMask() => LoadOrCreate()[SlotLit];
    public static long GetDuplicateMask() => LoadOrCreate()[SlotDuplicate];

    public static bool IsLit(int index) => IsSet(GetLitMask(), index);
    public static bool IsDuplicateFlagged(int index) => IsSet(GetDuplicateMask(), index);

    public static int GetLitCount() => CountBits(GetLitMask());
    public static bool IsBoardFull() => GetLitCount() >= Size;

    public static bool IsSet(long mask, int index) =>
        index >= 0 && index < Size && (mask & (1L << index)) != 0L;

    public static int CountBits(long mask)
    {
        int count = 0;
        while (mask != 0L)
        {
            mask &= mask - 1L;
            count++;
        }
        return count;
    }

    public static bool IsLineComplete(int line) => IsLineComplete(line, GetLitMask());

    public static bool IsLineComplete(int line, long litMask)
    {
        if (line < 0 || line >= LineCount) return false;
        long mask = LineMasks[line];
        return (litMask & mask) == mask;
    }

    /// <summary>How many cells of a line are still missing; used for "click here to finish X lines" hints.</summary>
    public static int GetLineMissingCount(int line, long litMask)
    {
        if (line < 0 || line >= LineCount) return Width;
        return Width - CountBits(litMask & LineMasks[line]);
    }

    public static int GetCompletedLineCount(long litMask)
    {
        int count = 0;
        for (int line = 0; line < LineCount; line++)
            if (IsLineComplete(line, litMask)) count++;
        return count;
    }

    public static string GetLineName(int line)
    {
        if (line < 0 || line >= LineCount) return string.Empty;
        if (line < Width) return $"ROW {line + 1}";
        if (line < Width * 2) return $"COL {line - Width + 1}";
        return line == Width * 2 ? "DIAGONAL A" : "DIAGONAL B";
    }

    #endregion

    #region Board Writes

    /// <summary>
    /// Applies a batch of drawn cell indices in one write. Duplicate flags of the previous
    /// draw are cleared first, so the flag always means "hit again by the latest draw".
    /// </summary>
    public static void ApplyDraws(IList<int> indices, out int newlyLit, out int duplicates)
    {
        newlyLit = 0;
        duplicates = 0;
        if (indices == null || indices.Count == 0) return;

        long[] data = LoadOrCreate();
        data[SlotDuplicate] = 0L;
        for (int i = 0; i < indices.Count; i++)
        {
            int index = indices[i];
            if (index < 0 || index >= Size) continue;
            long bit = 1L << index;
            if ((data[SlotLit] & bit) != 0L)
            {
                data[SlotDuplicate] |= bit;
                duplicates++;
            }
            else
            {
                data[SlotLit] |= bit;
                newlyLit++;
            }
        }
        Save(data);
    }

    /// <summary>Lights a cell through select tickets. Returns false when it was already lit.</summary>
    public static bool ApplySelect(int index)
    {
        if (index < 0 || index >= Size) return false;
        long[] data = LoadOrCreate();
        long bit = 1L << index;
        if ((data[SlotLit] & bit) != 0L) return false;
        data[SlotLit] |= bit;
        Save(data);
        return true;
    }

    public static void ClearDuplicateFlags()
    {
        long[] data = LoadOrCreate();
        if (data[SlotDuplicate] == 0L) return;
        data[SlotDuplicate] = 0L;
        Save(data);
    }

    #endregion

    #region Reward Claims

    public static bool IsLineClaimed(int line) =>
        line >= 0 && line < LineCount && (LoadOrCreate()[SlotLines] & (1L << line)) != 0L;

    public static bool IsMilestoneClaimed(int milestone) =>
        milestone >= 0 && milestone < MaxMilestones && (LoadOrCreate()[SlotMilestones] & (1L << milestone)) != 0L;

    /// <summary>Marks a line reward as taken. Returns false when it was already taken.</summary>
    public static bool ClaimLine(int line)
    {
        if (line < 0 || line >= LineCount) return false;
        return ClaimBit(SlotLines, line);
    }

    /// <summary>Marks a milestone reward as taken. Returns false when it was already taken.</summary>
    public static bool ClaimMilestone(int milestone)
    {
        if (milestone < 0 || milestone >= MaxMilestones) return false;
        return ClaimBit(SlotMilestones, milestone);
    }

    private static bool ClaimBit(int slot, int bitIndex)
    {
        long[] data = LoadOrCreate();
        long bit = 1L << bitIndex;
        if ((data[slot] & bit) != 0L) return false;
        data[slot] |= bit;
        Save(data);
        return true;
    }

    #endregion
}
