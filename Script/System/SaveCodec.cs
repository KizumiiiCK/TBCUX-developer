using System;
using System.Collections.Generic;
using System.IO;
using System.Text;

/// <summary>
/// Compact binary codec for the save payloads.
///
/// Exists because none of the obvious options work for this game's data:
/// <list type="bullet">
/// <item><c>BinaryFormatter</c> is unavailable under IL2CPP/WebGL (and obsolete everywhere else).</item>
/// <item><c>JsonUtility</c> cannot serialize <c>int[,]</c> / <c>string[,]</c> at all, and several
/// payloads are built on them (<see cref="GameProgressSave.SectionClearList"/>).</item>
/// <item><c>BuildaJson</c> is documented as SDK-internal ("游戏业务数据请用自己的序列化方案").</item>
/// </list>
///
/// Format rules, so a reader written later stays compatible:
/// <list type="bullet">
/// <item>Little-endian, via <see cref="BinaryWriter"/>'s defaults.</item>
/// <item>Every payload starts with a <see cref="Version"/> byte. Bump it and branch on read;
/// never silently change a layout, because old bytes live in players' cloud saves.</item>
/// <item>Lengths are 7-bit-encoded ints (<c>BinaryWriter.Write(string)</c> style) or explicit
/// <c>int</c> where a negative sentinel is needed.</item>
/// <item>A null array/string is written as length <c>-1</c>, distinct from an empty one. The
/// distinction matters: existing load paths test <c>?? Array.Empty&lt;&gt;()</c> and branch on
/// null to mean "no save yet" versus "saved but empty".</item>
/// </list>
///
/// Size matters here: privateKV rejects any value over 32KB, so the encoding is deliberately
/// tighter than JSON. Unit IDs dominate <see cref="GameProgressSave"/> and are written as
/// length-prefixed UTF-8 rather than quoted-and-comma'd JSON, roughly halving that payload.
/// </summary>
public static class SaveCodec
{
    /// <summary>Layout version stamped into every payload. Bump on any format change.</summary>
    public const byte Version = 1;

    private const int NullLength = -1;

    // ---- primitives ----

    private static void WriteHeader(BinaryWriter w) => w.Write(Version);

    private static void ReadHeader(BinaryReader r, string what)
    {
        byte v = r.ReadByte();
        if (v != Version)
            throw new InvalidDataException($"{what}: unsupported save version {v} (expected {Version}).");
    }

    /// <summary>Writes a possibly-null string. Null and empty are preserved distinctly.</summary>
    private static void WriteNullableString(BinaryWriter w, string s)
    {
        if (s == null)
        {
            w.Write(false);
            return;
        }
        w.Write(true);
        w.Write(s);
    }

    private static string ReadNullableString(BinaryReader r) => r.ReadBoolean() ? r.ReadString() : null;

    private static void WriteIntArray(BinaryWriter w, int[] a)
    {
        if (a == null) { w.Write(NullLength); return; }
        w.Write(a.Length);
        for (int i = 0; i < a.Length; i++) w.Write(a[i]);
    }

    private static int[] ReadIntArray(BinaryReader r)
    {
        int n = r.ReadInt32();
        if (n == NullLength) return null;
        var a = new int[n];
        for (int i = 0; i < n; i++) a[i] = r.ReadInt32();
        return a;
    }

    private static void WriteBoolArray(BinaryWriter w, bool[] a)
    {
        if (a == null) { w.Write(NullLength); return; }
        w.Write(a.Length);
        for (int i = 0; i < a.Length; i++) w.Write(a[i]);
    }

    private static bool[] ReadBoolArray(BinaryReader r)
    {
        int n = r.ReadInt32();
        if (n == NullLength) return null;
        var a = new bool[n];
        for (int i = 0; i < n; i++) a[i] = r.ReadBoolean();
        return a;
    }

    // Rank-2 arrays: the whole reason this file exists. Dimensions are stored so the reader
    // reconstructs the exact shape - the game grows these arrays when content is added
    // (GameProgressSave.UpdateCCL copies old values into a freshly-sized array), so shapes are
    // genuinely variable and cannot be assumed from constants.
    private static void WriteIntArray2D(BinaryWriter w, int[,] a)
    {
        if (a == null) { w.Write(NullLength); return; }
        int d0 = a.GetLength(0), d1 = a.GetLength(1);
        w.Write(d0);
        w.Write(d1);
        for (int i = 0; i < d0; i++)
            for (int j = 0; j < d1; j++)
                w.Write(a[i, j]);
    }

    private static int[,] ReadIntArray2D(BinaryReader r)
    {
        int d0 = r.ReadInt32();
        if (d0 == NullLength) return null;
        int d1 = r.ReadInt32();
        var a = new int[d0, d1];
        for (int i = 0; i < d0; i++)
            for (int j = 0; j < d1; j++)
                a[i, j] = r.ReadInt32();
        return a;
    }

    private static void WriteStringArray2D(BinaryWriter w, string[,] a)
    {
        if (a == null) { w.Write(NullLength); return; }
        int d0 = a.GetLength(0), d1 = a.GetLength(1);
        w.Write(d0);
        w.Write(d1);
        for (int i = 0; i < d0; i++)
            for (int j = 0; j < d1; j++)
                WriteNullableString(w, a[i, j]);
    }

    private static string[,] ReadStringArray2D(BinaryReader r)
    {
        int d0 = r.ReadInt32();
        if (d0 == NullLength) return null;
        int d1 = r.ReadInt32();
        var a = new string[d0, d1];
        for (int i = 0; i < d0; i++)
            for (int j = 0; j < d1; j++)
                a[i, j] = ReadNullableString(r);
        return a;
    }

    private static byte[] ToBytes(Action<BinaryWriter> write)
    {
        using (var ms = new MemoryStream())
        {
            // UTF8Encoding(false) so no BOM leaks into the byte stream; leaveOpen=false is fine
            // because we read the buffer via ToArray() after the writer is flushed by Dispose.
            using (var w = new BinaryWriter(ms, new UTF8Encoding(false)))
            {
                write(w);
            }
            return ms.ToArray();
        }
    }

    private static T FromBytes<T>(byte[] bytes, Func<BinaryReader, T> read) where T : class
    {
        if (bytes == null || bytes.Length == 0) return null;
        using (var ms = new MemoryStream(bytes, writable: false))
        using (var r = new BinaryReader(ms, new UTF8Encoding(false)))
        {
            return read(r);
        }
    }

    // ---- GameProgressSave: one chapter per payload ----
    // Sharded per chapter because the full 33-section array lands around 90-120KB, well past the
    // 32KB privateKV ceiling. Chapters are independent: nothing in SectionClearList references
    // another chapter, and the existing loader already looks chapters up by name.

    public static byte[] EncodeChapter(GameProgressSave.ChapterClearList chapter)
    {
        return ToBytes(w =>
        {
            WriteHeader(w);
            WriteNullableString(w, chapter?.ChapterName);
            var sections = chapter?.SectionList;
            if (sections == null) { w.Write(NullLength); return; }
            w.Write(sections.Length);
            for (int i = 0; i < sections.Length; i++) WriteSection(w, sections[i]);
        });
    }

    public static GameProgressSave.ChapterClearList DecodeChapter(byte[] bytes)
    {
        return FromBytes(bytes, r =>
        {
            ReadHeader(r, "chapter progress");
            var chapter = new GameProgressSave.ChapterClearList
            {
                ChapterName = ReadNullableString(r),
            };
            int n = r.ReadInt32();
            if (n == NullLength) return chapter;
            chapter.SectionList = new GameProgressSave.SectionClearList[n];
            for (int i = 0; i < n; i++) chapter.SectionList[i] = ReadSection(r);
            return chapter;
        });
    }

    private static void WriteSection(BinaryWriter w, GameProgressSave.SectionClearList s)
    {
        // A null section slot would break the shape on read; treat it as an empty section rather
        // than writing a sentinel, since the game never legitimately produces null slots.
        if (s == null) s = new GameProgressSave.SectionClearList();
        WriteNullableString(w, s.SectionName);
        w.Write(s.cleared);
        WriteBoolArray(w, s.reward_gained);
        WriteIntArray2D(w, s.clear_times);
        WriteIntArray2D(w, s.level_score);
        WriteStringArray2D(w, s.cleared_teams);
        WriteIntArray(w, s.cleared_cannon);
    }

    private static GameProgressSave.SectionClearList ReadSection(BinaryReader r)
    {
        return new GameProgressSave.SectionClearList
        {
            SectionName = ReadNullableString(r),
            cleared = r.ReadBoolean(),
            reward_gained = ReadBoolArray(r),
            clear_times = ReadIntArray2D(r),
            level_score = ReadIntArray2D(r),
            cleared_teams = ReadStringArray2D(r),
            cleared_cannon = ReadIntArray(r),
        };
    }

    // ---- CharacterUpgradeSave: bucketed by rarity digit ----
    // 251 units at ~90 bytes each is ~23KB in one blob - under the ceiling today, but with no
    // headroom for new units. Bucketing by the leading rarity digit keeps each value small and
    // matches how IDs are generated ($"{r}{code:000}", so the first char is always the bucket).

    public static byte[] EncodeUpgrades(Dictionary<string, CharacterUpgradeSave.UpgradeDetails> dict)
    {
        return ToBytes(w =>
        {
            WriteHeader(w);
            if (dict == null) { w.Write(NullLength); return; }
            w.Write(dict.Count);
            foreach (var kv in dict)
            {
                w.Write(kv.Key ?? string.Empty);
                WriteUpgrade(w, kv.Value);
            }
        });
    }

    public static Dictionary<string, CharacterUpgradeSave.UpgradeDetails> DecodeUpgrades(byte[] bytes)
    {
        return FromBytes(bytes, r =>
        {
            ReadHeader(r, "character upgrades");
            int n = r.ReadInt32();
            if (n == NullLength) return null;
            var dict = new Dictionary<string, CharacterUpgradeSave.UpgradeDetails>(n);
            for (int i = 0; i < n; i++)
            {
                string key = r.ReadString();
                dict[key] = ReadUpgrade(r);
            }
            return dict;
        });
    }

    private static void WriteUpgrade(BinaryWriter w, CharacterUpgradeSave.UpgradeDetails u)
    {
        if (u == null) u = new CharacterUpgradeSave.UpgradeDetails();
        WriteBoolArray(w, u.tire_unlocked);
        w.Write(u.talent_unlocked);
        w.Write(u.upgraded_level);
        w.Write(u.plus_level);

        // proficiency is nullable in practice: CharacterUpgradeSave.Load repairs null instances
        // on read, so it must round-trip as absent rather than crash.
        var p = u.proficiency;
        if (p == null)
        {
            w.Write(false);
            return;
        }
        w.Write(true);
        w.Write(p.level);
        WriteIntArray(w, p.pro_stack);
        WriteIntArray(w, p.pro_overflow);
    }

    private static CharacterUpgradeSave.UpgradeDetails ReadUpgrade(BinaryReader r)
    {
        var u = new CharacterUpgradeSave.UpgradeDetails
        {
            tire_unlocked = ReadBoolArray(r),
            talent_unlocked = r.ReadBoolean(),
            upgraded_level = r.ReadInt32(),
            plus_level = r.ReadInt32(),
        };
        if (!r.ReadBoolean())
        {
            u.proficiency = null;
            return u;
        }
        u.proficiency = new CharacterProficiency
        {
            level = r.ReadInt32(),
            pro_stack = ReadIntArray(r),
            pro_overflow = ReadIntArray(r),
        };
        return u;
    }

    // ---- small payloads ----

    public static byte[] EncodeIntArray(int[] a) => ToBytes(w => { WriteHeader(w); WriteIntArray(w, a); });

    public static int[] DecodeIntArray(byte[] bytes)
    {
        if (bytes == null || bytes.Length == 0) return null;
        using (var ms = new MemoryStream(bytes, writable: false))
        using (var r = new BinaryReader(ms, new UTF8Encoding(false)))
        {
            ReadHeader(r, "int array");
            return ReadIntArray(r);
        }
    }

    public static byte[] EncodeBoolArray(bool[] a) => ToBytes(w => { WriteHeader(w); WriteBoolArray(w, a); });

    public static bool[] DecodeBoolArray(byte[] bytes)
    {
        if (bytes == null || bytes.Length == 0) return null;
        using (var ms = new MemoryStream(bytes, writable: false))
        using (var r = new BinaryReader(ms, new UTF8Encoding(false)))
        {
            ReadHeader(r, "bool array");
            return ReadBoolArray(r);
        }
    }

    public static byte[] EncodeStringArray(string[] a)
    {
        return ToBytes(w =>
        {
            WriteHeader(w);
            if (a == null) { w.Write(NullLength); return; }
            w.Write(a.Length);
            for (int i = 0; i < a.Length; i++) WriteNullableString(w, a[i]);
        });
    }

    public static string[] DecodeStringArray(byte[] bytes)
    {
        return FromBytes(bytes, r =>
        {
            ReadHeader(r, "string array");
            int n = r.ReadInt32();
            if (n == NullLength) return null;
            var a = new string[n];
            for (int i = 0; i < n; i++) a[i] = ReadNullableString(r);
            return a;
        });
    }

    public static byte[] EncodeStringArray2D(string[,] a) => ToBytes(w => { WriteHeader(w); WriteStringArray2D(w, a); });

    public static string[,] DecodeStringArray2D(byte[] bytes)
    {
        return FromBytes(bytes, r =>
        {
            ReadHeader(r, "string array 2D");
            return ReadStringArray2D(r);
        });
    }

    public static byte[] EncodeDailyMapClear(DailyMapClearRecord record)
    {
        return ToBytes(w =>
        {
            WriteHeader(w);
            if (record == null) { w.Write(false); return; }
            w.Write(true);
            WriteNullableString(w, record.dateToken);
            var names = record.clearedSectionNames;
            if (names == null) { w.Write(NullLength); return; }
            w.Write(names.Count);
            for (int i = 0; i < names.Count; i++) WriteNullableString(w, names[i]);
        });
    }

    public static DailyMapClearRecord DecodeDailyMapClear(byte[] bytes)
    {
        return FromBytes(bytes, r =>
        {
            ReadHeader(r, "daily map clear");
            if (!r.ReadBoolean()) return null;
            var record = new DailyMapClearRecord
            {
                dateToken = ReadNullableString(r) ?? string.Empty,
            };
            int n = r.ReadInt32();
            if (n == NullLength)
            {
                record.clearedSectionNames = null;
                return record;
            }
            record.clearedSectionNames = new List<string>(n);
            for (int i = 0; i < n; i++) record.clearedSectionNames.Add(ReadNullableString(r));
            return record;
        });
    }

    public static byte[] EncodeIntList(List<int> list)
    {
        return ToBytes(w =>
        {
            WriteHeader(w);
            if (list == null) { w.Write(NullLength); return; }
            w.Write(list.Count);
            for (int i = 0; i < list.Count; i++) w.Write(list[i]);
        });
    }

    public static List<int> DecodeIntList(byte[] bytes)
    {
        return FromBytes(bytes, r =>
        {
            ReadHeader(r, "int list");
            int n = r.ReadInt32();
            if (n == NullLength) return null;
            var list = new List<int>(n);
            for (int i = 0; i < n; i++) list.Add(r.ReadInt32());
            return list;
        });
    }

    public static byte[] EncodeBontiquePurchases(BontiquePurchaseData data)
    {
        return ToBytes(w =>
        {
            WriteHeader(w);
            if (data == null) { w.Write(false); return; }
            w.Write(true);
            var entries = data.entries;
            if (entries == null) { w.Write(NullLength); return; }
            w.Write(entries.Count);
            for (int i = 0; i < entries.Count; i++)
            {
                var e = entries[i] ?? new BontiquePurchaseEntry();
                WriteNullableString(w, e.bid);
                // DateTime as ticks: Kind is deliberately dropped. These are shop-window dates
                // compared against PlatformTimeSystem's UTC+8 calendar dates, so only the date
                // component is ever consumed and re-adding a Kind byte would imply a precision
                // the callers do not actually have.
                w.Write(e.firstPurchaseDate.Ticks);
                w.Write(e.purchaseCount);
            }
        });
    }

    public static BontiquePurchaseData DecodeBontiquePurchases(byte[] bytes)
    {
        return FromBytes(bytes, r =>
        {
            ReadHeader(r, "bontique purchases");
            if (!r.ReadBoolean()) return null;
            var data = new BontiquePurchaseData();
            int n = r.ReadInt32();
            if (n == NullLength)
            {
                data.entries = null;
                return data;
            }
            data.entries = new List<BontiquePurchaseEntry>(n);
            for (int i = 0; i < n; i++)
            {
                data.entries.Add(new BontiquePurchaseEntry
                {
                    bid = ReadNullableString(r),
                    firstPurchaseDate = new DateTime(r.ReadInt64()),
                    purchaseCount = r.ReadInt32(),
                });
            }
            return data;
        });
    }
}
