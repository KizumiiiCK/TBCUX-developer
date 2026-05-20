using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEngine;

[CreateAssetMenu(fileName = "MapInfo", menuName = "ScriptableObjects/Map Info", order = 1)]
public class MapInfo : ScriptableObject
{
    public string sectionName = "";
    public string mapName = "";
    public string BGM=string.Empty;
    public string unlockRestriction = null;
    public int hardness = 1;
    public bool[] difficulty = new bool[12];
    public Color coverColor = Color.black;
    public TitleColor titleColor = TitleColor.white;
    public LevelTileInfo[] levelsOnMap;
}

[System.Serializable]
public class LevelTileInfo
{
    public string levelNameID;
    public Vector2 levelPosition;
    public LevelTileType tileType=LevelTileType.Null;
}
[System.Serializable]
public class EnemySummoner
{
    public int HealthPercentageOnBreak =100;
    public string bgm = "002";
    public EnemySummonInfo[] enemySummonInfos;
}
[System.Serializable]
public class EnemySummonInfo
{
    public string enemyID ="e000";
    public int ratio =100;
    public bool bossShock =false;
    public int repeat =1;
    public int firstAppear;
    public int repeatMin;
    public int repeatMax;
    [HideInInspector] public Sprite previewSprite;
}
[System.Serializable]
public class EXstage
{
    public bool exist = false;
    public int enter_rate;
    public string mapName;
}
[System.Serializable]
public enum LevelTileType { Null,Aku,Zombie,Future,Cursed,Butter,Niji,Mystery,Sage,Relic,Gold }
public enum TitleColor { white,yellow,blue,purple,orange,green,zerolegend,red,relic,black}
public class TitleColorMap
{
    public static Dictionary<TitleColor, Color> sectionMapping = new Dictionary<TitleColor, Color>() {
        {TitleColor.white, Color.white},
        {TitleColor.red, Color.red},
        {TitleColor.black, Color.black},
        {TitleColor.yellow, new Color(255f / 255f, 194f / 255f, 0f)},
        {TitleColor.blue, new Color(0f, 209f / 255f, 1f)},
        {TitleColor.purple, new Color(130f / 255f, 0f, 1f)},
        {TitleColor.orange, new Color(1f, 92f / 255f, 0f)},
        {TitleColor.green, new Color(142f / 255f, 217f / 255f, 44f / 255f)},
        {TitleColor.zerolegend, new Color(104f / 255f, 139f / 255f, 216f / 255f)},
        {TitleColor.relic, new Color(67f / 255f, 94f / 255f, 22f / 255f)},
    };

    public static Color GetSectionColor(TitleColor titleColor)
    {
        if (sectionMapping.TryGetValue(titleColor, out Color color)) return color;
        return Color.white;
    }
    public static Dictionary<LevelTileType, int> levelMapping = new Dictionary<LevelTileType, int>(){
        {LevelTileType.Null,0 },
    };
}
[System.Serializable]
public static class UpgradeCost 
{
    public static readonly int[,] XPcost = new int[7, 10]
    {
        { 1000, 2000, 3000, 4000, 5000, 6000, 7000, 8000, 9000, 10000},
        { 3500, 5600, 8540, 12460, 17360, 23240, 30100, 37940, 46760, 56560},
        { 5000, 8000, 12200, 17800, 24800, 33200, 43000, 54200, 66800, 80800 },
        { 6250, 8200, 12400, 17800, 24800, 42400, 64500, 93000, 148000, 298000},
        { 7800, 9800, 14800, 21800, 42500, 64300, 93200, 118000, 197400, 513500 },
        { 7800, 9800, 14800, 21800, 42500, 64300, 93200, 118000, 197400, 513500 },
        { 99999, 199998, 299997, 399996, 499995, 599994, 699993, 799992, 899991, 999999 }
    };
}
[System.Serializable]
public enum UpgradeMethod { none, unavailable, clearStage, items, clearStageAndItems, drawCards}
[System.Serializable]
public class UpgradeConsume
{
    public RewardName reward_name;
    public int count;
}