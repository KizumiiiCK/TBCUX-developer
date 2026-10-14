using System.Collections;
using System.Collections.Generic;
using UnityEngine;

[CreateAssetMenu(fileName = "upgrade", menuName = "ScriptableObjects/Character Upgrade Info", order = 1)]
[System.Serializable]
public class TotalUpgradeCost : ScriptableObject
{
    public Cost[] cost = new Cost[3];

    [Header("Talent")]
    /// <summary>勾选才给这个角色开放本能（Talent）。没勾等于「本角色还没做本能这条路」，图鉴里的本能按钮不亮。</summary>
    [Tooltip("勾选后该角色开放本能（Talent）解锁")]
    public bool talent_available = false;
    /// <summary>解锁本能要求的 upgrade 等级（不含 plus level）。填 0 或负数时按默认 45 处理。</summary>
    [Tooltip("解锁本能所需的升级等级（不含 plus level），<=0 时取默认值 45")]
    public int talent_required_level = 45;
    /// <summary>解锁本能的消耗。面板只有 4 个槽位，超出的条目不会显示（但仍会参与扣除校验，建议别超）。</summary>
    [Tooltip("解锁本能消耗的物品，最多 4 项（面板只有 4 个槽位）")]
    public UpgradeConsume[] talent_consume = new UpgradeConsume[0];

    [System.Serializable]
    public class Cost
    {
        public UpgradeMethod method;
        public UpgradeConsume[] upgrade_consume;
    }
}