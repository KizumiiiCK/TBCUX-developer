using System.Collections.Generic;
using Unity.Collections;
using Unity.VisualScripting;
using UnityEngine;
using UnityEngine.UI;

public class LevelEnemySummoner : MonoBehaviour
{
    private GameObject enemyUnit;
    [SerializeField] private EnemySummonInfo[] ESI;
    [SerializeField]private DogeBase dogeBase;
    private LevelController LC;
    private CharacterData[] CD;
    private UnitIdentity[] identities;
    private float deployPercentage;
    private string bgmCode;
    private int[] timepassed;
    private int[] nextTime;
    private int[] currentDeployed;
    private AnimDecryptPack[] characterDecryptedFiles;
    private bool[] runtimeInitialized;
    private LevelRestrictionHelper.RestrictionRules levelRestrictions;
    private bool isConfigured;

    private bool activated = false;
    //
    void Start()
    {
        if (!SetupEnemy())
        {
            enabled = false;
        }
    }
    private void FixedUpdate()
    {
        if (!isConfigured) return;
        if (LC == null || dogeBase == null || ESI == null) return;
        if (LC.isPloting) return;
        for(int i = 0; i < ESI.Length; i++)
        {
            if (dogeBase.GetHealthPercentage() > deployPercentage) continue;
            else if (!activated)
            {
                activated = true;
                if (!string.IsNullOrEmpty(bgmCode))
                    BGMTool.ChangeBGM(bgmCode);
            }
            if (dogeBase.GetHealthPercentage() <= 0) Destroy(this);
            if (currentDeployed[i] 
                == ESI[i].repeat) { continue; }
            if (timepassed[i] 
                == nextTime[i])
            {
                if (!LC.DeployAnEnemy()) continue;
                if (!Deploy(i)) continue;
                currentDeployed[i]++;
                //if (currentDeployed[i] == ESI[i].repeat) { continue; }
                timepassed[i] = 0;
                nextTime[i] = Random.Range(ESI[i].repeatMin, ESI[i].repeatMax);
            }
            timepassed[i]++;
        }
    }
    public void SetupEnemyDeployer(int percentage,EnemySummonInfo[] esi)
    {
        ESI = esi ?? new EnemySummonInfo[0];
        nextTime = new int[ESI.Length];
        timepassed = new int[ESI.Length];
        currentDeployed = new int[ESI.Length];
        for (int i = 0; i < ESI.Length; i++) 
        {
            deployPercentage = percentage / 100f;
            nextTime[i]=ESI[i].firstAppear;
            timepassed[i]=0;
            currentDeployed[i] = 0;
            if (ESI[i].bossShock)
            {
                GetComponent<LevelController>().SetBossLock();
            }
        }
        CD=new CharacterData[ESI.Length];
        identities = new UnitIdentity[ESI.Length];
        characterDecryptedFiles = new AnimDecryptPack[ESI.Length];
        runtimeInitialized = new bool[ESI.Length];
    }
    public void ApplyLevelRestrictions(LevelRestrictionHelper.RestrictionRules rules) => levelRestrictions = rules;
    public void SetChangeBGM(string bgm) => bgmCode = bgm;
    public bool SetupEnemy()
    {
        enemyUnit = BundledAddressables.LoadSync<GameObject>("Units/Enemy Units/enemyunit");
        LC = GetComponent<LevelController>();
        if (enemyUnit == null || LC == null || dogeBase == null || ESI == null || CD == null || identities == null || runtimeInitialized == null || characterDecryptedFiles == null)
        {
            Debug.LogError("[LevelEnemySummoner] Summoner is not fully configured.");
            isConfigured = false;
            return false;
        }
        for(int i = 0; i < ESI.Length; i++)
        {
            if (!CharacterPlacer.TryParse(ESI[i].enemyID, false, out identities[i]) || !identities[i].IsValid)
            {
                Debug.LogError($"[LevelEnemySummoner] Invalid unit id: {ESI[i].enemyID}");
                isConfigured = false;
                return false;
            }
            CharacterData loadedData = CharacterPlacer.LoadData(identities[i]);
            if (loadedData == null)
            {
                Debug.LogError($"[LevelEnemySummoner] Unit data not found for {ESI[i].enemyID}");
                isConfigured = false;
                return false;
            }
            CD[i] = loadedData;
            LevelRestrictionHelper.ApplyEnemyCharacterDataRestrictions(levelRestrictions, CD[i]);
            runtimeInitialized[i] = false;
            characterDecryptedFiles[i] = null;
        }
        isConfigured = true;
        return true;
    }
    private bool Deploy(int i)
    {
        if (!EnsureEnemyRuntimeInitialized(i)) return false;

        // 和 UnitDeployer 共用 LevelController 上的同一套席位计数器：两边的 y 都是
        // base.y - so/10f 且两个基地同为 BASE_Y_POSITION，同一条线在两队就是同一个视觉深度。
        int visualLayer = Random.Range(0, LevelController.VisualLayerCount);
        int visualOrder = visualLayer * LevelController.VisualLayerStride + LC.NextVisualSlot(visualLayer);
        float deviationY = visualLayer / 10f;
        GameObject enemy = CharacterPlacer.Place(
            identities[i],
            CD[i],
            characterDecryptedFiles[i],
            dogeBase.transform.position - new Vector3(ESI[i].bossShock ? -0.1f : 0.5f, deviationY, 0),
            LC,
            1,
            0f,
            ESI[i].ratio / 100f,
            visualOrder,
            visualOrder);
        if (enemy == null) return false;
        if (ESI[i].bossShock)
        {
            enemy.AddComponent<BossPositionLimit>();
            Character spawned = enemy.GetComponent<Character>();
            Aux_BossWave.EnsureOn(spawned);
        }
        return true;
    }

    private bool EnsureEnemyRuntimeInitialized(int index)
    {
        if (index < 0 || index >= ESI.Length) return false;
        if (runtimeInitialized != null && runtimeInitialized[index]) return true;
        if (CD == null || index >= CD.Length || CD[index] == null) return false;

        if (!CD[index].UNITYAnimated)
        {
            characterDecryptedFiles[index] = CharacterPlacer.Decrypt(identities[index], CD[index]);
            if (characterDecryptedFiles[index] == null) return false;
        }

        runtimeInitialized[index] = true;
        return true;
    }
    public void SetBase(GameObject db)
    {
        if (db == null)
        {
            dogeBase = GameObject.Find("DogeBase")?.GetComponent<DogeBase>();
            return;
        }
        dogeBase = db.GetComponent<DogeBase>();
    }
}
