using System.Collections;
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
        characterDecryptedFiles = new AnimDecryptPack[ESI.Length];
        runtimeInitialized = new bool[ESI.Length];
    }
    public void ApplyLevelRestrictions(LevelRestrictionHelper.RestrictionRules rules) => levelRestrictions = rules;
    public void SetChangeBGM(string bgm) => bgmCode = bgm;
    public bool SetupEnemy()
    {
        enemyUnit = BundledAddressables.LoadSync<GameObject>("Units/Enemy Units/enemyunit");
        LC = GetComponent<LevelController>();
        if (enemyUnit == null || LC == null || dogeBase == null || ESI == null || CD == null || runtimeInitialized == null || characterDecryptedFiles == null)
        {
            Debug.LogError("[LevelEnemySummoner] Summoner is not fully configured.");
            isConfigured = false;
            return false;
        }
        for(int i = 0; i < ESI.Length; i++)
        {
            string loadPath = $"Units/Enemy Units/{ESI[i].enemyID}/";
            CharacterData loadedData = BundledAddressables.LoadSync<CharacterData>(loadPath + "data");
            if (loadedData == null)
            {
                Debug.LogError($"[LevelEnemySummoner] Enemy data not found at {loadPath}data");
                isConfigured = false;
                return false;
            }
            CD[i] = loadedData.Clone();
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

        int sortingOrder = Random.Range(0, 11);
        int sr_samelayer = Random.Range(0, 6);
        float deviationY = sortingOrder / 10f;
        GameObject enemy = Instantiate(enemyUnit, dogeBase.transform.position - new Vector3(ESI[i].bossShock?-0.1f:0.5f, deviationY,0), Quaternion.identity);
        if (ESI[i].bossShock) enemy.AddComponent<BossPositionLimit>();
        Character ec= enemy.GetComponent<Character>();
        ec.SetPower(ESI[i].ratio / 100f);
        ec.LoadCharacterData(LC, CD[i]);
        ec.levelController = LC;
        CharacterVisualLoader.InitializeRuntimeCharacterVisual(
            enemy,
            false,
            ESI[i].enemyID,
            CD[i],
            characterDecryptedFiles[i],
            "Units",
            sortingOrder * 1000 + sr_samelayer * 100,
            sortingOrder * 1000 + sr_samelayer * 50
        );
        //WaveShock
        if (ESI[i].bossShock)
        {
            StartCoroutine(BossShock());
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
            characterDecryptedFiles[index] = CharacterVisualLoader.DecryptCharacterFiles(false, ESI[index].enemyID, CD[index]);
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
    private IEnumerator BossShock()
    {
        yield return new WaitForFixedUpdate();
        GameObject shk = Resources.Load<GameObject>($"Effects/boss_shock");
        Instantiate(shk, dogeBase.transform.position, Quaternion.identity);
        GameObject[] cats = GameObject.FindGameObjectsWithTag("Cat");
        foreach (GameObject c in cats)
        {
            CatCharacter cc = c.GetComponent<CatCharacter>();
            if (cc != null)
            {
                cc.StartKBCoroutine(KB_Type.bossShock);
            }
        }
    }
}
