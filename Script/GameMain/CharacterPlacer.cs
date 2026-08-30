using UnityEngine;

/// <summary>
/// 战场单位身份：编队（host）决定敌我，资源（asset）决定读猫还是读敌。
/// 代码以 "-" 开头表示放置对方角色。
/// </summary>
public readonly struct UnitIdentity
{
    public readonly bool HostIsCat;
    public readonly bool AssetIsCat;
    public readonly bool IsOpposite;
    public readonly string CharacterCode;

    public UnitIdentity(bool hostIsCat, bool assetIsCat, bool isOpposite, string characterCode)
    {
        HostIsCat = hostIsCat;
        AssetIsCat = assetIsCat;
        IsOpposite = isOpposite;
        CharacterCode = characterCode ?? string.Empty;
    }

    public bool IsValid
    {
        get
        {
            if (string.IsNullOrEmpty(CharacterCode)) return false;
            if (AssetIsCat) return CharacterCode.Length >= 5;
            return CharacterCode.Length > 0;
        }
    }
}

/// <summary>
/// 敌我双方共用的角色放置：实例化本方 prefab，加载指定资源，对方单位翻转 scale.x。
/// </summary>
public static class CharacterPlacer
{
    public const string CatUnitPrefabPath = "Units/Cat Units/catunit";
    public const string EnemyUnitPrefabPath = "Units/Enemy Units/enemyunit";
    public const char OppositePrefix = '-';

    public static bool TryParse(string rawCode, bool hostIsCat, out UnitIdentity identity)
    {
        identity = default;
        if (string.IsNullOrWhiteSpace(rawCode)) return false;

        string code = rawCode.Trim();
        bool opposite = code[0] == OppositePrefix;
        if (opposite)
        {
            if (code.Length < 2) return false;
            code = code.Substring(1).Trim();
        }

        bool assetIsCat = hostIsCat ? !opposite : opposite;
        identity = new UnitIdentity(hostIsCat, assetIsCat, opposite, code);
        return identity.IsValid;
    }

    public static string GetLoadPath(UnitIdentity identity)
    {
        return CharacterVisualLoader.GetCharacterLoadPath(identity.AssetIsCat, identity.CharacterCode);
    }

    public static string GetBundledFolderPath(UnitIdentity identity)
    {
        if (!identity.IsValid) return string.Empty;
        return "Assets/Bundled/" + GetLoadPath(identity).TrimEnd('/');
    }

    public static string GetHostPrefabPath(UnitIdentity identity)
    {
        return identity.HostIsCat ? CatUnitPrefabPath : EnemyUnitPrefabPath;
    }

    public static CharacterData LoadData(UnitIdentity identity)
    {
        if (!identity.IsValid) return null;
        return CharacterVisualLoader.LoadCharacterData(identity.AssetIsCat, identity.CharacterCode);
    }

    public static Sprite LoadIcon(UnitIdentity identity)
    {
        if (!identity.IsValid) return null;
        string path = GetLoadPath(identity);
        Sprite icon = BundledAddressables.LoadSync<Sprite>(path + "icon_deploy");
        if (icon == null) icon = BundledAddressables.LoadSync<Sprite>(path + "enemy_icon");
        return icon;
    }

    public static Sprite LoadEnemyIcon(UnitIdentity identity)
    {
        if (!identity.IsValid) return null;
        return BundledAddressables.LoadSync<Sprite>(GetLoadPath(identity) + "enemy_icon");
    }

    public static AnimDecryptPack Decrypt(UnitIdentity identity, CharacterData data)
    {
        if (!identity.IsValid || data == null || data.UNITYAnimated) return null;
        return CharacterVisualLoader.DecryptCharacterFiles(identity.AssetIsCat, identity.CharacterCode, data);
    }

    public static GameObject Place(
        UnitIdentity identity,
        CharacterData data,
        AnimDecryptPack decryptedPack,
        Vector3 worldPosition,
        LevelController levelController,
        int level = 1,
        float treasureCount = 0f,
        float power = 1f,
        int uaOrder = 0,
        int adOrder = 0)
    {
        if (!identity.IsValid || data == null) return null;

        GameObject prefab = CharacterVisualLoader.LoadPrefab(GetHostPrefabPath(identity));
        if (prefab == null) return null;

        GameObject spawned = Object.Instantiate(prefab, worldPosition, Quaternion.identity);
        Character character = spawned.GetComponent<Character>();
        if (character == null)
        {
            Object.Destroy(spawned);
            return null;
        }

        character.SetOppositeUnit(identity.IsOpposite);
        if (!Mathf.Approximately(power, 1f)) character.SetPower(power);
        character.LoadCharacterData(levelController, data, level, treasureCount);
        character.levelController = levelController;
        CharacterVisualLoader.InitializeRuntimeCharacterVisual(
            spawned,
            identity.AssetIsCat,
            identity.CharacterCode,
            data,
            decryptedPack,
            "Units",
            uaOrder,
            adOrder);

        if (identity.IsOpposite) FlipScaleX(spawned);
        return spawned;
    }

    public static void FlipScaleX(GameObject go)
    {
        if (go == null) return;
        Vector3 scale = go.transform.localScale;
        scale.x = -scale.x;
        go.transform.localScale = scale;
    }
}
