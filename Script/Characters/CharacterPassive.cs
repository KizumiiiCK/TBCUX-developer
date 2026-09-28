using System.Collections.Generic;

public abstract partial class Character
{
    private readonly List<PassiveNode> passiveEffects = new List<PassiveNode>();
    // 分发用的快照。注意它是「换新对象」而不是「原地 Clear + 重填」：
    // 被动钩子里可能先改动本角色的被动列表（置 dirty），再触发同一角色上的嵌套分发，
    // 那次嵌套分发会重建快照——如果原地改写，外层正在遍历的就是同一个 List，
    // 被动会被静默跳过或重复执行。换新对象后外层手里那份已脱钩，能安全遍历完。
    // 分配只发生在被动列表真的变化时（出场、装载、移除），不是每帧。
    private List<PassiveNode> passiveSnapshot = new List<PassiveNode>();
    private bool passiveSnapshotDirty = true;
    // Union of the hooks overridden by all installed passives. Lets each dispatcher
    // bail with a single bitmask test when nothing listens to that hook.
    private PassiveHooks aggregateHooks = PassiveHooks.None;

    public void AddPassiveEffect(PassiveNode effect)
    {
        passiveEffects.Add(effect);
        passiveSnapshotDirty = true;
    }
    public void RemovePassiveEffect(PassiveNode effect)
    {
        passiveEffects.Remove(effect);
        passiveSnapshotDirty = true;
    }

    public T GetPassive<T>() where T : class, PassiveNode
    {
        for (int i = 0; i < passiveEffects.Count; i++)
        {
            if (passiveEffects[i] is T found) return found;
        }
        return null;
    }

    private void BuildPassiveSnapshotIfNeeded()
    {
        if (!passiveSnapshotDirty) return;
        List<PassiveNode> rebuilt = new List<PassiveNode>(passiveEffects.Count);
        PassiveHooks hooks = PassiveHooks.None;
        for (int i = 0; i < passiveEffects.Count; i++)
        {
            PassiveNode node = passiveEffects[i];
            rebuilt.Add(node);
            if (node != null) hooks |= node.Hooks;
        }
        passiveSnapshot = rebuilt;
        aggregateHooks = hooks;
        passiveSnapshotDirty = false;
    }

    private bool HasPassiveHook(PassiveHooks hook)
    {
        BuildPassiveSnapshotIfNeeded();
        return (aggregateHooks & hook) != 0;
    }

    protected void Passive_OnDeployUnit()
    {
        if (!HasPassiveHook(PassiveHooks.OnDeployUnit)) return;
        List<PassiveNode> snap = passiveSnapshot;
        for (int i = 0; i < snap.Count; i++)
        {
            snap[i]?.OnDeployUnit(this);
        }
    }
    protected void Passive_OnBeforeTakeDamage(ref float dmg, List<AttackType> types)
    {
        if (!HasPassiveHook(PassiveHooks.OnBeforeTakeDamage)) return;
        // 负数 DMG 是治疗。约定：所有被动都照常收到通知（很多被动靠这个钩子重置内部状态），
        // 但只有 CanModifyHealing 的被动能真的改写治疗量，其余的写入落在副本上被丢弃。
        // 判定用进入时的符号，避免链条中途有人翻号导致后半段被动被区别对待。
        bool healing = dmg < 0f;
        List<PassiveNode> snap = passiveSnapshot;
        for (int i = 0; i < snap.Count; i++)
        {
            PassiveNode node = snap[i];
            if (node == null) continue;
            if (healing && !node.CanModifyHealing)
            {
                float observed = dmg;
                node.OnBeforeTakeDamage(this, ref observed, types);
                continue;
            }
            node.OnBeforeTakeDamage(this, ref dmg, types);
        }
    }
    protected void Passive_OnMatchedTraits(List<AttackType> types)
    {
        if (!HasPassiveHook(PassiveHooks.OnMatchedTraits)) return;
        List<PassiveNode> snap = passiveSnapshot;
        for (int i = 0; i < snap.Count; i++)
        {
            snap[i]?.OnMatchedTraits(this, types);
        }
    }
    protected void Passive_OnAfterTakeDamage()
    {
        if (!HasPassiveHook(PassiveHooks.OnAfterTakeDamage)) return;
        List<PassiveNode> snap = passiveSnapshot;
        for (int i = 0; i < snap.Count; i++)
        {
            snap[i]?.OnAfterTakeDamage(this);
        }
    }
    protected void Passive_OnStartAttack()
    {
        if (!HasPassiveHook(PassiveHooks.OnStartAttack)) return;
        List<PassiveNode> snap = passiveSnapshot;
        for (int i = 0; i < snap.Count; i++)
        {
            snap[i]?.OnStartAttack(this);
        }
    }
    protected void Passive_OnExitAttack()
    {
        if (!HasPassiveHook(PassiveHooks.OnFinishAttack)) return;
        List<PassiveNode> snap = passiveSnapshot;
        for (int i = 0; i < snap.Count; i++)
        {
            snap[i]?.OnFinishAttack(this);
        }
    }
    protected int Passive_OnAfterSwitchingAnim(int index)
    {
        if (!HasPassiveHook(PassiveHooks.OnAfterSwitchingAnim)) return index;
        List<PassiveNode> snap = passiveSnapshot;
        for (int i = 0; i < snap.Count; i++)
        {
            snap[i]?.OnAfterSwitchingAnim(this, ref index);
        }
        return index;
    }
    protected void Passive_OnAttacking(ref float dmg, ref List<AttackType> types)
    {
        if (!HasPassiveHook(PassiveHooks.OnAttacking)) return;
        List<PassiveNode> snap = passiveSnapshot;
        for (int i = 0; i < snap.Count; i++)
        {
            snap[i]?.OnAttacking(this, ref dmg, ref types);
        }
    }
    protected void Passive_OnAfterAttack(float dmg, List<CharacterEffect> ces, List<AttackType> types)
    {
        if (!HasPassiveHook(PassiveHooks.OnAfterAttack)) return;
        List<PassiveNode> snap = passiveSnapshot;
        for (int i = 0; i < snap.Count; i++)
        {
            snap[i]?.OnAfterAttack(this, dmg, ces, types);
        }
    }
    protected void Passive_OnBeforeKB()
    {
        if (!HasPassiveHook(PassiveHooks.OnBeforeKB)) return;
        List<PassiveNode> snap = passiveSnapshot;
        for (int i = 0; i < snap.Count; i++)
        {
            snap[i]?.OnBeforeKB(this);
        }
    }
    protected void Passive_OnAfterKB()
    {
        if (!HasPassiveHook(PassiveHooks.OnAfterKB)) return;
        List<PassiveNode> snap = passiveSnapshot;
        for (int i = 0; i < snap.Count; i++)
        {
            snap[i]?.OnAfterKB(this);
        }
    }
    protected void Passive_OnDead()
    {
        if (!HasPassiveHook(PassiveHooks.OnDead)) return;
        List<PassiveNode> snap = passiveSnapshot;
        for (int i = 0; i < snap.Count; i++)
        {
            snap[i]?.OnDead(this);
        }
    }

}
