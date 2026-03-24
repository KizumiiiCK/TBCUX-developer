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
    private string loadPath;
    private CharacterData[] CD;
    private Texture2D[] unitTexture;
    private TextAsset[] imagecut;
    private TextAsset[] mamodel;
    private TextAsset[] maanim_walk;
    private TextAsset[] maanim_idle;
    private TextAsset[] maanim_attack;
    private TextAsset[] maanim_kb;
    private TextAsset[] maanim_dive;
    private TextAsset[] maanim_out;
    private float deployPercentage;
    private string bgmCode;
    private int[] timepassed;
    private int[] nextTime;
    private int[] currentDeployed;
    AnimDecryptPack[] characterDecryptedFiles;

    private bool activated = false;
    //
    void Start()
    {
        SetupEnemy();
    }
    private void FixedUpdate()
    {
        for(int i = 0; i < ESI.Length; i++)
        {
            if (dogeBase.GetHealthPercentage() > deployPercentage) continue;
            else if (!activated) { activated = true; BGMTool.ChangeBGM(bgmCode); }
            if (dogeBase.GetHealthPercentage() <= 0) Destroy(this);
            if (currentDeployed[i] 
                == ESI[i].repeat) { continue; }
            if (timepassed[i] 
                == nextTime[i])
            {
                if (!LC.DeployAnEnemy()) continue;
                Deploy(i);
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
        ESI = esi;
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
        unitTexture=new Texture2D[ESI.Length];
        imagecut=new TextAsset[ESI.Length];
        mamodel=new TextAsset[ESI.Length];
        maanim_walk=new TextAsset[ESI.Length];
        maanim_idle=new TextAsset[ESI.Length];
        maanim_attack=new TextAsset[ESI.Length];
        maanim_kb=new TextAsset[ESI.Length];
        maanim_dive=new TextAsset[ESI.Length];
        maanim_out=new TextAsset[ESI.Length];
        characterDecryptedFiles = new AnimDecryptPack[ESI.Length];
    }
    public void SetChangeBGM(string bgm) => bgmCode = bgm;
    public void SetupEnemy()
    {
        enemyUnit = Resources.Load<GameObject>("Units/Enemy Units/enemyunit");
        LC = GetComponent<LevelController>();
        for(int i = 0; i < ESI.Length; i++)
        {
            loadPath = $"Units/Enemy Units/{ESI[i].enemyID}/";
            CD[i]= Resources.Load<CharacterData>(loadPath + "data").Clone();
            if (!CD[i].UNITYAnimated)
            {
                unitTexture[i] = Resources.Load<Texture2D>(loadPath + "sprite");
                imagecut[i] = Resources.Load<TextAsset>(loadPath + "imgcut");
                mamodel[i] = Resources.Load<TextAsset>(loadPath + "mamodel");
                maanim_walk[i] = Resources.Load<TextAsset>(loadPath + "maanim_walk");
                maanim_idle[i] = Resources.Load<TextAsset>(loadPath + "maanim_idle");
                maanim_attack[i] = Resources.Load<TextAsset>(loadPath + "maanim_attack");
                maanim_kb[i] = Resources.Load<TextAsset>(loadPath + "maanim_kb");
                List<TextAsset> maanims = new List<TextAsset>(4) { maanim_walk[i], maanim_idle[i], maanim_attack[i], maanim_kb[i]};
                if (CD[i].traits.Z)
                {
                    maanim_dive[i] = Resources.Load<TextAsset>(loadPath + "maanim_dive");
                    maanim_out[i] = Resources.Load<TextAsset>(loadPath + "maanim_out");
                    maanims.Add(maanim_dive[i]);
                    maanims.Add(maanim_out[i]);
                }
                AnimEncryptPack animEncryptPack = new AnimEncryptPack(unitTexture[i], imagecut[i], mamodel[i], maanims.ToArray());
                characterDecryptedFiles[i] = AnimFileDecrypter.DecryptEncryptPack(animEncryptPack);
            }
        }
    }
    private void Deploy(int i)
    {
        int sortingOrder = Random.Range(0, 11);
        int sr_samelayer = Random.Range(0, 6);
        float deviationY = sortingOrder / 10f;
        GameObject enemy = Instantiate(enemyUnit, dogeBase.transform.position - new Vector3(ESI[i].bossShock?-0.1f:0.5f, deviationY,0), Quaternion.identity);
        if (ESI[i].bossShock) enemy.AddComponent<BossPositionLimit>();
        Character ec= enemy.GetComponent<Character>();
        ec.SetPower(ESI[i].ratio / 100f);
        ec.LoadCharacterData(LC, CD[i]);
        ec.levelController = LC;
        if (CD[i].UNITYAnimated)
        {
            GameObject uaunit = Instantiate(Resources.Load<GameObject>($"Units/Enemy Units/{ESI[i].enemyID}/uaunit"), enemy.transform.position, Quaternion.identity);
            uaunit.transform.SetParent(enemy.transform);
            ResetAnimationOrderLayer(uaunit, "Units", sortingOrder * 1000 + sr_samelayer * 100);
        }
        else
        {
            AnimationDisplayer ad = enemy.GetComponent<AnimationDisplayer>();
            ad.Initialization(characterDecryptedFiles[i]);
            ad.OrderLayerStart = sortingOrder * 1000 + sr_samelayer * 50;
            ad.ResetModelOrderLayer();
        }
        //WaveShock
        if (ESI[i].bossShock)
        {
            StartCoroutine(BossShock());
        }
    }
    public void SetBase(GameObject db) { dogeBase=db.GetComponent<DogeBase>(); }
    private static void ResetAnimationOrderLayer(GameObject go, string sortingLayer, int order)
    {
        if (go == null) return;
        if (go.TryGetComponent(out SpriteRenderer sr))
        {
            sr.sortingLayerName = sortingLayer;
            sr.sortingOrder = order;
        }
        foreach (Transform child in go.transform) ResetAnimationOrderLayer(child.gameObject, sortingLayer, order);
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
