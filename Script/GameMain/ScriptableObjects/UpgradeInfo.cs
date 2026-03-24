using System.Collections;
using System.Collections.Generic;
using UnityEngine;

[CreateAssetMenu(fileName = "upgrade", menuName = "ScriptableObjects/Character Upgrade Info", order = 1)]
[System.Serializable]
public class TotalUpgradeCost : ScriptableObject
{
    public Cost[] cost = new Cost[3];
    [System.Serializable]
    public class Cost
    {
        public UpgradeMethod method;
        public UpgradeConsume[] upgrade_consume;
    }
}