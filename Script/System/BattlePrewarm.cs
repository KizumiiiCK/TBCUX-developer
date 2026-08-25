using System.Collections;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Derives the full Addressables asset set a battle needs, so it can be downloaded before the
/// battle scene is entered instead of being blocked on mid-frame.
///
/// This is possible because <see cref="LevelData"/> is fully declarative: background id, base
/// image, combat effects and the enemy summon table are all statically enumerable. The player's
/// team comes from <see cref="SelectionsSave"/>. Nothing here needs the battle scene to be live.
///
/// Note LevelData itself is still a Resources asset (baked into the build, no download), so it can
/// be read immediately; the Addressables content it points at is what needs prewarming.
/// </summary>
public static class BattlePrewarm
{
    // Mirrors CharacterVisualLoader.DecryptCharacterFiles + BuildExtraAnimIndices.
    private static readonly string[] BaseMaanims = { "walk", "idle", "attack", "kb" };
    private static readonly string[] ExtraMaanims = { "p", "in", "dive", "out" };

    /// <summary>Upper bound for the Units/Projectiles/pNNN scan. Raise if new ids are added.</summary>
    private const int MaxProjectileId = 100;

    /// <summary>Address of the LevelData asset for the pref-selected level.</summary>
    public static string GetLevelDataAddress(string chapterName, string sectionName, int diff, int levelNum)
        => $"LevelData/LevelEnemyData/{chapterName}/{sectionName}/dif{diff}/{levelNum}";

    /// <summary>
    /// Prewarms the battle described by the current PlayerPrefs selection, using the saved team.
    /// This mirrors how LevelController reads its own inputs (LoadLevelInfoFromPref +
    /// SelectionsSave.GetRow), so what gets prewarmed matches what the scene will ask for.
    /// </summary>
    public static IEnumerator PrewarmCurrentBattleRoutine(
        System.Action<float, string> onProgress = null,
        System.Action<LevelData> onLevelDataResolved = null)
    {
        string chapterName = PlayerPrefs.GetString(UXPref.ChapterName, UXPref.DefaultChapterName);
        string sectionName = PlayerPrefs.GetString(UXPref.SectionName);
        int diff = PlayerPrefs.GetInt(UXPref.Difficulty);
        int levelNum = PlayerPrefs.GetInt(UXPref.LevelNum);
        string[] team = SelectionsSave.GetRow(PlayerPrefs.GetInt(SelectionsSave.pref_teamnum, 0));

        yield return PrewarmBattleRoutine(
            chapterName, sectionName, diff, levelNum, team, onProgress, onLevelDataResolved);
    }

    /// <summary>
    /// Runs the whole battle prewarm: resolve LevelData, then everything it references.
    /// Yield this from a LoadingPage task before switching to the battle scene.
    /// </summary>
    public static IEnumerator PrewarmBattleRoutine(
        string chapterName,
        string sectionName,
        int diff,
        int levelNum,
        string[] teamCodes,
        System.Action<float, string> onProgress = null,
        System.Action<LevelData> onLevelDataResolved = null)
    {
        if (!BundledAddressables.IsReady) yield return BundledAddressables.InitializeRoutine();

        // Stage 1 - LevelData. This one still lives under Resources/, which is baked into the WebGL
        // build and therefore already in memory - no download needed. Reading it here (rather than
        // in the battle scene) is what lets us enumerate everything else up front.
        string levelAddress = GetLevelDataAddress(chapterName, sectionName, diff, levelNum);
        onProgress?.Invoke(0.02f, "Reading level data...");
        LevelData ld = Resources.Load<LevelData>(levelAddress);
        onLevelDataResolved?.Invoke(ld);
        if (ld == null)
        {
            // Caller decides how to surface this; LoadingPage will fail the task.
            Debug.LogError($"[BattlePrewarm] LevelData not found at Resources/'{levelAddress}'.");
            yield break;
        }

        // Stage 2 - every Addressable the level references.
        var stage2 = BuildBattleList(ld, teamCodes);
        yield return BundledAddressables.PrewarmRoutine(stage2,
            (p, label) => onProgress?.Invoke(0.02f + p * 0.98f, label));
    }

    /// <summary>Resources-relative path of the LevelData asset (not an Addressables address).</summary>
    public static LevelData LoadLevelData(string chapterName, string sectionName, int diff, int levelNum)
        => Resources.Load<LevelData>(GetLevelDataAddress(chapterName, sectionName, diff, levelNum));

    /// <summary>
    /// Queues every address a battle reads synchronously: level scenery, the player's team and
    /// every enemy in the summon table.
    /// </summary>
    public static BundledAddressables.PrewarmList BuildBattleList(LevelData ld, string[] teamCodes)
    {
        var list = new BundledAddressables.PrewarmList();
        AddLevelScenery(list, ld);
        AddTeam(list, teamCodes);
        AddEnemies(list, ld);
        AddSharedBattleAssets(list);
        return list;
    }

    /// <summary>Background, doge base sprite and combat effect prefabs (LevelController.SetupMapAndBases).</summary>
    private static void AddLevelScenery(BundledAddressables.PrewarmList list, LevelData ld)
    {
        if (ld == null) return;

        // LevelController.cs:406 -> BackgroundInitializer.UpdateMaterialProperties
        list.Add<Sprite>($"Background/Maps/{ld.BackgroundID}");

        // LevelController.cs:429
        list.Add<Sprite>($"Units/DogeBases/{ld.BaseImageID}");

        // LevelController.cs:458
        if (ld.CombatEffect != null)
        {
            for (int i = 0; i < ld.CombatEffect.Length; i++)
                list.Add<GameObject>($"Background/CombatEffects/{ld.CombatEffect[i]}");
        }

        // BGM per summoner phase (LevelEnemySummoner.SetChangeBGM -> BGMTool).
        // The field holds a logical id ("002"); BGMTool normalizes it to the catalog address.
        if (ld.enemySummoners != null)
        {
            for (int i = 0; i < ld.enemySummoners.Length; i++)
            {
                string bgm = BGMTool.NormalizeBgmAddress(ld.enemySummoners[i]?.bgm);
                if (!string.IsNullOrEmpty(bgm)) list.Add<AudioClip>(bgm);
            }
        }
    }

    /// <summary>The 13 deployer slots (10 main + 3 guest) from the saved team row.</summary>
    private static void AddTeam(BundledAddressables.PrewarmList list, string[] teamCodes)
    {
        if (teamCodes == null) return;
        for (int i = 0; i < teamCodes.Length; i++) AddUnitByCode(list, teamCodes[i]);
    }

    /// <summary>Every distinct enemy in the summon table, plus the shared enemy unit prefab.</summary>
    private static void AddEnemies(BundledAddressables.PrewarmList list, LevelData ld)
    {
        if (ld?.enemySummoners == null) return;

        // LevelEnemySummoner.cs:91
        list.Add<GameObject>("Units/Enemy Units/enemyunit");

        var seen = new HashSet<string>();
        for (int i = 0; i < ld.enemySummoners.Length; i++)
        {
            EnemySummonInfo[] infos = ld.enemySummoners[i]?.enemySummonInfos;
            if (infos == null) continue;

            for (int j = 0; j < infos.Length; j++)
            {
                string id = infos[j]?.enemyID;
                if (string.IsNullOrEmpty(id) || !seen.Add(id)) continue;
                AddUnit(list, false, id);
            }
        }
    }

    /// <summary>Prefabs and audio every battle touches regardless of level.</summary>
    private static void AddSharedBattleAssets(BundledAddressables.PrewarmList list)
    {
        // CharacterSummoner.cs:41-46 - the cat unit shell prefab.
        list.Add<GameObject>("Units/Cat Units/catunit");

        // Wave / surge attacks spawn these from combat callbacks (CharacterCombat.cs:421,430).
        // They fire mid-frame from attack logic and cannot be coroutines, so they must be resident.
        list.Add<GameObject>("Units/Cat Units/waveunit");
        list.Add<GameObject>("Units/Enemy Units/waveunit");
        list.Add<GameObject>("Units/Cat Units/surgeunit");
        list.Add<GameObject>("Units/Enemy Units/surgeunit");

        AddPlayerBaseAssets(list);
        AddProjectileAssets(list);
    }

    /// <summary>
    /// The player's cat base: skin, decoration and cannon are chosen in the base menu and read from
    /// PlayerPrefs by CatBase.InitializeCharacter, so they are known before the battle starts.
    /// </summary>
    private static void AddPlayerBaseAssets(BundledAddressables.PrewarmList list)
    {
        int numBase = PlayerPrefs.GetInt(UXPref.BASE_BaseNum, 0);
        int numDeco = PlayerPrefs.GetInt(UXPref.BASE_DecorationNum, 0);
        int cannonType = PlayerPrefs.GetInt(UXPref.BASE_CannonNum, 0);

        list.Add<Sprite>($"Units/CatBases/base/{numBase}");
        list.Add<Sprite>($"Units/CatBases/decorations/{numDeco}");
        list.Add<Sprite>($"Units/CatBases/head/{cannonType}");

        // CatBase.cs:177 / CannonUnit.cs:41 - the cannon and its effect units.
        list.Add<GameObject>($"Units/CatBases/effectUnits/{cannonType}/cannonUnit");
        list.AddNumbered<GameObject>($"Units/CatBases/effectUnits/{cannonType}/eff", 16);
        // CatBase.cs:243 - install-complete effect, hard-coded to set 5.
        list.Add<GameObject>("Units/CatBases/effectUnits/5/eff/1");
    }

    /// <summary>
    /// Projectile prefabs (PassiveEditor.cs:1092). The id comes from per-unit passive data and is
    /// only known at fire time, so the whole catalogued range is queued; absent ids are skipped.
    /// </summary>
    private static void AddProjectileAssets(BundledAddressables.PrewarmList list)
    {
        for (int i = 0; i <= MaxProjectileId; i++)
        {
            string address = $"Units/Projectiles/p{i:000}/projunit";
            if (BundledAddressables.Exists(address, typeof(GameObject))) list.Add<GameObject>(address);
        }
    }

    /// <summary>
    /// Resolves a deployer/summon code through the same parser gameplay uses, then queues the
    /// unit's assets. Codes that do not parse are skipped silently (empty slots are normal).
    /// </summary>
    public static void AddUnitByCode(BundledAddressables.PrewarmList list, string code)
    {
        if (string.IsNullOrEmpty(code)) return;
        if (!CharacterPlacer.TryParse(code, true, out UnitIdentity identity) || !identity.IsValid) return;
        AddUnit(list, identity.AssetIsCat, identity.CharacterCode);
    }

    /// <summary>
    /// Queues one unit's full asset set, mirroring CharacterVisualLoader:
    /// data + icon + (UA prefab | sprite/imgcut/mamodel/maanims).
    /// Both animation styles are queued because which one is used depends on flags inside the
    /// CharacterData, which is not loaded yet at list-build time. Absent addresses are skipped by
    /// the prewarm routine, so over-queueing is cheap and safe.
    /// </summary>
    public static void AddUnit(BundledAddressables.PrewarmList list, bool cat, string characterCode)
    {
        if (list == null || string.IsNullOrEmpty(characterCode)) return;

        string root = CharacterVisualLoader.GetCharacterLoadPath(cat, characterCode);

        list.Add<CharacterData>(root + "data");

        // CharacterPlacer.LoadIcon probes icon_deploy then enemy_icon.
        list.Add<Sprite>(root + "icon_deploy");
        list.Add<Sprite>(root + "enemy_icon");

        // UNITY/Spine animated units use a prefab...
        list.Add<GameObject>(root + "uaunit");

        // ...custom-animated units use the decrypt pack.
        list.Add<Texture2D>(root + "sprite");
        list.Add<TextAsset>(root + "imgcut");
        list.Add<TextAsset>(root + "mamodel");
        for (int i = 0; i < BaseMaanims.Length; i++)
            list.Add<TextAsset>(root + "maanim_" + BaseMaanims[i]);
        for (int i = 0; i < ExtraMaanims.Length; i++)
            list.Add<TextAsset>(root + "maanim_" + ExtraMaanims[i]);
    }
}
