using UnityEngine;

public class LevelController_Testificate : LevelController
{
    [Header("Tester starts edit here")]
    [Header("Assign test level data here")]
    public LevelData levelData;
    [Header("Treasure count override")]
    public int treasureCount_test = 0;
    [Header("Start with max wallet")]
    public bool MaxMoney = false;
    [Header("Configured test team")]
    public string[] cats = new string[13]
    {
        "00000","00000","00000","00000","00000","00000","00000","00000","00000","00000",
        string.Empty,string.Empty,string.Empty
    };
    [Header("Configured test levels")]
    public int[] catLevels = new int[13]
    {
        1,1,1,1,1,1,1,1,1,1,1,1,1
    };

    protected override void InitializeLevelData()
    {
        testificateMode = true;
        treasureCount = RewardingSystem.GetAmount(RewardName.WorldTreasures);

        if (levelData == null)
        {
            Debug.LogError("No test level configured.");
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
        levelRestrictions = LevelRestrictionHelper.Parse(LD.Restriction);
        ApplyLevelRestrictionSettings();
        SetupCombatEffects();
        SetupCombatAura();
    }

    public override void SetupCatDeployers()
    {
        characters_code = cats;
        RefreshTeamRestrictionState();

        for (int i = 0; i < MAIN_DEPLOYER_COUNT; i++)
        {
            UnitDeployer deployer = Deployers.GetChild(i).GetComponent<UnitDeployer>();
            deployer.SetupDeployer(
                characters_code[i],
                treasureCount,
                0,
                0,
                GetDeploymentLevel(characters_code[i], catLevels[i]),
                GetDeploymentCostMultiplier(characters_code[i]));
            LevelRestrictionHelper.ApplyToDeployer(
                deployer,
                characters_code[i],
                levelRestrictions,
                false,
                ShouldLockAllCatsByRestriction());
        }

        for (int i = MAIN_DEPLOYER_COUNT; i < TOTAL_DEPLOYER_COUNT; i++)
        {
            CharacterData characterData = LoadGuestCharacterData(characters_code[i]);
            if (characterData == null)
            {
                GuestDeployers.GetChild(i - MAIN_DEPLOYER_COUNT).gameObject.SetActive(false);
                continue;
            }

            UnitDeployer deployer = GuestDeployers.GetChild(i - MAIN_DEPLOYER_COUNT).GetComponent<UnitDeployer>();
            deployer.SetupDeployer(
                characters_code[i],
                treasureCount,
                0,
                0,
                GetDeploymentLevel(characters_code[i], catLevels[i]),
                GetDeploymentCostMultiplier(characters_code[i]));
            LevelRestrictionHelper.ApplyToDeployer(
                deployer,
                characters_code[i],
                levelRestrictions,
                true,
                ShouldLockAllCatsByRestriction());
        }
    }
}
