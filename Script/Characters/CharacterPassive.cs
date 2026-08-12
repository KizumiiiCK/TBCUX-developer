using System.Collections.Generic;

public abstract partial class Character
{
    private readonly List<PassiveNode> passiveEffects = new List<PassiveNode>();
    private readonly List<PassiveNode> passiveSnapshot = new List<PassiveNode>();
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

    private void BuildPassiveSnapshotIfNeeded()
    {
        if (!passiveSnapshotDirty) return;
        passiveSnapshot.Clear();
        aggregateHooks = PassiveHooks.None;
        for (int i = 0; i < passiveEffects.Count; i++)
        {
            PassiveNode node = passiveEffects[i];
            passiveSnapshot.Add(node);
            if (node != null) aggregateHooks |= node.Hooks;
        }
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
        for (int i = 0; i < passiveSnapshot.Count; i++)
        {
            passiveSnapshot[i]?.OnDeployUnit(this);
        }
    }
    protected void Passive_OnBeforeTakeDamage(ref float dmg, List<AttackType> types)
    {
        if (!HasPassiveHook(PassiveHooks.OnBeforeTakeDamage)) return;
        for (int i = 0; i < passiveSnapshot.Count; i++)
        {
            passiveSnapshot[i]?.OnBeforeTakeDamage(this, ref dmg, types);
        }
    }
    protected void Passive_OnMatchedTraits(List<AttackType> types)
    {
        if (!HasPassiveHook(PassiveHooks.OnMatchedTraits)) return;
        for (int i = 0; i < passiveSnapshot.Count; i++)
        {
            passiveSnapshot[i]?.OnMatchedTraits(this, types);
        }
    }
    protected void Passive_OnAfterTakeDamage()
    {
        if (!HasPassiveHook(PassiveHooks.OnAfterTakeDamage)) return;
        for (int i = 0; i < passiveSnapshot.Count; i++)
        {
            passiveSnapshot[i]?.OnAfterTakeDamage(this);
        }
    }
    protected void Passive_OnStartAttack()
    {
        if (!HasPassiveHook(PassiveHooks.OnStartAttack)) return;
        for (int i = 0; i < passiveSnapshot.Count; i++)
        {
            passiveSnapshot[i]?.OnStartAttack(this);
        }
    }
    protected void Passive_OnExitAttack()
    {
        if (!HasPassiveHook(PassiveHooks.OnFinishAttack)) return;
        for (int i = 0; i < passiveSnapshot.Count; i++)
        {
            passiveSnapshot[i]?.OnFinishAttack(this);
        }
    }
    protected int Passive_OnAfterSwitchingAnim(int index)
    {
        if (!HasPassiveHook(PassiveHooks.OnAfterSwitchingAnim)) return index;
        for (int i = 0; i < passiveSnapshot.Count; i++)
        {
            passiveSnapshot[i]?.OnAfterSwitchingAnim(this, ref index);
        }
        return index;
    }
    protected void Passive_OnAttacking(ref float dmg, ref List<AttackType> types)
    {
        if (!HasPassiveHook(PassiveHooks.OnAttacking)) return;
        for (int i = 0; i < passiveSnapshot.Count; i++)
        {
            passiveSnapshot[i]?.OnAttacking(this, ref dmg, ref types);
        }
    }
    protected void Passive_OnAfterAttack(float dmg, List<CharacterEffect> ces, List<AttackType> types)
    {
        if (!HasPassiveHook(PassiveHooks.OnAfterAttack)) return;
        for (int i = 0; i < passiveSnapshot.Count; i++)
        {
            passiveSnapshot[i]?.OnAfterAttack(this, dmg, ces, types);
        }
    }
    protected void Passive_OnBeforeKB()
    {
        if (!HasPassiveHook(PassiveHooks.OnBeforeKB)) return;
        for (int i = 0; i < passiveSnapshot.Count; i++)
        {
            passiveSnapshot[i]?.OnBeforeKB(this);
        }
    }
    protected void Passive_OnAfterKB()
    {
        if (!HasPassiveHook(PassiveHooks.OnAfterKB)) return;
        for (int i = 0; i < passiveSnapshot.Count; i++)
        {
            passiveSnapshot[i]?.OnAfterKB(this);
        }
    }
    protected void Passive_OnDead()
    {
        if (!HasPassiveHook(PassiveHooks.OnDead)) return;
        for (int i = 0; i < passiveSnapshot.Count; i++)
        {
            passiveSnapshot[i]?.OnDead(this);
        }
    }

}
