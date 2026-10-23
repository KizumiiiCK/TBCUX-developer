using System.Collections;
using UnityEngine;

/// <summary>
/// PU 玩法的放人者：一条 PU 规则一个实例，挂在 Level Initializer 上，骨架同 <see cref="LevelEnemySummoner"/>。
/// <para>
/// 到点后先把单位从猫咪基地放出来，再延迟一个 FixedUpdate 放一发 boss 震波当提示 ——
/// 时序和敌方 bossShock（<see cref="Aux_BossWave"/>）完全一致，但这一发<b>不击退任何单位</b>、
/// 不锁狗基地、不切 BGM，只有特效和预制体自带的音效。
/// </para>
/// <para>
/// 「不许死」那部分不在这里：放人时给单位挂上 <see cref="Aux_ProtectUnitRule"/>，
/// 生命与基地的联动全由那条被动负责，本组件放完人就没事可做了。
/// </para>
/// </summary>
public class LevelProtectUnitSummoner : MonoBehaviour
{
    private const string BossShockPrefabPath = "Effects/boss_shock";
    /// <summary>出场位置：同部署器放猫一样，落在猫咪基地右侧半格。</summary>
    private const float SPAWN_X_OFFSET = 0.5f;

    private LevelController LC;
    private CatBase catBase;
    private UnitIdentity identity;
    private CharacterData CD;
    private AnimDecryptPack decryptedFiles;
    private int level = 1;
    private int deployFrames;
    private int treasureCount;

    private bool isConfigured;
    private int framesPassed;

    /// <summary>
    /// 配置失败（角色码非法、数据或解密包缺失）时返回 false 并自行禁用：
    /// 保护目标放不出来就当这条规则不存在，不能连累整关跑不起来。
    /// </summary>
    public bool Configure(
        LevelController levelController,
        CatBase baseComponent,
        string code,
        int unitLevel,
        int deployDelayFrames,
        int treasure)
    {
        LC = levelController;
        catBase = baseComponent;
        treasureCount = treasure;

        if (LC == null || catBase == null)
        {
            Debug.LogError("[LevelProtectUnitSummoner] Missing level controller / cat base.");
            enabled = false;
            return false;
        }

        if (!CharacterPlacer.TryParse(code, true, out identity) || !identity.IsValid)
        {
            Debug.LogError($"[LevelProtectUnitSummoner] Invalid unit id: {code}");
            enabled = false;
            return false;
        }

        CD = CharacterPlacer.LoadData(identity);
        if (CD == null)
        {
            Debug.LogError($"[LevelProtectUnitSummoner] Unit data not found for {code}");
            enabled = false;
            return false;
        }

        // 和部署器出的猫一个待遇：本关对猫方的 CharacterData 限制（OH / S+ / hd ...）照常施加。
        LevelRestrictionHelper.ApplyCatCharacterDataRestrictions(LC.LevelRestrictions, CD);

        if (!CD.UNITYAnimated)
        {
            decryptedFiles = CharacterPlacer.Decrypt(identity, CD);
            if (decryptedFiles == null)
            {
                Debug.LogError($"[LevelProtectUnitSummoner] Failed to decrypt {code}");
                enabled = false;
                return false;
            }
        }

        // 等级按规则写死，不过 ApplyUnitLevelCap：PU 是剧本安排的单位，不该被 LC 压级。
        level = Mathf.Max(1, unitLevel);
        deployFrames = Mathf.Max(0, deployDelayFrames);
        framesPassed = 0;
        isConfigured = true;
        return true;
    }

    private void FixedUpdate()
    {
        if (!isConfigured) return;
        if (LC == null || catBase == null) return;
        if (LC.isPloting) return;
        // 还没放人就先把关打完了（基地被爆/放弃）：这条规则作罢，别在结算后又冒一只猫出来。
        if (LC.IsBattleConcluded)
        {
            isConfigured = false;
            return;
        }

        if (framesPassed >= deployFrames) DeployProtectUnit();
        framesPassed++;
    }

    private void DeployProtectUnit()
    {
        isConfigured = false; // 只放一次。

        int visualLayer = Random.Range(0, LevelController.VisualLayerCount);
        int visualOrder = visualLayer * LevelController.VisualLayerStride + LC.NextVisualSlot(visualLayer);
        GameObject spawned = CharacterPlacer.Place(
            identity,
            CD,
            decryptedFiles,
            catBase.transform.position + new Vector3(SPAWN_X_OFFSET, -visualLayer / 10f, 0),
            LC,
            level,
            treasureCount,
            1f,
            visualOrder,
            visualOrder);

        if (spawned == null)
        {
            // 放不出来就整条规则作废：此时还没有「保护目标」，不能反过来判负。
            Debug.LogError($"[LevelProtectUnitSummoner] Failed to place protect unit {identity.CharacterCode}");
            return;
        }

        // 不受 CC / maxCatDeploy 上限阻挡，但必须计数：CatCharacter.OnDestroy 是无条件 RemoveACat 的，
        // 不加这一笔，这只猫一死就会把别人的名额凭空多让出来一个。
        LC.ForceCountCatDeploy();

        Character protectUnit = spawned.GetComponent<Character>();
        if (protectUnit == null)
        {
            Debug.LogError($"[LevelProtectUnitSummoner] Spawned object has no Character: {identity.CharacterCode}");
            return;
        }

        Aux_ProtectUnitRule.EnsureOn(protectUnit);
        StartCoroutine(PlayHintWaveRoutine());
    }

    /// <summary>
    /// 提示用的 boss 震波：只在猫咪基地放一个 boss_shock（预制体自带音效），不对任何单位发 KB。
    /// 延迟一帧和 Aux_BossWave 对齐——单位先落地，震波紧跟其后。
    /// </summary>
    private IEnumerator PlayHintWaveRoutine()
    {
        yield return new WaitForFixedUpdate();
        if (catBase == null) yield break;
        GameObject shockPrefab = Resources.Load<GameObject>(BossShockPrefabPath);
        if (shockPrefab == null)
        {
            Debug.LogWarning($"[LevelProtectUnitSummoner] Missing prefab: {BossShockPrefabPath}");
            yield break;
        }
        Instantiate(shockPrefab, catBase.transform.position, Quaternion.identity);
    }
}
