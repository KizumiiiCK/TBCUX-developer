using System.Collections.Generic;

/// <summary>
/// The privateKV key namespace.
///
/// **These strings are permanent.** Once a build ships, a player's progress lives under these
/// exact keys; renaming one silently orphans that data and the player loses progress. Treat this
/// file as append-only - add keys, never rewrite them.
///
/// Format is <c>tbcx-&lt;domain&gt;</c>, plus a shard suffix where a payload is split. Only
/// <c>[A-Za-z0-9_-]</c> is used: the platform accepts <c>:</c> for legacy keys but new code is
/// told to stay within the stricter set, and hyphens read better than underscores here.
///
/// Key budget (platform limit is 100 live keys per player per game):
/// <list type="bullet">
/// <item>7 chapter progress shards - one per playable chapter under Resources/LevelData/Chapters</item>
/// <item>7 character-upgrade shards - one per rarity digit 0..6</item>
/// <item>9 single-value payloads</item>
/// </list>
/// 23 keys total, which also fits in one 32-key <c>getMany</c>, so the boot pull is a single
/// round-trip.
/// </summary>
public static class SaveKeys
{
    // ---- sharded: game progress, one key per chapter ----
    // Sharded because the full 33-section array is roughly 90-120KB as JSON and still over 32KB
    // packed, versus about 6-15KB per chapter.

    public const string ProgressPrefix = "tbcx-progress-";

    /// <summary>
    /// Chapter names that get a progress shard. These are directory names under
    /// <c>Resources/LevelData/Chapters/</c>; <c>CPImages</c> and <c>tobeupdated</c> are excluded
    /// because they hold artwork and unfinished content, not playable sections.
    ///
    /// A chapter missing from this list still works (its key is simply not prefetched at boot),
    /// but it costs an extra round-trip, so new chapters should be added here.
    /// </summary>
    public static readonly string[] Chapters =
    {
        "World_I",
        "World_II",
        "World_III",
        "Future_I",
        "LEGEND",
        "Dream_Pre",
        "Dungeon",
    };

    public static string Progress(string chapterName) => ProgressPrefix + Sanitize(chapterName);

    // ---- sharded: character upgrades, one key per rarity ----
    // 251 units pack to roughly 23KB in a single value - under the ceiling but with no headroom.
    // Unit IDs are generated as $"{rarity}{code:000}" so the leading digit is a free shard key.

    public const string UpgradePrefix = "tbcx-units-";

    /// <summary>Rarity digits used by <c>CharacterUpgradeSave.EnumerateAllIds</c> (0..6).</summary>
    public const int RarityCount = 7;

    public static string Upgrades(int rarity) => UpgradePrefix + rarity;

    // ---- single-value payloads ----

    public const string Inventory = "tbcx-inventory";
    public const string TeamSelections = "tbcx-teams";
    public const string TeamNames = "tbcx-team-names";
    public const string EnemyMeet = "tbcx-enemy-met";
    public const string BontiquePurchases = "tbcx-shop-purchases";
    public const string DailyMapClear = "tbcx-daily-clear";
    public const string DrawPending = "tbcx-draw-pending";

    /// <summary>
    /// Daily check-in streak. Used to live in a Supabase table; it is player-scoped progression
    /// like everything else here, so it belongs in privateKV now that there is no game server.
    /// </summary>
    public const string CheckIn = "tbcx-checkin";

    /// <summary>
    /// Claimed Builda pay <c>orderId</c> values. Same order must never grant twice.
    /// </summary>
    public const string PayOrders = "tbcx-pay-orders";

    /// <summary>
    /// Every key the boot pull should fetch. Order is irrelevant; count matters, because staying
    /// at or below 32 keeps the boot pull to one <c>getMany</c>.
    /// </summary>
    public static List<string> AllKeys()
    {
        var keys = new List<string>(Chapters.Length + RarityCount + 9);

        for (int i = 0; i < Chapters.Length; i++) keys.Add(Progress(Chapters[i]));
        for (int r = 0; r < RarityCount; r++) keys.Add(Upgrades(r));

        keys.Add(Inventory);
        keys.Add(TeamSelections);
        keys.Add(TeamNames);
        keys.Add(EnemyMeet);
        keys.Add(BontiquePurchases);
        keys.Add(DailyMapClear);
        keys.Add(DrawPending);
        keys.Add(CheckIn);
        keys.Add(PayOrders);

        return keys;
    }

    /// <summary>
    /// Maps a chapter name onto the platform's allowed key charset. Chapter directory names are
    /// already safe today (letters and underscores), so this only guards against a future chapter
    /// name with a character the platform would reject outright.
    /// </summary>
    private static string Sanitize(string name)
    {
        if (string.IsNullOrEmpty(name)) return "unknown";
        var sb = new System.Text.StringBuilder(name.Length);
        for (int i = 0; i < name.Length; i++)
        {
            char c = name[i];
            bool safe = (c >= 'A' && c <= 'Z') || (c >= 'a' && c <= 'z')
                        || (c >= '0' && c <= '9') || c == '_' || c == '-';
            sb.Append(safe ? c : '-');
        }
        return sb.ToString();
    }
}
