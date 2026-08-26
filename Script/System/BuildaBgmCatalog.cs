using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Maps a logical BGM name onto its path inside the platform's <c>assets.zip</c>.
///
/// Why a table is needed at all: Addressables addresses are the file name *without* extension
/// (see the BGM group asset), but the platform plays files by their real relative path, so the
/// extension has to come from somewhere. Half this library is <c>.ogg</c> and half <c>.mp3</c>,
/// so it cannot be guessed.
///
/// Two entries are also *renamed* on the way out, and only here - the Unity-side assets keep their
/// current names so no Addressables reference has to change:
/// <list type="bullet">
/// <item><c>Who_am_I_extend.MP3</c> - the platform only accepts lowercase <c>.mp3</c>/<c>.ogg</c>/
/// <c>.wav</c>, and an uppercase extension risks rejection on upload and 404s on a case-sensitive
/// CDN.</item>
/// <item><c>Pfeffermouse - Out Of Order.mp3</c> - spaces have to be percent-encoded in a URL, which
/// is one more thing to get wrong in a path that is assembled at runtime.</item>
/// </list>
///
/// **The packaging step must apply the same two renames.** <c>tools/stage-builda-audio.ps1</c>
/// generates the staging tree from this same rule set; if the two ever disagree, the affected track
/// silently fails to play on device while working fine in the editor.
/// </summary>
public static class BuildaBgmCatalog
{
    /// <summary>Directory prefix inside assets.zip. The platform reserves <c>audio/**</c>.</summary>
    public const string AudioRoot = "audio/bgm/";

    // address|file-name-in-assets-zip
    // Kept as one literal block rather than 52 dictionary lines so it stays diffable and it is
    // obvious at a glance which tracks are ogg and which are mp3.
    private const string Table = @"
000|000.ogg
001|001.ogg
002|002.ogg
003|003.ogg
004|004.ogg
006|006.ogg
030|030.ogg
031|031.ogg
032|032.ogg
033|033.ogg
034|034.ogg
047|047.ogg
048|048.ogg
148|148.ogg
149|149.ogg
150|150.ogg
300|300.ogg
301|301.ogg
302|302.ogg
victory|victory.ogg
dojovictory|dojovictory.ogg
lose|lose.ogg
zrdc|zrdc.ogg
bills_piano|bills_piano.mp3
dct|dct.mp3
GF_EV4_intermission|GF_EV4_intermission.mp3
GF_EV6_90w_pt2|GF_EV6_90w_pt2.mp3
HazeReverb-43-antinova|HazeReverb-43-antinova.mp3
HazeReverb-Battle-Warehouse3|HazeReverb-Battle-Warehouse3.mp3
HazeReverb-Feast|HazeReverb-Feast.mp3
HazeReverb-moon|HazeReverb-moon.mp3
HazeReverb-night_raid|HazeReverb-night_raid.mp3
HazeReverb-Nibiru|HazeReverb-Nibiru.mp3
HazeReverb-Skoll|HazeReverb-Skoll.mp3
HazeReverb-spooky|HazeReverb-spooky.mp3
HazeReverb_Mayanow|HazeReverb_Mayanow.mp3
KillingMeorKissingMe|KillingMeorKissingMe.mp3
lilytales-desert|lilytales-desert.mp3
lilytales-fight|lilytales-fight.mp3
lilytales-relic|lilytales-relic.mp3
lilytales-title|lilytales-title.mp3
lowtea|lowtea.mp3
M_19summer_lobby|M_19summer_lobby.mp3
Rabiribi_shop|Rabiribi_shop.mp3
silent_love|silent_love.mp3
snwt|snwt.mp3
starry|starry.mp3
The_Long_Goodbye|The_Long_Goodbye.mp3
toihi|toihi.mp3
UndertheMoonlight|UndertheMoonlight.mp3
Pfeffermouse - Out Of Order|Pfeffermouse-OutOfOrder.mp3
Who_am_I_extend|Who_am_I_extend.mp3
";

    private static Dictionary<string, string> pathByAddress;

    /// <summary>Number of catalogued tracks, for sanity-checking against the staged folder.</summary>
    public static int Count
    {
        get { EnsureBuilt(); return pathByAddress.Count; }
    }

    /// <summary>
    /// Resolves an Addressables BGM address to its path inside assets.zip, or null when the track is
    /// not catalogued.
    ///
    /// A null return is a content bug, not a runtime condition: it means a BGM exists in the
    /// Addressables group but was never added here, so it would be missing from assets.zip too.
    /// The caller logs it loudly for exactly that reason.
    /// </summary>
    public static string TryGetPath(string address)
    {
        if (string.IsNullOrEmpty(address)) return null;
        EnsureBuilt();
        return pathByAddress.TryGetValue(address, out string file) ? AudioRoot + file : null;
    }

    public static bool Contains(string address)
    {
        if (string.IsNullOrEmpty(address)) return false;
        EnsureBuilt();
        return pathByAddress.ContainsKey(address);
    }

    /// <summary>All catalogued file names (without the <see cref="AudioRoot"/> prefix).</summary>
    public static IEnumerable<KeyValuePair<string, string>> All()
    {
        EnsureBuilt();
        return pathByAddress;
    }

    private static void EnsureBuilt()
    {
        if (pathByAddress != null) return;

        pathByAddress = new Dictionary<string, string>(64);
        string[] lines = Table.Split('\n');
        for (int i = 0; i < lines.Length; i++)
        {
            string line = lines[i].Trim('\r', ' ', '\t');
            if (line.Length == 0) continue;

            int bar = line.IndexOf('|');
            if (bar <= 0 || bar >= line.Length - 1)
            {
                Debug.LogError($"[BuildaBgmCatalog] Malformed table row: '{line}'");
                continue;
            }
            pathByAddress[line.Substring(0, bar)] = line.Substring(bar + 1);
        }
    }
}
