using System.Collections.Generic;

public abstract partial class Character
{
    private readonly List<PassiveNode> passiveEffects = new List<PassiveNode>();
    private readonly List<PassiveNode> passiveSnapshot = new List<PassiveNode>();
    private bool passiveSnapshotDirty = true;

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

    //
    private void BuildPassiveSnapshotIfNeeded()
    {
        if (!passiveSnapshotDirty) return;
        passiveSnapshot.Clear();
        for (int i = 0; i < passiveEffects.Count; i++)
        {
            passiveSnapshot.Add(passiveEffects[i]);
        }
        passiveSnapshotDirty = false;
    }

    protected void Passive_OnDeployUnit()
    {
        BuildPassiveSnapshotIfNeeded();
        for (int i = 0; i < passiveSnapshot.Count; i++)
        {
            passiveSnapshot[i]?.OnDeployUnit(this);
        }
    }
    protected void Passive_OnBeforeTakeDamage(ref float dmg, List<AttackType> types)
    {
        BuildPassiveSnapshotIfNeeded();
        for (int i = 0; i < passiveSnapshot.Count; i++)
        {
            passiveSnapshot[i]?.OnBeforeTakeDamage(this, ref dmg, types);
        }
    }
    protected void Passive_OnAfterTakeDamage()
    {
        BuildPassiveSnapshotIfNeeded();
        for (int i = 0; i < passiveSnapshot.Count; i++)
        {
            passiveSnapshot[i]?.OnAfterTakeDamage(this);
        }
    }
    protected void Passive_OnStartAttack()
    {
        BuildPassiveSnapshotIfNeeded();
        for (int i = 0; i < passiveSnapshot.Count; i++)
        {
            passiveSnapshot[i]?.OnStartAttack(this);
        }
    }
    protected void Passive_OnFinishAttack()
    {
        BuildPassiveSnapshotIfNeeded();
        for (int i = 0; i < passiveSnapshot.Count; i++)
        {
            passiveSnapshot[i]?.OnFinishAttack(this);
        }
    }
    protected void Passive_OnAttacking(ref float dmg, ref List<AttackType> types)
    {
        BuildPassiveSnapshotIfNeeded();
        for (int i = 0; i < passiveSnapshot.Count; i++)
        {
            passiveSnapshot[i]?.OnAttacking(this, ref dmg, ref types);
        }
    }
    protected void Passive_OnAfterAttack(float dmg, List<CharacterEffect> ces, List<AttackType> types)
    {
        BuildPassiveSnapshotIfNeeded();
        for (int i = 0; i < passiveSnapshot.Count; i++)
        {
            passiveSnapshot[i]?.OnAfterAttack(this, dmg, ces, types);
        }
    }
    protected void Passive_OnBeforeKB()
    {
        BuildPassiveSnapshotIfNeeded();
        for (int i = 0; i < passiveSnapshot.Count; i++)
        {
            passiveSnapshot[i]?.OnBeforeKB(this);
        }
    }
    protected void Passive_OnAfterKB()
    {
        BuildPassiveSnapshotIfNeeded();
        for (int i = 0; i < passiveSnapshot.Count; i++)
        {
            passiveSnapshot[i]?.OnAfterKB(this);
        }
    }
    protected void Passive_OnDead()
    {
        BuildPassiveSnapshotIfNeeded();
        for (int i = 0; i < passiveSnapshot.Count; i++)
        {
            passiveSnapshot[i]?.OnDead(this);
        }
    }

}