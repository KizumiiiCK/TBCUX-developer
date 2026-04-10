using System.Collections;
using System.Collections.Generic;
using UnityEngine;

[CreateAssetMenu(fileName = "LevelData", menuName = "ScriptableObjects/Level Data", order = 1)]
[System.Serializable]
public class LevelData : ScriptableObject
{
    public string levelName;
    public int gainXP = 0;
    public int BaseHealth = 1000;
    public int mapSize = 6000;
    public int maxEmenyCount = 50;
    public int maxCatCount = 50;
    public int BackgroundID = 0;
    public int BaseImageID = 0;
    public string[] CombatEffect;
    public string[] Restriction;
    public Aura[] CombatAura;
    public EXstage exstage;
    public Reward[] rewardlist;
    public EnemySummoner[] enemySummoners;
}
[System.Serializable]
public class Aura
{
    public PostProcessType AuraType;
    public Vector4 Parameters;
    public Color AuraColor;
} 
public enum PostProcessType
{
    none, bloom, vignette, grading, grain, chromatic, motionblur
}
