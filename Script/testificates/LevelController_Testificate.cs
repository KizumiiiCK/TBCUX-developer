using UnityEngine;

public class LevelController_Testificate : LevelController
{
    [Header("Tester starts edit here")]
    [Header("将关卡数据拖入此空格中")]
    public LevelData levelData;
    [Header("宝物数量（过一关为1，世界一二三关卡数为 48/ 50/ 52，全满宝为150）")]
    public int treasureCount_test = 0;
    [Header("是否开满级钱包")]
    public bool MaxMoney=false;
    //[Header("（如需修改则打勾）强制修改地图大小，范围为 2100 ~ 6000 ")]
    //public bool forceMS = false;
    //public int forcedMapSize = 3000;
    //[Header("（如需修改则打勾，否则按宝物设置）强制修改产钱速度，范围为 0.06 ~ 1 ")]
    //public bool forceMuiltiplier = false;
    //public float money_multiplier = 0.06f;
    [Header("修改放置角色，5位数代码（最后3位为嘉宾角色，可不填）")]
    public string[] cats = new string[13]
    {
        "00000","00000","00000","00000","00000","00000","00000","00000","00000","00000",
        string.Empty,string.Empty,string.Empty
    };
    [Header("修改放置角色的等级，范围 1 - 50")]
    public int[] catLevels = new int[13]{
        1,1,1,1,1,1,1,1,1,1,1,1,1
    };

    protected override void InitializeLevelData()
    {
        testificateMode = true;
        treasureCount = RewardingSystem.GetAmount(RewardName.WorldTreasures);

        if (levelData == null)
        {
            Debug.LogError("没有配置测试关卡！");
            return;
        }

        LD = levelData;
        treasureCount = treasureCount_test;
        if (MaxMoney)
        {
            current_money_level = MAX_MONEY_LEVEL;
            UpgradeMoney();
        }

        int mapSize = LD.mapSize;
        CalculateMoneyMultiplier();
        SetupMapAndBases(mapSize);
        SetupLevelInfo();
        SetupCombatEffects();
        SetupCombatAura();
    }

    public override void SetupCatDeployers()
    {
        characters_code = cats;

        // 设置主要部署器
        for (int i = 0; i < MAIN_DEPLOYER_COUNT; i++)
        {
            Deployers.GetChild(i).GetComponent<UnitDeployer>().SetupDeployer(
                characters_code[i], treasureCount, 0, 0, catLevels[i]);
        }

        // 设置访客部署器
        for (int i = MAIN_DEPLOYER_COUNT; i < TOTAL_DEPLOYER_COUNT; i++)
        {
            CharacterData characterData = LoadCharacterData(characters_code[i]);
            if (characterData == null)
            {
                GuestDeployers.GetChild(i - MAIN_DEPLOYER_COUNT).gameObject.SetActive(false);
                continue;
            }

            UnitDeployer deployer = GuestDeployers.GetChild(i - MAIN_DEPLOYER_COUNT).GetComponent<UnitDeployer>();
            deployer.SetupDeployer(characters_code[i], treasureCount, 0, 0, catLevels[i]);
            deployer.ResetCoolDown();
            deployer.GuestMark();
        }
    }
}
